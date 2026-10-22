Imports System.Globalization
Imports System.IO
Imports System.Runtime.InteropServices
Imports System.Text
Imports System.Text.RegularExpressions
Imports System.Threading
Imports ExCSS
Imports Microsoft.Identity.Extensions
Imports OpenCvSharp
Imports System.Xml.Linq
Imports SHDocVw
Imports OCV = OpenCvSharp
Imports SDI = System.Drawing.Imaging
Imports SK = SkiaSharp
Imports SS = Svg.Skia

'↓こちら警告削除用
<Assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification:="UI クラスは全インスタンスメソッドとして扱う")>
Public Class MainForm
    Inherits Form

#Region "初期設定 / ユーティリティ / 更新・実行・進捗"

#Region "定数(固定パス) 宣言"
    '========================  WinAPI  ========================
    Private Const SW_RESTORE As Integer = 9

    <Runtime.InteropServices.DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function SetForegroundWindow(hWnd As IntPtr) As Boolean
    End Function

    <Runtime.InteropServices.DllImport("user32.dll", SetLastError:=True)>
    Private Shared Function ShowWindow(hWnd As IntPtr, nCmdShow As Integer) As Boolean
    End Function

    '========================  固定パス  ========================
    Private ReadOnly INPUT_DIR As String = "C:\Users\Public\NARANJA\local\BalloonTransformation\input"
    Private ReadOnly MASK_DIR As String = "C:\Users\Public\NARANJA\local\BalloonTransformation\mask"
    Private ReadOnly OUTPUT_DIR As String = "C:\Users\Public\NARANJA\local\BalloonTransformation\output"
    Private ReadOnly MASK_ARCHIVE_DIR As String = "N:\naranja\9000_NDC\Data\Images\LatexBalloonMaskArchive"

    '========================  UI  ========================
    Private ReadOnly ButtonRun As New Button()
    Private ReadOnly ButtonCancel As New Button()
    Private ReadOnly ButtonRefresh As New Button()
    Private ReadOnly MainProgressBar As New ProgressBar()
    Private ReadOnly MainProgressLabel As New Label()
    Private ReadOnly InputCaptionLabel As New Label()
    Private ReadOnly InputPathLinkLabel As New LinkLabel()
    Private ReadOnly MaskCaptionLabel As New Label()
    Private ReadOnly MaskPathLinkLabel As New LinkLabel()
    Private ReadOnly OutputCaptionLabel As New Label()
    Private ReadOnly OutputPathLinkLabel As New LinkLabel()
    Private ReadOnly ArchiveCaptionLabel As New Label()
    Private ReadOnly ArchivePathLinkLabel As New LinkLabel()
    Private ReadOnly CountsLabel As New Label()
    Private ReadOnly InputPictureBox As New PictureBox()
    Private ReadOnly OutputPictureBox As New PictureBox()
    Private ReadOnly InputBadgeLabel As New Label()
    Private ReadOnly InputFullPathLabel As New Label()
    Private ReadOnly OutputBadgeLabel As New Label()
    Private ReadOnly OutputFullPathLabel As New Label()
    Private ReadOnly MaskPreviewPictureBoxes(8) As PictureBox
    Private ReadOnly MaskScaleLabels(8) As Label
    Private ReadOnly LogTextBox As New TextBox()

    '========================  メンバ  ========================
    Private workingInputFileFullPath As String
    Private workingMaskFileFullPath As String

    Private ProcessingCancellationTokenSource As CancellationTokenSource
    Private errorTotalCount As Integer = 0
    Private ReadOnly WORKING_ZOOM_PIXEL As Integer = 3000

    'webpに変換するファイルたち
    Dim srcFilesToConvert As List(Of String)
#End Region

#Region "コンストラクタ"

    Public Sub New()
        Directory.CreateDirectory(INPUT_DIR)
        Directory.CreateDirectory(MASK_DIR)
        Directory.CreateDirectory(OUTPUT_DIR)
        Directory.CreateDirectory(MASK_ARCHIVE_DIR)

        Me.Text = "BalloonTransformation UI"
        Me.Width = 1180
        Me.Height = 900
        Me.StartPosition = FormStartPosition.CenterScreen
        Me.Font = New Font("Segoe UI", 9)

        ' 実行・キャンセル・更新ボタン
        ButtonRun.Text = "実行 (Run)"
        ButtonRun.SetBounds(20, 10, 80, 40)

        ButtonCancel.Text = "キャンセル"
        ButtonCancel.SetBounds(120, 10, 80, 40)
        ButtonCancel.Enabled = False

        ButtonRefresh.Text = "更新 (Rescan)"
        ButtonRefresh.SetBounds(220, 10, 80, 40)

        Dim baseY As Integer = 60
        Dim stepY As Integer = 22

        ' パス表示（入力・マスク・出力・保管）
        InputCaptionLabel.Text = "入力:"
        InputCaptionLabel.SetBounds(20, baseY, 40, 22)

        InputPathLinkLabel.Text = INPUT_DIR
        InputPathLinkLabel.SetBounds(42, baseY, 400, 22)
        InputPathLinkLabel.AutoEllipsis = True

        MaskCaptionLabel.Text = "マスク:"
        MaskCaptionLabel.SetBounds(20, baseY + stepY, 40, 22)

        MaskPathLinkLabel.Text = MASK_DIR
        MaskPathLinkLabel.SetBounds(42, baseY + stepY, 400, 22)
        MaskPathLinkLabel.AutoEllipsis = True

        OutputCaptionLabel.Text = "出力:"
        OutputCaptionLabel.SetBounds(20, baseY + stepY * 2, 40, 22)

        OutputPathLinkLabel.Text = OUTPUT_DIR
        OutputPathLinkLabel.SetBounds(42, baseY + stepY * 2, 400, 22)
        OutputPathLinkLabel.AutoEllipsis = True

        ArchiveCaptionLabel.Text = "保管:"
        ArchiveCaptionLabel.SetBounds(20, baseY + stepY * 3, 40, 22)

        ArchivePathLinkLabel.Text = MASK_ARCHIVE_DIR
        ArchivePathLinkLabel.SetBounds(42, baseY + stepY * 3, 400, 22)
        ArchivePathLinkLabel.AutoEllipsis = True

        CountsLabel.SetBounds(20, 130, 520, 22)

        ' 入力画像プレビュー
        InputPictureBox.SetBounds(20, 160, 300, 300)
        InputPictureBox.SizeMode = PictureBoxSizeMode.Zoom
        InputPictureBox.BorderStyle = BorderStyle.FixedSingle

        InputBadgeLabel.Text = "INPUT"
        InputBadgeLabel.BackColor = Drawing.Color.Black
        InputBadgeLabel.ForeColor = Drawing.Color.White
        InputBadgeLabel.TextAlign = ContentAlignment.MiddleCenter
        InputBadgeLabel.SetBounds(InputPictureBox.Left + 6, InputPictureBox.Top + 6, 70, 22)

        InputFullPathLabel.BackColor = Drawing.Color.Black
        InputFullPathLabel.ForeColor = Drawing.Color.White
        InputFullPathLabel.AutoEllipsis = True
        InputFullPathLabel.SetBounds(InputPictureBox.Left + 6, InputPictureBox.Bottom - 26, InputPictureBox.Width - 12, 20)

        ' 出力画像プレビュー
        OutputPictureBox.SetBounds(20, 480, 300, 300)
        OutputPictureBox.SizeMode = PictureBoxSizeMode.Zoom
        OutputPictureBox.BorderStyle = BorderStyle.FixedSingle

        OutputBadgeLabel.Text = "OUTPUT"
        OutputBadgeLabel.BackColor = Drawing.Color.Black
        OutputBadgeLabel.ForeColor = Drawing.Color.White
        OutputBadgeLabel.TextAlign = Drawing.ContentAlignment.MiddleCenter
        OutputBadgeLabel.SetBounds(OutputPictureBox.Left + 6, OutputPictureBox.Top + 6, 80, 22)

        OutputFullPathLabel.BackColor = Drawing.Color.Black
        OutputFullPathLabel.ForeColor = Drawing.Color.White
        OutputFullPathLabel.AutoEllipsis = True
        OutputFullPathLabel.SetBounds(OutputPictureBox.Left + 6, OutputPictureBox.Bottom - 26, OutputPictureBox.Width - 12, 20)

        Dim rightColumnY As Integer = 450

        ' 右側マスクプレビュー 3x3 グリッド
        Dim gridStartX As Integer = rightColumnY
        Dim gridStartY As Integer = 180
        Dim cellWidth As Integer = 150
        Dim cellHeight As Integer = 150

        Dim rightColumnWidth As Integer = cellWidth * 3 + 20

        ' 進捗バー
        Dim mainProgressBarWidth As Integer = 300
        MainProgressBar.SetBounds(rightColumnY, 20, mainProgressBarWidth, 22)
        MainProgressLabel.SetBounds(MainProgressBar.Right + 10, MainProgressBar.Top + 2, rightColumnWidth - mainProgressBarWidth, 22)

        ' ログ表示テキストボックス
        LogTextBox.Multiline = True
        LogTextBox.ScrollBars = ScrollBars.Vertical
        LogTextBox.ReadOnly = True
        LogTextBox.BorderStyle = BorderStyle.FixedSingle
        LogTextBox.SetBounds(rightColumnY, 50, rightColumnWidth, gridStartY - 70)

        Dim maskIndex As Integer = 0
        For rowIndex As Integer = 0 To 2
            For columnIndex As Integer = 0 To 2
                If maskIndex > 8 Then
                    Exit For
                End If

                Dim maskPictureBox As New PictureBox() With {
                    .BorderStyle = BorderStyle.FixedSingle,
                    .SizeMode = PictureBoxSizeMode.Zoom,
                    .BackColor = Drawing.Color.White
                }
                maskPictureBox.SetBounds(
                    gridStartX + columnIndex * (cellWidth + 10),
                    gridStartY + rowIndex * (cellHeight + 40),
                    cellWidth,
                    cellHeight
                )

                Dim maskScaleLabel As New Label() With {
                    .Text = "口巻倍率: ---%",
                    .TextAlign = ContentAlignment.MiddleCenter
                }
                maskScaleLabel.SetBounds(maskPictureBox.Left, maskPictureBox.Bottom + 4, maskPictureBox.Width, 18)

                MaskPreviewPictureBoxes(maskIndex) = maskPictureBox
                MaskScaleLabels(maskIndex) = maskScaleLabel
                maskIndex += 1
            Next
        Next

        Me.Controls.AddRange(
            New Control() {
                ButtonRun, ButtonCancel, ButtonRefresh,
                MainProgressBar, MainProgressLabel, LogTextBox,
                InputCaptionLabel, InputPathLinkLabel,
                MaskCaptionLabel, MaskPathLinkLabel,
                OutputCaptionLabel, OutputPathLinkLabel,
                ArchiveCaptionLabel, ArchivePathLinkLabel,
                CountsLabel,
                InputPictureBox, OutputPictureBox,
                InputBadgeLabel, InputFullPathLabel,
                OutputBadgeLabel, OutputFullPathLabel
            }
        )

        For i As Integer = 0 To 8
            If MaskPreviewPictureBoxes(i) IsNot Nothing Then
                Me.Controls.Add(MaskPreviewPictureBoxes(i))
            End If

            If MaskScaleLabels(i) IsNot Nothing Then
                Me.Controls.Add(MaskScaleLabels(i))
            End If
        Next

        AddHandler ButtonRun.Click, AddressOf OnRunClicked
        AddHandler ButtonCancel.Click, AddressOf OnCancelClicked
        AddHandler ButtonRefresh.Click, AddressOf OnRefreshClicked

        AddHandler InputPathLinkLabel.LinkClicked, Sub() OpenFolder(INPUT_DIR)
        AddHandler MaskPathLinkLabel.LinkClicked, Sub() OpenFolder(MASK_DIR)
        AddHandler OutputPathLinkLabel.LinkClicked, Sub() OpenFolder(OUTPUT_DIR)
        AddHandler ArchivePathLinkLabel.LinkClicked, Sub() OpenFolder(MASK_ARCHIVE_DIR)

        InitRefresh()


        srcFilesToConvert = New List(Of String)
    End Sub

#End Region

