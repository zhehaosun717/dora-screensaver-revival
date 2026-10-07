using DoraSaver.Core;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace DoraSaver.UI;

/// <summary>
/// A WebView2 control that plays one SWF through the bundled Ruffle build. Only pages from the
/// local asset folder are allowed; everything else (navigation, popups, downloads) is blocked.
/// </summary>
internal sealed class PlayerView : WebView2
{
    // Every process must pass identical options, or a second instance sharing the user data
    // folder (e.g. the dialog's preview plus a full-screen test) fails to start.
    private const string BrowserArguments = "--autoplay-policy=no-user-gesture-required";

    private const int MaxRenderRecoveries = 3;

    // SetLoaderDllFolderPath throws once any other WebView2 API has run in the process, so all players
    // (one per monitor) share one environment. Only touched on the UI thread, so no locking.
    private static Task<CoreWebView2Environment>? _sharedEnvironment;

    private int _renderRecoveries;

    public PlayerView()
    {
        DefaultBackgroundColor = Color.Black;
        BackColor = Color.Black;
        Dock = DockStyle.Fill;
    }

    /// <summary>The page saw a key press or click (backup for the global input hooks).</summary>
    public event EventHandler<string>? WakeRequested;

    public async Task StartAsync(string assetFolder, SaverInfo saver, LayoutMode layout, bool muted)
    {
        CoreWebView2Environment environment = await SharedEnvironmentAsync(assetFolder);
        await EnsureCoreWebView2Async(environment);
        CoreWebView2 core = CoreWebView2;

        CoreWebView2Settings settings = core.Settings;
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreDevToolsEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.IsZoomControlEnabled = false;
        settings.IsPinchZoomEnabled = false;
        settings.IsSwipeNavigationEnabled = false;
        settings.IsGeneralAutofillEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        settings.AreHostObjectsAllowed = false;

        core.IsMuted = muted;
        core.SetVirtualHostNameToFolderMapping(
            PlayerPaths.VirtualHost, assetFolder, CoreWebView2HostResourceAccessKind.DenyCors);

        core.NavigationStarting += (_, e) =>
        {
            if (!PlayerPaths.IsPlayerUri(e.Uri))
            {
                e.Cancel = true;
                Log.Error($"Blocked navigation to {e.Uri}");
            }
        };
        core.NewWindowRequested += (_, e) => e.Handled = true;
        core.DownloadStarting += (_, e) => e.Cancel = true;
        core.WebMessageReceived += (_, e) => OnPageMessage(e.TryGetWebMessageAsString());
        core.ProcessFailed += (_, e) => OnProcessFailed(e);

        Uri uri = PlayerPaths.BuildPlayerUri(saver, layout);
        Log.Info($"Playing {uri} (muted={muted})");
        core.Navigate(uri.ToString());
    }

    // No retry on failure: the loader path can only be set once, so a second attempt would just throw.
    private static Task<CoreWebView2Environment> SharedEnvironmentAsync(string assetFolder) =>
        _sharedEnvironment ??= CreateEnvironmentAsync(assetFolder);

    private static async Task<CoreWebView2Environment> CreateEnvironmentAsync(string assetFolder)
    {
        CoreWebView2Environment.SetLoaderDllFolderPath(DependencyResolver.LoaderFolder(assetFolder));
        var options = new CoreWebView2EnvironmentOptions(BrowserArguments);
        return await CoreWebView2Environment.CreateAsync(
            browserExecutableFolder: null,
            userDataFolder: PlayerPaths.WebViewUserDataFolder,
            options: options);
    }

    private void OnPageMessage(string? message)
    {
        if (message is null)
        {
            return;
        }

        if (message.StartsWith("[wake]", StringComparison.Ordinal))
        {
            WakeRequested?.Invoke(this, message);
        }
        else if (message.StartsWith("[error]", StringComparison.Ordinal))
        {
            Log.Error($"Player page: {message}");
        }
    }

    /// <summary>
    /// A crashed renderer would otherwise leave WebView2's grey "something went wrong" page on screen.
    /// Reload a few times, then fall back to the black host window.
    /// </summary>
    private void OnProcessFailed(CoreWebView2ProcessFailedEventArgs e)
    {
        Log.Error($"WebView2 process failed: {e.ProcessFailedKind} ({e.Reason})");
        bool rendererFailure = e.ProcessFailedKind is CoreWebView2ProcessFailedKind.RenderProcessExited
            or CoreWebView2ProcessFailedKind.RenderProcessUnresponsive
            or CoreWebView2ProcessFailedKind.FrameRenderProcessExited;

        if (rendererFailure && _renderRecoveries < MaxRenderRecoveries && CoreWebView2 is not null)
        {
            _renderRecoveries++;
            CoreWebView2.Reload();
            return;
        }

        if (rendererFailure || e.ProcessFailedKind == CoreWebView2ProcessFailedKind.BrowserProcessExited)
        {
            Visible = false;
        }
    }
}
