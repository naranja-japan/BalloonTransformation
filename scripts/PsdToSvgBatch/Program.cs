using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Photoshop;
using SkiaSharp;

/// <summary>
/// Sempertex標本画像 などフラットフォルダの *.psd を、NDC と同形の埋込PNG正方形 SVG へ一括変換。
/// Photoshop CS4 COM（可視統合 → Save for Web PN24 / Matte OFF）→ Skia で正方形 SVG。
/// </summary>
internal static class Program
{
    private const int RpcEServerCallRetryLater = unchecked((int)0x8001010A);
    private const int RpcECallRejected = unchecked((int)0x80010001);
    private static Photoshop.Application? _ps;

    [STAThread]
    private static int Main(string[] args)
    {
        var sourceDir = args.Length > 0
            ? args[0]
            : @"\\naranja.local\dfs02\naranja\9000_NDC\Data\Images\Sempertex標本画像";

        if (!Directory.Exists(sourceDir))
        {
            Console.Error.WriteLine("フォルダがありません: " + sourceDir);
            return 1;
        }

        var psds = Directory.EnumerateFiles(sourceDir, "*.psd")
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        Console.WriteLine($"対象 PSD: {psds.Count} 件  dir={sourceDir}");
        if (psds.Count == 0)
        {
            return 0;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "NaranjaTools", "PsdToSvgBatch");
        Directory.CreateDirectory(tempDir);

        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            $"SempertexPsdToSvg_{DateTime.Now:yyyyMMddHHmmss}.log");
        void Log(string msg)
        {
            var line = $"{DateTime.Now:HH:mm:ss} {msg}";
            Console.WriteLine(line);
            try
            {
                File.AppendAllText(logPath, line + Environment.NewLine, new UTF8Encoding(false));
            }
            catch
            {
                // ignore log IO
            }
        }

        var ok = 0;
        var skip = 0;
        var fail = 0;

        Console.Out.AutoFlush = true;
        Console.Error.AutoFlush = true;
        OleMessageFilter.Register();
        try
        {
            EnsurePhotoshop();
            Log("Photoshop 起動 OK");

            for (var i = 0; i < psds.Count; i++)
            {
                var psdPath = psds[i];
                var stem = Path.GetFileNameWithoutExtension(psdPath);
                var svgPath = Path.Combine(sourceDir, stem + ".svg");
                var label = $"{i + 1}/{psds.Count} {Path.GetFileName(psdPath)}";

                if (File.Exists(svgPath) && new FileInfo(svgPath).Length > 0)
                {
                    skip++;
                    Log($"SKIP 既存SVG {label}");
                    continue;
                }

                var tempPng = Path.Combine(tempDir, $"psd2svg_{Guid.NewGuid():N}.png");
                var converted = false;
                string lastErr = string.Empty;

                for (var attempt = 1; attempt <= 8; attempt++)
                {
                    try
                    {
                        if (ConvertOne(psdPath, tempPng, svgPath, out var err))
                        {
                            converted = true;
                            break;
                        }

                        lastErr = err;
                        if (!IsComBusy(err) || attempt == 8)
                        {
                            break;
                        }

                        Log($"RETRY {attempt}/8 busy {label}");
                        Thread.Sleep(1500 * attempt);
                        try { EnsurePhotoshop(); } catch { QuitPhotoshop(); Thread.Sleep(2000); EnsurePhotoshop(); }
                    }
                    catch (COMException comEx) when (IsComBusy(comEx.ErrorCode) && attempt < 8)
                    {
                        lastErr = comEx.Message;
                        Log($"RETRY {attempt}/8 COM {label}: {comEx.Message}");
                        Thread.Sleep(1500 * attempt);
                        try { EnsurePhotoshop(); } catch { QuitPhotoshop(); Thread.Sleep(2000); EnsurePhotoshop(); }
                    }
                    catch (Exception ex)
                    {
                        lastErr = ex.Message;
                        break;
                    }
                    finally
                    {
                        TryDelete(tempPng);
                    }
                }

                if (converted)
                {
                    ok++;
                    var bytes = new FileInfo(svgPath).Length;
                    Log($"OK {label} → {bytes:N0} bytes");
                }
                else
                {
                    fail++;
                    Log($"FAIL {label}: {lastErr}");
                }

                // Save for Web 直後のビジーを避ける
                Thread.Sleep(400);
            }
        }
        finally
        {
            QuitPhotoshop();
            OleMessageFilter.Revoke();
            Log($"完了 OK={ok} Skip={skip} Fail={fail}");
            Console.WriteLine("ログ: " + logPath);
        }

