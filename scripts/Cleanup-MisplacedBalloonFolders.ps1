#Requires -Version 7
<#
.SYNOPSIS
  BT が誤配置した非レンジ ID####### をレンジへ回収し、レンジ側を整理する。

.DESCRIPTION
  (0) 対応レンジをバックアップ
  (1) レンジ 01_写真: 2026/02/01 0:00 未満のファイル削除 + history 削除
  (2) レンジ 02_編集: 同上
  (2b) レンジ 99_過去データ: 中身をすべて削除（フォルダ自体は残す）
  (3) レンジ 03_最終: tbl402/tbl813 原画ファイル未登録の直下ファイルを削除。
      サブフォルダは「楽天」のみ残す
  (4) 非レンジ 02_編集/03_最終 → レンジへ移植（同名は非レンジ優先で上書き）
      空になった非レンジ ID####### を削除

.PARAMETER Execute
  指定時のみ実変更。省略時は DryRun（ログのみ）。

.PARAMETER BalloonRoot
  既定: \\naranja.local\dfs02\naranja\4000_商品画像\4200_balloon

.PARAMETER BackupRoot
  既定: C:\Users\nakashimajunichiro\Desktop\ToDo未清算\sempertex20260808

.EXAMPLE
  pwsh -File .\scripts\Cleanup-MisplacedBalloonFolders.ps1
  pwsh -File .\scripts\Cleanup-MisplacedBalloonFolders.ps1 -Execute
#>
[CmdletBinding()]
param(
    [switch]$Execute,
    [string]$BalloonRoot = '\\naranja.local\dfs02\naranja\4000_商品画像\4200_balloon',
    [string]$BackupRoot = 'C:\Users\nakashimajunichiro\Desktop\ToDo未清算\sempertex20260808',
    [string]$ConnectionString = 'Data Source=svsql02;Initial Catalog=db_naranja;Integrated Security=True;TrustServerCertificate=True',
    [datetime]$Cutoff = '2026-02-01'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$DryRun = -not $Execute
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$logDir = Join-Path $BackupRoot '_logs'
$logPath = Join-Path $logDir ("Cleanup-MisplacedBalloonFolders_{0}{1}.log" -f $stamp, $(if ($DryRun) { '_DRYRUN' } else { '' }))

function Write-Log {
    param([string]$Message, [string]$Level = 'INFO')
    $line = '[{0}] [{1}] {2}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Level, $Message
    Write-Host $line
    Add-Content -LiteralPath $logPath -Value $line -Encoding UTF8
}

function Get-RangeFolderName {
    param([int]$ProductId)
    $bucket = [Math]::Floor($ProductId / 10000)
    return ('ID{0:000}0000-\ID{1:0000000}' -f $bucket, $ProductId)
}

function Invoke-FsAction {
    param(
        [string]$Description,
        [scriptblock]$Action
    )
    if ($DryRun) {
        Write-Log ("DRYRUN: {0}" -f $Description)
        return
    }
    & $Action
    Write-Log $Description
}

function Remove-DirectoryTree {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return }
    Invoke-FsAction "DELETE DIR  $Path" {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
}

function Remove-FileSafe {
    param([string]$Path)
    if (-not (Test-Path -LiteralPath $Path)) { return }
    Invoke-FsAction "DELETE FILE $Path" {
        Remove-Item -LiteralPath $Path -Force
    }
}

function Copy-DirectoryMirror {
    param([string]$Source, [string]$Destination)
    if (-not (Test-Path -LiteralPath $Source)) {
        Write-Log ("SKIP backup (missing): {0}" -f $Source) 'WARN'
        return
    }
    if ($DryRun) {
        Write-Log ("DRYRUN: BACKUP  {0} -> {1}" -f $Source, $Destination)
        return
    }
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    # /E 空サブ含み /COPY:DAT /R:1 /W:1 /NFL /NDL /NJH /NJS
    $null = & robocopy $Source $Destination /E /COPY:DAT /R:2 /W:2 /NFL /NDL /NJH /NJS /NP
    $rc = $LASTEXITCODE
    if ($rc -ge 8) {
        throw "robocopy failed ($rc): $Source -> $Destination"
    }
    Write-Log ("BACKUP  {0} -> {1} (robocopy exit={2})" -f $Source, $Destination, $rc)
}

function Get-RegisteredOriginalNames {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [int]$ProductId
    )
    $names = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $sql = @'
SELECT LTRIM(RTRIM(原画ファイル)) AS 原画ファイル
FROM dbo.tbl402商品色コード
WHERE 商品ID = @pid AND NULLIF(LTRIM(RTRIM(原画ファイル)), N'') IS NOT NULL
UNION
SELECT LTRIM(RTRIM(原画ファイル)) AS 原画ファイル
FROM dbo.tbl813Webページ画像親
WHERE 商品ID = @pid AND NULLIF(LTRIM(RTRIM(原画ファイル)), N'') IS NOT NULL
'@
    $cmd = $Connection.CreateCommand()
    $cmd.CommandText = $sql
    [void]$cmd.Parameters.AddWithValue('@pid', $ProductId)
    $reader = $cmd.ExecuteReader()
    try {
        while ($reader.Read()) {
            [void]$names.Add([string]$reader['原画ファイル'])
        }
    }
    finally {
        $reader.Close()
        $cmd.Dispose()
    }
    Write-Output -NoEnumerate $names
}

