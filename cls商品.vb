#Disable Warning IDE1006 ' 命名スタイル

Imports System.Data.SqlClient
Imports System.IO
Imports System.Security.Cryptography
Imports Microsoft.Data.SqlClient

Public Class cls商品

#Region "クラス定数"
    '変更時：在庫基底変更() を実行
    '変更履歴 2019/09/22 700000 -> 830000 (3月7日分)
    '変更履歴 2021/09/18 830000 -> 930000 (2021/01/21頃)
    Public Const intStockFinalID As Integer = 930000 ' ---- その1個前までの在庫数。このIDでは、在庫数を数える。
    Public Const intStockBaseID As Integer = -1

    '楽天商品名に含まれる送料関連表示は以下の4種類、ここ以外にこの定数を使わない。順番で管理
    Public Shared ReadOnly shippingTypesInProductName As New List(Of String) From {"【送料無料】【メール便】", "【メール便OK(5点まで)】", "【メール便OK】", "【送料無料】"}
    Public Const soryoKomiSoryoHontai As Integer = 600 '送料込(ヤマト通常)の場合に一律で足す価格
    Public Const soryoKomiNekoposSoryoHontai As Integer = 300 '送料込(メール便)の場合に一律で足す価格

    '棚位置情報でチェックディジット的につかうひらがな
    Public Const cdHiraganas As String = "あいうえおかきくけこさしすせそたちつてとなにぬねのはひふへほまみむめもやゆよらりるれろん"
    Public Const cdHiraganaLength As Integer = 44
#End Region


#Region "商品汎用情報"

#Region "基本商品情報 名称 コード"

    Public Shared Function 商品フルネーム楽天(ByVal productColorCodeId As Integer) As String
        Dim sentakushiId As Integer = clsNDC.GetSqlInteger(0, "")

        Dim productFullName As String = clsNDC.GetSqlString("SELECT dbo.tbl82511楽天商品.商品名 FROM dbo.tbl802Webアイテム INNER JOIN dbo.tbl82513楽天SKU ON dbo.tbl802Webアイテム.WebアイテムID = dbo.tbl82513楽天SKU.WebアイテムID INNER JOIN dbo.tbl82511楽天商品 ON dbo.tbl82513楽天SKU.楽天ページID = dbo.tbl82511楽天商品.楽天ページID INNER JOIN dbo.tbl82501楽天ページNDC ON dbo.tbl82511楽天商品.楽天ページID = dbo.tbl82501楽天ページNDC.楽天ページID WHERE (dbo.tbl802Webアイテム.商品色コードID = {0}) ORDER BY dbo.tbl82501楽天ページNDC.楽天ナランハ表示順序", productColorCodeId)

        If String.IsNullOrEmpty(productFullName) Then
            productFullName = String.Format("▼楽天対応商品無(商品色コードID:{0}/NDC商品名:{1})", productColorCodeId, cls商品.商品フルネーム(productColorCodeId))
        End If

        Return productFullName

    End Function

    Public Shared Function 商品フルネーム(ByVal productColorCodeId As Integer, Optional withProductCode As Boolean = True, Optional ByVal ifEnglish As Boolean = False) As String
        Dim productFullName As String
        Dim productCodeColumn As String = String.Empty
        If withProductCode Then
            productCodeColumn = "ISNULL(tbl402商品色コード.商品コード,N'') + N' ' + "
        End If

        If ifEnglish Then
            productFullName = clsNDC.GetSqlString("SELECT " & productCodeColumn & "tbl401商品.英語名 + N' ' + tbl402商品色コード.英語色名 AS 商品フルネーム " &
                            "FROM tbl401商品 INNER JOIN tbl402商品色コード ON tbl401商品.商品ID = tbl402商品色コード.商品ID " &
                            "WHERE(tbl402商品色コード.商品色コードID = {0})", productColorCodeId)
        Else
            productFullName = clsNDC.GetSqlString("SELECT " & productCodeColumn & "tbl401商品.商品名 + N' ' + tbl402商品色コード.色名 AS 商品フルネーム " &
                            "FROM tbl401商品 INNER JOIN tbl402商品色コード ON tbl401商品.商品ID = tbl402商品色コード.商品ID " &
                            "WHERE(tbl402商品色コード.商品色コードID = {0})", productColorCodeId)
        End If

        productFullName = clsNDC.DisplayName(productFullName)
        Return productFullName

    End Function

#End Region


#End Region

