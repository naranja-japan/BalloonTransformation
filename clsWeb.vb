#Disable Warning IDE1006 ' 命名スタイル

Imports System.Reflection.MethodBase

'↓こちら警告削除用
<Assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA2211:Mark members as static", Justification:="UI クラスは全インスタンスメソッドとして扱う")>
Public Class clsWeb

#Region "定数・メンバ変数"

    Public Const WEB_SERVER_NUMBER_W3C As Integer = 9 'Mark Up Validation用 192.168.1.21@オフィス 192.168.21.21@センター

    Public Const WEB_SERVER_NUMBER_DEV_WEB2021 As Integer = 4
    Public Const WEB_SERVER_NUMBER_TEST_WEB2021 As Integer = 5
    Public Const WEB_SERVER_NUMBER_STAGING_WEB2021 As Integer = 6
    Public Const WEB_SERVER_NUMBER_WWW_WEB2021 As Integer = 7
    '2023/07/28 楽天FTP→SFTPに伴い、元々のナランハのSFTP接続プログラムを流用
    Public Const WEB_SERVER_NUMBER_RAKUTEN_CABINET As Integer = 102
    Public Const WEB_SERVER_NUMBER_RAKUTEN_GOLD As Integer = 103

    Public Const LARGE_ROOT_PASSWORD As String = "5$qb+12-79PN"
    Public Const VPS_ROOT_PASSWORD As String = "yuZeA/=yF1-8"

    'true で ソースチェック(携帯サイト表示)を常時抑制
    Public Shared sourceCheckDisabled As Boolean = False

    '各種ディレクトリ
    'プログラム用(画像・リソース以外のファイルでgit管理外の物を利用する際に利用)
    Public Const WEB_PROJECT_NDC As String = "\\naranja.local\dfs02\naranja\2000_Web\web2015"
    Public Const WEB_PROJECT_NDC_WEB_2021 As String = "\\naranja.local\dfs02\naranja\2000_Web\naranja-web-2021"

    'プログラム用(主にDBに格納する画像データが/img/...で始まるため、その親となるパスをこの変数で保存
    Public Const WEBROOT_NDC As String = "\\naranja.local\dfs02\naranja\2000_Web\naranja-web-2021\backend\public"
    Public Const WEBROOT_NDC_WEB_2021 As String = "\\naranja.local\dfs02\naranja\2000_Web\naranja-web-2021\backend\public"

    'このフォルダの画像はgit管理外
    Public Shared WEBROOT_IMG_DB_SERVER As String = "\\naranja.local\dfs02\naranja\2000_Web\naranja-web-2021\backend\public\img\db"
    Public Shared WEBROOT_IMG_DB_SRC As String = "\\naranja.local\dfs02\naranja\2000_Web\naranja-web-2021\backend\public\img\db-src"

    '上記に対応する楽天キャビネットパス
    Public Const RAKUTEN_CABINET_IMG_DB As String = "https://image.rakuten.co.jp/naranja-japan/cabinet"

    '適宜最新バージョンに入れ替えてください。
    Public Shared defaultUserAgentPc As String = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:142.0) Gecko/20100101 Firefox/142.0"
    Public Shared defaultUserAgentSp As String = "Mozilla/5.0 (iPhone; CPU iPhone OS 14_6 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/14.0.3 Mobile/15E148 Safari/604.1"

    'Web2015:0-3 naranja-web-2021:4-7 移行時に再度考慮
    '↓そう、これらはすっきりするはず。
    Public Shared domain As String() = {"localhost", "test.naranja.co.jp", "staging.naranja.co.jp", "www.naranja.co.jp", "naranja-web-2021.local.localhost", "naranja-web-2021-dev.net", "www-naranja-co-jp.net", "www.naranja.co.jp"}
    '↑web2021移行後常時SSLになると↑↓これは統合できるが今は分離。
    Public Shared baseUrl As String() = {"http://localhost", "http://test.naranja.co.jp", "http://staging.naranja.co.jp", "http://www.naranja.co.jp", "https://naranja-web-2021.local.localhost", "https://naranja-web-2021-dev.net", "https://www-naranja-co-jp.net", "https://www.naranja.co.jp"}

    Public Shared domainName As String() = {"vagrant", "test", "staging", "www", "vagrant", "test", "staging", "www"}
    'web2015移行後、名前解決完了後に 54.178.52.225 を 正しく変更
    Public Shared hostedServer As String() = {"192.168.33.10", "vps.naranja.co.jp", "www.naranja.co.jp", "www.naranja.co.jp", "127.0.0.1", "naranja-web-2021-dev.net", "www-naranja-co-jp.net", "54.178.52.225"}

    '画像サイズ 特大/大/小/極小
    Public Shared webPictureWidth As Integer() = {800, 400, 150, 60}
    Public Shared webPictureHeight As Integer() = {800, 400, 150, 60}

    'パスワードの設定について --- 本ソースは権限者のみ閲覧可能
    '変更時は、以下をバックアップし、
    '以下を書き換え、UNIXコマンドで変更をし、
    '関係者に連絡。
    Public Shared pwWwwNaranjaweb As String = "Ab:8C@yb0owg"
    Public Shared pwWwwRoot As String = "5$qb+12-79PN"
    Public Shared pwVpsNaranjaweb As String = "m6MBM+ONZ1uL"
    Public Shared pwVpsRoot As String = "yuZeA/=yF1-8"