        return fail > 0 ? 2 : 0;
    }

    private static bool IsComBusy(int errorCode) =>
        errorCode == RpcEServerCallRetryLater || errorCode == RpcECallRejected;

    private static bool IsComBusy(string? message) =>
        !string.IsNullOrEmpty(message) &&
        (message.Contains("0x8001010A", StringComparison.OrdinalIgnoreCase) ||
         message.Contains("ビジー", StringComparison.Ordinal) ||
         message.Contains("busy", StringComparison.OrdinalIgnoreCase));

    private static void EnsurePhotoshop()
    {
        if (_ps is not null)
        {
            try
            {
                _ = _ps.Name;
                _ps.DisplayDialogs = PsDialogModes.psDisplayNoDialogs;
                return;
            }
            catch
            {
                _ps = null;
            }
        }

        _ps = new Photoshop.Application
        {
            Visible = true
        };
        _ps.Preferences.RulerUnits = PsUnits.psPixels;
        _ps.DisplayDialogs = PsDialogModes.psDisplayNoDialogs;
        Thread.Sleep(1500);
    }

    private static void QuitPhotoshop()
    {
        try
        {
            if (_ps is not null)
            {
                while (_ps.Documents.Count > 0)
                {
                    try { _ps.ActiveDocument.Close(PsSaveOptions.psDoNotSaveChanges); } catch { break; }
                }

                _ps.Quit();
            }
        }
        catch
        {
            // ignore
        }
        finally
        {
            _ps = null;
            Thread.Sleep(1000);
        }
    }

    private static bool ConvertOne(string psdPath, string tempPng, string svgPath, out string errorMessage)
    {
        errorMessage = string.Empty;
        Document? doc = null;
        Document? dup = null;

        try
        {
            EnsurePhotoshop();
            doc = WithComRetry(() => _ps!.Open(psdPath));
            dup = WithComRetry(() => doc.Duplicate());
            WithComRetry(() => { _ps!.ActiveDocument = dup; return true; });

            UnlockBackground(dup);
            MergeVisible(_ps!);

            if (!SaveTransparentPng(dup, tempPng, out errorMessage))
            {
                return false;
            }
        }
        finally
        {
            try { WithComRetry(() => { dup?.Close(PsSaveOptions.psDoNotSaveChanges); return true; }); } catch { }
            try { WithComRetry(() => { doc?.Close(PsSaveOptions.psDoNotSaveChanges); return true; }); } catch { }
        }

        using var loaded = SKBitmap.Decode(tempPng);
        if (loaded is null)
        {
            errorMessage = "統合PNGの読込に失敗しました。";
            return false;
        }

        var side = Math.Max(loaded.Width, loaded.Height);
        using var square = new SKBitmap(side, side, SKColorType.Rgba8888, SKAlphaType.Premul);
        square.Erase(SKColors.Transparent);
        using (var canvas = new SKCanvas(square))
        {
            canvas.DrawBitmap(loaded, (side - loaded.Width) / 2f, (side - loaded.Height) / 2f);
        }

        SaveEmbeddedSvg(square, svgPath);
        if (!File.Exists(svgPath))
        {
            errorMessage = "SVG出力後のファイルがありません。";
            return false;
        }

        return true;
    }

    private static T WithComRetry<T>(Func<T> action, int maxAttempts = 10)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return action();
            }
            catch (COMException ex) when (IsComBusy(ex.ErrorCode) && attempt < maxAttempts)
            {
                Thread.Sleep(500 * attempt);
            }
        }
    }

    private static void UnlockBackground(Document doc)
    {
        WithComRetry(() =>
        {
            foreach (ArtLayer layer in doc.ArtLayers)
            {
                if (layer.IsBackgroundLayer)
                {
                    layer.IsBackgroundLayer = false;
                }
            }

            return true;
        });
    }

    private static void MergeVisible(Photoshop.Application ps)
    {
        WithComRetry(() =>
        {
            var doc = ps.ActiveDocument;
            var visibleCount = 0;
            foreach (ArtLayer layer in doc.ArtLayers)
            {
                if (layer.Visible)
                {
                    visibleCount++;
                }
            }

            if (visibleCount <= 1 && doc.LayerSets.Count == 0)
            {
                return true;
            }

            try
            {
                var idMrgV = ps.CharIDToTypeID("MrgV");
                ps.ExecuteAction(idMrgV, null, PsDialogModes.psDisplayNoDialogs);
            }
            catch
            {
                // CS4: 1枚・ロック等で Merge Visible 不可 → PNG 保存に任せる
            }

            return true;
        });
    }

    /// <summary>NDC Photoshop_ドキュメントを透過PngへSaveAs と同形（Save for Web PN24 / Mtt=False）。</summary>
    private static bool SaveTransparentPng(Document doc, string pngFullPath, out string errorMessage)
    {
        errorMessage = string.Empty;
        try
        {
            var outDir = Path.GetDirectoryName(pngFullPath);
            if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
            {
                Directory.CreateDirectory(outDir);
            }

            TryDelete(pngFullPath);

            return WithComRetry(() =>
            {
                var psApp = doc.Application;
                psApp.DisplayDialogs = PsDialogModes.psDisplayNoDialogs;
                psApp.ActiveDocument = doc;

                var descSfw = new ActionDescriptor();
                descSfw.PutEnumerated(
                    psApp.CharIDToTypeID("Op  "),
                    psApp.CharIDToTypeID("SWOp"),
                    psApp.CharIDToTypeID("OpSa"));
                descSfw.PutEnumerated(
                    psApp.CharIDToTypeID("Fmt "),
                    psApp.CharIDToTypeID("IRFm"),
                    psApp.CharIDToTypeID("PN24"));
                descSfw.PutBoolean(psApp.CharIDToTypeID("Intr"), false);
                descSfw.PutBoolean(psApp.CharIDToTypeID("Trns"), true);
                descSfw.PutBoolean(psApp.CharIDToTypeID("Mtt "), false);
                descSfw.PutBoolean(psApp.CharIDToTypeID("SHTM"), false);
                descSfw.PutBoolean(psApp.CharIDToTypeID("SImg"), true);
                descSfw.PutBoolean(psApp.CharIDToTypeID("SSSO"), false);
                descSfw.PutList(psApp.CharIDToTypeID("SSLt"), new ActionList());
                descSfw.PutBoolean(psApp.CharIDToTypeID("DIDr"), false);
                descSfw.PutPath(psApp.CharIDToTypeID("In  "), pngFullPath);

                var descExpr = new ActionDescriptor();
                descExpr.PutObject(
                    psApp.CharIDToTypeID("Usng"),
                    psApp.StringIDToTypeID("SaveForWeb"),
                    descSfw);
                psApp.ExecuteAction(
                    psApp.CharIDToTypeID("Expr"),
                    descExpr,
                    PsDialogModes.psDisplayNoDialogs);

                // Save for Web は非同期気味にビジーが残ることがある
                for (var wait = 0; wait < 40; wait++)
                {
                    if (File.Exists(pngFullPath) && new FileInfo(pngFullPath).Length > 0)
                    {
                        return true;
                    }

                    Thread.Sleep(250);
                }

                throw new InvalidOperationException("PNG保存後にファイルがありません: " + pngFullPath);
            });
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>NDC SvgDirect_SaveSvgShared と同形。</summary>
    private static void SaveEmbeddedSvg(SKBitmap master, string svgFullPath)
    {
        var side = master.Width;
        byte[] pngBytes;
        using (var img = SKImage.FromBitmap(master))
        using (var data = img.Encode(SKEncodedImageFormat.Png, 100))
        {
            pngBytes = data.ToArray();
        }

        var b64 = Convert.ToBase64String(pngBytes);
        var sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8""?>");
        sb.AppendFormat(
            CultureInfo.InvariantCulture,
            @"<svg xmlns=""http://www.w3.org/2000/svg"" xmlns:xlink=""http://www.w3.org/1999/xlink"" width=""{0}px"" height=""{0}px"" viewBox=""0 0 {0} {0}"">" + Environment.NewLine,
            side);
        sb.AppendFormat(
            CultureInfo.InvariantCulture,
            @"  <image width=""{0}"" height=""{0}"" xlink:href=""data:image/png;base64,{1}"" href=""data:image/png;base64,{1}"" />" + Environment.NewLine,
            side,
            b64);
        sb.AppendLine("</svg>");

        var outDir = Path.GetDirectoryName(svgFullPath);
        if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
        {
            Directory.CreateDirectory(outDir);
        }

        File.WriteAllText(svgFullPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignore
        }
    }
}

/// <summary>
/// Photoshop COM の RPC_E_SERVERCALL_RETRYLATER を自動リトライするメッセージフィルタ。
/// </summary>
internal sealed class OleMessageFilter : IOleMessageFilter
{
    private const int Handled = 0;
    private const int RetryLater = 2;
    private const int CancelCall = -1;

    public static void Register()
    {
        CoRegisterMessageFilter(new OleMessageFilter(), out _);
    }

    public static void Revoke()
    {
        CoRegisterMessageFilter(null, out _);
    }

    int IOleMessageFilter.HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo) =>
        Handled;

    int IOleMessageFilter.RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType)
    {
        // SERVERCALL_RETRYLATER = 2
        if (dwRejectType == 2)
        {
            if (dwTickCount < 120_000)
            {
                return 500; // ミリ秒後に再試行
            }
        }

        return CancelCall;
    }

    int IOleMessageFilter.MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType) =>
        Handled; // PENDINGMSG_WAITDEFPROCESS 相当

    [DllImport("ole32.dll")]
    private static extern int CoRegisterMessageFilter(IOleMessageFilter? newFilter, out IOleMessageFilter? oldFilter);
}

[ComImport]
[Guid("00000016-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IOleMessageFilter
{
    [PreserveSig]
    int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo);

    [PreserveSig]
    int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType);

    [PreserveSig]
    int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType);
}
