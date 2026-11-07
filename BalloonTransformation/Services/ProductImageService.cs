namespace Naranja.BalloonTransformation.Services;

public sealed record ProductColorResolution(
    int ProductColorCodeId,
    int ProductId,
    short BrandId,
    string OutputFileStem,
    string ProductFullName,
    string StatusText,
    string? ExistingOriginalPath);

public static class ProductImageService
{
    private static readonly ProductImageApiClient Api = new();

    public static ProductColorResolution? ResolveByFullName(string maskPath, string inputPath)
    {
        var stem = $"_{Path.GetFileNameWithoutExtension(maskPath)}_{Path.GetFileNameWithoutExtension(inputPath)}_";
        stem = System.Text.RegularExpressions.Regex.Replace(stem, @"_[^_]+原画_", "_");
        stem = System.Text.RegularExpressions.Regex.Replace(stem, @"_口巻[0-9]+p_", "_");
        stem = System.Text.RegularExpressions.Regex.Replace(stem, "_+", "_").Trim('_');
        var fullName = stem.Replace('_', ' ').Trim();
        if (string.IsNullOrEmpty(fullName))
        {
            return null;
        }

        var hit = Api.GetByFullName(fullName);
        if (hit is null)
        {
            return null;
        }

        var status = hit.IsDiscontinued
            ? "(廃盤)"
            : hit.IsAvailable ? "〇" : "(現在選択不可)";

        string? existing = null;
        var folder = GetProductImageFolder(hit.ProductId, hit.BrandId);
        if (!string.IsNullOrEmpty(hit.OriginalImageFile) && folder is not null)
        {
            var candidate = Path.Combine(folder, AppPaths.FinalSubFolder, hit.OriginalImageFile);
            if (File.Exists(candidate))
            {
                existing = candidate;
            }
        }

        return new ProductColorResolution(
            hit.ProductColorCodeId,
            hit.ProductId,
            hit.BrandId,
            stem,
            fullName,
            status,
            existing);
    }

    public static string? GetProductImageFolder(int productId, short brandId, bool create = true)
    {
        if (productId <= 0)
        {
            return null;
        }

        var brandFolder = brandId switch
        {
            8 => "4100_juggling",
            9 => "4200_balloon",
            10 => "4300_bar",
            _ => "4900_company"
        };

        var basePath = string.Format(
            @"{0}\naranja\4000_商品画像\{1}\ID{2:000}0000-\ID{3:0000000}",
            AppPaths.DfsRoot,
            brandFolder,
            productId / 10000,
            productId);

        if (create)
        {
            Directory.CreateDirectory(basePath);
            foreach (var sub in new[]
            {
                "01_写真", @"01_写真\history",
                "02_編集", @"02_編集\history",
                AppPaths.FinalSubFolder, "05_Web掲載リソース", "98_その他", "99_過去データ"
            })
            {
                Directory.CreateDirectory(Path.Combine(basePath, sub));
            }
        }

        return basePath;
    }

    public static void UpdateOriginalImageFile(int productColorCodeId, string svgFileName)
        => Api.UpdateOriginalImageFile(productColorCodeId, svgFileName);
}