function Clear-OldFilesAndHistory {
    param(
        [string]$SubFolderPath,
        [string]$Label
    )
    if (-not (Test-Path -LiteralPath $SubFolderPath)) {
        Write-Log ("SKIP {0} (missing): {1}" -f $Label, $SubFolderPath)
        return
    }

    Get-ChildItem -LiteralPath $SubFolderPath -File -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.LastWriteTime -lt $Cutoff } |
        ForEach-Object {
            Remove-FileSafe $_.FullName
        }

    $history = Join-Path $SubFolderPath 'history'
    if (Test-Path -LiteralPath $history) {
        Remove-DirectoryTree $history
    }
}

function Clear-FolderContentsKeepDir {
    param(
        [string]$FolderPath,
        [string]$Label
    )
    if (-not (Test-Path -LiteralPath $FolderPath)) {
        Write-Log ("SKIP {0} (missing): {1}" -f $Label, $FolderPath)
        return
    }

    Get-ChildItem -LiteralPath $FolderPath -Force -ErrorAction SilentlyContinue | ForEach-Object {
        if ($_.PSIsContainer) {
            Remove-DirectoryTree $_.FullName
        }
        else {
            Remove-FileSafe $_.FullName
        }
    }
}

function Clean-FinalFolder {
    param(
        [string]$FinalDir,
        [System.Collections.Generic.HashSet[string]]$RegisteredNames
    )
    if (-not (Test-Path -LiteralPath $FinalDir)) {
        Write-Log ("SKIP 03_最終 (missing): {0}" -f $FinalDir)
        return
    }

    Get-ChildItem -LiteralPath $FinalDir -File -Force -ErrorAction SilentlyContinue | ForEach-Object {
        if (-not $RegisteredNames.Contains($_.Name)) {
            Remove-FileSafe $_.FullName
        }
        else {
            Write-Log ("KEEP registered: {0}" -f $_.FullName)
        }
    }

    Get-ChildItem -LiteralPath $FinalDir -Directory -Force -ErrorAction SilentlyContinue | ForEach-Object {
        if ($_.Name -ieq '楽天') {
            Write-Log ("KEEP subdir 楽天: {0}" -f $_.FullName)
        }
        else {
            Remove-DirectoryTree $_.FullName
        }
    }
}

