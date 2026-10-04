using DoraSaver.Core;
using DoraSaver.Native;
using DoraSaver.UI;

namespace DoraSaver;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        // The .scr lives in System32; the WebView2 assemblies live in the install folder.
        AppDomain.CurrentDomain.AssemblyResolve += (_, e) => DependencyResolver.Resolve(e.Name);
        return Run(args);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int Run(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Log.Error("Unhandled UI exception", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Log.Error("Unhandled exception", e.ExceptionObject as Exception);

        ScreenSaverArgs parsed = ScreenSaverArgs.Parse(args);
        SaverInfo saver = SaverCatalog.Resolve(typeof(Program).Assembly, Application.ExecutablePath);
        var store = new RegistrySettingsStore();
        string? assets = PlayerPaths.FindAssetFolder(
            Path.GetDirectoryName(Application.ExecutablePath) ?? ".",
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));

        try
        {
            return parsed.Mode switch
            {
                SaverMode.Show => RunFullScreen(saver, store.Load(saver), assets),
                SaverMode.Preview => RunPreview(parsed.WindowHandle, saver, store.Load(saver), assets),
                SaverMode.Configure => RunSettings(parsed.WindowHandle, saver, store, assets),
                _ => 0,
            };
        }
        catch (Exception ex)
        {
            Log.Error($"Fatal error in mode {parsed.Mode}", ex);
            return 1;
        }
    }

    private static int RunFullScreen(SaverInfo saver, SaverSettings settings, string? assets)
    {
        using var singleInstance = new Mutex(initiallyOwned: true, @"Local\DoraSaver.FullScreen", out bool isFirst);
        if (!isFirst)
        {
            return 0;
        }

        if (assets is null)
        {
            Log.Error("Asset folder not found; showing a blank screen");
        }

        Screen primary = Screen.PrimaryScreen ?? Screen.AllScreens[0];
        FullScreenForm main = new(primary, hostsPlayer: assets is not null);
        List<FullScreenForm> others = Screen.AllScreens
            .Where(s => s.DeviceName != primary.DeviceName)
            .Select(s => new FullScreenForm(s, hostsPlayer: assets is not null && settings.AllMonitors))
            .ToList();
        List<FullScreenForm> forms = [main, .. others];

        foreach (FullScreenForm form in forms)
        {
            form.FormClosed += (_, _) => Application.Exit();
            form.Show();
        }

        main.Activate();

        // Hooks go in right before the message loop starts pumping, so Windows never sees them time out.
        using var input = new InputMonitor();
        input.Wake += (_, _) => Application.Exit();
        foreach (FullScreenForm form in forms)
        {
            form.WakeRequested += (_, reason) => input.RequestWake(reason);
        }

        if (assets is not null)
        {
            // Queued so it starts once the message loop is running.
            main.BeginInvoke((Action)(() =>
            {
                _ = main.StartPlayerAsync(assets, saver, settings, withSound: true);
                foreach (FullScreenForm other in others)
                {
                    _ = other.StartPlayerAsync(assets, saver, settings, withSound: false);
                }
            }));
        }

        Application.Run();
        return 0;
    }

    private static int RunPreview(IntPtr parent, SaverInfo saver, SaverSettings settings, string? assets)
    {
        if (assets is null)
        {
            Log.Error("Asset folder not found; skipping preview");
            return 0;
        }

        using var form = new PreviewForm(parent);
        form.Load += (_, _) =>
        {
            form.AttachToParent();
            _ = form.StartPlayerAsync(assets, saver, settings);
        };
        Application.Run(form);
        return 0;
    }

    private static int RunSettings(IntPtr owner, SaverInfo saver, ISettingsStore store, string? assets)
    {
        string? fileName = Path.GetFileNameWithoutExtension(Application.ExecutablePath);
        UiText text = UiText.For(UiText.Detect(fileName, System.Globalization.CultureInfo.CurrentUICulture));
        string title = fileName is { Length: > 0 } && SaverCatalog.FindByFileName(fileName) is not null
            ? fileName
            : text.Language == UiLanguage.Japanese ? saver.JapaneseName : saver.ChineseName;
        string installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "DoraSaver");
        string? warning = assets is null ? string.Format(text.MissingAssetsFormat, installDir) : null;

        using var form = new SettingsForm(title, saver, store.Load(saver), warning, text);
        DialogResult result = owner != IntPtr.Zero
            ? form.ShowDialog(new Win32Owner(owner))
            : form.ShowDialog();

        if (result == DialogResult.OK)
        {
            try
            {
                store.Save(saver, form.Result);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
            {
                Log.Error("Saving settings failed", ex);
                MessageBox.Show(string.Format(text.SaveFailedFormat, ex.Message), title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        return 0;
    }

    private sealed class Win32Owner(IntPtr handle) : IWin32Window
    {
        public IntPtr Handle { get; } = handle;
    }
}