#Region "画像"

    Public Shared Function webp2png(fileFullPath As String, Optional newFileFullPath As String = "") As Boolean

        '2022/10/04 Irfanviewでは背景うまく対応していない、dwebp.exeをインストール。
        'https://storage.googleapis.com/downloads.webmproject.org/releases/webp/index.html
        '上記より libwebp-{0}-windows-x64.zip をダウンロードし解凍 ({0}は最新バージョン)
        '\bin\dwebp.exe を
        '\\naranja.local\dfs02\naranja\9000_NDC\クライアント設定系\NARANJA\drivers に配置
        '(そしてこれを自分のpublicフォルダにコピー)
        Try
            If Not String.IsNullOrEmpty(newFileFullPath) AndAlso Path.GetExtension(newFileFullPath).Equals(".png", StringComparison.OrdinalIgnoreCase) Then
                Return clsNDC.EM("webp->png ファイル指定不正")
            End If

            'C:\Users\Public\NARANJA\driversにはパスが通っている chrome
            Dim dwebpPath As String = "NARANJA\drivers\dwebp.exe"

            Dim dwebpExe As String = String.Format("C:\Users\Public\{0}", dwebpPath)
            Dim dwebpLocalFolder As String = Path.GetDirectoryName(dwebpExe)
            If Not Directory.Exists(dwebpLocalFolder) Then
                Directory.CreateDirectory(dwebpLocalFolder)
            End If
            If Not File.Exists(dwebpExe) Then
                File.Copy(String.Format("{0}\naranja\9000_NDC\クライアント設定系\{1}", clsNDC.dfsRoot, dwebpPath), dwebpExe)
            End If

            If Path.GetExtension(fileFullPath).Equals(".webp", StringComparison.OrdinalIgnoreCase) Then
                Return clsNDC.EM("拡張子非対応")
            End If
            Dim startInfo As New ProcessStartInfo(dwebpExe)

            Dim strFileType As String = ".png"
            Dim pngFile = String.Format("{0}\{1}{2}", Path.GetDirectoryName(fileFullPath), Path.GetFileNameWithoutExtension(fileFullPath), strFileType)

            If String.IsNullOrEmpty(newFileFullPath) Then
                newFileFullPath = pngFile
            End If

            If File.Exists(newFileFullPath) Then
                If MessageBox.Show(String.Format("指定のファイル{0}{0}{1}{0}{0}は存在します。上書きしますか？", vbCrLf, newFileFullPath), "上書確認", MessageBoxButtons.OKCancel) <> DialogResult.OK Then
                    Return False
                End If
                File.Delete(newFileFullPath)
            End If

            startInfo.Arguments = String.Format("{0} -o {1}", fileFullPath, newFileFullPath)

            Dim p As Process = Process.Start(startInfo)

            If Not p.WaitForExit(5000) Then
                Return clsNDC.EM("dwebp.exe タイムアウト Code 046")
            End If

            If File.Exists(newFileFullPath) Then
                Return True
            End If
            Return clsNDC.EM(String.Format(".webpより作成した.pngファイル{0}{0}{1}{0}{0}が見当たりません。", vbCrLf, newFileFullPath))
        Catch ex As Exception
            Return clsNDC.EM(ex.Message)
        End Try

    End Function

    Public Shared Function 原画ファイルフルパス(ByVal productColorCodeId As Integer, ByRef fullPath As String, Optional ask As Boolean = True) As Boolean
        'productColorCodeId < 0 時は Webページ画像親ID
        Try
            fullPath = String.Empty
            If productColorCodeId = 0 Then
                Return False
            End If
            Dim productId As Integer
            Dim gengaFile As String
            If productColorCodeId > 0 Then
                productId = clsNDC.GetSqlInteger(0, "SELECT 商品ID FROM tbl402商品色コード WHERE (商品色コードID = {0})", productColorCodeId)
                gengaFile = clsNDC.GetSqlString("SELECT 原画ファイル FROM tbl402商品色コード WHERE (商品色コードID = {0})", productColorCodeId)
            Else
                productId = clsNDC.GetSqlInteger(0, "SELECT 商品ID FROM tbl813Webページ画像親 WHERE (Webページ画像親ID = {0})", -productColorCodeId)
                gengaFile = clsNDC.GetSqlString("SELECT 原画ファイル FROM tbl813Webページ画像親 WHERE (Webページ画像親ID = {0})", -productColorCodeId)
            End If

            Dim basePath As String = String.Empty
            If Not cls商品.商品画像フォルダ(productId, False, basePath) Then
                Return False
            End If

            If String.IsNullOrEmpty(gengaFile) Then
                Return clsNDC.EM("原画ファイルが登録されていません。", "エラー", ask)
            End If

            fullPath = String.Format("{0}\03_最終\{1}", basePath, gengaFile)
            If File.Exists(fullPath) Then
                Return True
            Else
                Return clsNDC.EM(String.Format("登録されたファイル {0} が存在しません。", fullPath), "エラー", ask)
            End If
        Catch ex As Exception
            Return clsNDC.EM("原画ファイル(フルパス)の取得に失敗しました。" & ex.Message, "エラー", ask)
        End Try
    End Function

    Public Shared Function 商品画像フォルダ(ByVal productId As Integer, ByVal ifDisplay As Boolean, ByRef basePath As String, Optional subDir As String = "") As Boolean
        Try
            If productId <= 0 Then
                Return False
            End If

            Dim strBrand As String = String.Empty
            Select Case clsNDC.GetSqlShort(0, "SELECT ブランドID FROM tbl401商品 WHERE (商品ID = {0})", productId)
                Case 8 : strBrand = "4100_juggling\"
                Case 9 : strBrand = "4200_balloon\"
                Case 10 : strBrand = "4300_bar\"
                Case Else
                    strBrand = "4900_company\"
            End Select
            basePath = String.Format("{0}\naranja\4000_商品画像\{1}ID{2:0000000}", clsNDC.dfsRoot, strBrand, productId)
            If Not Directory.Exists(basePath) Then Directory.CreateDirectory(basePath)
            Dim strSubDirectory() As String = New String() {
                "01_写真", "01_写真\history",
                "02_編集", "02_編集\history",
                "03_最終", "05_Web掲載リソース", "98_その他", "99_過去データ"}
            For intIndex = 0 To strSubDirectory.Length - 1
                If Not Directory.Exists(basePath & "\" & strSubDirectory(intIndex)) Then
                    Directory.CreateDirectory(basePath & "\" & strSubDirectory(intIndex))
                End If
            Next
            If Not String.IsNullOrEmpty(subDir) AndAlso Directory.Exists(basePath & "\" & subDir) Then
                basePath = basePath & "\" & subDir
            End If
            If ifDisplay Then
                System.Diagnostics.Process.Start("EXPLORER.EXE", "/n, " & basePath)
            End If

            Return True
        Catch ex As Exception
            MessageBox.Show("画像フォルダの確認および作成に失敗しました。")
            Return False
        End Try
    End Function

    ''' <summary>編集用ソースは psd/ai（02_編集）。原画正本は 03_最終 の .svg（NDC と同規約）。</summary>
    Public Const 編集サブフォルダ As String = "02_編集"
    Public Const 編集Historyサブフォルダ As String = "02_編集\history"
    Public Const 最終サブフォルダ As String = "03_最終"

    Public Shared Function 編集Historyフォルダ(productBasePath As String, Optional createIfMissing As Boolean = True) As String
        Dim dir As String = Path.Combine(productBasePath, 編集Historyサブフォルダ)
        If createIfMissing AndAlso Not Directory.Exists(dir) Then
            Directory.CreateDirectory(dir)
        End If
        Return dir
    End Function

    Public Shared Function getHashOfFile(fileFullPath As String, ByRef resultString As String) As Boolean
        resultString = String.Empty
        Try
            If String.IsNullOrEmpty(fileFullPath) OrElse Not File.Exists(fileFullPath) Then
                Return False
            End If
            Using fs As New FileStream(fileFullPath, FileMode.Open, FileAccess.Read, FileShare.Read)
                Using md5 As MD5 = MD5.Create()
                    Dim bs As Byte() = md5.ComputeHash(fs)
                    resultString = BitConverter.ToString(bs).ToLowerInvariant().Replace("-", "")
                End Using
            End Using
            Return True
        Catch
            Return False
        End Try
    End Function

    ''' <summary>
    ''' 編集ファイルを 02_編集\history\{stem}_yyyyMMddHHmmss{ext} へ移動。
    ''' compareWithFullPath 指定時はハッシュ同一なら移動せず False。
    ''' </summary>
    Public Shared Function 編集ファイルをHistoryへ退避(
            editFileFullPath As String,
            Optional compareWithFullPath As String = Nothing,
            Optional force As Boolean = False) As Boolean
        Try
            If String.IsNullOrEmpty(editFileFullPath) OrElse Not File.Exists(editFileFullPath) Then
                Return False
            End If

            Dim editDir As String = Path.GetDirectoryName(editFileFullPath)
            If editDir Is Nothing OrElse Not editDir.EndsWith("\" & 編集サブフォルダ, StringComparison.OrdinalIgnoreCase) Then
                Return False
            End If

            If Not force AndAlso Not String.IsNullOrEmpty(compareWithFullPath) AndAlso File.Exists(compareWithFullPath) Then
                Dim hashEdit As String = String.Empty
                Dim hashNew As String = String.Empty
                If getHashOfFile(editFileFullPath, hashEdit) AndAlso
                   getHashOfFile(compareWithFullPath, hashNew) AndAlso
                   String.Equals(hashEdit, hashNew, StringComparison.OrdinalIgnoreCase) Then
                    Return False
                End If
            End If

            Dim productBase As String = editDir.Substring(0, editDir.Length - ("\" & 編集サブフォルダ).Length)
            Dim historyDir As String = 編集Historyフォルダ(productBase, True)
            Dim stem As String = Path.GetFileNameWithoutExtension(editFileFullPath)
            Dim ext As String = Path.GetExtension(editFileFullPath)
            Dim stamp As String = DateTime.Now.ToString("yyyyMMddHHmmss")
            Dim dest As String = Path.Combine(historyDir, String.Format("{0}_{1}{2}", stem, stamp, ext))
            Dim suffix As Integer = 2
            While File.Exists(dest)
                dest = Path.Combine(historyDir, String.Format("{0}_{1}_{2}{3}", stem, stamp, suffix, ext))
                suffix += 1
            End While
            File.Move(editFileFullPath, dest)
            Return True
        Catch
            Return False
        End Try
    End Function

    ''' <summary>
    ''' 編集用（psd/ai）を 02_編集 へ配置。既存同名かつハッシュ違いなら history へ退避してからコピー。
    ''' 既に同一ハッシュならコピーせずそのパスを返す。
    ''' </summary>
    Public Shared Function 編集フォルダへ配置(
            productId As Integer,
            sourceFileFullPath As String,
            ByRef editFileFullPath As String,
            ByRef errorMessage As String) As Boolean
        editFileFullPath = String.Empty
        errorMessage = String.Empty
        Try
            If productId <= 0 Then
                errorMessage = "商品IDが不正です。"
                Return False
            End If
            If String.IsNullOrEmpty(sourceFileFullPath) OrElse Not File.Exists(sourceFileFullPath) Then
                errorMessage = "ソースファイルがありません。"
                Return False
            End If
            Dim extLower As String = Path.GetExtension(sourceFileFullPath).ToLowerInvariant()
            If extLower <> ".psd" AndAlso extLower <> ".ai" Then
                errorMessage = "編集フォルダへ配置できるのは .psd / .ai のみです。"
                Return False
            End If

            Dim basePath As String = String.Empty
            If Not 商品画像フォルダ(productId, False, basePath) Then
                errorMessage = "商品画像フォルダの取得に失敗しました。"
                Return False
            End If

            Dim editDir As String = Path.Combine(basePath, 編集サブフォルダ)
            If Not Directory.Exists(editDir) Then Directory.CreateDirectory(editDir)
            編集Historyフォルダ(basePath, True)

            Dim destName As String = Path.GetFileName(sourceFileFullPath)
            editFileFullPath = Path.Combine(editDir, destName)

            If String.Equals(
                Path.GetFullPath(sourceFileFullPath),
                Path.GetFullPath(editFileFullPath),
                StringComparison.OrdinalIgnoreCase) Then
                Return True
            End If

            If File.Exists(editFileFullPath) Then
                Dim hashEdit As String = String.Empty
                Dim hashSrc As String = String.Empty
                If getHashOfFile(editFileFullPath, hashEdit) AndAlso
                   getHashOfFile(sourceFileFullPath, hashSrc) AndAlso
                   String.Equals(hashEdit, hashSrc, StringComparison.OrdinalIgnoreCase) Then
                    Return True
                End If
                If Not 編集ファイルをHistoryへ退避(editFileFullPath, force:=True) AndAlso File.Exists(editFileFullPath) Then
                    errorMessage = "既存の編集ファイルを history へ退避できませんでした。"
                    Return False
                End If
            End If

            File.Copy(sourceFileFullPath, editFileFullPath, True)
            Return True
        Catch ex As Exception
            errorMessage = ex.Message
            Return False
        End Try
    End Function
#End Region




End Class
