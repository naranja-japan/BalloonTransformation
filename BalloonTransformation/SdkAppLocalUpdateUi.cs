using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Naranja.Platform.Common.Deploy;

namespace Naranja.BalloonTransformation;

internal sealed class SdkAppLocalUpdateUi
{
    private readonly string _appName;
    private readonly InfoBar _bar;
    private readonly TextBlock _message;
    private readonly Button _later;
    private readonly Button _update;
    private readonly Action<string> _onError;
    private SdkAppUpdateCheckResult? _pending;

    public SdkAppLocalUpdateUi(
        string appName,
        InfoBar bar,
        TextBlock message,
        Button later,
        Button update,
        Action<string> onError,
        Action? onLater = null)
    {
        _appName = appName;
        _bar = bar;
        _message = message;
        _later = later;
        _update = update;
        _onError = onError;
        later.Click += (_, _) =>
        {
            _bar.IsOpen = false;
            onLater?.Invoke();
        };
        update.Click += Update_Click;
    }

    public bool Check()
    {
        try
        {
            var result = SdkAppUpdateChecker.Check(_appName);
            _pending = result.UpdateAvailable ? result : null;
            if (!result.UpdateAvailable)
            {
                _bar.IsOpen = false;
                return false;
            }

            _bar.Title = string.Empty;
            _bar.Message = string.Empty;
            _message.Text =
                $"新しい版 {result.DeployJsonVersionText} があります（実行中 {result.RunningVersionText}）。完了後はスタートメニューから起動してください。";
            _bar.IsOpen = true;
            return true;
        }
        catch (Exception ex)
        {
            _pending = null;
            _bar.IsOpen = false;
            _onError($"版チェックに失敗しました: {ex.Message}");
            return false;
        }
    }

    private void Update_Click(object sender, RoutedEventArgs e)
    {
        var pending = _pending;
        if (pending is null || !pending.UpdateAvailable || pending.DeployInfo is null)
        {
            _bar.IsOpen = false;
            return;
        }

        _update.IsEnabled = false;
        _later.IsEnabled = false;

        if (!SdkAppUpdateRestarter.TryStart(
                _appName,
                pending.ExePath,
                pending.DeployInfo.Version,
                out var error))
        {
            _update.IsEnabled = true;
            _later.IsEnabled = true;
            _onError(error ?? "同期プロセスを起動できませんでした。");
            return;
        }

        Environment.Exit(0);
    }
}
