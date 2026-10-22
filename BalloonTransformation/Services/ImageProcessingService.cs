using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using OpenCvSharp;
using SkiaSharp;
using Svg.Skia;

namespace Naranja.BalloonTransformation.Services;

public static class ImageProcessingService
{
    public static Mat PrepareOriginalToWorkingMat(string svgPath)
    {
        using var loaded = LoadSvgAsSkBitmap(svgPath)
            ?? throw new InvalidOperationException("原画 SVG の読込に失敗しました。");

        const int inputMinPixel = 800;
        if (loaded.Width < inputMinPixel && loaded.Height < inputMinPixel)
        {
            throw new InvalidOperationException(
                $"原画ファイル({loaded.Width}×{loaded.Height}) 指定ピクセル({inputMinPixel})未満");
        }

        var maxSide = Math.Max(loaded.Width, loaded.Height);
        using var square = new SKBitmap(maxSide, maxSide, SKColorType.Rgba8888, SKAlphaType.Premul);
        square.Erase(SKColors.Transparent);
        using (var canvas = new SKCanvas(square))
        {
            canvas.DrawBitmap(loaded, (maxSide - loaded.Width) / 2f, (maxSide - loaded.Height) / 2f);
        }

        using var resized = ResizeSkBitmap(square, AppPaths.WorkingZoomPixel, AppPaths.WorkingZoomPixel);
        using var img = SKImage.FromBitmap(resized);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return Cv2.ImDecode(data.ToArray(), ImreadModes.Unchanged);
    }

    public static SKBitmap? LoadSvgAsSkBitmap(string svgPath)
    {
        var xml = XDocument.Load(svgPath);
        var root = xml.Root;
        if (root is null)
        {
            return null;
        }

        var width = ParseSvgLengthPixels(root.Attribute("width"));
        var height = ParseSvgLengthPixels(root.Attribute("height"));
        var viewBoxAttr = root.Attribute("viewBox");
        if ((width <= 0 || height <= 0) && viewBoxAttr is not null)
        {
            var viewBoxValues = viewBoxAttr.Value.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
            if (viewBoxValues.Length == 4)
            {
                width = (int)Math.Ceiling(double.Parse(viewBoxValues[2], CultureInfo.InvariantCulture));
                height = (int)Math.Ceiling(double.Parse(viewBoxValues[3], CultureInfo.InvariantCulture));
            }
        }

        using var stream = File.OpenRead(svgPath);
        var skSvg = new SKSvg();
        if (skSvg.Load(stream) is null || skSvg.Picture is null)
        {
            return null;
        }

        if (width <= 0 || height <= 0)
        {
            var cull = skSvg.Picture.CullRect;
            width = (int)Math.Ceiling(cull.Width);
            height = (int)Math.Ceiling(cull.Height);
        }

        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var bmp = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        bmp.Erase(SKColors.Transparent);
        using var canvas = new SKCanvas(bmp);
        canvas.DrawPicture(skSvg.Picture);
        return bmp;
    }

    public static SKBitmap RasterizeSvgPreview(string svgPath, int width, int height)
    {
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.White);
        var svg = new SKSvg();
        svg.Load(svgPath);
        if (svg.Picture is null)
        {
            return new SKBitmap(width, height);
        }