#End Region


#Region "Avif/Webp"

    Public Shared Function WebImgDbServer保存(filesFullPathSource As List(Of String)) As Boolean

        '失敗を気にしない
        clsWeb.GifPng2AvifWebp(filesFullPathSource)
        Return True

    End Function

    Public Shared Function WebSrcFullPath2WebServerFullPath(fileFullPathSrc As String, ByRef fileFullPathServer As String, convertWebp As Boolean) As Boolean
        'ファイル名をdb-srcからdbに変更。.webpへの変更時のみtrueを返す。
        Dim extension As String = Path.GetExtension(fileFullPathSrc).ToLower
        Dim converted As Boolean = False
        If extension = ".png" OrElse extension = ".gif" Then
            '静止画/動画GIF
            extension = ".webp"
            converted = True
            If convertWebp Then
                clsWeb.GifPng2AvifWebp(New List(Of String) From {fileFullPathSrc})
            End If
        End If

        If fileFullPathSrc.StartsWith(clsWeb.WEBROOT_IMG_DB_SRC) Then
            fileFullPathSrc = String.Format("{0}{1}", clsWeb.WEBROOT_IMG_DB_SERVER, fileFullPathSrc.Substring(clsWeb.WEBROOT_IMG_DB_SRC.Length))
        End If

        fileFullPathServer = String.Format("{0}\{1}{2}", Path.GetDirectoryName(fileFullPathSrc), Path.GetFileNameWithoutExtension(fileFullPathSrc), extension)

        'webpに変換されていたらTrue
        Return converted

    End Function
    Public Shared Function GifPng2AvifWebp(filesFullPath As List(Of String), Optional convertPattern As Integer = 0, Optional quality As Integer = 60) As Boolean
        'convertPattern 0:  clsWeb.WEBROOT_IMG_DB_SCR から clsWeb.WEBROOT_IMG_DB_SERVER (webpのみ)
        'convertPattern 1:  同位置 (webpのみ)
        'convertPattern 2:  同位置 (avifのみ)
        If filesFullPath.Count = 0 Then
            Return True
        End If
        'フルパスのリスト
        Try
            'テクニカルですが、filesFullPathのうち「共通のフォルダ」までを保存し
            'そこから先をフォルダ込みでsrcに移して変換
            Dim commonDirectories As New List(Of String)
            Dim currentFileDirectories As New List(Of String)
            For Each fileFullpath In filesFullPath
                currentFileDirectories.Clear()
                currentFileDirectories.AddRange(Strings.Split(Path.GetDirectoryName(fileFullpath), "\"))
                If commonDirectories.Count = 0 Then
                    commonDirectories.AddRange(currentFileDirectories)
                Else
                    Dim maxDirIndex As Integer = Math.Min(commonDirectories.Count, currentFileDirectories.Count)
                    Dim dirIndex As Integer = 0
                    Do While dirIndex < maxDirIndex
                        If commonDirectories(dirIndex) <> currentFileDirectories(dirIndex) Then
                            'ここから後ろを削除
                            For dirIndexToDelete As Integer = dirIndex To commonDirectories.Count - 1
                                commonDirectories.RemoveAt(dirIndex)
                            Next
                            Exit Do
                        End If
                        dirIndex += 1
                    Loop
                End If
            Next

            '共通フォルダ抽出終了
            Dim commonDirectoryPath As String = String.Empty
            For dirIndex As Integer = 0 To commonDirectories.Count - 1
                commonDirectoryPath &= commonDirectories(dirIndex) & "\"
            Next

            Dim nodeSharpFolder As String = "C:\Users\Public\NARANJA\node\sharp"
            Dim nodeSharpSrcFolder As String = String.Format("{0}\src", nodeSharpFolder)
            Dim nodeSharpDestFolder As String = String.Format("{0}\dest", nodeSharpFolder)

            Dim avifPrefix As String
            If convertPattern = 2 Then
                'avifを作る場合にはwebpも作らなければおかしくなるヘンテコなバグ
                avifPrefix = "avif-"
            Else
                avifPrefix = String.Empty
            End If

            Dim batFile As String = String.Format("{0}\_{1}webp-converter-{2:000}.bat", nodeSharpFolder, avifPrefix, quality)
            If Not File.Exists(batFile) Then
                If MessageBox.Show(String.Format("Webp変換のために必要なファイルがローカルにコピーされていません。{0}{0}" &
                                                 "OKをクリックでコピーします。{0}{0}よろしいですか？{0}※少々時間がかかります。", vbCrLf), "Webp変換", MessageBoxButtons.OKCancel) <> DialogResult.OK Then
                    Return False
                End If
                Dim sharpDir As String = "\NARANJA\node\sharp"
                Dim sharpDirServer As String = String.Format("{0}\naranja\9000_NDC\クライアント設定系{1}", clsNDC.dfsRoot, sharpDir)
                Dim sharpDirLocal As String = String.Format("C:\Users\Public{0}", sharpDir)
                clsNDC.MirrorFolder(False, #2049/1/1#, sharpDirServer, sharpDirLocal)
            End If

            If Not Directory.Exists(nodeSharpDestFolder) Then
                Directory.CreateDirectory(nodeSharpDestFolder)
            End If

            clsWeb.clearDirectory(nodeSharpSrcFolder)
            clsWeb.clearDirectory(nodeSharpDestFolder)

            Dim copyToFullPath As String
            Dim copyToDirectory As String
            For Each fileFullpath In filesFullPath
                '共通フォルダ以下をSrcフォルダにコピー
                copyToFullPath = String.Format("{0}\{1}", nodeSharpSrcFolder, fileFullpath.Substring(commonDirectoryPath.Length))
                copyToDirectory = Path.GetDirectoryName(copyToFullPath)
                If Not Directory.Exists(copyToDirectory) Then
                    Directory.CreateDirectory(copyToDirectory)
                End If
                File.Copy(fileFullpath, copyToFullPath, True)
            Next

            Dim startInfo As New ProcessStartInfo(batFile)
            Dim p As Process = Process.Start(startInfo)
            If Not p.WaitForExit(180000) Then
                Return clsNDC.EM("png/gif → avif/webp タイムアウト")
            End If

            '書き戻し
            Dim copyFromDirectory As String
            Dim fileNameWoExtension As String
            Dim targetDirectory As String
            For Each fileFullpath In filesFullPath
                copyFromDirectory = Path.GetDirectoryName(String.Format("{0}\{1}", nodeSharpDestFolder, fileFullpath.Substring(commonDirectoryPath.Length)))
                fileNameWoExtension = Path.GetFileNameWithoutExtension(fileFullpath)
                targetDirectory = Path.GetDirectoryName(fileFullpath)
                If convertPattern = 0 Then
                    If Not fileFullpath.StartsWith(clsWeb.WEBROOT_IMG_DB_SRC) Then
                        Return clsNDC.EM(String.Format("ファイル{0}{0}{1}{0}{0}がimg/db-src フォルダ({2})に含まれていません。", vbCrLf, fileFullpath, clsWeb.WEBROOT_IMG_DB_SRC))
                    End If
                    targetDirectory = Replace(targetDirectory, clsWeb.WEBROOT_IMG_DB_SRC, clsWeb.WEBROOT_IMG_DB_SERVER)
                End If
                If Not Directory.Exists(targetDirectory) Then
                    Directory.CreateDirectory(targetDirectory)
                End If
                If convertPattern = 2 Then
                    File.Copy(String.Format("{0}\{1}.avif", copyFromDirectory, fileNameWoExtension), String.Format("{0}\{1}.avif", targetDirectory, fileNameWoExtension), True)
                Else
                    File.Copy(String.Format("{0}\{1}.webp", copyFromDirectory, fileNameWoExtension), String.Format("{0}\{1}.webp", targetDirectory, fileNameWoExtension), True)
                End If
            Next

            Return True
        Catch ex As Exception
            Return clsNDC.EM(ex)
        End Try

    End Function
    Private Shared Function clearDirectory(directoryFullPath As String) As Boolean
        Try
            Dim dirInfo As New DirectoryInfo(directoryFullPath)
            If Not clsWeb.RemoveReadonlyAttribute(dirInfo) Then
                Return clsNDC.EM("clsWeb.RemoveReadonlyAttribute(dirInfo) 例外")
            End If

            For Each fi As FileInfo In dirInfo.GetFiles()
                fi.Delete()
            Next fi
            For Each di As DirectoryInfo In dirInfo.GetDirectories()
                di.Delete(True)
            Next di
            Return True
        Catch ex As Exception
            Return clsNDC.EM(ex)
        End Try
    End Function

    Public Shared Function RemoveReadonlyAttribute(ByVal dirInfo As DirectoryInfo) As Boolean
        Try
            If (dirInfo.Attributes And FileAttributes.ReadOnly) = FileAttributes.ReadOnly Then
                dirInfo.Attributes = FileAttributes.Normal
            End If
            For Each fi As FileInfo In dirInfo.GetFiles()
                If (fi.Attributes And FileAttributes.ReadOnly) = FileAttributes.ReadOnly Then
                    fi.Attributes = FileAttributes.Normal
                End If
            Next fi
            For Each di As DirectoryInfo In dirInfo.GetDirectories()
                If Not clsWeb.RemoveReadonlyAttribute(di) Then
                    Return False
                End If
            Next di
            Return True
        Catch ex As Exception
            Return clsNDC.EM(ex)
        End Try
    End Function

#End Region


#Region "画像・リソース"

    Public Shared Function Web代表画像(webPageId As Integer, imageType As Integer, ByRef fileFullPath As String) As Boolean
        'imageType 0-3 clsWeb.webPictureWidth(imageType) を参照
        Dim htSqlParams As New Hashtable
        clsNDC.SetSqlParams(htSqlParams, "@WebページID", SqlDbType.Int, 0, webPageId)

        Dim webItemIdExtended As Integer = clsNDC.GetSqlInteger(0, "SELECT Webページ画像統合ID FROM tbl815Webページ画像統合 WHERE (WebページID = {0}) AND (画像代表 = 1)", webPageId)
        If webItemIdExtended = 0 Then
            Return clsNDC.EM("Web代表画像の取得に失敗しました。")
        End If

        Dim productColorCodeId As Integer

        If webItemIdExtended > 0 Then
            'この場合だけ、productColorCodeIdに変換
            productColorCodeId = clsNDC.GetSqlInteger(0, "SELECT 商品色コードID FROM tbl802Webアイテム WHERE (WebアイテムID = {0})", webItemIdExtended)
        Else
            productColorCodeId = webItemIdExtended
        End If

        Return clsWeb.Web画像ファイル(productColorCodeId, imageType, fileFullPath)

    End Function

    'productColorCodeId > 0 --- 商品色コードIDより productColorCodeId < 0 --- Webページ画像親IDより
    'imageType 0-3 clsWeb.webPictureWidth(imageType) を参照 
    'imgDbSrc=true: clsWeb.WEBROOT_IMG_DB_SRC(.png/.gif)フォルダ /img/db-src 以下の規約の画像ファイルを取得
    'imgDbSrc=false: clsWeb.WEBROOT_IMG_DB_SERVER(.webp)フォルダ /img/db 以下の規約の画像ファイルを取得

    Public Shared Function Web画像ファイル(productColorCodeId As Integer, imageType As Integer, ByRef fileFullPath As String,
                                     Optional imgDbSrc As Boolean = True,
                                     Optional ask As Boolean = False,
                                     Optional gifFile As Boolean = False) As Boolean

        Dim webImageDbFolder As String

        If productColorCodeId = 0 Then
            fileFullPath = String.Empty
            Return clsNDC.EM("Web画像ファイル - 商品色コードID取得失敗", "エラー", ask)
        ElseIf productColorCodeId > 0 Then
            webImageDbFolder = "product-color-code"
        Else
            webImageDbFolder = "web-page-images"
            productColorCodeId = -productColorCodeId
        End If

        Dim extension As String
        If gifFile Then
            extension = ".gif"
        ElseIf imgDbSrc Then
            extension = ".png"
        Else
            extension = ".webp"
        End If

        Dim imgDbFolder As String
        If imgDbSrc Then
            imgDbFolder = clsWeb.WEBROOT_IMG_DB_SRC
        Else
            imgDbFolder = clsWeb.WEBROOT_IMG_DB_SERVER
        End If

        fileFullPath = String.Format("{0}\{1}\id{2:0000}000\id{3:0000000}-{4}{5}", imgDbFolder, webImageDbFolder, productColorCodeId \ 1000, productColorCodeId, clsWeb.webPictureWidth(imageType), extension)

        'clsWeb.WEBROOT_IMG_DB_SERVER内のファイルが無ければ、clsWeb.WEBROOT_IMG_DB_SCRより作成
        If Not imgDbSrc AndAlso Not File.Exists(fileFullPath) Then
            Dim fileFromOriginalSrc As String = String.Format("{0}\{1}\id{2:0000}000\id{3:0000000}-{4}{5}", clsWeb.WEBROOT_IMG_DB_SRC, webImageDbFolder, productColorCodeId \ 1000, productColorCodeId, clsWeb.webPictureWidth(imageType), extension)
            If File.Exists(fileFromOriginalSrc) Then
                clsWeb.GifPng2AvifWebp(New List(Of String) From {fileFromOriginalSrc})
            End If
        End If

        If File.Exists(fileFullPath) Then
            Return True
        Else
            '2021/06/15 過去画像置き場を策定 同じフォルダの\img → \img-unknown-deleted (ask=trueの場合のみ)
            Dim fileFullPathUnknownDeleted As String = Replace(fileFullPath, "\img\db-src\", "\img-unknown-deleted\db-src\")
            If ask AndAlso File.Exists(fileFullPathUnknownDeleted) Then
                fileFullPath = fileFullPathUnknownDeleted
                Return True
            End If

            Return clsNDC.EM(String.Format("画像ファイル{0}{1}{0}が存在しないか、取得に失敗しました。", vbCrLf, fileFullPath), "エラー", ask)
        End If

    End Function

    'webPageId に属する画像ファイルを取得
    'imageType 0-3 clsWeb.webPictureWidth(imageType) を参照 
    '/img/db 以下の規約の画像ファイルを取得
    'Public Shared Function Webページ関連ファイル取得(webPageId As Integer, ByRef imgSources As ArrayList) As Boolean
    '関連する商品色コードIDおよび画像親IDを取得


    '★ついでに商品ページの「画像フォルダ」
    '★各Webページの「画像フォルダ」についての仕様も確定せよ。
    'Dim webImageDbFolder As String

    'If productColorCodeId = 0 Then
    '    fileFullPath = String.Empty
    '    Return clsNDC.EM("Web画像ファイル - 商品色コードID取得失敗", "エラー", ask)
    'ElseIf productColorCodeId > 0 Then
    '    webImageDbFolder = "product-color-code"
    'Else
    '    webImageDbFolder = "web-page-images"
    '    productColorCodeId = -productColorCodeId
    'End If
    'fileFullPath = String.Format("{0}\{1}\id{2:0000}000\id{3:0000000}-{4}.png", clsWeb.WEBROOT_IMG_DB, webImageDbFolder, productColorCodeId \ 1000, productColorCodeId, clsWeb.webPictureWidth(imageType))
    'If File.Exists(fileFullPath) Then
    '    Return True
    'Else
    '    Return clsNDC.EM(String.Format("画像ファイル{0}{1}{0}が存在しないか、取得に失敗しました。", vbCrLf, fileFullPath), "エラー", ask)
    'End If

    'End Function

    'メタデータ --- ファイル名(拡張子付)_meta.txt 形式。ただし、拡張子がmp4またはwebmの場合、ファイル名(拡張子無)_mp4_webm_meta.txt
    '[key]=value 形式で設定。以下に設定内容
    'title
    'width
    'height

    Public Shared Function メタデータファイル取得(ByVal fileFullPath As String, ByRef metaFileFullPath As String) As Boolean
        If Not File.Exists(fileFullPath) Then
            Return clsNDC.EM("指定されたファイル " & fileFullPath & " は存在しません。")
        End If

        metaFileFullPath = fileFullPath & "_meta.txt"
        If Path.GetExtension(fileFullPath).Equals(".mp4", StringComparison.OrdinalIgnoreCase) OrElse
                Path.GetExtension(fileFullPath).Equals(".webm", StringComparison.OrdinalIgnoreCase) Then
            metaFileFullPath = Regex.Replace(metaFileFullPath, "\.(mp4|webm)_meta\.txt", "_mp4_webm_meta.txt")
        End If

        Return True

    End Function

    Public Shared Function メタデータ設定(ByVal fileFullPath As String, ByVal key As String, ByVal value As String) As Boolean
        Try
            Dim ht As New Hashtable
            Dim metaFileFullPath As String = String.Empty
            If Not clsWeb.メタデータファイル取得(fileFullPath, metaFileFullPath) Then
                Return False
            End If

            'メタデータファイルが無くてもそのまま続行
            clsWeb.メタデータ取得(fileFullPath, ht)

            ht(key) = value
            Using sw As New StreamWriter(metaFileFullPath, False, Encoding.UTF8)
                For Each eachKey In ht.Keys
                    sw.Write("[{0}]={1}", eachKey, ht(eachKey))
                    sw.WriteLine()
                Next
            End Using

            Return True

        Catch ex As Exception
            Return clsNDC.EM(ex, GetCurrentMethod.Name)
        End Try

    End Function

    Public Shared Function メタデータ取得(ByVal fileFullPath As String, ByRef ht As Hashtable) As Boolean
        Try
            '基本は clsWeb.WEBROOT_IMG_DB_SRC内に設定
            Dim metaFileFullPath As String = String.Empty
            If Not clsWeb.メタデータファイル取得(fileFullPath, metaFileFullPath) Then
                Return False
            End If

            ht.Clear()

            If File.Exists(metaFileFullPath) Then
                Using sr As New StreamReader(metaFileFullPath)
                    Do While sr.Peek() >= 0
                        Dim line As String = sr.ReadLine
                        If Not String.IsNullOrEmpty(line) AndAlso Regex.IsMatch(line, "^\[[A-Za-z0-9_]+\]=.+$") Then
                            Dim key As String = Regex.Match(line, "^\[(?<1>[A-Za-z0-9_]+)\]=.+$").Groups(1).Value
                            Dim value As String = Regex.Match(line, "^\[[A-Za-z0-9_]+\]=(?<1>.+)$").Groups(1).Value
                            ht(key) = value
                        End If
                    Loop
                End Using
            Else
                Return False
            End If
            Return True

        Catch ex As Exception
            Return clsNDC.EM(ex, GetCurrentMethod.Name)
        End Try

    End Function

    Public Shared Function メタデータ消去(ByVal fileFullPath As String) As Boolean
        Try
            Dim metaFileFullPath As String = String.Empty
            If Not clsWeb.メタデータファイル取得(fileFullPath, metaFileFullPath) Then
                Return False
            End If

            File.Delete(metaFileFullPath)
            Return True

        Catch ex As Exception
            Return clsNDC.EM(ex, GetCurrentMethod.Name)
        End Try

    End Function

#End Region


End Class
