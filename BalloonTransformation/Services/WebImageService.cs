namespace Naranja.BalloonTransformation.Services;

public static class WebImageService
{
    public static string ProductColorPngPath(int productColorCodeId, int widthPx)
    {
        return string.Format(
            @"{0}\product-color-code\id{1:0000}000\id{2:0000000}-{3}.png",
            AppPaths.WebImgDbSrc,
            productColorCodeId / 1000,
            productColorCodeId,
            widthPx);
    }

    /// <summary>
    /// db-src の PNG を node/sharp バッチで webp 化して db へ書く。バッチが無ければ何もしない。
    /// </summary>
    public static void TryConvertSrcPngsToWebp(IReadOnlyList<string> srcPngs, Action<string>? log = null)
    {
        if (srcPngs.Count == 0)
        {
            return;
        }

        var bat = @"C:\Users\Public\NARANJA\node\sharp\_webp-converter-060.bat";
        if (!File.Exists(bat))
        {
            log?.Invoke("webp 変換バッチが無いため PNG のみ出力");
            return;
        }

        try
        {
            GifPng2AvifWebp.Convert(srcPngs, log);
        }
        catch (Exception ex)
        {
            log?.Invoke("webp 変換失敗: " + ex.Message);
        }
    }
}