        var cull = svg.Picture.CullRect;
        var scale = Math.Min(width / cull.Width, height / cull.Height);
        canvas.Translate(-cull.Left * scale + (width - cull.Width * scale) / 2f,
            -cull.Top * scale + (height - cull.Height * scale) / 2f);
        canvas.Scale(scale, scale);
        canvas.DrawPicture(svg.Picture);
        canvas.Flush();
        using var image = surface.Snapshot();
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 90);
        return SKBitmap.Decode(encoded) ?? new SKBitmap(width, height);
    }

    public static void SplitBalloon(Mat source, out Mat aUp, out Mat aDown)
    {
        var imageWidth = source.Cols;
        var imageHeight = source.Rows;
        const byte alphaThreshold = 4;
        var nonZero = new int[imageHeight];
        const int countThresholdPercentUp = 50;
        const int countThresholdPercentDown = 30;
        var countThresholdAlphaUp = imageWidth * countThresholdPercentUp * 255 / 100;
        var countThresholdAlphaDown = imageWidth * countThresholdPercentDown * 255 / 100;
        var roughStep = Math.Max(1, imageHeight / 100);
        var phaseNumber = 0;
        var initialRowIndex = 0;
        var yPixelIndex = 0;

        while (yPixelIndex < imageHeight)
        {
            var rowCount = 0;
            for (var x = 0; x < imageWidth; x++)
            {
                var px = source.Get<Vec4b>(yPixelIndex, x);
                var alpha = px.Item3;
                if (alpha >= alphaThreshold)
                {
                    rowCount += alpha;
                }
            }

            if (phaseNumber < 2)
            {
                if (phaseNumber == 0 && rowCount > countThresholdAlphaUp)
                {
                    phaseNumber++;
                }
                else if (phaseNumber == 1 && rowCount < countThresholdAlphaDown)
                {
                    phaseNumber++;
                }
            }

            if (phaseNumber < 2)
            {
                var yEnd = Math.Min(imageHeight - 1, yPixelIndex + roughStep - 1);
                for (var yy = yPixelIndex; yy <= yEnd; yy++)
                {
                    nonZero[yy] = -1;
                }

                yPixelIndex = yEnd + 1;
                if (yPixelIndex >= imageHeight)
                {
                    throw new InvalidOperationException(
                        $"SplitBalloon失敗 phaseNumber={phaseNumber} 横幅{countThresholdPercentUp}% → {countThresholdPercentDown}% 縦STEP {roughStep} で引っかからず");
                }
            }
            else
            {
                if (initialRowIndex == 0)
                {
                    initialRowIndex = yPixelIndex;
                }

                nonZero[yPixelIndex] = rowCount;
                yPixelIndex++;
            }
        }

        if (initialRowIndex == 0)
        {
            throw new InvalidOperationException($"SplitBalloon失敗 phaseNumber={phaseNumber} < 2");
        }

        var joinY = -1;
        var finalY = imageHeight - 1;
        var bottomValleyWidth = nonZero[initialRowIndex];
        var bottomValleyIndex = 0;
        var maxAfterMin = 0;

        for (var y = initialRowIndex; y < imageHeight - 3; y++)
        {
            var rowVal = nonZero[y];
            if (rowVal < 0)
            {
                continue;
            }

            if (rowVal > bottomValleyWidth + 1024)
            {
                bottomValleyIndex = y;
                break;
            }

            if (rowVal < bottomValleyWidth)
            {
                bottomValleyWidth = rowVal;
            }
        }

        if (bottomValleyIndex == 0)
        {
            throw new InvalidOperationException("SplitBalloon失敗 bottomValleyIndex取得失敗、くびれ無");
        }

        for (var y = bottomValleyIndex; y < imageHeight - 3; y++)
        {
            var rowVal = nonZero[y];
            if (rowVal < 0)
            {
                continue;
            }

            if (maxAfterMin < rowVal)
            {
                maxAfterMin = rowVal;
            }

            if (finalY == imageHeight - 1 && rowVal < 100)
            {
                finalY = y;
            }
        }

        if (maxAfterMin == 0)
        {
            throw new InvalidOperationException("SplitBalloon失敗 maxAfterMin 取得失敗、くびれ後幅無し");
        }

        for (var y = initialRowIndex; y < imageHeight - 3; y++)
        {
            if (nonZero[y + 2] > maxAfterMin)
            {
                continue;
            }

            var v0 = nonZero[y];
            var v1 = nonZero[y + 1];
            var v2 = nonZero[y + 2];
            if (v0 < 0 || v1 < 0 || v2 < 0)
            {
                continue;
            }

            if ((v1 - v0) - (v2 - v1) < -512)
            {
                joinY = y + 1;
                break;
            }
        }

        if (joinY < 0)
        {
            throw new InvalidOperationException("SplitBalloon失敗 凸性崩れた瞬間取得失敗");
        }

        if (finalY < joinY)
        {
            finalY = joinY;
        }

        if (finalY >= imageHeight)
        {
            finalY = imageHeight - 1;
        }

        aUp = new Mat(imageHeight, imageWidth, MatType.CV_8UC4, Scalar.All(0));
        var copyHeight = Math.Max(0, Math.Min(joinY, imageHeight));
        if (copyHeight > 0)
        {
            using var srcRoi = new Mat(source, new Rect(0, 0, imageWidth, copyHeight));
            using var dstRoi = new Mat(aUp, new Rect(0, 0, imageWidth, copyHeight));
            srcRoi.CopyTo(dstRoi);
        }

        var downHeight = finalY - joinY + 1;
        if (downHeight <= 0)
        {
            aDown = new Mat(1, imageWidth, MatType.CV_8UC4, Scalar.All(0));
        }
        else
        {
            aDown = new Mat(source, new Rect(0, joinY, imageWidth, downHeight)).Clone();
        }
    }

    public static Mat ApplyMaskTransform(Mat aUp, string maskSvgPath)
    {
        const int angleSamples = 4096;
        const int alphaThr = 5;
        const double innerMargin = 0.995;
        var workingW = AppPaths.WorkingZoomPixel;
        var workingH = AppPaths.WorkingZoomPixel;
        if (aUp.Empty())
        {
            throw new ArgumentException("ApplyMaskTransform: aUp is empty");
        }

        using var srcBgraHi = ToBgra(aUp);
        using var maskAlphaHi = RasterizeMaskAlpha(maskSvgPath, workingW, workingH);
        var srcChHi = Cv2.Split(srcBgraHi);
        using var srcAlphaHi = srcChHi[3];
        var cx = workingW / 2.0;
        var cy = workingH / 2.0;
        using var srcProfile = BuildRadialProfile(srcAlphaHi, cx, cy, angleSamples);
        using var dstProfile = BuildRadialProfile(maskAlphaHi, cx, cy, angleSamples);
        foreach (var m in srcChHi.Take(3))
        {
            m.Dispose();
        }

        using var mapX = new Mat(workingH, workingW, MatType.CV_32FC1);
        using var mapY = new Mat(workingH, workingW, MatType.CV_32FC1);
        var twoPi = 2.0 * Math.PI;
        for (var y = 0; y < workingH; y++)
        {
            for (var x = 0; x < workingW; x++)
            {
                var aMask = maskAlphaHi.Get<byte>(y, x);
                if (aMask < alphaThr)
                {
                    mapX.Set(y, x, float.NaN);
                    mapY.Set(y, x, float.NaN);
                    continue;
                }

                var dx = (x + 0.5) - cx;
                var dy = (y + 0.5) - cy;
                var rDst = Math.Sqrt(dx * dx + dy * dy);
                var theta = Math.Atan2(dy, dx);
                if (theta < 0)
                {
                    theta += twoPi;
                }

                var rSrcMax = Math.Max(1.0, RadiusAtAngle(srcProfile, theta));
                var rDstMax = Math.Max(1.0, RadiusAtAngle(dstProfile, theta));
                var innerDstMax = rDstMax * innerMargin;
                if (rDst > innerDstMax)
                {
                    mapX.Set(y, x, float.NaN);
                    mapY.Set(y, x, float.NaN);
                    continue;
                }

                var t = rDst / innerDstMax;
                var rSrc = t * rSrcMax;
                mapX.Set(y, x, (float)(cx + Math.Cos(theta) * rSrc));
                mapY.Set(y, x, (float)(cy + Math.Sin(theta) * rSrc));
            }
        }

        using var warpedHi = new Mat();
        Cv2.Remap(srcBgraHi, warpedHi, mapX, mapY, InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(0));
        var warpedChHi = Cv2.Split(warpedHi);
        using var warpedAlphaHi = warpedChHi[3];
        var finalAlphaHi = new Mat();
        Cv2.Min(warpedAlphaHi, maskAlphaHi, finalAlphaHi);
        var b = warpedChHi[0];
        var g = warpedChHi[1];
        var r = warpedChHi[2];
        using var b32 = new Mat();
        using var g32 = new Mat();
        using var r32 = new Mat();
        using var a32 = new Mat();
        using var f32 = new Mat();
        using var denom = new Mat();
        using var scale = new Mat();
        b.ConvertTo(b32, MatType.CV_32FC1);
        g.ConvertTo(g32, MatType.CV_32FC1);
        r.ConvertTo(r32, MatType.CV_32FC1);
        warpedAlphaHi.ConvertTo(a32, MatType.CV_32FC1);
        finalAlphaHi.ConvertTo(f32, MatType.CV_32FC1);
        Cv2.Max(a32, new Scalar(1.0), denom);
        Cv2.Divide(f32, denom, scale);
        Cv2.Multiply(b32, scale, b32);
        Cv2.Multiply(g32, scale, g32);
        Cv2.Multiply(r32, scale, r32);
        b32.ConvertTo(b, MatType.CV_8UC1);
        g32.ConvertTo(g, MatType.CV_8UC1);
        r32.ConvertTo(r, MatType.CV_8UC1);
        warpedChHi[3].Dispose();
        warpedChHi[3] = finalAlphaHi;
        var result = new Mat();
        Cv2.Merge(warpedChHi, result);
        foreach (var m in warpedChHi)
        {
            m.Dispose();
        }

        return result;
    }

    public static Mat AttachDownPart(Mat aUp, Mat aDown, int kPercent, int canvasW, int canvasH, int overlapPx = 1)
    {
        if (aUp.Empty())
        {
            throw new ArgumentException("aUp is empty");
        }

        if (aDown.Empty())
        {
            throw new ArgumentException("aDown is empty");
        }

        Mat upFit = aUp;
        var needsResize = aUp.Cols != canvasW || aUp.Rows != canvasH;
        if (needsResize)
        {
            upFit = new Mat();
            Cv2.Resize(aUp, upFit, new Size(canvasW, canvasH), 0, 0, InterpolationFlags.Lanczos4);
        }

        var result = new Mat(canvasH, canvasW, MatType.CV_8UC4, Scalar.All(0));
        upFit.CopyTo(result);
        var kScale = Math.Max(1, kPercent) / 100.0;
        var downScaled = new Mat();
        Cv2.Resize(aDown, downScaled, new Size(), kScale, kScale, InterpolationFlags.Lanczos4);
        var downChannels = downScaled.Split();
        var downAlpha = downChannels[3];
        using var binaryMask = new Mat();
        Cv2.Threshold(downAlpha, binaryMask, 0, 255, ThresholdTypes.Binary);
        using var nonZeroPoints = new Mat();
        Cv2.FindNonZero(binaryMask, nonZeroPoints);
        if (nonZeroPoints.Empty())
        {
            foreach (var c in downChannels)
            {
                c.Dispose();
            }

            downScaled.Dispose();
            if (needsResize)
            {
                upFit.Dispose();
            }

            return result;
        }

        var boundingRect = Cv2.BoundingRect(nonZeroPoints);
        var topRow = boundingRect.Y;
        var centerX = FindMouthTopCenterX(downAlpha);
        if (centerX < 0)
        {
            centerX = boundingRect.X + boundingRect.Width / 2;
        }

        var bottomOpaqueRow = FindBottomNonTransparent(upFit);
        if (bottomOpaqueRow < 0)
        {
            bottomOpaqueRow = canvasH - 1;
        }

        var dstY = (bottomOpaqueRow - overlapPx) - topRow;
        var dstX = (canvasW / 2) - centerX;
        var overflowY = (dstY + downScaled.Rows) - canvasH;
        if (overflowY > 0)
        {
            var newHeight = Math.Max(1, downScaled.Rows - overflowY);
            if (newHeight < downScaled.Rows)
            {
                var scaleY = newHeight / (double)downScaled.Rows;
                var newWidth = (int)Math.Round(downScaled.Cols * scaleY);
                var temp = new Mat();
                Cv2.Resize(downScaled, temp, new Size(Math.Max(1, newWidth), newHeight), 0, 0, InterpolationFlags.Area);
                downScaled.Dispose();
                downScaled = temp;
                foreach (var c in downChannels)
                {
                    c.Dispose();
                }

                downChannels = downScaled.Split();
                downAlpha = downChannels[3];
                Cv2.Threshold(downAlpha, binaryMask, 0, 255, ThresholdTypes.Binary);
                Cv2.FindNonZero(binaryMask, nonZeroPoints);
                boundingRect = Cv2.BoundingRect(nonZeroPoints);
                topRow = boundingRect.Y;
                centerX = FindMouthTopCenterX(downAlpha);
                if (centerX < 0)
                {
                    centerX = boundingRect.X + boundingRect.Width / 2;
                }

                dstY = (bottomOpaqueRow - overlapPx) - topRow;
                dstX = (canvasW / 2) - centerX;
            }
        }

        var destinationRect = new Rect(dstX, dstY, downScaled.Cols, downScaled.Rows);
        var clipped = IntersectRect(destinationRect, new Rect(0, 0, canvasW, canvasH));
        if (clipped.Width > 0 && clipped.Height > 0)
        {
            var sourceStartX = clipped.X - dstX;
            var sourceStartY = clipped.Y - dstY;
            for (var oy = 0; oy < clipped.Height; oy++)
            {
                for (var ox = 0; ox < clipped.Width; ox++)
                {
                    var downPixel = downScaled.Get<Vec4b>(sourceStartY + oy, sourceStartX + ox);
                    var alphaDown = downPixel.Item3 / 255.0;
                    if (alphaDown <= 0)
                    {
                        continue;
                    }

                    var yyDst = clipped.Y + oy;
                    var xxDst = clipped.X + ox;
                    var upPixel = result.Get<Vec4b>(yyDst, xxDst);
                    var alphaUp = upPixel.Item3 / 255.0;
                    var alphaOut = alphaDown + alphaUp * (1 - alphaDown);
                    byte outB = upPixel.Item0, outG = upPixel.Item1, outR = upPixel.Item2;
                    if (alphaOut > 0)
                    {
                        outB = (byte)Math.Round(LimitB((downPixel.Item0 * alphaDown + upPixel.Item0 * alphaUp * (1 - alphaDown)) / alphaOut));
                        outG = (byte)Math.Round(LimitB((downPixel.Item1 * alphaDown + upPixel.Item1 * alphaUp * (1 - alphaDown)) / alphaOut));
                        outR = (byte)Math.Round(LimitB((downPixel.Item2 * alphaDown + upPixel.Item2 * alphaUp * (1 - alphaDown)) / alphaOut));
                    }

                    result.Set(yyDst, xxDst, new Vec4b(outB, outG, outR, (byte)Math.Round(LimitB(alphaOut * 255.0))));
                }
            }
        }

        foreach (var c in downChannels)
        {
            c.Dispose();
        }

        downScaled.Dispose();
        if (needsResize)
        {
            upFit.Dispose();
        }

        return result;
    }

    public static bool SplitSvgIntoMainAndInnerMasks(string originalSvgPath, out string mainMaskSvgPath, out string? innerMaskSvgPath)
    {
        var svgText = File.ReadAllText(originalSvgPath, Encoding.UTF8);
        const string redPathPattern = @"<path\b[^>]*\bfill\s*=\s*""(?:#ff0000|#FF0000|red|Red|RED)""[^>]*/?>";
        var options = RegexOptions.IgnoreCase | RegexOptions.Singleline;
        var redMatches = Regex.Matches(svgText, redPathPattern, options);
        if (redMatches.Count == 0)
        {
            mainMaskSvgPath = originalSvgPath;
            innerMaskSvgPath = null;
            return false;
        }

        var mainSvgText = Regex.Replace(svgText, redPathPattern, "", options);
        var firstPathIndex = svgText.IndexOf("<path", StringComparison.OrdinalIgnoreCase);
        var header = firstPathIndex >= 0 ? svgText[..firstPathIndex] : svgText;
        var svgEndIndex = svgText.LastIndexOf("</svg>", StringComparison.OrdinalIgnoreCase);
        var footer = svgEndIndex >= 0 ? svgText[svgEndIndex..] : "";
        var sb = new StringBuilder();
        sb.Append(header).AppendLine();
        foreach (Match m in redMatches)
        {
            sb.AppendLine(m.Value);
        }

        sb.AppendLine(footer);
        var tempDir = Path.GetTempPath();
        mainMaskSvgPath = Path.Combine(tempDir, $"mask_main_{Guid.NewGuid():N}.svg");
        innerMaskSvgPath = Path.Combine(tempDir, $"mask_inner_{Guid.NewGuid():N}.svg");
        File.WriteAllText(mainMaskSvgPath, mainSvgText, Encoding.UTF8);
        File.WriteAllText(innerMaskSvgPath, sb.ToString(), Encoding.UTF8);
        return true;
    }

    public static Mat ApplyInnerFillWithSubMask(Mat baseMat, string innerMaskSvgPath)
    {
        if (string.IsNullOrEmpty(innerMaskSvgPath) || baseMat.Empty())
        {
            return baseMat.Clone();
        }

        using var subMaskAlpha = RasterizeMaskAlpha(innerMaskSvgPath, baseMat.Cols, baseMat.Rows);
        if (subMaskAlpha.Empty())
        {
            return baseMat.Clone();
        }

        const int alphaThr = 100;
        var channels = Cv2.Split(baseMat);
        if (channels.Length < 4)
        {
            foreach (var m in channels)
            {
                m.Dispose();
            }

            return baseMat.Clone();
        }

        var width = baseMat.Cols;
        var height = baseMat.Rows;
        long sumB = 0, sumG = 0, sumR = 0, count = 0;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (subMaskAlpha.Get<byte>(y, x) < alphaThr || channels[3].Get<byte>(y, x) < alphaThr)
                {
                    continue;
                }

                sumB += channels[0].Get<byte>(y, x);
                sumG += channels[1].Get<byte>(y, x);
                sumR += channels[2].Get<byte>(y, x);
                count++;
            }
        }

        if (count == 0)
        {
            foreach (var m in channels)
            {
                m.Dispose();
            }

            return baseMat.Clone();
        }

        var avgB = sumB / (double)count;
        var avgG = sumG / (double)count;
        var avgR = sumR / (double)count;
        var result = baseMat.Clone();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var aMask = subMaskAlpha.Get<byte>(y, x);
                if (aMask < alphaThr)
                {
                    continue;
                }

                var vBase = result.Get<Vec4b>(y, x);
                var aF = aMask / 255.0;
                var aB = vBase.Item3 / 255.0;
                var aOut = aF + aB * (1.0 - aF);
                byte outB = vBase.Item0, outG = vBase.Item1, outR = vBase.Item2;
                if (aOut > 0)
                {
                    outB = (byte)Math.Round(LimitB((avgB * aF + vBase.Item0 * aB * (1.0 - aF)) / aOut));
                    outG = (byte)Math.Round(LimitB((avgG * aF + vBase.Item1 * aB * (1.0 - aF)) / aOut));
                    outR = (byte)Math.Round(LimitB((avgR * aF + vBase.Item2 * aB * (1.0 - aF)) / aOut));
                }

                result.Set(y, x, new Vec4b(outB, outG, outR, (byte)Math.Round(LimitB(aOut * 255.0))));
            }
        }

        foreach (var m in channels)
        {
            m.Dispose();
        }

        return result;
    }

    public static void WriteFinalOutputs(Mat combined, ProductColorResolution resolved, Action<string>? log, out string? previewPngPath)
    {
        previewPngPath = null;
        var productBase = ProductImageService.GetProductImageFolder(resolved.ProductId);
        if (productBase is null)
        {
            log?.Invoke($"FinalOutputsスキップ 画像用フォルダ取得失敗: {resolved.ProductFullName}");
            return;
        }

        var finalDir = Path.Combine(productBase, AppPaths.FinalSubFolder);
        Directory.CreateDirectory(finalDir);
        Cv2.ImEncode(".png", combined, out var pngBytes);
        if (pngBytes is null || pngBytes.Length == 0)
        {
            throw new InvalidOperationException("合成画像の PNG 化に失敗しました。");
        }

        using var master = SKBitmap.Decode(pngBytes)
            ?? throw new InvalidOperationException("合成画像の読込に失敗しました。");

        var webPngs = new List<string>();
        foreach (var targetPixel in new[] { 1500, 800, 400, 150, 60 })
        {
            using var sized = ResizeSkBitmap(master, targetPixel, targetPixel);
            if (targetPixel == 1500)
            {
                var svgName = resolved.OutputFileStem + ".svg";
                var svgPath = Path.Combine(finalDir, svgName);
                SaveEmbeddedSvg(sized, svgPath);
                ProductImageService.UpdateOriginalImageFile(resolved.ProductColorCodeId, svgName);
                log?.Invoke($"原画SVG:{svgPath}");
            }
            else
            {
                var pngPath = WebImageService.ProductColorPngPath(resolved.ProductColorCodeId, targetPixel);
                Directory.CreateDirectory(Path.GetDirectoryName(pngPath)!);
                SaveSkPng(sized, pngPath);
                webPngs.Add(pngPath);
                if (targetPixel == 400)
                {
                    previewPngPath = pngPath;
                }
            }
        }

        WebImageService.TryConvertSrcPngsToWebp(webPngs, log);
    }

    public static int ParseKPercent(string maskPath)
    {
        var match = Regex.Match(Path.GetFileName(maskPath), @"_口巻([0-9]{1,3})p", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return 100;
        }

        return Math.Clamp(int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), 1, 999);
    }

    public static void ForceDelete(string path)
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

    private static SKBitmap ResizeSkBitmap(SKBitmap source, int width, int height)
    {
        if (source.Width == width && source.Height == height)
        {
            return source.Copy();
        }

        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
        return source.Resize(info, new SKSamplingOptions(SKCubicResampler.Mitchell))
            ?? throw new InvalidOperationException($"リサイズに失敗しました ({source.Width}x{source.Height} → {width}x{height})");
    }

    private static void SaveSkPng(SKBitmap bitmap, string pngFullPath)
    {
        using var img = SKImage.FromBitmap(bitmap);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(pngFullPath, data.ToArray());
    }

    private static void SaveEmbeddedSvg(SKBitmap master, string svgFullPath)
    {
        var side = master.Width;
        using var img = SKImage.FromBitmap(master);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        var b64 = Convert.ToBase64String(data.ToArray());
        var sb = new StringBuilder();
        sb.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        sb.AppendFormat(
            CultureInfo.InvariantCulture,
            """<svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="{0}px" height="{0}px" viewBox="0 0 {0} {0}">""" + Environment.NewLine,
            side);
        sb.AppendFormat(
            CultureInfo.InvariantCulture,
            """  <image width="{0}" height="{0}" xlink:href="data:image/png;base64,{1}" href="data:image/png;base64,{1}" />""" + Environment.NewLine,
            side,
            b64);
        sb.AppendLine("</svg>");
        Directory.CreateDirectory(Path.GetDirectoryName(svgFullPath)!);
        File.WriteAllText(svgFullPath, sb.ToString(), new UTF8Encoding(false));
    }

    private static Mat RasterizeMaskAlpha(string svgPath, int width, int height)
    {
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var surface = SKSurface.Create(info);
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var svg = new SKSvg();
        svg.Load(svgPath);
        if (svg.Picture is null)
        {
            return new Mat(height, width, MatType.CV_8UC1, Scalar.All(0));
        }

        var cull = svg.Picture.CullRect;
        var scale = Math.Min(width / cull.Width, height / cull.Height);
        canvas.Translate(-cull.Left * scale + (width - cull.Width * scale) / 2f,
            -cull.Top * scale + (height - cull.Height * scale) / 2f);
        canvas.Scale(scale, scale);
        canvas.DrawPicture(svg.Picture);
        canvas.Flush();
        using var image = surface.Snapshot();
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 95);
        using var bgra = Cv2.ImDecode(encoded.ToArray(), ImreadModes.Unchanged);
        var channels = bgra.Split();
        var alpha = channels[3].Clone();
        foreach (var c in channels)
        {
            c.Dispose();
        }

        return alpha;
    }

    private static Mat ToBgra(Mat source)
    {
        if (source.Type() == MatType.CV_8UC4)
        {
            return source.Clone();
        }

        var dest = new Mat();
        if (source.Type() == MatType.CV_8UC3)
        {
            Cv2.CvtColor(source, dest, ColorConversionCodes.BGR2BGRA);
        }
        else if (source.Type() == MatType.CV_8UC1)
        {
            Cv2.CvtColor(source, dest, ColorConversionCodes.GRAY2BGRA);
        }
        else
        {
            source.ConvertTo(dest, MatType.CV_8UC4);
        }

        return dest;
    }

    private static Mat BuildRadialProfile(Mat alpha, double centerX, double centerY, int sampleCount)
    {
        const double radiusStep = 0.5;
        const int alphaLow = 5;
        const int alphaHigh = 220;
        const int maxConsecutiveTransparent = 10;
        var radialProfile = new Mat(1, sampleCount, MatType.CV_32FC1);
        var twoPi = 2.0 * Math.PI;
        var deltaTheta = twoPi / sampleCount;
        var width = alpha.Cols;
        var height = alpha.Rows;
        var maxRadius = Math.Sqrt(width * width + height * height);
        for (var sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
        {
            var angle = sampleIndex * deltaTheta;
            var cosAngle = Math.Cos(angle);
            var sinAngle = Math.Sin(angle);
            var accumLength = 0.0;
            var radius = 0.0;
            var consecutiveTransparent = 0;
            while (radius <= maxRadius)
            {
                var sampleX = centerX + cosAngle * radius;
                var sampleY = centerY + sinAngle * radius;
                if (sampleX < 0 || sampleX >= width - 1 || sampleY < 0 || sampleY >= height - 1)
                {
                    break;
                }

                var rawAlpha = alpha.Get<byte>((int)sampleY, (int)sampleX);
                if (rawAlpha < alphaLow)
                {
                    consecutiveTransparent++;
                    if (consecutiveTransparent >= maxConsecutiveTransparent)
                    {
                        break;
                    }
                }
                else
                {
                    consecutiveTransparent = 0;
                    accumLength += radiusStep * (Math.Min((int)rawAlpha, alphaHigh) / (double)alphaHigh);
                }

                radius += radiusStep;
            }

            radialProfile.Set(0, sampleIndex, (float)accumLength);
        }

        return radialProfile;
    }

    private static double RadiusAtAngle(Mat profile, double theta)
    {
        var twoPi = 2.0 * Math.PI;
        var profileLength = profile.Cols;
        if (profileLength <= 0)
        {
            return 0;
        }

        var continuousIndex = (theta / twoPi) * profileLength;
        continuousIndex -= Math.Floor(continuousIndex / profileLength) * profileLength;
        var index0 = (int)Math.Floor(continuousIndex) % profileLength;
        var index1 = (index0 + 1) % profileLength;
        var t = continuousIndex - Math.Floor(continuousIndex);
        var radius0 = (double)profile.Get<float>(0, index0);
        var radius1 = (double)profile.Get<float>(0, index1);
        return radius0 * (1 - t) + radius1 * t;
    }

    private static int FindMouthTopCenterX(Mat alpha)
    {
        const int topRowCount = 4;
        const double alphaThresh = 3;
        if (alpha.Empty())
        {
            return -1;
        }

        using var opaqueMask = new Mat();
        Cv2.Threshold(alpha, opaqueMask, alphaThresh, 255, ThresholdTypes.Binary);
        var firstY = -1;
        for (var y = 0; y < opaqueMask.Rows; y++)
        {
            using var row = opaqueMask.Row(y);
            if (Cv2.CountNonZero(row) > 0)
            {
                firstY = y;
                break;
            }
        }

        if (firstY < 0)
        {
            return -1;
        }

        var rowCount = Math.Min(topRowCount, opaqueMask.Rows - firstY);
        var topRoi = new Rect(0, firstY, opaqueMask.Cols, rowCount);
        using var topRows = new Mat(opaqueMask, topRoi);
        using var topNonZero = new Mat();
        Cv2.FindNonZero(topRows, topNonZero);
        if (topNonZero.Empty())
        {
            return -1;
        }

        var topRect = Cv2.BoundingRect(topNonZero);
        return topRect.X + topRect.Width / 2;
    }

    private static int FindBottomNonTransparent(Mat image)
    {
        if (image.Empty())
        {
            return -1;
        }

        var channels = image.Split();
        if (channels.Length < 4)
        {
            foreach (var c in channels)
            {
                c.Dispose();
            }

            return image.Rows - 1;
        }

        using var alpha = channels[3];
        for (var y = alpha.Rows - 1; y >= 0; y--)
        {
            using var row = alpha.Row(y);
            if (Cv2.CountNonZero(row) > 0)
            {
                foreach (var c in channels.Take(3))
                {
                    c.Dispose();
                }

                return y;
            }
        }

        foreach (var c in channels.Take(3))
        {
            c.Dispose();
        }

        return -1;
    }

    private static Rect IntersectRect(Rect a, Rect b)
    {
        var x = Math.Max(a.X, b.X);
        var y = Math.Max(a.Y, b.Y);
        var r = Math.Min(a.X + a.Width, b.X + b.Width);
        var bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);
        var width = r - x;
        var height = bottom - y;
        return width <= 0 || height <= 0 ? new Rect(0, 0, 0, 0) : new Rect(x, y, width, height);
    }

    private static int ParseSvgLengthPixels(XAttribute? attr)
    {
        if (attr is null)
        {
            return 0;
        }

        var match = Regex.Match(attr.Value, @"\d+");
        return match.Success && int.TryParse(match.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
            ? v
            : 0;
    }

    private static double LimitB(double source) => Math.Clamp(source, 0, 255);
}