#Region "初期更新/再読込"

    Private Sub InitRefresh()
        Try
            LoadMaskPreviews()
            UpdateCounts()
            LogInfo("Init(Refresh)", False)
        Catch ex As Exception
            LogDebug("Init(Refresh) " & ex.Message)
        End Try
    End Sub

    Private Sub OnRefreshClicked(sender As Object, e As EventArgs)
        InitRefresh()
    End Sub

    ''' <summary>
    ''' マスクフォルダ内の SVG を最大 9 枚読み込み、右側の 3x3 グリッドにプレビューを表示。
    ''' ファイル名から -kNNN を読み取って「口巻倍率」をラベルに表示する。
    ''' </summary>
    Private Sub LoadMaskPreviews()
        For i As Integer = 0 To 8
            If MaskPreviewPictureBoxes(i) IsNot Nothing Then
                MaskPreviewPictureBoxes(i).Image = Nothing
            End If

            If MaskScaleLabels(i) IsNot Nothing Then
                MaskScaleLabels(i).Text = "口巻倍率: ---%"
            End If
        Next

        Dim maskSvgPaths As List(Of String) = Directory.EnumerateFiles(MASK_DIR, "*.svg").OrderBy(Function(p) p).Take(9).ToList()
        Dim maskIndex As Integer = 0

        For Each maskSvgPath As String In maskSvgPaths
            Try
                Dim svgLoader As New SS.SKSvg()

                Using svgFileStream As New FileStream(maskSvgPath, FileMode.Open, FileAccess.Read)
                    svgLoader.Load(svgFileStream)
                End Using

                If svgLoader.Picture IsNot Nothing Then
                    Dim pictureBoxWidth As Integer = MaskPreviewPictureBoxes(maskIndex).Width
                    Dim pictureBoxHeight As Integer = MaskPreviewPictureBoxes(maskIndex).Height

                    Dim skImageInfo As New SkiaSharp.SKImageInfo(pictureBoxWidth, pictureBoxHeight)

                    Using surface As SkiaSharp.SKSurface = SkiaSharp.SKSurface.Create(skImageInfo)
                        Dim canvas As SkiaSharp.SKCanvas = surface.Canvas
                        canvas.Clear(SkiaSharp.SKColors.White)

                        Dim cullRect As SkiaSharp.SKRect = svgLoader.Picture.CullRect
                        Dim scale As Single = Math.Min(pictureBoxWidth / cullRect.Width,
                                                       pictureBoxHeight / cullRect.Height)

                        Dim offsetX As Single = (pictureBoxWidth - cullRect.Width * scale) / 2.0F
                        Dim offsetY As Single = (pictureBoxHeight - cullRect.Height * scale) / 2.0F

                        canvas.Translate(offsetX, offsetY)
                        canvas.Scale(scale, scale)
                        canvas.DrawPicture(svgLoader.Picture)
                        canvas.Flush()

                        Using skImage As SkiaSharp.SKImage = surface.Snapshot()
                            Using encoded As SkiaSharp.SKData =
                                skImage.Encode(SkiaSharp.SKEncodedImageFormat.Png, 90)
                                Using memoryStream As New MemoryStream(encoded.ToArray())
                                    MaskPreviewPictureBoxes(maskIndex).Image = New Bitmap(memoryStream)
                                End Using
                            End Using
                        End Using
                    End Using

                    MaskPreviewPictureBoxes(maskIndex).SizeMode = PictureBoxSizeMode.Zoom
                End If

                Dim wrapScalePercent As Integer = ParseKPercent(maskSvgPath)
                MaskScaleLabels(maskIndex).Text = $"口巻倍率: {wrapScalePercent}%"

            Catch ex As Exception
                LogDebug($"MaskPreview {Path.GetFileName(maskSvgPath)} {ex.Message}")
            End Try

            maskIndex += 1
            If maskIndex >= 9 Then
                Exit For
            End If
        Next

        Me.Refresh()
    End Sub

    Private Sub UpdateCounts()
        Dim inputCount As Integer = ListInputOriginalFiles().Count
        Dim maskCount As Integer = Directory.EnumerateFiles(MASK_DIR, "*.svg").Count()
        CountsLabel.Text = String.Format("入力ファイル: {0} / マスク {1}", inputCount, maskCount)
    End Sub

#End Region

