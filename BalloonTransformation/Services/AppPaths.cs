namespace Naranja.BalloonTransformation.Services;

public static class AppPaths
{
    public const string DfsRoot = @"\\naranja.local\dfs02";
    public const string InputDir = @"C:\Users\Public\NARANJA\local\BalloonTransformation\input";
    public const string MaskDir = @"C:\Users\Public\NARANJA\local\BalloonTransformation\mask";
    public const string OutputDir = @"C:\Users\Public\NARANJA\local\BalloonTransformation\output";
    public const string MaskArchiveDir = @"N:\naranja\9000_NDC\Data\Images\LatexBalloonMaskArchive";

    public const string WebImgDbSrc =
        @"\\naranja.local\dfs02\naranja\2000_Web\naranja-web-2021\backend\public\img\db-src";
    public const string WebImgDbServer =
        @"\\naranja.local\dfs02\naranja\2000_Web\naranja-web-2021\backend\public\img\db";

    public const string FinalSubFolder = "03_最終";
    public const int WorkingZoomPixel = 3000;
    public static readonly int[] WebPictureWidths = [800, 400, 150, 60];

    public static void EnsureWorkFolders()
    {
        Directory.CreateDirectory(InputDir);
        Directory.CreateDirectory(MaskDir);
        Directory.CreateDirectory(OutputDir);
        Directory.CreateDirectory(MaskArchiveDir);
    }
}
