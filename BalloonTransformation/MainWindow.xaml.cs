using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Naranja.BalloonTransformation.Services;
using Windows.Storage.Streams;

namespace Naranja.BalloonTransformation;

public sealed partial class MainWindow : Window
{
    private CancellationTokenSource? _cts;

    public MainWindow()
    {
        InitializeComponent();
        AppPaths.EnsureWorkFolders();
        InputPathLink.Content = "入力: " + AppPaths.InputDir;
        InputPathLink.Tag = AppPaths.InputDir;
        MaskPathLink.Content = "マスク: " + AppPaths.MaskDir;
        MaskPathLink.Tag = AppPaths.MaskDir;
        OutputPathLink.Content = "出力: " + AppPaths.OutputDir;
        OutputPathLink.Tag = AppPaths.OutputDir;
        ArchivePathLink.Content = "保管: " + AppPaths.MaskArchiveDir;
        ArchivePathLink.Tag = AppPaths.MaskArchiveDir;
        RefreshUi();
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e) => RefreshUi();

    private void PathLink_Click(object sender, RoutedEventArgs e)
    {
        if (sender is HyperlinkButton { Tag: string path })
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
        }
    }

    private void RefreshUi()
    {
        var inputs = BalloonPipelineService.ListInputFiles();
        var masks = BalloonPipelineService.ListMaskFiles();
        CountsLabel.Text = $"入力ファイル: {inputs.Count} / マスク {masks.Count}";
        MaskGrid.Children.Clear();
        MaskGrid.RowDefinitions.Clear();
        MaskGrid.ColumnDefinitions.Clear();
        for (var i = 0; i < 3; i++)
        {
            MaskGrid.RowDefinitions.Add(new RowDefinition());
            MaskGrid.ColumnDefinitions.Add(new ColumnDefinition());
        }

        var index = 0;
        foreach (var mask in masks.Take(9))
        {
            var panel = new StackPanel { Margin = new Thickness(4) };
            var image = new Image { Width = 140, Height = 140, Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform };
            try
            {
                using var bmp = ImageProcessingService.RasterizeSvgPreview(mask, 140, 140);
                image.Source = ToBitmapImage(EncodePng(bmp));
            }
            catch
            {
                // preview optional
            }

            panel.Children.Add(new Border
            {
                BorderThickness = new Thickness(1),
                Child = image
            });
            panel.Children.Add(new TextBlock
            {
                Text = $"口巻倍率: {ImageProcessingService.ParseKPercent(mask)}%",
                HorizontalAlignment = HorizontalAlignment.Center
            });
            Grid.SetRow(panel, index / 3);
            Grid.SetColumn(panel, index % 3);
            MaskGrid.Children.Add(panel);
            index++;
        }
    }

    private async void RunButton_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        RunButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        var token = _cts.Token;
        var progress = new Progress<PipelineProgress>(p =>
        {
            if (p.Total > 0)
            {
                MainProgress.Maximum = p.Total;
                MainProgress.Value = Math.Clamp(p.Processed, 0, p.Total);
                ProgressLabel.Text = $"進捗: {p.Processed}/{p.Total}";
            }

            if (!string.IsNullOrEmpty(p.Message))
            {
                LogBox.Text += DateTime.Now.ToString("HH:mm:ss ") + p.Message + Environment.NewLine;
            }

            if (!string.IsNullOrEmpty(p.InputPath) && File.Exists(p.InputPath))
            {
                TrySetPreview(InputPreview, p.InputPath);
            }

            if (!string.IsNullOrEmpty(p.PreviewPngPath) && File.Exists(p.PreviewPngPath))
            {
                TrySetPreview(OutputPreview, p.PreviewPngPath);
            }
        });

        try
        {
            await Task.Run(() => BalloonPipelineService.Run(token, progress), token);
        }
        catch (OperationCanceledException)
        {
            LogBox.Text += "キャンセルしました。" + Environment.NewLine;
        }
        catch (Exception ex)
        {
            LogBox.Text += "失敗: " + ex.Message + Environment.NewLine;
        }
        finally
        {
            RunButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private static void TrySetPreview(Image target, string path)
    {
        try
        {
            if (path.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
            {
                using var bmp = ImageProcessingService.RasterizeSvgPreview(path, 300, 300);
                target.Source = ToBitmapImage(EncodePng(bmp));
                return;
            }

            target.Source = ToBitmapImage(File.ReadAllBytes(path));
        }
        catch
        {
            // preview optional
        }
    }

    private static byte[] EncodePng(SkiaSharp.SKBitmap bmp)
    {
        using var img = SkiaSharp.SKImage.FromBitmap(bmp);
        using var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    private static BitmapImage ToBitmapImage(byte[] bytes)
    {
        var image = new BitmapImage();
        using var stream = new InMemoryRandomAccessStream();
        using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
        {
            writer.WriteBytes(bytes);
            writer.StoreAsync().AsTask().GetAwaiter().GetResult();
        }

        stream.Seek(0);
        image.SetSource(stream);
        return image;
    }
}