#Region "ログ & 共通ユーティリティ"
    Private Sub LogFile(logLine As String)
        'ファイルに残すログ
        Try
            Dim logFilePath As String = Path.Combine(OUTPUT_DIR, "log", String.Format("{0:yyyyMMdd}.txt", DateTime.Today))
            If Not Directory.Exists(Path.GetDirectoryName(logFilePath)) Then
                Directory.CreateDirectory(Path.GetDirectoryName(logFilePath))
            End If

            Using sw As New StreamWriter(logFilePath, append:=True, encoding:=Encoding.UTF8)
                sw.WriteLine(logLine)
            End Using
        Catch ex As Exception
            AppendLog(String.Format("FinalOutputs ログ記載 {0}", ex.Message), False)
        End Try

    End Sub

    Private Sub ForceDelete(sourceFullPath As String)
        Try
            If File.Exists(sourceFullPath) Then
                File.Delete(sourceFullPath)
            End If
        Catch
            '例外を飲み込んで何もしない
        End Try
    End Sub

    Private Function LimitB(source As Double) As Double
        If source > 255 Then
            Return 255
        ElseIf source < 0 Then
            Return 0
        Else
            Return source
        End If
    End Function

    ''' <summary>
    ''' ログテキストボックスへの出力。UI スレッド以外から呼ばれても安全に動作する。
    ''' </summary>
    Private Sub AppendLog(message As String,
                          Optional countError As Boolean = False,
                          Optional showTime As Boolean = True,
                          Optional addNewLine As Boolean = True,
                          Optional debugOnly As Boolean = False)

        ' デバッガが付いていない場合は debugOnly ログは出さない
        If debugOnly AndAlso Not Diagnostics.Debugger.IsAttached Then
            Exit Sub
        End If

        If LogTextBox Is Nothing Then
            Exit Sub
        End If

        If LogTextBox.InvokeRequired Then
            LogTextBox.Invoke(
                New Action(Of String, Boolean, Boolean, Boolean, Boolean)(AddressOf AppendLog),
                message,
                countError,
                showTime,
                addNewLine,
                debugOnly
            )
            Return
        End If

        Dim logLine As String = If(showTime, $"[{DateTime.Now:HH:mm:ss}] {message}", message)
        If countError Then
            Me.errorTotalCount += 1
        End If
        LogTextBox.AppendText(logLine)

        If addNewLine Then
            LogTextBox.AppendText(Environment.NewLine)
        End If

    End Sub

    Private Sub LogInfo(message As String, countError As Boolean)
        AppendLog(message, countError, showTime:=False)

    End Sub

    Private Sub LogDebug(message As String)
        AppendLog(message, countError:=False, showTime:=True, debugOnly:=True)
    End Sub

    ''' <summary>
    ''' 同一行に "S:MTEX" のようなステップ記号を足していくためのマーカー。
    ''' </summary>
    Private Sub StepMark(token As String)
        AppendLog(token, countError:=False, showTime:=False, addNewLine:=False)
    End Sub

    Private Sub StepNewLine()
        AppendLog("", countError:=False, showTime:=False, addNewLine:=True)
    End Sub

    Private Sub OpenFolder(directoryFullPath As String)
        If String.IsNullOrEmpty(directoryFullPath) Then
            Return
        End If

        ' フォルダが無ければ先に作る
        If Not Directory.Exists(directoryFullPath) Then
            Directory.CreateDirectory(directoryFullPath)
        End If

        Dim targetPath As String = Path.GetFullPath(directoryFullPath).TrimEnd("\"c).ToLowerInvariant()

        Try
            Dim shellWindows As New ShellWindows()

            For Each win As InternetExplorer In shellWindows
                Try
                    Dim url As String = win.LocationURL
                    If String.IsNullOrEmpty(url) Then Continue For

                    ' フォルダ以外（ブラウザ等）はスキップ
                    If Not url.StartsWith("file", StringComparison.OrdinalIgnoreCase) Then
                        Continue For
                    End If

                    ' file:///C:/Users/... → C:\Users\...
                    Dim uri As New Uri(url)
                    Dim folderPath As String =
                    Path.GetFullPath(uri.LocalPath).TrimEnd("\"c).ToLowerInvariant()

                    If folderPath = targetPath Then
                        ' 既にこのフォルダが開いている → そのウィンドウを前面に
                        Dim hWnd As New IntPtr(win.HWND)
                        ShowWindow(hWnd, SW_RESTORE)      ' 最小化されてたら戻す
                        SetForegroundWindow(hWnd)          ' 最前面へ
                        Return
                    End If

                Catch
                    ' どれか1つのウィンドウで失敗しても、他は続行
                End Try
            Next
        Catch
            ' ShellWindows 自体が取れなかった場合などは無視して新規起動にフォールバック
        End Try

        ' ここまで来たら、該当フォルダは開かれていない → 新規で Explorer 起動
        Process.Start("explorer.exe", directoryFullPath)
    End Sub

    Private Sub DebugSaveMatAsPng(mat As OCV.Mat, fileName As String)
        If mat Is Nothing OrElse mat.Empty() Then
            Return
        End If

        Dim fullPath As String = Path.Combine(OUTPUT_DIR, fileName)

        Try
            Dim type As MatType = mat.Type()

            Select Case type
                Case MatType.CV_8UC1, MatType.CV_8UC3, MatType.CV_8UC4
                    ' そのまま保存
                    OCV.Cv2.ImWrite(fullPath, mat)

                Case MatType.CV_32FC1
                    ' 0..255 に正規化してから 8bitグレースケールとして保存
                    Using norm As New OCV.Mat()
                        Using tmp8 As New OCV.Mat()
                            OCV.Cv2.Normalize(mat, norm, 0, 255, NormTypes.MinMax)
                            norm.ConvertTo(tmp8, MatType.CV_8UC1)
                            OCV.Cv2.ImWrite(fullPath, tmp8)
                        End Using
                    End Using

                Case Else
                    ' その他の型は、一旦 8UC1 に落として無理やり可視化（簡易）
                    Using tmp8 As New OCV.Mat()
                        mat.ConvertTo(tmp8, MatType.CV_8UC1)
                        OCV.Cv2.ImWrite(fullPath, tmp8)
                    End Using
            End Select

        Catch ex As Exception
            ' デバッグ用途なので、失敗しても本処理は止めない
            AppendLog(String.Format("DebugSaveMatAsPng Error: {0}", ex.Message), False)
        End Try
    End Sub

    ' PNG を一時ファイルに保存してから Move することで、途中で壊れたファイルが残らないようにする。
    Private Sub SavePng(bitmap As Bitmap, fullPath As String)
        Try
            Dim directoryPath As String = Path.GetDirectoryName(fullPath)
            If String.IsNullOrEmpty(directoryPath) Then
                Exit Sub
            End If
            Directory.CreateDirectory(directoryPath)
            ' 一時ファイルは .__tmp_ で作って Move。処理成功時には残らない想定。
            Dim temporaryFilePath As String = Path.Combine(directoryPath, String.Format(".__tmp_{0:N}.png", Guid.NewGuid()))
            Using cloneBitmap As New Bitmap(bitmap.Width, bitmap.Height, SDI.PixelFormat.Format32bppArgb)
                Using graphics As Graphics = Graphics.FromImage(cloneBitmap)
                    graphics.DrawImageUnscaled(bitmap, 0, 0)
                End Using
                cloneBitmap.Save(temporaryFilePath, SDI.ImageFormat.Png)
            End Using
            ForceDelete(fullPath)
            File.Move(temporaryFilePath, fullPath)
        Catch ex As Exception
            AppendLog(String.Format("SavePng to {0}(EX {1}:{2})", fullPath, ex.GetType().Name, ex.Message), True)
        End Try
    End Sub
#End Region

#Region "実行/進捗"
    Private Async Sub OnRunClicked(sender As Object, e As EventArgs)
        ButtonRun.Enabled = False
        ButtonCancel.Enabled = True
        ButtonRefresh.Enabled = False

        ProcessingCancellationTokenSource = New CancellationTokenSource()

        MainProgressBar.Value = 0
        MainProgressLabel.Text = ""

        Try
            Await Task.Run(
            Sub()
                ProcessAll(ProcessingCancellationTokenSource.Token)
            End Sub
        )
        Catch ex As OperationCanceledException
            ' キャンセル時専用のメッセージ（エラー扱いにはしない）
            AppendLog("キャンセルされました。")
        Finally

            ButtonRun.Enabled = True
            ButtonCancel.Enabled = False
            ButtonRefresh.Enabled = True
        End Try

    End Sub

    Private Sub OnCancelClicked(sender As Object, e As EventArgs)
        ProcessingCancellationTokenSource?.Cancel()
        AppendLog("キャンセル要求を送信しました。")
    End Sub

    Private Sub UpdateProgressPlus(count As Integer)
        UpdateProgress(MainProgressBar.Value + count, MainProgressBar.Maximum)
    End Sub

    Private Sub UpdateProgress(processedCount As Integer, totalCount As Integer)
        If Me.InvokeRequired Then
            Me.Invoke(
                New Action(Of Integer, Integer)(AddressOf UpdateProgress),
                processedCount,
                totalCount
            )
            Return
        End If

        If totalCount <= 0 Then
            MainProgressBar.Value = 0
            MainProgressLabel.Text = "進捗:  0/0"
            Return
        End If

        MainProgressBar.Maximum = totalCount
        MainProgressBar.Value = Math.Max(0, Math.Min(totalCount, processedCount))

        Dim percent As Integer = CInt(processedCount * 100.0 / totalCount)
        MainProgressLabel.Text = $"進捗: {processedCount}/{totalCount} ({percent}%)"
    End Sub

#End Region

#End Region


#Region "実際画像処理"

#Region "ワークフロー（1枚処理 / 各マスク / 全体処理）表示更新"
    Private Sub ProcessMask(inputFullPath As String, token As CancellationToken)

        Dim maskSvgFiles As List(Of String) = Directory.EnumerateFiles(MASK_DIR, "*.svg").OrderBy(Function(x) x).ToList()
        Dim maskCount As Integer = maskSvgFiles.Count

        Dim workingDir As String = Path.GetTempPath()
        Dim workingSourcePng As String = Path.Combine(workingDir, "bt_src3k_" & Guid.NewGuid().ToString("N") & ".png")

        Dim aUp As Bitmap = Nothing
        Dim aDown As Bitmap = Nothing
        Dim aUpMat As OCV.Mat = Nothing
        Dim aDownMat As OCV.Mat = Nothing

        Try
            '以下マスク共通部分
            '(1) SVG 原画 → 3000x3000 PNG (Skia)
            PrepareOriginalToWorkingPng(inputFullPath, workingSourcePng)

            Using sourceBitmap As New Bitmap(workingSourcePng)
                '(2) A-u / A-d に分割
                SplitBalloon(sourceBitmap, aUp, aDown)
                aUpMat = BitmapToMatBGRA(aUp)
                aDownMat = BitmapToMatBGRA(aDown)
                For Each maskSvgPath As String In maskSvgFiles
                    token.ThrowIfCancellationRequested()
                    UpdateProgressPlus(1)
                    '出力ファイル名は OUTPUT_DIR\入力ファイル名(拡張子無)\マスク-入力ファイル-サイズ.拡張子 で決め打ち
                    Me.workingInputFileFullPath = inputFullPath
                    Me.workingMaskFileFullPath = maskSvgPath
                    Me.ProcessOne(aUpMat, aDownMat, maskSvgPath)
                Next
            End Using

            'ここで貯めたwebp変換用ファイルを吐き出す
            StepMark("[webp-")
            If clsWeb.WebImgDbServer保存(srcFilesToConvert) Then
                srcFilesToConvert.Clear()
            End If
            StepMark("Done]")

        Catch ex As Exception
            Dim logLine As String = String.Format("ProcessMask例外(入力ファイル:{0})-{1}", Path.GetFileName(inputFullPath), ex.Message)
            LogFile(logLine)
            Throw New Exception(logLine)
        Finally
            ' Mat / Bitmap / 一時ファイルの後始末
            aUp?.Dispose()
            aDown?.Dispose()
            aUpMat?.Dispose()
            aDownMat?.Dispose()
            ForceDelete(workingSourcePng)
        End Try

    End Sub

    Private Sub ProcessOne(aUpMat As OCV.Mat, aDownMat As OCV.Mat, maskSvgPath As String)

        Me.workingMaskFileFullPath = maskSvgPath

        ' tbl402 にサイズ＋色名（商品フルネーム）が無い場合は画像作成全体をスキップ
        Dim outputFileFinalName As String = String.Empty
        Dim productFullNameC As String = String.Empty
        Dim pccId As Integer = 0
        Dim pId As Integer = 0
        If Not TryResolveProductColorCode(outputFileFinalName, productFullNameC, pccId, pId) Then
            AppendLog(String.Format("ProcessOneスキップ 商品色コードID未検出: {0}", productFullNameC), False)
            LogFile(String.Format("{0}{1}データベース非存在{1}スキップ", productFullNameC, vbTab))
            Return
        End If

        Dim workingDir As String = Path.GetTempPath()
        Dim mainMaskPath As String = Nothing
        Dim innerMaskPath As String = Nothing

        Dim warpedUpMat As OCV.Mat = Nothing
        Dim combinedMat As OCV.Mat = Nothing

        Try
            '(1) 黒マスク M と赤マスク M' に分離
            Dim hasInner As Boolean = SplitSvgIntoMainAndInnerMasks(maskSvgPath, mainMaskPath, innerMaskPath)

            '(2) マスクワープ (A-u → mainMask 形状)
            warpedUpMat = ApplyMaskTransformOCV(aUpMat, mainMaskPath)

            '(3) 口巻 (A-d) の合成
            Dim kuchimakiPercent As Integer = ParseKPercent(maskSvgPath)
            combinedMat = AttachDownPartOCV(warpedUpMat, aDownMat, kuchimakiPercent, WORKING_ZOOM_PIXEL, WORKING_ZOOM_PIXEL)

            '(4) 突起部分（赤マスク M'） ----------
            If hasInner AndAlso Not String.IsNullOrEmpty(innerMaskPath) Then
                combinedMat = ApplyInnerFillWithSubMask(combinedMat, innerMaskPath)
                ' 作成したマスクSVGの後片付け（元ファイルは残す）
                ForceDelete(mainMaskPath)
                ForceDelete(innerMaskPath)
            End If

            '(5) 3000x3000 → 新原画 SVG＋Web PNG (Skia)
            FinalOutputs(combinedMat, pccId, pId, outputFileFinalName, productFullNameC)

        Catch ex As Exception
            Dim logLine As String = String.Format("ProcessOne例外(Mask:{0})-{1}", Path.GetFileName(maskSvgPath), ex.Message)
            LogFile(logLine)
            'ここはそのまま続行
            'Throw New Exception(logLine)
        Finally
            warpedUpMat?.Dispose()
            combinedMat?.Dispose()
        End Try
    End Sub

    ''' <summary>
    ''' 入力 SVG 原画を処理するメインループ。
    ''' </summary>
    Private Sub ProcessAll(token As CancellationToken)

        srcFilesToConvert.Clear()

        Dim inputFiles As List(Of String) = ListInputOriginalFiles()
        Dim inputCount As Integer = inputFiles.Count
        If inputFiles.Count = 0 Then
            AppendLog("ERROR No Input Files")
            Return
        End If

        Dim maskCount As Integer = Directory.EnumerateFiles(MASK_DIR, "*.svg").Count()
        If maskCount = 0 OrElse maskCount > 9 Then
            AppendLog("Mask Not Found or > 9")
            Return
        End If

        Me.errorTotalCount = 0
        Dim processCountFile As Integer = 0
        Dim firstErrorMessage As String = Nothing

        For Each inputFullPath As String In inputFiles
            Try
                UpdateProgress(processCountFile * maskCount, inputCount * maskCount)
                processCountFile += 1
                ProcessMask(inputFullPath, token)
            Catch ex As Exception
                Me.errorTotalCount += 1
                Dim errorMessage As String = String.Format("エラー ファイル:{0} 例外:{1}", Path.GetFileName(inputFullPath), ex.Message)
                AppendLog(errorMessage)
                If firstErrorMessage Is Nothing Then
                    firstErrorMessage = errorMessage
                End If
            End Try
        Next

        If Me.errorTotalCount > 0 Then
            AppendLog(String.Format("DONE with errors (count={0})", Me.errorTotalCount))
            Dim dialogText As String = String.Format("処理中に {1} 件のエラーが発生しました。{0}{2}", vbCrLf, Me.errorTotalCount, firstErrorMessage)

            If Me.InvokeRequired Then
                Me.Invoke(
                        New Action(
                            Sub()
                                MessageBox.Show(dialogText, "BalloonTransformation", MessageBoxButtons.OK, MessageBoxIcon.Error)
                            End Sub))
            Else
                MessageBox.Show(dialogText, "BalloonTransformation", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End If
        Else
            AppendLog("DONE all")
        End If

    End Sub

    ' 入力プレビュー 
    Private Sub UpdateInputPreview(filePath As String)
        If Not File.Exists(filePath) Then
            Return
        End If

        If Me.InvokeRequired Then
            Me.Invoke(New Action(Of String)(AddressOf UpdateInputPreview), filePath)
            Return
        End If

        Try
            Dim bytes As Byte() = File.ReadAllBytes(filePath)

            Using ms As New MemoryStream(bytes)
                Using tempBitmap As New Bitmap(ms)
                    Dim previewBitmap As New Bitmap(tempBitmap.Width,
                                                    tempBitmap.Height,
                                                    SDI.PixelFormat.Format32bppArgb)
                    Using graphics As Graphics = Graphics.FromImage(previewBitmap)
                        graphics.DrawImage(tempBitmap, 0, 0, previewBitmap.Width, previewBitmap.Height)
                    End Using

                    Dim oldImage As Image = InputPictureBox.Image
                    InputPictureBox.Image = previewBitmap

                    oldImage?.Dispose()
                End Using
            End Using

            InputFullPathLabel.Text = filePath
        Catch ex As Exception
            AppendLog($"InputPreview Error: {ex.Message}")
        End Try
    End Sub

    ' 出力プレビュー
    Private Sub UpdateOutputPreview(filePath As String)
        If Not File.Exists(filePath) Then
            Return
        End If

        If Me.InvokeRequired Then
            Me.Invoke(New Action(Of String)(AddressOf UpdateOutputPreview), filePath)
            Return
        End If

        Try
            Dim bytes As Byte() = File.ReadAllBytes(filePath)

            Using ms As New MemoryStream(bytes)
                Using tempBitmap As New Bitmap(ms)
                    Dim previewBitmap As New Bitmap(tempBitmap.Width,
                                                    tempBitmap.Height,
                                                    SDI.PixelFormat.Format32bppArgb)
                    Using graphics As Graphics = Graphics.FromImage(previewBitmap)
                        graphics.DrawImage(tempBitmap, 0, 0, previewBitmap.Width, previewBitmap.Height)
                    End Using

                    Dim oldImage As Image = OutputPictureBox.Image
                    OutputPictureBox.Image = previewBitmap

                    oldImage?.Dispose()
                End Using
            End Using

            OutputFullPathLabel.Text = filePath
        Catch ex As Exception
            AppendLog($"OutputPreview Error: {ex.Message}")
        End Try
    End Sub

#End Region

#Region "画像処理 ヘルパ "
    Private Function BitmapToMatBGRA(sourceBitmap As Bitmap) As OCV.Mat
        Using ms As New MemoryStream()
            sourceBitmap.Save(ms, SDI.ImageFormat.Png)
            Return OCV.Cv2.ImDecode(ms.ToArray(), ImreadModes.Unchanged)
        End Using
    End Function

    ''' <summary>
    ''' 任意の Mat を BGRA (CV_8UC4) に変換するヘルパー。
    ''' </summary>
    Private Function ToBGRA(sourceMat As OCV.Mat) As OCV.Mat
        If sourceMat Is Nothing OrElse sourceMat.Empty() Then
            Return New OCV.Mat()
        End If

        If sourceMat.Type() = MatType.CV_8UC4 Then
            Return sourceMat.Clone()
        End If

        Dim destinationMat As New OCV.Mat()

        Select Case sourceMat.Type()
            Case MatType.CV_8UC3
                OCV.Cv2.CvtColor(sourceMat, destinationMat, ColorConversionCodes.BGR2BGRA)
            Case MatType.CV_8UC1
                OCV.Cv2.CvtColor(sourceMat, destinationMat, ColorConversionCodes.GRAY2BGRA)
            Case Else
                sourceMat.ConvertTo(destinationMat, MatType.CV_8UC4)
        End Select

        Return destinationMat
    End Function

    Private Function RasterizeMaskAlphaOCV(svgPath As String, width As Integer, height As Integer, ByRef maskAlpha As OCV.Mat) As Boolean

        Try
            Dim skImageInfo As New SkiaSharp.SKImageInfo(width, height, SkiaSharp.SKColorType.Bgra8888, SkiaSharp.SKAlphaType.Premul)

            Using surface As SkiaSharp.SKSurface = SkiaSharp.SKSurface.Create(skImageInfo)
                Dim canvas As SkiaSharp.SKCanvas = surface.Canvas
                canvas.Clear(SkiaSharp.SKColors.Transparent)

                Dim svgLoader As New SS.SKSvg()
                svgLoader.Load(svgPath)

                Dim svgPicture As SkiaSharp.SKPicture = svgLoader.Picture
                If svgPicture Is Nothing Then
                    LogInfo("RMAO失敗-SVG load failed", True)
                    Return False
                End If

                Dim viewBox As SkiaSharp.SKRect = svgPicture.CullRect
                Dim scale As Single = Math.Min(skImageInfo.Width / viewBox.Width,
                                               skImageInfo.Height / viewBox.Height)

                Dim translateX As Single = -viewBox.Left * scale + (skImageInfo.Width - viewBox.Width * scale) / 2.0F
                Dim translateY As Single = -viewBox.Top * scale + (skImageInfo.Height - viewBox.Height * scale) / 2.0F

                canvas.Translate(translateX, translateY)
                canvas.Scale(scale, scale)
                canvas.DrawPicture(svgPicture)
                canvas.Flush()

                Using skImage As SkiaSharp.SKImage = surface.Snapshot()
                    Using encoded As SkiaSharp.SKData =
                        skImage.Encode(SkiaSharp.SKEncodedImageFormat.Png, 95)
                        Dim bgraMaskMat As OCV.Mat =
                            OCV.Cv2.ImDecode(encoded.ToArray(), ImreadModes.Unchanged)

                        Dim channels() As OCV.Mat = bgraMaskMat.Split()
                        Dim alphaChannel As OCV.Mat = channels(3)

                        maskAlpha = alphaChannel.Clone()

                        For Each channelMat As OCV.Mat In channels
                            channelMat.Dispose()
                        Next

                        bgraMaskMat.Dispose()
                    End Using
                End Using
            End Using
            Return True
        Catch ex As Exception
            LogInfo(String.Format("RasterizeMaskAlphaOCV失敗 例外-{0}", ex.Message), True)
            Return False
        End Try
    End Function

    Private Function ParseKPercent(maskPath As String) As Integer
        Dim match As Match = Regex.Match(Path.GetFileName(maskPath), "_口巻([0-9]{1,3})p", RegexOptions.IgnoreCase)
        If match.Success Then
            '拡大の場合もある
            Dim value As Integer = Integer.Parse(match.Groups(1).Value)
            Return Math.Max(1, Math.Min(999, value))
        Else
            Return 100
        End If
    End Function

    Private Function IntersectRect(a As Rect, b As Rect) As Rect

        Dim x As Integer = Math.Max(a.X, b.X)
        Dim y As Integer = Math.Max(a.Y, b.Y)
        Dim r As Integer = Math.Min(a.X + a.Width, b.X + b.Width)
        Dim bottom As Integer = Math.Min(a.Y + a.Height, b.Y + b.Height)

        Dim width As Integer = r - x
        Dim height As Integer = bottom - y

        If width <= 0 OrElse height <= 0 Then
            Return New Rect(0, 0, 0, 0)
        End If

        Return New Rect(x, y, width, height)
    End Function
#End Region

#Region "(1) SVG 原画を作業用 3000px PNG へ"

    Private Function ListInputOriginalFiles() As List(Of String)
        Return Directory.EnumerateFiles(INPUT_DIR, "*.svg").OrderBy(Function(x) x).ToList()
    End Function

    ''' <summary>SVG 原画を長辺正方形化し、WORKING_ZOOM_PIXEL へ拡大して透過 PNG を書く。</summary>
    Private Sub PrepareOriginalToWorkingPng(inputFullPath As String, outputWorkingPngPath As String)
        StepMark("[PreparePng-")

        Const inputMinPixel As Integer = 800

        Try
            StepMark("PP(1)-")
            Using loaded As SK.SKBitmap = LoadSvgAsSkBitmap(inputFullPath)
                If loaded Is Nothing Then
                    Throw New Exception("原画 SVG の読込に失敗しました。")
                End If

                Dim originalWidth As Integer = loaded.Width
                Dim originalHeight As Integer = loaded.Height
                If originalWidth < inputMinPixel AndAlso originalHeight < inputMinPixel Then
                    Throw New Exception(String.Format("原画ファイル({0}×{1}) 指定ピクセル({2})未満", originalWidth, originalHeight, inputMinPixel))
                End If

                StepMark("PP(2)-")
                Dim maxSide As Integer = Math.Max(originalWidth, originalHeight)
                Using square As New SK.SKBitmap(maxSide, maxSide, SK.SKColorType.Rgba8888, SK.SKAlphaType.Premul)
                    square.Erase(SK.SKColors.Transparent)
                    Using canvas As New SK.SKCanvas(square)
                        Dim ox As Single = (maxSide - originalWidth) / 2.0F
                        Dim oy As Single = (maxSide - originalHeight) / 2.0F
                        canvas.DrawBitmap(loaded, ox, oy)
                    End Using

                    StepMark("PP(3)-")
                    Using resized As SK.SKBitmap = ResizeSkBitmap(square, WORKING_ZOOM_PIXEL, WORKING_ZOOM_PIXEL)
                        StepMark("PP(4)-")
                        SaveSkBitmapAsPng(resized, outputWorkingPngPath)
                    End Using
                End Using
            End Using

        Catch ex As Exception
            ForceDelete(outputWorkingPngPath)
            Throw New Exception(String.Format("原画調整 ファイル:{0} エラー:{1}", Path.GetFileName(inputFullPath), ex.Message))
        Finally
            StepMark("PP:Done]")
        End Try
    End Sub

    ''' <summary>NDC SvgDirect_Svgから埋込Png読込 と同形。width/viewBox が取れなければ CullRect。</summary>
    Private Function LoadSvgAsSkBitmap(svgPath As String) As SK.SKBitmap
        Dim xml As XDocument = XDocument.Load(svgPath)
        Dim root As XElement = xml.Root
        If root Is Nothing Then
            Return Nothing
        End If

        Dim width As Integer = ParseSvgLengthPixels(root.Attribute("width"))
        Dim height As Integer = ParseSvgLengthPixels(root.Attribute("height"))
        Dim viewBoxAttr As XAttribute = root.Attribute("viewBox")
        If (width <= 0 OrElse height <= 0) AndAlso viewBoxAttr IsNot Nothing Then
            Dim viewBoxValues() As String = CStr(viewBoxAttr).Split({" "c, ","c}, StringSplitOptions.RemoveEmptyEntries)
            If viewBoxValues.Length = 4 Then
                width = CInt(Math.Ceiling(Double.Parse(viewBoxValues(2), CultureInfo.InvariantCulture)))
                height = CInt(Math.Ceiling(Double.Parse(viewBoxValues(3), CultureInfo.InvariantCulture)))
            End If
        End If

        Using stream As FileStream = File.OpenRead(svgPath)
            Dim skSvg As New SS.SKSvg()
            If skSvg.Load(stream) Is Nothing OrElse skSvg.Picture Is Nothing Then
                Return Nothing
            End If

            If width <= 0 OrElse height <= 0 Then
                Dim cull As SK.SKRect = skSvg.Picture.CullRect
                width = CInt(Math.Ceiling(cull.Width))
                height = CInt(Math.Ceiling(cull.Height))
            End If
            If width <= 0 OrElse height <= 0 Then
                Return Nothing
            End If

            Dim bmp As New SK.SKBitmap(width, height, SK.SKColorType.Rgba8888, SK.SKAlphaType.Premul)
            bmp.Erase(SK.SKColors.Transparent)
            Using canvas As New SK.SKCanvas(bmp)
                canvas.DrawPicture(skSvg.Picture)
            End Using
            Return bmp
        End Using
    End Function

    Private Shared Function ParseSvgLengthPixels(attr As XAttribute) As Integer
        If attr Is Nothing Then
            Return 0
        End If
        Dim match As Match = Regex.Match(CStr(attr), "\d+")
        If Not match.Success Then
            Return 0
        End If
        Dim value As Integer
        If Integer.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, value) Then
            Return value
        End If
        Return 0
    End Function

    Private Function ResizeSkBitmap(source As SK.SKBitmap, width As Integer, height As Integer) As SK.SKBitmap
        If source.Width = width AndAlso source.Height = height Then
            Return source.Copy()
        End If
        Dim info As New SK.SKImageInfo(width, height, SK.SKColorType.Rgba8888, SK.SKAlphaType.Premul)
        Dim resized As SK.SKBitmap = source.Resize(info, New SK.SKSamplingOptions(SK.SKCubicResampler.Mitchell))
        If resized Is Nothing Then
            Throw New Exception(String.Format("リサイズに失敗しました ({0}x{1} → {2}x{3})", source.Width, source.Height, width, height))
        End If
        Return resized
    End Function

    Private Shared Sub SaveSkBitmapAsPng(bitmap As SK.SKBitmap, pngFullPath As String)
        Dim outDir As String = Path.GetDirectoryName(pngFullPath)
        If Not String.IsNullOrEmpty(outDir) AndAlso Not Directory.Exists(outDir) Then
            Directory.CreateDirectory(outDir)
        End If
        Using img As SK.SKImage = SK.SKImage.FromBitmap(bitmap)
            Using data As SK.SKData = img.Encode(SK.SKEncodedImageFormat.Png, 100)
                File.WriteAllBytes(pngFullPath, data.ToArray())
            End Using
        End Using
    End Sub

#End Region

#Region "(2) A-u(風船部分)/A-d(口巻部分) 検出と分割"
    Private Sub SplitBalloon(sourceBitmap As Bitmap, ByRef aUp As Bitmap, ByRef aDown As Bitmap)

        StepMark("[SplitBalloon-")

        Dim imageWidth As Integer = sourceBitmap.Width
        Dim imageHeight As Integer = sourceBitmap.Height

        ' ほぼ透明かどうかの判定に使うしきい値
        Dim alphaThreshold As Byte = 4

        ' 各行の「非透明ピクセル数」カウント（ここでは α を加算した値）
        Dim nonZeroPixelCountPerRow(imageHeight - 1) As Integer
        Dim bitmapData As SDI.BitmapData = sourceBitmap.LockBits(New Rectangle(0, 0, imageWidth, imageHeight), SDI.ImageLockMode.ReadOnly, SDI.PixelFormat.Format32bppArgb)

        StepMark("SB(1)-")

        ' Phase0 - 行のα総和が画像幅の k% 未満
        ' Phase1 - 行のα総和が画像幅の k%以上
        ' Phase2 - 行のα総和が画像幅の h%以下 (ここに至ってから実際に記録する)

        Dim countThresholdPercentUp As Integer = 50   ' 上記k
        Dim countThresholdPercentDown As Integer = 30 ' 上記h

        ' rowCount に alpha を加算しているので、閾値は「画素数×255×割合」
        Dim countThresholdAlphaUp As Integer =
        imageWidth * countThresholdPercentUp * 255 \ 100
        Dim countThresholdAlphaDown As Integer =
        imageWidth * countThresholdPercentDown * 255 \ 100

        ' ざっくりスキャンのステップ（縦方向 1%ごと）
        Dim roughStep As Integer = Math.Max(1, imageHeight \ 100)

        Dim phaseNumber As Integer = 0
        Dim initialRowIndex As Integer = 0

        Try
            Dim stride As Integer = bitmapData.Stride
            Dim baseScan0 As IntPtr = bitmapData.Scan0

            Dim yPixelIndex As Integer = 0

            Do While yPixelIndex < imageHeight
                Dim rowPointer As IntPtr = IntPtr.Add(baseScan0, yPixelIndex * stride)
                Dim rowCount As Integer = 0

                For x As Integer = 0 To imageWidth - 1
                    ' ARGB の A (アルファ) だけを見る - αの濃さも加味する
                    Dim alpha As Byte = Marshal.ReadByte(rowPointer, x * 4 + 3)
                    If alpha >= alphaThreshold Then
                        ' α の総和を足す
                        rowCount += alpha
                    End If
                Next

                ' 次の phase に進むか確認
                If phaseNumber < 2 Then
                    If phaseNumber = 0 AndAlso rowCount > countThresholdAlphaUp Then
                        phaseNumber += 1
                    ElseIf phaseNumber = 1 AndAlso rowCount < countThresholdAlphaDown Then
                        phaseNumber += 1
                    End If
                End If

                If phaseNumber < 2 Then
                    ' Phase0/1 中はざっくりサンプリング：この区間は「未使用」扱いにしておく
                    Dim yEnd As Integer = Math.Min(imageHeight - 1, yPixelIndex + roughStep - 1)
                    For yy As Integer = yPixelIndex To yEnd
                        nonZeroPixelCountPerRow(yy) = -1
                    Next
                    yPixelIndex = yEnd + 1

                    If yPixelIndex >= imageHeight Then
                        Throw New Exception(String.Format(
                        "SplitBalloon失敗 phaseNumber={0} 横幅{1}% → {2}% 縦STEP {3} で引っかからず",
                        phaseNumber, countThresholdPercentUp, countThresholdPercentDown, roughStep))
                    End If
                Else
                    ' Phase2：初めてここを通った行を initialRowIndex とし、
                    ' 以降は1ピクセル刻みで rowCount を記録していく
                    If initialRowIndex = 0 Then
                        initialRowIndex = yPixelIndex
                    End If

                    nonZeroPixelCountPerRow(yPixelIndex) = rowCount
                    yPixelIndex += 1
                End If
            Loop
        Catch ex As Exception
            Throw New Exception(ex.Message)
        Finally
            sourceBitmap.UnlockBits(bitmapData)
        End Try

        ' phase2 に至っていなければアウト
        If initialRowIndex = 0 Then
            Throw New Exception(String.Format("SplitBalloon失敗 phaseNumber={0} < 2", phaseNumber))
        End If

        StepMark("SB(2)-")

        Dim joinY As Integer = -1
        Dim finalY As Integer = imageHeight - 1

        ' 単調減少が終わる場所 (bottomValleyWidth) を検出し、
        ' その後の最も太い部分 (maxAfterMin) を計測、口巻は幅 maxAfterMin 以降で
        Dim bottomValleyWidth As Integer = nonZeroPixelCountPerRow(initialRowIndex)
        Dim bottomValleyIndex As Integer = 0
        Dim maxAfterMin As Integer = 0

        For yPixelIndex As Integer = initialRowIndex To imageHeight - 3
            Dim rowVal As Integer = nonZeroPixelCountPerRow(yPixelIndex)
            If rowVal < 0 Then
                ' 未使用行（rough scan 区間）は飛ばす
                Continue For
            End If

            ' α の重み付きのため、4px ≒ 1024 を誤差とみなす。
            If rowVal > bottomValleyWidth + 1024 Then
                bottomValleyIndex = yPixelIndex
                Exit For
            ElseIf rowVal < bottomValleyWidth Then
                ' 最小値を更新
                bottomValleyWidth = rowVal
            End If
        Next

        If bottomValleyIndex = 0 Then
            Throw New Exception("SplitBalloon失敗 bottomValleyIndex取得失敗、くびれ無")
        End If

        ' bottomValleyIndex 以降で、
        ' ・最大の幅 maxAfterMin（口巻の最大幅）
        ' ・「実質終端」とみなす finalY（かなり細くなった行）を求める
        For yPixelIndex As Integer = bottomValleyIndex To imageHeight - 3
            Dim rowVal As Integer = nonZeroPixelCountPerRow(yPixelIndex)
            If rowVal < 0 Then
                Continue For
            End If

            If maxAfterMin < rowVal Then
                maxAfterMin = rowVal
            End If

            ' rowVal が十分小さい（ほぼ無視できる）行が最初に現れたら、
            ' それ以降は「口巻として不要な細かい部分」とみなして finalY とする
            If finalY = imageHeight - 1 AndAlso rowVal < 100 Then
                finalY = yPixelIndex
            End If
        Next

        If maxAfterMin = 0 Then
            Throw New Exception("SplitBalloon失敗 maxAfterMin 取得失敗、くびれ後幅無し")
        End If

        ' joinY（上部と口巻の境界）を探す：
        ' 「単調減少していた曲線の凸性が崩れた瞬間」を検出
        For yPixelIndex As Integer = initialRowIndex To imageHeight - 3
            ' 口巻最大幅より小さい場所から開始
            If nonZeroPixelCountPerRow(yPixelIndex + 2) > maxAfterMin Then
                Continue For
            End If

            Dim v0 As Integer = nonZeroPixelCountPerRow(yPixelIndex)
            Dim v1 As Integer = nonZeroPixelCountPerRow(yPixelIndex + 1)
            Dim v2 As Integer = nonZeroPixelCountPerRow(yPixelIndex + 2)
            If v0 < 0 OrElse v1 < 0 OrElse v2 < 0 Then
                Continue For
            End If

            Dim prevDifDif As Integer = (v1 - v0) - (v2 - v1)

            If prevDifDif < -512 Then
                ' 凸性が崩れた（計算上、その中間の yPixelIndex+1 を境界とする）
                joinY = yPixelIndex + 1
                Exit For
            End If
        Next

        If joinY < 0 Then
            Throw New Exception("SplitBalloon失敗 凸性崩れた瞬間取得失敗")
        End If

        ' finalY を安全な範囲にクランプ
        If finalY < joinY Then
            finalY = joinY
        End If
        If finalY >= imageHeight Then
            finalY = imageHeight - 1
        End If

        StepMark("SB(3)-")

        '========================  ここから A-u と A-d を実際に作る  ========================

        '--- 上部 A-u：元と同じ高さで、joinY より下は完全透明 ---
        aUp = New Bitmap(imageWidth, imageHeight, SDI.PixelFormat.Format32bppPArgb)
        Using gUp As Graphics = Graphics.FromImage(aUp)
            ' 全面透明で初期化
            gUp.Clear(Drawing.Color.Transparent)

            ' joinY までをそのままコピー
            Dim copyHeight As Integer = Math.Max(0, Math.Min(joinY, imageHeight))
            If copyHeight > 0 Then
                gUp.DrawImage(
                sourceBitmap,
                New Rectangle(0, 0, imageWidth, copyHeight),   ' 描画先
                New Rectangle(0, 0, imageWidth, copyHeight),   ' 描画元
                GraphicsUnit.Pixel
            )
            End If
        End Using

        '--- 下部 A-d：joinY ～ finalY を切り出し、下側の余計な透明行を削る ---
        Dim downHeight As Integer = finalY - joinY + 1
        If downHeight <= 0 Then
            ' 万一 downHeight が0以下になったら、高さ1の完全透明を返す
            aDown = New Bitmap(imageWidth, 1, SDI.PixelFormat.Format32bppPArgb)
            Using gDown As Graphics = Graphics.FromImage(aDown)
                gDown.Clear(Drawing.Color.Transparent)
            End Using
        Else
            aDown = New Bitmap(imageWidth, downHeight, SDI.PixelFormat.Format32bppPArgb)
            Using gDown As Graphics = Graphics.FromImage(aDown)
                gDown.Clear(Drawing.Color.Transparent)
                gDown.DrawImage(
                sourceBitmap,
                New Rectangle(0, 0, imageWidth, downHeight),          ' 描画先
                New Rectangle(0, joinY, imageWidth, downHeight),      ' 描画元
                GraphicsUnit.Pixel
            )
            End Using
        End If
        StepMark("SB(Done)]")

    End Sub

#End Region

#Region "(3) マスク駆動ワープ（OpenCV Remap 利用）"

    Private Function ApplyMaskTransformOCV(aUp As OpenCvSharp.Mat, maskSvgPath As String) As OpenCvSharp.Mat

        StepMark("[ApplyMaskTransformOCV-")
        'aUPはすでに canvasW × canvasH

        Dim workingW As Integer = WORKING_ZOOM_PIXEL
        Dim workingH As Integer = WORKING_ZOOM_PIXEL

        Const ANGLE_SAMPLES As Integer = 4096 '4096
        Const ALPHA_THR As Integer = 5
        Const INNER_MARGIN As Double = 0.995

        If aUp Is Nothing OrElse aUp.Empty() Then
            Throw New ArgumentException("ApplyMaskTransformOCV: aUp is empty")
        End If


        StepMark("AMTO(1)-")
        '========================  (1) 一時ファイルパスを用意  ========================
        Dim tempDir As String = Path.GetTempPath()
        Directory.CreateDirectory(tempDir)

        Dim workingPngFullPath As String = Path.Combine(tempDir, $"in_ps_{Guid.NewGuid():N}.png")
        Dim tempWarpedPs As String = Path.Combine(tempDir, $"warped_ps_{Guid.NewGuid():N}.png")
        Dim tempWarpedFinal As String = Path.Combine(tempDir, $"warped_final_{Guid.NewGuid():N}.png")

        Dim resultMat As New OpenCvSharp.Mat

        Try
            '========================  (2) aUp(Mat) → PNG保存（workingW × workingH リサイズ済）  ========================
            StepMark("AMTO(2)-")
            Dim workingSourceMat As OpenCvSharp.Mat = ToBGRA(aUp)
            OpenCvSharp.Cv2.ImWrite(workingPngFullPath, workingSourceMat)
            workingSourceMat.Dispose()

            ' OpenCVで読み込む
            Dim srcPsMat As OpenCvSharp.Mat = OpenCvSharp.Cv2.ImRead(workingPngFullPath, OpenCvSharp.ImreadModes.Unchanged)

            '========================  (3) マスクを workingW x workingH にラスタライズ（Skia → α）  ========================
            StepMark("AMTO(3)-")
            Dim maskAlphaHi As OpenCvSharp.Mat = Nothing
            RasterizeMaskAlphaOCV(maskSvgPath, workingW, workingH, maskAlphaHi)

            '========================  (4) 半径プロファイルの準備  ========================
            StepMark("AMTO(4)-")
            Dim srcBgraHi As OpenCvSharp.Mat = ToBGRA(srcPsMat)
            srcPsMat.Dispose()

            Dim srcChHi() As OpenCvSharp.Mat = OpenCvSharp.Cv2.Split(srcBgraHi)
            Dim srcAlphaHi As OpenCvSharp.Mat = srcChHi(3)

            Dim cx As Double = workingW / 2.0
            Dim cy As Double = workingH / 2.0

            Dim srcProfile As OpenCvSharp.Mat = BuildRadialProfile(srcAlphaHi, cx, cy, ANGLE_SAMPLES)
            Dim dstProfile As OpenCvSharp.Mat = BuildRadialProfile(maskAlphaHi, cx, cy, ANGLE_SAMPLES)

            For Each m In srcChHi
                m.Dispose()
            Next
            srcAlphaHi.Dispose()

            '========================  (5) Remap 用 mapX/mapY を生成  ========================
            StepMark("AMTO(5)-")
            'xとyは逆位置が正しい
            Dim mapX As New OpenCvSharp.Mat(workingH, workingW, OpenCvSharp.MatType.CV_32FC1)
            Dim mapY As New OpenCvSharp.Mat(workingH, workingW, OpenCvSharp.MatType.CV_32FC1)

            Dim twoPi As Double = 2.0 * Math.PI

            For y As Integer = 0 To workingH - 1
                For x As Integer = 0 To workingW - 1
                    Dim aMask As Integer = CInt(maskAlphaHi.Get(Of Byte)(y, x))
                    If aMask < ALPHA_THR Then
                        ' この画素には何も描画しない（NaN指定で Remap は触らない）
                        mapX.Set(Of Single)(y, x, Single.NaN)
                        mapY.Set(Of Single)(y, x, Single.NaN)
                        Continue For
                    End If

                    Dim dx As Double = (x + 0.5) - cx
                    Dim dy As Double = (y + 0.5) - cy
                    Dim rDst As Double = Math.Sqrt(dx * dx + dy * dy)
                    Dim theta As Double = Math.Atan2(dy, dx)
                    If theta < 0 Then
                        theta += twoPi
                    End If

                    Dim rSrcMax As Double = Math.Max(1.0, RadiusAtAngle(srcProfile, theta))
                    Dim rDstMax As Double = Math.Max(1.0, RadiusAtAngle(dstProfile, theta))

                    Dim innerDstMax As Double = rDstMax * INNER_MARGIN
                    If rDst > innerDstMax Then
                        mapX.Set(Of Single)(y, x, Single.NaN)
                        mapY.Set(Of Single)(y, x, Single.NaN)
                        Continue For
                    End If

                    Dim t As Double = rDst / innerDstMax
                    Dim rSrc As Double = t * rSrcMax

                    Dim sx As Double = cx + Math.Cos(theta) * rSrc
                    Dim sy As Double = cy + Math.Sin(theta) * rSrc

                    mapX.Set(Of Single)(y, x, CSng(sx))
                    mapY.Set(Of Single)(y, x, CSng(sy))
                Next
            Next

            srcProfile.Dispose()
            dstProfile.Dispose()

            '========================  (6) Remap 実行（直α BGRA のまま）  ========================
            StepMark("AMTO(6)-")
            Dim warpedHi As New OpenCvSharp.Mat()
            OpenCvSharp.Cv2.Remap(srcBgraHi, warpedHi, mapX, mapY, OpenCvSharp.InterpolationFlags.Linear, OpenCvSharp.BorderTypes.Constant, OpenCvSharp.Scalar.All(0))

            srcBgraHi.Dispose()
            mapX.Dispose()
            mapY.Dispose()

            '========================  (7) α合成：最終 α = min(warped α, mask α)  ========================
            StepMark("AMTO(7)-")
            Dim warpedChHi() As OpenCvSharp.Mat = OpenCvSharp.Cv2.Split(warpedHi)
            Dim warpedAlphaHi As OpenCvSharp.Mat = warpedChHi(3)
            Dim finalAlphaHi As New OpenCvSharp.Mat()
            OpenCvSharp.Cv2.Min(warpedAlphaHi, maskAlphaHi, finalAlphaHi)
            maskAlphaHi.Dispose()

            ' RGB を finalAlpha / warpedAlpha でスケール（pre-multiply の整合性確保）
            Dim b As OpenCvSharp.Mat = warpedChHi(0)
            Dim g As OpenCvSharp.Mat = warpedChHi(1)
            Dim r As OpenCvSharp.Mat = warpedChHi(2)

            Dim b32 As New OpenCvSharp.Mat()
            Dim g32 As New OpenCvSharp.Mat()
            Dim r32 As New OpenCvSharp.Mat()
            Dim a32 As New OpenCvSharp.Mat()
            Dim f32 As New OpenCvSharp.Mat()
            Dim denom As New OpenCvSharp.Mat()
            Dim scale As New OpenCvSharp.Mat()

            b.ConvertTo(b32, OpenCvSharp.MatType.CV_32FC1)
            g.ConvertTo(g32, OpenCvSharp.MatType.CV_32FC1)
            r.ConvertTo(r32, OpenCvSharp.MatType.CV_32FC1)
            warpedAlphaHi.ConvertTo(a32, OpenCvSharp.MatType.CV_32FC1)
            finalAlphaHi.ConvertTo(f32, OpenCvSharp.MatType.CV_32FC1)

            ' denom = max(warpedAlpha, 1.0)
            OpenCvSharp.Cv2.Max(a32, New OpenCvSharp.Scalar(1.0), denom)

            ' scale = finalAlpha / denom
            OpenCvSharp.Cv2.Divide(f32, denom, scale)

            OpenCvSharp.Cv2.Multiply(b32, scale, b32)
            OpenCvSharp.Cv2.Multiply(g32, scale, g32)
            OpenCvSharp.Cv2.Multiply(r32, scale, r32)

            b32.ConvertTo(b, OpenCvSharp.MatType.CV_8UC1)
            g32.ConvertTo(g, OpenCvSharp.MatType.CV_8UC1)
            r32.ConvertTo(r, OpenCvSharp.MatType.CV_8UC1)

            b32.Dispose() : g32.Dispose() : r32.Dispose()
            a32.Dispose() : f32.Dispose() : denom.Dispose() : scale.Dispose()
            warpedAlphaHi.Dispose()

            warpedChHi(0) = b
            warpedChHi(1) = g
            warpedChHi(2) = r
            warpedChHi(3).Dispose()
            warpedChHi(3) = finalAlphaHi

            'これが最終結果
            OpenCvSharp.Cv2.Merge(warpedChHi, resultMat)

            For Each m In warpedChHi
                m.Dispose()
            Next
            warpedHi.Dispose()

        Finally
            ' 一時ファイルの掃除（失敗しても無視）
            For Each p In New String() {workingPngFullPath, tempWarpedPs, tempWarpedFinal}
                ForceDelete(p)
            Next
        End Try

        Return resultMat
    End Function


    ''' <summary>
    ''' αマップに対して、中心(centerX,centerY)から各角度ごとに「αを積分した有効半径」を求める。
    ''' 半径方向に RADIUS_STEP ずつ進み、各ステップで (RADIUS_STEP * 有効α) を加算していく。
    ''' 有効α = clamp(α, ALPHA_LOW .. ALPHA_HIGH) / ALPHA_HIGH
    ''' </summary>
    Private Function BuildRadialProfile(alpha As OCV.Mat, centerX As Double, centerY As Double, sampleCount As Integer) As OCV.Mat

        StepMark("[BRP-")
        ' 半径のサンプリング間隔（細かくしたいなら 0.1 とか 0.25）
        Const RADIUS_STEP As Double = 0.5

        ' これ未満の α は「ほぼ透明」とみなして 0 扱い
        Const ALPHA_LOW As Integer = 5

        ' これ以上の α は「完全不透明」とみなして 255 相当扱い
        Const ALPHA_HIGH As Integer = 220

        ' 「しばらくずっと透明」が続いたら打ち切るための許容ステップ数
        Const MAX_CONSECUTIVE_TRANSPARENT As Integer = 10

        Dim radialProfile As New OCV.Mat(1, sampleCount, MatType.CV_32FC1)

        Dim twoPi As Double = 2.0 * Math.PI
        Dim deltaTheta As Double = twoPi / sampleCount

        Dim width As Integer = alpha.Cols
        Dim height As Integer = alpha.Rows

        ' 画面対角線長くらいまで見ておけば十分
        Dim maxRadius As Double = Math.Sqrt(width * width + height * height)

        For sampleIndex As Integer = 0 To sampleCount - 1
            Dim angle As Double = sampleIndex * deltaTheta
            Dim cosAngle As Double = Math.Cos(angle)
            Dim sinAngle As Double = Math.Sin(angle)

            Dim accumLength As Double = 0.0
            Dim radius As Double = 0.0
            Dim consecutiveTransparent As Integer = 0

            While radius <= maxRadius
                Dim sampleX As Double = centerX + cosAngle * radius
                Dim sampleY As Double = centerY + sinAngle * radius

                ' 画面外に出たら終了
                If sampleX < 0 OrElse sampleX >= width - 1 OrElse
               sampleY < 0 OrElse sampleY >= height - 1 Then
                    Exit While
                End If

                ' ここでは最近傍サンプリングで十分
                Dim rawAlpha As Integer = CInt(alpha.Get(Of Byte)(CInt(sampleY), CInt(sampleX)))

                If rawAlpha < ALPHA_LOW Then
                    ' ほぼ透明 → 有効α=0
                    consecutiveTransparent += 1
                    If consecutiveTransparent >= MAX_CONSECUTIVE_TRANSPARENT Then
                        ' しばらくずっと透明なら、もう形状は終わったとみなして打ち切り
                        Exit While
                    End If
                Else
                    ' 透明でない → 有効αで積分に反映
                    consecutiveTransparent = 0

                    ' 上限を ALPHA_HIGH でクランプしてから正規化
                    Dim clamped As Integer = Math.Min(rawAlpha, ALPHA_HIGH)
                    Dim normalized As Double = clamped / CDbl(ALPHA_HIGH)

                    accumLength += RADIUS_STEP * normalized
                End If

                radius += RADIUS_STEP
            End While

            ' この角度方向での「有効半径」に相当する値としてセット
            radialProfile.Set(Of Single)(0, sampleIndex, CSng(accumLength))
        Next

        StepMark("Done]")
        Return radialProfile
    End Function

    ''' <summary>
    ''' 角度 theta に対応する radial profile 上の半径値を、補間付きで取得する。
    ''' </summary>
    Private Function RadiusAtAngle(profile As OCV.Mat, theta As Double) As Double

        Dim twoPi As Double = 2.0 * Math.PI
        Dim profileLength As Integer = profile.Cols

        If profileLength <= 0 Then
            Return 0
        End If

        ' 角度 → [0, profileLength) の連続インデックスに変換
        Dim continuousIndex As Double = (theta / twoPi) * profileLength

        ' 0..n に正規化
        continuousIndex -= Math.Floor(continuousIndex / profileLength) * profileLength

        Dim index0 As Integer = CInt(Math.Floor(continuousIndex)) Mod profileLength
        Dim index1 As Integer = (index0 + 1) Mod profileLength
        Dim t As Double = continuousIndex - Math.Floor(continuousIndex)

        Dim radius0 As Double = CDbl(profile.Get(Of Single)(0, index0))
        Dim radius1 As Double = CDbl(profile.Get(Of Single)(0, index1))

        ' 線形補間
        Return radius0 * (1 - t) + radius1 * t
    End Function

#End Region

#Region "(4) 口巻の合成"

    ''' <summary>
    ''' A-u（上部）と A-d（口巻）を合成する。
    ''' aUp       : 上部（BGRA, 任意サイズ）
    ''' aDown     : 口巻（BGRA, 任意サイズ）
    ''' kPercent  : 口巻倍率(%)
    ''' canvasW/H : 最終出力キャンバスサイズ
    ''' overlapPx : 上端を A-u に少し食い込ませて黒フチを防ぐオフセット（px）
    ''' </summary>
    Private Function AttachDownPartOCV(aUp As OCV.Mat, aDown As OCV.Mat, kPercent As Integer, canvasW As Integer, canvasH As Integer, Optional overlapPx As Integer = 1) As OCV.Mat

        StepMark("[AttachDownPartOCV-")
        If aUp Is Nothing OrElse aUp.Empty() Then
            Throw New ArgumentException("aUp is empty")
        End If

        If aDown Is Nothing OrElse aDown.Empty() Then
            Throw New ArgumentException("aDown is empty")
        End If

        ' 1) 出力キャンバスに A-u をフィット（必要ならリサイズ）
        Dim upFit As OCV.Mat = aUp
        Dim needsResize As Boolean = (aUp.Cols <> canvasW OrElse aUp.Rows <> canvasH)

        If needsResize Then
            upFit = New OCV.Mat()
            OCV.Cv2.Resize(aUp, upFit, New OCV.Size(canvasW, canvasH), 0, 0, InterpolationFlags.Lanczos4)
        End If

        Dim outW As Integer = canvasW
        Dim outH As Integer = canvasH

        ' 結果キャンバス（最初は A-u を丸ごとコピー）
        Dim result As New OCV.Mat(outH, outW, MatType.CV_8UC4, Scalar.All(0))
        upFit.CopyTo(result)

        ' 2) 口巻を k% でリサイズ
        Dim kScale As Double = Math.Max(1, kPercent) / 100.0

        Dim downScaled As New OCV.Mat()
        OCV.Cv2.Resize(aDown, downScaled, New OCV.Size(), kScale, kScale, InterpolationFlags.Lanczos4)

        ' 3) 口巻の α>0 外接矩形（縦位置）と、上端4px・α≥4 の左右中央を取得
        Dim downChannels() As OCV.Mat = downScaled.Split()
        Dim downAlpha As OCV.Mat = downChannels(3)

        Dim binaryMask As New OCV.Mat()
        OCV.Cv2.Threshold(downAlpha, binaryMask, 0, 255, ThresholdTypes.Binary)
        Dim nonZeroPoints As New OCV.Mat()
        OCV.Cv2.FindNonZero(binaryMask, nonZeroPoints)

        If nonZeroPoints.Empty() Then
            ' 非透明画素がない場合はそのまま A-u を返す
            For Each channelMat As OCV.Mat In downChannels
                channelMat.Dispose()
            Next

            downAlpha.Dispose()
            binaryMask.Dispose()
            nonZeroPoints.Dispose()
            downScaled.Dispose()

            If needsResize Then
                upFit.Dispose()
            End If

            Return result
        End If

        Dim boundingRect As Rect = OCV.Cv2.BoundingRect(nonZeroPoints)

        Dim topRow As Integer = boundingRect.Y
        Dim centerX As Integer = FindMouthTopCenterX(downAlpha)
        If centerX < 0 Then
            centerX = boundingRect.X + boundingRect.Width \ 2
        End If

        ' 4) A-u の「不透明最下行」（口巻が触れる位置）を探す
        Dim bottomOpaqueRow As Integer = FindBottomNonTransparent(upFit)
        If bottomOpaqueRow < 0 Then
            bottomOpaqueRow = outH - 1
        End If

        ' 上端が bottomOpaqueRow に overlapPx 食い込むように配置
        Dim dstY As Integer = (bottomOpaqueRow - overlapPx) - topRow
        Dim dstX As Integer = (outW \ 2) - centerX

        ' 5) 下側にはみ出す場合は必要最小限で縦縮小（過剰な縮小を避ける）
        Dim overflowY As Integer = (dstY + downScaled.Rows) - outH

        If overflowY > 0 Then
            Dim newHeight As Integer = Math.Max(1, downScaled.Rows - overflowY)

            If newHeight < downScaled.Rows Then
                Dim scaleY As Double = newHeight / CDbl(downScaled.Rows)
                Dim newWidth As Integer =
                    CInt(Math.Round(downScaled.Cols * scaleY))

                Dim temp As New OCV.Mat()
                OCV.Cv2.Resize(downScaled,
                              temp,
                              New OCV.Size(Math.Max(1, newWidth), newHeight),
                              0,
                              0,
                              InterpolationFlags.Area)

                downScaled.Dispose()
                downScaled = temp

                ' α/矩形を再計算
                For Each channelMat As OCV.Mat In downChannels
                    channelMat.Dispose()
                Next

                downChannels = downScaled.Split()
                downAlpha = downChannels(3)

                binaryMask.Dispose()
                nonZeroPoints.Dispose()

                binaryMask = New OCV.Mat()
                OCV.Cv2.Threshold(downAlpha,
                                 binaryMask,
                                 0,
                                 255,
                                 ThresholdTypes.Binary)

                nonZeroPoints = New OCV.Mat()
                OCV.Cv2.FindNonZero(binaryMask, nonZeroPoints)

                boundingRect = OCV.Cv2.BoundingRect(nonZeroPoints)
                topRow = boundingRect.Y
                centerX = FindMouthTopCenterX(downAlpha)
                If centerX < 0 Then
                    centerX = boundingRect.X + boundingRect.Width \ 2
                End If

                dstY = (bottomOpaqueRow - overlapPx) - topRow
                dstX = (outW \ 2) - centerX
            End If
        End If

        ' 6) 交差領域を計算
        Dim destinationRect As New Rect(dstX, dstY, downScaled.Cols, downScaled.Rows)
        Dim canvasBounds As New Rect(0, 0, outW, outH)
        Dim clippedRect As Rect = IntersectRect(destinationRect, canvasBounds)

        If clippedRect.Width > 0 AndAlso clippedRect.Height > 0 Then
            ' downScaled 内の対応領域
            Dim sourceStartX As Integer = clippedRect.X - dstX
            Dim sourceStartY As Integer = clippedRect.Y - dstY

            For offsetY As Integer = 0 To clippedRect.Height - 1
                Dim yyDst As Integer = clippedRect.Y + offsetY
                Dim yySrc As Integer = sourceStartY + offsetY

                For offsetX As Integer = 0 To clippedRect.Width - 1
                    Dim xxDst As Integer = clippedRect.X + offsetX
                    Dim xxSrc As Integer = sourceStartX + offsetX

                    Dim downPixel As OCV.Vec4b = downScaled.Get(Of OCV.Vec4b)(yySrc, xxSrc)

                    Dim alphaDown As Double = downPixel(3) / 255.0
                    If alphaDown <= 0.0 Then
                        Continue For
                    End If

                    Dim upPixel As OCV.Vec4b = result.Get(Of OCV.Vec4b)(yyDst, xxDst)

                    Dim alphaUp As Double = upPixel(3) / 255.0

                    ' Porter-Duff "over"
                    Dim alphaOut As Double = alphaDown + alphaUp * (1 - alphaDown)

                    Dim outB As Byte = upPixel(0)
                    Dim outG As Byte = upPixel(1)
                    Dim outR As Byte = upPixel(2)

                    If alphaOut > 0 Then
                        Dim numeratorB As Double =
                            downPixel(0) * alphaDown + upPixel(0) * alphaUp * (1 - alphaDown)
                        Dim numeratorG As Double =
                            downPixel(1) * alphaDown + upPixel(1) * alphaUp * (1 - alphaDown)
                        Dim numeratorR As Double =
                            downPixel(2) * alphaDown + upPixel(2) * alphaUp * (1 - alphaDown)

                        Dim bFloat As Double = LimitB(numeratorB / alphaOut)
                        Dim gFloat As Double = LimitB(numeratorG / alphaOut)
                        Dim rFloat As Double = LimitB(numeratorR / alphaOut)

                        outB = CByte(Math.Round(bFloat))
                        outG = CByte(Math.Round(gFloat))
                        outR = CByte(Math.Round(rFloat))
                    End If

                    Dim alphaOut255 As Double = LimitB(alphaOut * 255.0)

                    result.Set(Of OCV.Vec4b)(
                        yyDst,
                        xxDst,
                        New OCV.Vec4b(outB, outG, outR, CByte(Math.Round(alphaOut255))))
                Next
            Next
        End If

        ' 後始末
        For Each channelMat As OCV.Mat In downChannels
            channelMat.Dispose()
        Next

        downAlpha.Dispose()
        binaryMask.Dispose()
        nonZeroPoints.Dispose()
        downScaled.Dispose()

        If needsResize Then
            upFit.Dispose()
        End If

        StepMark("-DONE]")

        Return result
    End Function


    ''' <summary>
    ''' 口巻αマップの、α≥4 となる最上行から4行の minX/maxX 中点を返す。
    ''' 見つからなければ -1。
    ''' </summary>
    Private Function FindMouthTopCenterX(alpha As OCV.Mat) As Integer
        Const topRowCount As Integer = 4
        ' THRESH_BINARY は src > thresh なので、3 なら α≥4
        Const alphaThresh As Double = 3

        If alpha Is Nothing OrElse alpha.Empty() Then
            Return -1
        End If

        Using opaqueMask As New OCV.Mat()
            OCV.Cv2.Threshold(alpha, opaqueMask, alphaThresh, 255, ThresholdTypes.Binary)

            Dim firstY As Integer = -1
            For y As Integer = 0 To opaqueMask.Rows - 1
                Using row As OCV.Mat = opaqueMask.Row(y)
                    If OCV.Cv2.CountNonZero(row) > 0 Then
                        firstY = y
                        Exit For
                    End If
                End Using
            Next

            If firstY < 0 Then
                Return -1
            End If

            Dim rowCount As Integer = Math.Min(topRowCount, opaqueMask.Rows - firstY)
            Dim topRoi As New Rect(0, firstY, opaqueMask.Cols, rowCount)

            Using topRows As New OCV.Mat(opaqueMask, topRoi)
                Using topNonZero As New OCV.Mat()
                    OCV.Cv2.FindNonZero(topRows, topNonZero)
                    If topNonZero.Empty() Then
                        Return -1
                    End If

                    Dim topRect As Rect = OCV.Cv2.BoundingRect(topNonZero)
                    Return topRect.X + topRect.Width \ 2
                End Using
            End Using
        End Using
    End Function

    ''' <summary>
    ''' A-u の不透明最下行探索
    ''' BGRA Mat から「α&gt;0 の画素が存在する最も下の行」の y を返す。
    ''' 見つからなければ -1。
    ''' </summary>
    Private Function FindBottomNonTransparent(image As OCV.Mat) As Integer
        If image Is Nothing OrElse image.Empty() Then
            Return -1
        End If

        Dim channels() As OCV.Mat = image.Split()
        If channels.Length < 4 Then
            ' αがなければ「全部不透明」とみなして最下行を返す
            For Each channelMat As OCV.Mat In channels
                channelMat.Dispose()
            Next

            Return image.Rows - 1
        End If

        Using alphaChannel As OCV.Mat = channels(3)
            For y As Integer = alphaChannel.Rows - 1 To 0 Step -1
                Dim row As OCV.Mat = alphaChannel.Row(y)
                Dim nonZeroCount As Integer = OCV.Cv2.CountNonZero(row)
                row.Dispose()

                If nonZeroCount > 0 Then
                    For Each channelMat As OCV.Mat In channels
                        channelMat.Dispose()
                    Next

                    Return y
                End If
            Next
        End Using

        For Each channelMat As OCV.Mat In channels
            channelMat.Dispose()
        Next

        Return -1
    End Function

#End Region

#Region "(5) リンクバルーン突起部分追加"
    Private Function SplitSvgIntoMainAndInnerMasks(originalSvgPath As String,
                                               ByRef mainMaskSvgPath As String,
                                               ByRef innerMaskSvgPath As String) As Boolean

        Dim svgText As String = File.ReadAllText(originalSvgPath, Encoding.UTF8)

        ' 「赤い path 要素」をマッチする正規表現
        '   <path ... fill="#ff0000" ... />
        '   <path ... fill="red" ... />
        Dim redPathPattern As String =
        "<path\b[^>]*\bfill\s*=\s*""(?:#ff0000|#FF0000|red|Red|RED)""[^>]*/?>"

        Dim options As RegexOptions =
        RegexOptions.IgnoreCase Or RegexOptions.Singleline

        Dim redMatches As MatchCollection =
        Regex.Matches(svgText, redPathPattern, options)

        If redMatches.Count = 0 Then
            ' 赤パスが無ければ:
            '  - M は元SVGそのまま
            '  - M' は「指定なし」
            mainMaskSvgPath = originalSvgPath
            innerMaskSvgPath = Nothing
            Return False
        End If

        '=====================================================
        ' 1) main 用 SVG: 赤パスだけ削除（これは今まで通りでOK）
        '=====================================================
        Dim mainSvgText As String =
        Regex.Replace(svgText, redPathPattern, "", options)

        '=====================================================
        ' 2) inner 用 SVG: 「赤パスだけ」を残した SVG を組み立てる
        '=====================================================

        ' ヘッダ部分（最初の <path が現れるまで）を抜き出す
        Dim firstPathIndex As Integer =
        svgText.IndexOf("<path", StringComparison.OrdinalIgnoreCase)

        Dim header As String
        If firstPathIndex >= 0 Then
            header = svgText.Substring(0, firstPathIndex)
        Else
            ' path が無い特殊なケース（実質無いはずだが一応）
            header = svgText
        End If

        ' フッタ（</svg> 以降）を抜き出す
        Dim footer As String = ""
        Dim svgEndIndex As Integer =
        svgText.LastIndexOf("</svg>", StringComparison.OrdinalIgnoreCase)
        If svgEndIndex >= 0 Then
            footer = svgText.Substring(svgEndIndex)
        End If

        Dim sb As New StringBuilder()
        sb.Append(header)
        sb.AppendLine()

        ' 赤パスだけを並べて書き込む
        For Each m As Match In redMatches
            sb.AppendLine(m.Value)
        Next

        sb.AppendLine(footer)

        Dim innerSvgText As String = sb.ToString()

        '=====================================================
        ' 3) TEMPフォルダにGUID名で保存
        '=====================================================
        Dim tempDir As String = Path.GetTempPath()

        Dim guidMain As String = Guid.NewGuid().ToString("N")
        Dim guidInner As String = Guid.NewGuid().ToString("N")

        Dim mainPath As String = Path.Combine(tempDir, $"mask_main_{guidMain}.svg")
        Dim innerPath As String = Path.Combine(tempDir, $"mask_inner_{guidInner}.svg")

        File.WriteAllText(mainPath, mainSvgText, Encoding.UTF8)
        File.WriteAllText(innerPath, innerSvgText, Encoding.UTF8)

        mainMaskSvgPath = mainPath
        innerMaskSvgPath = innerPath

        Return True
    End Function

    Private Function ApplyInnerFillWithSubMask(baseMat As OCV.Mat, innerMaskSvgPath As String) As OCV.Mat

        StepMark("[ApplyInnerFillWithSubMask-")

        ' マスク指定なしなら、そのまま返す
        If String.IsNullOrEmpty(innerMaskSvgPath) Then
            Return baseMat.Clone()
        End If

        If baseMat Is Nothing OrElse baseMat.Empty() Then
            Return New OCV.Mat()
        End If

        Dim width As Integer = baseMat.Cols
        Dim height As Integer = baseMat.Rows

        '========================  1) M' をラスタライズして αマスク取得  ========================
        Dim subMaskAlpha As OCV.Mat = Nothing
        RasterizeMaskAlphaOCV(innerMaskSvgPath, width, height, subMaskAlpha)

        If subMaskAlpha Is Nothing OrElse subMaskAlpha.Empty() Then
            ' マスクが作れなければ何もしない
            Return baseMat.Clone()
        End If

        'ここでのαの閾値は高めに
        Const ALPHA_THR As Integer = 100

        '========================  2) M' 内側の平均色（B,G,R）を計算  ========================
        Dim channels() As OCV.Mat = OCV.Cv2.Split(baseMat)
        If channels.Length < 4 Then
            ' α付きでない画像はそのまま返す
            subMaskAlpha.Dispose()
            For Each m In channels
                m.Dispose()
            Next
            Return baseMat.Clone()
        End If

        Dim baseB As OCV.Mat = channels(0)
        Dim baseG As OCV.Mat = channels(1)
        Dim baseR As OCV.Mat = channels(2)
        Dim baseA As OCV.Mat = channels(3)

        Dim sumB As Long = 0
        Dim sumG As Long = 0
        Dim sumR As Long = 0
        Dim count As Long = 0

        For y As Integer = 0 To height - 1
            For x As Integer = 0 To width - 1
                '両方が重なっている場所・
                Dim aMask As Byte = subMaskAlpha.Get(Of Byte)(y, x)
                If aMask < ALPHA_THR Then
                    Continue For
                End If

                Dim aBase As Byte = baseA.Get(Of Byte)(y, x)
                If aBase < ALPHA_THR Then
                    Continue For
                End If

                Dim bVal As Byte = baseB.Get(Of Byte)(y, x)
                Dim gVal As Byte = baseG.Get(Of Byte)(y, x)
                Dim rVal As Byte = baseR.Get(Of Byte)(y, x)

                sumB += bVal
                sumG += gVal
                sumR += rVal
                count += 1
            Next
        Next

        If count = 0 Then
            ' 有効画素が1つもなければ何もしない
            subMaskAlpha.Dispose()
            For Each m In channels
                m.Dispose()
            Next
            Return baseMat.Clone()
        End If

        Dim avgB As Double = CDbl(sumB) / count
        Dim avgG As Double = CDbl(sumG) / count
        Dim avgR As Double = CDbl(sumR) / count

        '========================  3) M' の単色レイヤーを B の上にオーバーレイ  ========================
        ' Porter-Duff "over":
        '   F = 塗りレイヤー (avgB,avgG,avgR, aMask)
        '   B = baseMat の元色
        '   aF = aMask / 255, aB = baseA / 255
        '   aOut = aF + aB * (1 - aF)
        '   COut = (F*C_f*aF + B*C_b*aB*(1 - aF)) / aOut
        Dim result As OCV.Mat = baseMat.Clone()

        For y As Integer = 0 To height - 1
            For x As Integer = 0 To width - 1
                Dim aMask As Byte = subMaskAlpha.Get(Of Byte)(y, x)
                If aMask < ALPHA_THR Then
                    Continue For
                End If

                Dim vBase As OCV.Vec4b = result.Get(Of OCV.Vec4b)(y, x)
                Dim aBase255 As Byte = vBase(3)

                Dim aF As Double = aMask / 255.0
                Dim aB As Double = aBase255 / 255.0

                Dim aOut As Double = aF + aB * (1.0 - aF)

                Dim outB As Byte = vBase(0)
                Dim outG As Byte = vBase(1)
                Dim outR As Byte = vBase(2)

                If aOut > 0.0 Then
                    Dim numB As Double = avgB * aF + vBase(0) * aB * (1.0 - aF)
                    Dim numG As Double = avgG * aF + vBase(1) * aB * (1.0 - aF)
                    Dim numR As Double = avgR * aF + vBase(2) * aB * (1.0 - aF)

                    Dim bD As Double = LimitB(numB / aOut)
                    Dim gD As Double = LimitB(numG / aOut)
                    Dim rD As Double = LimitB(numR / aOut)

                    outB = CByte(Math.Round(bD))
                    outG = CByte(Math.Round(gD))
                    outR = CByte(Math.Round(rD))
                End If

                Dim aOut255 As Double = LimitB(aOut * 255.0)


                result.Set(Of OCV.Vec4b)(y, x, New OCV.Vec4b(outB, outG, outR, CByte(Math.Round(aOut255))))
            Next
        Next

        '========================  4) 後片付け  ========================
        subMaskAlpha.Dispose()
        For Each m In channels
            m.Dispose()
        Next

        StepMark("-Done]")
        Return result
    End Function

#End Region

#End Region


#Region "NDCへの埋込 DB処理"

#Region "(6)最終ファイル生成"
    Private Sub FinalName(ByRef outputFileFinalName As String, ByRef productFullNameC As String)
        '***原画_と_口巻(数字)pを削除
        outputFileFinalName = String.Format("_{0}_{1}_", Path.GetFileNameWithoutExtension(Me.workingMaskFileFullPath), Path.GetFileNameWithoutExtension(Me.workingInputFileFullPath))
        outputFileFinalName = Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(Regex.Replace(outputFileFinalName, "_[^_]+原画_", "_"), "_口巻[0-9]+p_", "_"), "_+", "_"), "^_", ""), "_$", "")
        productFullNameC = Trim(Replace(outputFileFinalName, "_", " "))

    End Sub

    ''' <summary>
    ''' マスク＋入力原画から商品フルネームを作り、tbl402 の商品色コードID を解決する。
    ''' </summary>
    Private Function TryResolveProductColorCode(
            ByRef outputFileFinalName As String,
            ByRef productFullNameC As String,
            ByRef pccId As Integer,
            ByRef pId As Integer) As Boolean
        outputFileFinalName = String.Empty
        productFullNameC = String.Empty
        pccId = 0
        pId = 0

        FinalName(outputFileFinalName, productFullNameC)
        If String.IsNullOrEmpty(productFullNameC) Then
            Return False
        End If

        pccId = clsNDC.GetSqlInteger(
            0,
            "SELECT dbo.tbl402商品色コード.商品色コードID FROM dbo.tbl402商品色コード INNER JOIN dbo.tbl401商品 ON dbo.tbl402商品色コード.商品ID = dbo.tbl401商品.商品ID WHERE (dbo.tbl402商品色コード.商品フルネーム = {0}) ORDER BY dbo.tbl401商品.廃盤チェック, dbo.tbl402商品色コード.現在選択可 DESC",
            clsNDC.EscapeSQLString(productFullNameC))
        If pccId <= 0 Then
            Return False
        End If

        pId = clsNDC.GetSqlInteger(0, "SELECT 商品ID FROM dbo.tbl402商品色コード WHERE (商品色コードID = {0})", pccId)
        Return pId > 0
    End Function

    ''' <summary>
    ''' 3000x3000 から 1500（03_最終.svg 正本）および 800/400/150/60 (png) を Skia で生成する。
    ''' 02_編集 PSD は書かない。pccId は ProcessOne 側で解決済みであること。
    ''' </summary>
    Private Sub FinalOutputs(
            combinedMat As OCV.Mat,
            pccId As Integer,
            pId As Integer,
            outputFileFinalName As String,
            productFullNameC As String)

        StepMark("[FinalOutputs-")
        Dim logLineBuilder As New StringBuilder
        logLineBuilder.AppendFormat("{1}{0}", vbTab, productFullNameC)

        Dim productBaseFolder As String = String.Empty

        Dim pccIdStatus As String = clsNDC.GetSqlString("SELECT CASE WHEN dbo.tbl401商品.廃盤チェック = 1 THEN N'(廃盤)' WHEN dbo.tbl402商品色コード.現在選択可 = 0 THEN N'(現在選択不可)' ELSE N'〇' END AS e FROM dbo.tbl402商品色コード INNER JOIN dbo.tbl401商品 ON dbo.tbl402商品色コード.商品ID = dbo.tbl401商品.商品ID WHERE (dbo.tbl402商品色コード.商品色コードID = {0})", pccId)
        logLineBuilder.AppendFormat("{1}{0}", vbTab, pccIdStatus)

        Dim gengaFileFullPathOriginal As String = String.Empty
        If cls商品.原画ファイルフルパス(pccId, gengaFileFullPathOriginal, False) Then
            logLineBuilder.AppendFormat("{1}{0}", vbTab, gengaFileFullPathOriginal)
        Else
            logLineBuilder.AppendFormat("{1}{0}", vbTab, "(元原画ファイル:無)")
        End If

        If Not cls商品.商品画像フォルダ(pId, False, productBaseFolder) Then
            logLineBuilder.AppendFormat("★画像用フォルダ取得失敗★{0}", vbTab)
            AppendLog(String.Format("FinalOutputsスキップ 画像用フォルダ取得失敗: {0}", productFullNameC), False)
            StepMark("Skip-NoPictureFolder]")
            LogFile(logLineBuilder.ToString)
            Return
        End If

        Dim finalDir As String = Path.Combine(productBaseFolder, cls商品.最終サブフォルダ)
        If Not Directory.Exists(finalDir) Then
            Directory.CreateDirectory(finalDir)
        End If

        Try
            Dim pngBytes() As Byte = Nothing
            OCV.Cv2.ImEncode(".png", combinedMat, pngBytes)
            If pngBytes Is Nothing OrElse pngBytes.Length = 0 Then
                Throw New Exception("合成画像の PNG 化に失敗しました。")
            End If

            Using master As SK.SKBitmap = SK.SKBitmap.Decode(pngBytes)
                If master Is Nothing Then
                    Throw New Exception("合成画像の読込に失敗しました。")
                End If

                Dim targetPixels As New List(Of Integer) From {1500, 800, 400, 150, 60}
                For Each targetPixel In targetPixels
                    StepMark(String.Format("FO({0})-", targetPixel))
                    Using sized As SK.SKBitmap = ResizeSkBitmap(master, targetPixel, targetPixel)
                        If targetPixel = 1500 Then
                            Dim svgFileName As String = outputFileFinalName & ".svg"
                            Dim svgFullPath As String = Path.Combine(finalDir, svgFileName)
                            SaveEmbeddedSvgFromBitmap(sized, svgFullPath)
                            If Not File.Exists(svgFullPath) Then
                                Throw New Exception("03_最終 への SVG 保存に失敗しました: " & svgFullPath)
                            End If
                            logLineBuilder.AppendFormat("原画SVG:{1}{0}", vbTab, svgFullPath)

                            clsNDC.ExecuteNonQueryNDC(
                                "UPDATE dbo.tbl402商品色コード SET 原画ファイル = {1} WHERE (商品色コードID = {0})",
                                pccId,
                                clsNDC.EscapeSQLString(svgFileName))
                        Else
                            Dim webPictureIndex As Integer = Array.IndexOf(clsWeb.webPictureWidth, targetPixel)
                            Dim outputFullPath As String = String.Format(
                                "{0}\product-color-code\id{1:0000}000\id{2:0000000}-{3}.png",
                                clsWeb.WEBROOT_IMG_DB_SRC,
                                pccId \ 1000,
                                pccId,
                                clsWeb.webPictureWidth(webPictureIndex))
                            SaveSkBitmapAsPng(sized, outputFullPath)

                            If targetPixel = 400 Then
                                UpdateOutputPreview(outputFullPath)
                            End If
                        End If
                    End Using
                Next
            End Using

            StepMark("Done]")

        Catch ex As Exception
            logLineBuilder.AppendFormat("最終出力例外({1}){0}", vbTab, ex.Message)
            AppendLog(String.Format("FinalOutputs例外 {0}", ex.Message), True)
        End Try

        LogFile(logLineBuilder.ToString)

    End Sub

    ''' <summary>
    ''' 正方形 SKBitmap を埋め込み PNG の SVG として保存（NDC SvgDirect_SaveSvgShared と同形）。
    ''' </summary>
    Private Shared Sub SaveEmbeddedSvgFromBitmap(master As SK.SKBitmap, svgFullPath As String)
        Dim side As Integer = master.Width
        Dim pngBytes As Byte()
        Using img As SK.SKImage = SK.SKImage.FromBitmap(master)
            Using data As SK.SKData = img.Encode(SK.SKEncodedImageFormat.Png, 100)
                pngBytes = data.ToArray()
            End Using
        End Using

        Dim b64 As String = Convert.ToBase64String(pngBytes)
        Dim sb As New StringBuilder()
        sb.AppendLine("<?xml version=""1.0"" encoding=""UTF-8""?>")
        sb.AppendFormat(
            CultureInfo.InvariantCulture,
            "<svg xmlns=""http://www.w3.org/2000/svg"" xmlns:xlink=""http://www.w3.org/1999/xlink"" width=""{0}px"" height=""{0}px"" viewBox=""0 0 {0} {0}"">" & vbCrLf,
            side)
        sb.AppendFormat(
            CultureInfo.InvariantCulture,
            "  <image width=""{0}"" height=""{0}"" xlink:href=""data:image/png;base64,{1}"" href=""data:image/png;base64,{1}"" />" & vbCrLf,
            side, b64)
        sb.AppendLine("</svg>")

        Dim outDir As String = Path.GetDirectoryName(svgFullPath)
        If Not String.IsNullOrEmpty(outDir) AndAlso Not Directory.Exists(outDir) Then
            Directory.CreateDirectory(outDir)
        End If
        File.WriteAllText(svgFullPath, sb.ToString(), New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False))
    End Sub

#End Region


#End Region
End Class
