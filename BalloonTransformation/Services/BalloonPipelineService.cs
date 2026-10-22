using OpenCvSharp;

namespace Naranja.BalloonTransformation.Services;

public sealed class PipelineProgress
{
    public int Processed { get; init; }
    public int Total { get; init; }
    public string Message { get; init; } = "";
    public string? PreviewPngPath { get; init; }
    public string? InputPath { get; init; }
}

public static class BalloonPipelineService
{
    public static List<string> ListInputFiles()
    {
        AppPaths.EnsureWorkFolders();
        return [.. Directory.EnumerateFiles(AppPaths.InputDir, "*.svg").OrderBy(p => p, StringComparer.OrdinalIgnoreCase)];
    }

    public static List<string> ListMaskFiles()
    {
        AppPaths.EnsureWorkFolders();
        return [.. Directory.EnumerateFiles(AppPaths.MaskDir, "*.svg").OrderBy(p => p, StringComparer.OrdinalIgnoreCase)];
    }

    public static void Run(CancellationToken token, IProgress<PipelineProgress> progress)
    {
        AppPaths.EnsureWorkFolders();
        var inputs = ListInputFiles();
        var masks = ListMaskFiles();
        if (inputs.Count == 0)
        {
            throw new InvalidOperationException("入力 SVG がありません。");
        }

        if (masks.Count is 0 or > 9)
        {
            throw new InvalidOperationException("マスクが 0 件、または 9 件超です。");
        }

        var total = inputs.Count * masks.Count;
        var processed = 0;
        var errors = 0;
        foreach (var input in inputs)
        {
            token.ThrowIfCancellationRequested();
            progress.Report(new PipelineProgress
            {
                Processed = processed,
                Total = total,
                Message = Path.GetFileName(input),
                InputPath = input
            });

            try
            {
                ProcessMask(input, masks, token, progress, ref processed, total);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors++;
                progress.Report(new PipelineProgress
                {
                    Processed = processed,
                    Total = total,
                    Message = $"エラー {Path.GetFileName(input)}: {ex.Message}"
                });
            }
        }

        progress.Report(new PipelineProgress
        {
            Processed = processed,
            Total = total,
            Message = errors == 0 ? "DONE all" : $"DONE with errors (count={errors})"
        });
    }

    private static void ProcessMask(
        string inputFullPath,
        List<string> maskSvgFiles,
        CancellationToken token,
        IProgress<PipelineProgress> progress,
        ref int processed,
        int total)
    {
        using var source = ImageProcessingService.PrepareOriginalToWorkingMat(inputFullPath);
        ImageProcessingService.SplitBalloon(source, out var aUp, out var aDown);
        try
        {
            foreach (var maskSvgPath in maskSvgFiles)
            {
                token.ThrowIfCancellationRequested();
                processed++;
                string? preview = null;
                try
                {
                    preview = ProcessOne(aUp, aDown, maskSvgPath, inputFullPath, progress);
                }
                catch (Exception ex)
                {
                    progress.Report(new PipelineProgress
                    {
                        Processed = processed,
                        Total = total,
                        Message = $"ProcessOne例外({Path.GetFileName(maskSvgPath)}): {ex.Message}"
                    });
                }

                progress.Report(new PipelineProgress
                {
                    Processed = processed,
                    Total = total,
                    Message = Path.GetFileName(maskSvgPath),
                    PreviewPngPath = preview,
                    InputPath = inputFullPath
                });
            }
        }
        finally
        {
            aUp.Dispose();
            aDown.Dispose();
        }
    }

    private static string? ProcessOne(Mat aUp, Mat aDown, string maskSvgPath, string inputFullPath, IProgress<PipelineProgress> progress)
    {
        var resolved = ProductImageService.ResolveByFullName(maskSvgPath, inputFullPath);
        if (resolved is null)
        {
            progress.Report(new PipelineProgress { Message = $"スキップ 商品色コードID未検出: {Path.GetFileName(inputFullPath)}" });
            return null;
        }

        var hasInner = ImageProcessingService.SplitSvgIntoMainAndInnerMasks(maskSvgPath, out var mainMaskPath, out var innerMaskPath);
        using var warped = ImageProcessingService.ApplyMaskTransform(aUp, mainMaskPath);
        var kuchimaki = ImageProcessingService.ParseKPercent(maskSvgPath);
        var combined = ImageProcessingService.AttachDownPart(warped, aDown, kuchimaki, AppPaths.WorkingZoomPixel, AppPaths.WorkingZoomPixel);
        try
        {
            if (hasInner && !string.IsNullOrEmpty(innerMaskPath))
            {
                var filled = ImageProcessingService.ApplyInnerFillWithSubMask(combined, innerMaskPath!);
                combined.Dispose();
                combined = filled;
                if (!string.Equals(mainMaskPath, maskSvgPath, StringComparison.OrdinalIgnoreCase))
                {
                    ImageProcessingService.ForceDelete(mainMaskPath);
                }

                ImageProcessingService.ForceDelete(innerMaskPath!);
            }

            ImageProcessingService.WriteFinalOutputs(combined, resolved, msg =>
                progress.Report(new PipelineProgress { Message = msg }), out var preview);
            AppendLogFile($"{resolved.ProductFullName}\t{resolved.StatusText}\t{resolved.ExistingOriginalPath ?? "(元原画ファイル:無)"}");
            return preview;
        }
        finally
        {
            combined.Dispose();
        }
    }

    private static void AppendLogFile(string line)
    {
        try
        {
            var logDir = Path.Combine(AppPaths.OutputDir, "log");
            Directory.CreateDirectory(logDir);
            File.AppendAllText(Path.Combine(logDir, $"{DateTime.Today:yyyyMMdd}.txt"), line + Environment.NewLine);
        }
        catch
        {
            // ignore
        }
    }
}
