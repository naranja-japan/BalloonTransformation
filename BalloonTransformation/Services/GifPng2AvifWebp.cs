using System.Diagnostics;

namespace Naranja.BalloonTransformation.Services;

internal static class GifPng2AvifWebp
{
    public static void Convert(IReadOnlyList<string> filesFullPath, Action<string>? log)
    {
        if (filesFullPath.Count == 0)
        {
            return;
        }

        var common = LongestCommonDirectory(filesFullPath);
        var nodeSharpFolder = @"C:\Users\Public\NARANJA\node\sharp";
        var srcFolder = Path.Combine(nodeSharpFolder, "src");
        var destFolder = Path.Combine(nodeSharpFolder, "dest");
        var batFile = Path.Combine(nodeSharpFolder, "_webp-converter-060.bat");
        if (!File.Exists(batFile))
        {
            throw new FileNotFoundException("webp converter bat missing", batFile);
        }

        Directory.CreateDirectory(destFolder);
        ClearDirectory(srcFolder);
        ClearDirectory(destFolder);

        foreach (var file in filesFullPath)
        {
            var rel = file[common.Length..].TrimStart('\\', '/');
            var copyTo = Path.Combine(srcFolder, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(copyTo)!);
            File.Copy(file, copyTo, true);
        }

        using var p = Process.Start(new ProcessStartInfo(batFile) { UseShellExecute = true });
        if (p is null || !p.WaitForExit(180_000))
        {
            throw new TimeoutException("png → webp timeout");
        }

        foreach (var file in filesFullPath)
        {
            if (!file.StartsWith(AppPaths.WebImgDbSrc, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var rel = file[common.Length..].TrimStart('\\', '/');
            var fromDir = Path.GetDirectoryName(Path.Combine(destFolder, rel))!;
            var stem = Path.GetFileNameWithoutExtension(file);
            var targetDir = Path.GetDirectoryName(file)!.Replace(
                AppPaths.WebImgDbSrc,
                AppPaths.WebImgDbServer,
                StringComparison.OrdinalIgnoreCase);
            Directory.CreateDirectory(targetDir);
            var from = Path.Combine(fromDir, stem + ".webp");
            var to = Path.Combine(targetDir, stem + ".webp");
            if (File.Exists(from))
            {
                File.Copy(from, to, true);
            }
            else
            {
                log?.Invoke("webp 出力なし: " + stem);
            }
        }
    }

    private static string LongestCommonDirectory(IReadOnlyList<string> files)
    {
        var parts = Path.GetDirectoryName(files[0])!.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToList();
        foreach (var file in files.Skip(1))
        {
            var cur = Path.GetDirectoryName(file)!.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var n = Math.Min(parts.Count, cur.Length);
            var i = 0;
            while (i < n && string.Equals(parts[i], cur[i], StringComparison.OrdinalIgnoreCase))
            {
                i++;
            }

            parts.RemoveRange(i, parts.Count - i);
        }

        return string.Join(Path.DirectorySeparatorChar, parts) + Path.DirectorySeparatorChar;
    }

    private static void ClearDirectory(string directoryFullPath)
    {
        if (!Directory.Exists(directoryFullPath))
        {
            Directory.CreateDirectory(directoryFullPath);
            return;
        }

        foreach (var f in Directory.GetFiles(directoryFullPath))
        {
            File.Delete(f);
        }

        foreach (var d in Directory.GetDirectories(directoryFullPath))
        {
            Directory.Delete(d, true);
        }
    }
}