function Move-FolderContentsOverwrite {
    param(
        [string]$SourceDir,
        [string]$DestDir,
        [string]$Label
    )
    if (-not (Test-Path -LiteralPath $SourceDir)) {
        Write-Log ("SKIP migrate {0} (no source): {1}" -f $Label, $SourceDir)
        return
    }

    if (-not $DryRun -and -not (Test-Path -LiteralPath $DestDir)) {
        New-Item -ItemType Directory -Path $DestDir -Force | Out-Null
    }

    # 直下ファイル
    Get-ChildItem -LiteralPath $SourceDir -File -Force -ErrorAction SilentlyContinue | ForEach-Object {
        $destPath = Join-Path $DestDir $_.Name
        if ($DryRun) {
            $exists = Test-Path -LiteralPath $destPath
            Write-Log ("DRYRUN: MIGRATE FILE{0} {1} -> {2}" -f $(if ($exists) { ' (overwrite)' } else { '' }), $_.FullName, $destPath)
        }
        else {
            Move-Item -LiteralPath $_.FullName -Destination $destPath -Force
            Write-Log ("MIGRATE FILE {0} -> {1}" -f $_.FullName, $destPath)
        }
    }

    # 直下サブフォルダ（楽天等）。history は (1)(2) で削除方針のため移植しない
    Get-ChildItem -LiteralPath $SourceDir -Directory -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -ine 'history' } |
        ForEach-Object {
        $destPath = Join-Path $DestDir $_.Name
        if ($DryRun) {
            Write-Log ("DRYRUN: MIGRATE DIR {0} -> {1}" -f $_.FullName, $destPath)
            return
        }
        if (-not (Test-Path -LiteralPath $destPath)) {
            New-Item -ItemType Directory -Path $destPath -Force | Out-Null
        }
        $null = & robocopy $_.FullName $destPath /E /MOVE /COPY:DAT /R:2 /W:2 /NFL /NDL /NJH /NJS /NP /IS /IT
        $rc = $LASTEXITCODE
        if ($rc -ge 8) { throw "robocopy migrate dir failed ($rc): $($_.FullName)" }
        if (Test-Path -LiteralPath $_.FullName) {
            Remove-Item -LiteralPath $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
        }
        Write-Log ("MIGRATE DIR {0} -> {1} (rc={2})" -f $_.FullName, $destPath, $rc)
    }

    # 非レンジ側に残った空の history は削除対象（空フォルダ掃除）
    $srcHistory = Join-Path $SourceDir 'history'
    if (Test-Path -LiteralPath $srcHistory) {
        Remove-DirectoryTree $srcHistory
    }
}

New-Item -ItemType Directory -Path $logDir -Force | Out-Null
Write-Log ("=== Cleanup-MisplacedBalloonFolders start DryRun={0} Cutoff={1:yyyy-MM-dd} ===" -f $DryRun, $Cutoff)
Write-Log ("BalloonRoot={0}" -f $BalloonRoot)
Write-Log ("BackupRoot={0}" -f $BackupRoot)

if (-not (Test-Path -LiteralPath $BalloonRoot)) {
    throw "BalloonRoot not found: $BalloonRoot"
}
if (-not (Test-Path -LiteralPath $BackupRoot)) {
    New-Item -ItemType Directory -Path $BackupRoot -Force | Out-Null
}

$nonRangeDirs = @(
    Get-ChildItem -LiteralPath $BalloonRoot -Directory -ErrorAction Stop |
        Where-Object { $_.Name -match '^ID\d{7}$' } |
        Sort-Object Name
)
Write-Log ("Non-range folders: {0}" -f $nonRangeDirs.Count)
if ($nonRangeDirs.Count -eq 0) {
    Write-Log 'Nothing to do.'
    exit 0
}

$targets = foreach ($dir in $nonRangeDirs) {
    $productId = [int]$dir.Name.Substring(2)
    $rangeName = Get-RangeFolderName $productId
    [pscustomobject]@{
        ProductId    = $productId
        NonRangePath = $dir.FullName
        NonRangeName = $dir.Name
        RangeName    = $rangeName
        RangePath    = Join-Path $BalloonRoot $rangeName
    }
}

# (0) backup unique range folders
$backupStampDir = Join-Path $BackupRoot ("ranges_{0}" -f $stamp)
Write-Log ("--- (0) Backup range folders -> {0} ---" -f $backupStampDir)
$uniqueRanges = $targets | Sort-Object RangePath -Unique
foreach ($t in $uniqueRanges) {
    if (-not (Test-Path -LiteralPath $t.RangePath)) {
        Write-Log ("WARN range missing, will create later if needed: {0}" -f $t.RangePath) 'WARN'
        continue
    }
    $dest = Join-Path $backupStampDir $t.RangeName
    Copy-DirectoryMirror -Source $t.RangePath -Destination $dest
}

$conn = New-Object System.Data.SqlClient.SqlConnection $ConnectionString
$conn.Open()
Write-Log 'SQL connected.'

try {
    foreach ($t in $targets) {
        Write-Log ("===== ProductId={0} {1} -> {2} =====" -f $t.ProductId, $t.NonRangeName, $t.RangeName)

        if (-not (Test-Path -LiteralPath $t.RangePath)) {
            Invoke-FsAction ("CREATE DIR {0}" -f $t.RangePath) {
                New-Item -ItemType Directory -Path $t.RangePath -Force | Out-Null
                foreach ($sub in @('01_写真', '02_編集', '03_最終', '05_Web掲載リソース', '98_その他', '99_過去データ')) {
                    New-Item -ItemType Directory -Path (Join-Path $t.RangePath $sub) -Force | Out-Null
                }
            }
        }

        $photoDir = Join-Path $t.RangePath '01_写真'
        $editDir = Join-Path $t.RangePath '02_編集'
        $finalDir = Join-Path $t.RangePath '03_最終'
        $archiveDir = Join-Path $t.RangePath '99_過去データ'

        Write-Log '--- (1) Clean 01_写真 ---'
        Clear-OldFilesAndHistory -SubFolderPath $photoDir -Label '01_写真'

        Write-Log '--- (2) Clean 02_編集 ---'
        Clear-OldFilesAndHistory -SubFolderPath $editDir -Label '02_編集'

        Write-Log '--- (2b) Clear 99_過去データ (keep folder) ---'
        Clear-FolderContentsKeepDir -FolderPath $archiveDir -Label '99_過去データ'

        Write-Log '--- (3) Clean 03_最終 (DB unregistered) ---'
        $registered = Get-RegisteredOriginalNames -Connection $conn -ProductId $t.ProductId
        Write-Log ("Registered names count={0}" -f $registered.Count)
        Clean-FinalFolder -FinalDir $finalDir -RegisteredNames $registered

        Write-Log '--- (4) Migrate non-range -> range ---'
        $srcEdit = Join-Path $t.NonRangePath '02_編集'
        $srcFinal = Join-Path $t.NonRangePath '03_最終'
        Move-FolderContentsOverwrite -SourceDir $srcEdit -DestDir $editDir -Label '02_編集'
        Move-FolderContentsOverwrite -SourceDir $srcFinal -DestDir $finalDir -Label '03_最終'

        # 空になった非レンジのみ削除
        if ($DryRun) {
            Write-Log ("DRYRUN: after migrate, DELETE non-range if empty: {0}" -f $t.NonRangePath)
        }
        else {
            $leftFiles = @(Get-ChildItem -LiteralPath $t.NonRangePath -Recurse -File -Force -ErrorAction SilentlyContinue)
            if ($leftFiles.Count -eq 0) {
                Remove-Item -LiteralPath $t.NonRangePath -Recurse -Force
                Write-Log ("DELETE empty non-range {0}" -f $t.NonRangePath)
            }
            else {
                Write-Log ("KEEP non-range (not empty, {0} files left): {1}" -f $leftFiles.Count, $t.NonRangePath) 'WARN'
                $leftFiles | Select-Object -First 30 | ForEach-Object {
                    Write-Log ("  leftover: {0}" -f $_.FullName) 'WARN'
                }
            }
        }
    }
}
finally {
    if ($conn.State -ne 'Closed') { $conn.Close() }
    $conn.Dispose()
}

Write-Log ("=== done DryRun={0} log={1} ===" -f $DryRun, $logPath)
if ($DryRun) {
    Write-Host ''
    Write-Host 'DryRun のみです。実実行は -Execute を付けて再実行してください。'
}
