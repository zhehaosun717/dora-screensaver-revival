using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using DoraSaver.Core;
using Microsoft.Win32;

namespace DoraSaver.Setup.Engine;

/// <summary>Everything that touches Windows itself: files in System32, the registry, running savers.</summary>
internal static class SystemIntegration
{
    public const string ProductName = "DoraSaver";
    public const string UninstallKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\DoraSaverRevival";
    public const string RepositoryUrl = "https://github.com/zhehaosun717/dora-screensaver-revival";

    private const string DesktopKey = @"Control Panel\Desktop";
    private const string ActiveSaverValue = "SCRNSAVE.EXE";
    private const string WebView2ClientId = "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";

    public static string InstallFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "DoraSaver");

    public static string DataFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DoraSaver");

    public static string System32 => Environment.SystemDirectory;

    public static bool IsOurScreensaver(string path)
    {
        try
        {
            return File.Exists(path) && FileVersionInfo.GetVersionInfo(path).ProductName == ProductName;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static IReadOnlyList<string> FindInstalledScreensavers() =>
        Directory.EnumerateFiles(System32, "*.scr").Where(IsOurScreensaver).ToList();

    /// <summary>A running saver or dialog preview keeps its files locked.</summary>
    public static int StopRunningScreensavers()
    {
        int stopped = 0;
        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                string? path = TryGetPath(process);
                if (path is null || !path.EndsWith(".scr", StringComparison.OrdinalIgnoreCase) || !IsOurScreensaver(path))
                {
                    continue;
                }

                try
                {
                    process.Kill();
                    process.WaitForExit(5000);
                    stopped++;
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // Already exited, or not ours to stop; the file copy will report a real problem.
                }
            }
        }

        return stopped;
    }

    /// <summary>
    /// The original installers wrote Shift-JIS file names through a Chinese (GBK) code page, e.g.
    /// "僪儔偊傕傫抋惗擔婰擮SS.scr". Recreate that mangling to find them.
    /// </summary>
    public static string MojibakeName(string japanese) =>
        Encoding.GetEncoding(936).GetString(Encoding.GetEncoding(932).GetBytes(japanese));

    /// <summary>
    /// Moves the broken Flash-based originals of the savers that were just installed out of SysWOW64
    /// (never deletes them). Problems with one item are reported and do not stop the others.
    /// </summary>
    public static IReadOnlyList<string> BackUpBrokenOriginals(IEnumerable<SaverInfo> installed, Action<string> warn)
    {
        string wow64 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64");
        string backup = Path.Combine(DataFolder, "original-backup");
        var moved = new List<string>();
        foreach (SaverInfo saver in installed)
        {
            string old = MojibakeName(saver.JapaneseName);
            foreach (string item in new[] { old + ".scr", old + " dir" })
            {
                string source = Path.Combine(wow64, item);
                string target = Path.Combine(backup, item);
                if (!File.Exists(source) && !Directory.Exists(source))
                {
                    continue;
                }

                try
                {
                    Directory.CreateDirectory(backup);
                    if (File.Exists(source))
                    {
                        File.Move(source, UniquePath(target));
                    }
                    else
                    {
                        Directory.Move(source, UniquePath(target));
                    }

                    moved.Add(item);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    warn($"{item}: {ex.Message}");
                }
            }
        }

        return moved;
    }

    /// <summary>
    /// Keeps the user's chosen screensaver working when its file was replaced: the old garbled
    /// original or one of our savers under a name that is no longer installed maps to the new file.
    /// </summary>
    public static string? RepointActiveScreensaver(IReadOnlyDictionary<string, string> newPathById)
    {
        using RegistryKey? desktop = OpenUserDesktopKey();
        if (desktop?.GetValue(ActiveSaverValue) is not string current || current.Length == 0)
        {
            return null;
        }

        string name = Path.GetFileNameWithoutExtension(current);
        SaverInfo? saver = SaverCatalog.FindByFileName(name)
            ?? SaverCatalog.All.FirstOrDefault(s => MojibakeName(s.JapaneseName) == name);
        if (saver is null || File.Exists(current) && !IsStale(current))
        {
            return null;
        }

        if (!newPathById.TryGetValue(saver.Id, out string? replacement))
        {
            return null;
        }

        desktop.SetValue(ActiveSaverValue, replacement, RegistryValueKind.String);
        return replacement;
    }

    public static bool IsWebView2Installed()
    {
        string[] keys =
        [
            $@"SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{WebView2ClientId}",
            $@"SOFTWARE\Microsoft\EdgeUpdate\Clients\{WebView2ClientId}",
        ];
        bool machine = keys.Any(k => HasVersion(Registry.LocalMachine, k));
        return machine || HasVersion(Registry.CurrentUser, $@"Software\Microsoft\EdgeUpdate\Clients\{WebView2ClientId}");
    }

    public static void WriteUninstallEntry(string setupExe, string version, long sizeKb)
    {
        using RegistryKey key = Registry.LocalMachine.CreateSubKey(UninstallKeyPath, writable: true);
        key.SetValue("DisplayName", "Doraemon Screensaver Revival");
        key.SetValue("DisplayVersion", version);
        key.SetValue("Publisher", "dora-screensaver-revival");
        key.SetValue("DisplayIcon", setupExe);
        key.SetValue("InstallLocation", InstallFolder);
        key.SetValue("UninstallString", $"\"{setupExe}\" /uninstall");
        key.SetValue("QuietUninstallString", $"\"{setupExe}\" /uninstall /quiet");
        key.SetValue("URLInfoAbout", RepositoryUrl);
        key.SetValue("EstimatedSize", (int)Math.Min(int.MaxValue, sizeKb), RegistryValueKind.DWord);
        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
    }

    public static bool IsInstalled()
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(UninstallKeyPath);
        return key is not null || FindInstalledScreensavers().Count > 0;
    }

    public static void RemoveUninstallEntry() => Registry.LocalMachine.DeleteSubKeyTree(UninstallKeyPath, throwOnMissingSubKey: false);

    /// <summary>Clears the active screensaver if it points at a file that no longer exists.</summary>
    public static void ClearActiveScreensaverIfMissing()
    {
        using RegistryKey? desktop = OpenUserDesktopKey();
        if (desktop?.GetValue(ActiveSaverValue) is string current && current.Length > 0 && !File.Exists(current))
        {
            desktop.SetValue(ActiveSaverValue, string.Empty, RegistryValueKind.String);
        }
    }

    /// <summary>
    /// Opens Screen Saver Settings as the signed-in user, not elevated like this setup: the desktop
    /// (Explorer) runs it. Otherwise its previews would run as administrator too.
    /// </summary>
    public static void OpenScreenSaverSettings()
    {
        string control = Path.Combine(System32, "control.exe");
        const string arguments = "desk.cpl,,@screensaver";
        try
        {
            dynamic shellWindows = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39"))!)!;
            object location = 0;
            object root = 0;
            const int desktop = 8; // SWC_DESKTOP
            const int needDispatch = 1; // SWFO_NEEDDISPATCH
            dynamic browser = shellWindows.FindWindowSW(ref location, ref root, desktop, out int _, needDispatch);
            dynamic shell = browser.Document.Application;
            shell.ShellExecute(control, arguments, System32, "open", 1);
        }
        catch (Exception ex) when (ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or InvalidCastException or NullReferenceException)
        {
            // No desktop to ask (e.g. a remote session): open it directly.
            Process.Start(new ProcessStartInfo(control, arguments) { UseShellExecute = false, WorkingDirectory = System32 });
        }
    }

    /// <summary>
    /// The screensaver choice of the person at the desk. When a standard user elevates with an
    /// administrator's password, HKCU is the administrator's hive, so use the owner of Explorer in
    /// this session instead, and fall back to HKCU (e.g. over SSH, where there is no Explorer).
    /// </summary>
    private static RegistryKey? OpenUserDesktopKey()
    {
        string? sid = InteractiveUserSid();
        string? current = WindowsIdentity.GetCurrent().User?.Value;
        if (sid is not null && sid != current)
        {
            RegistryKey? key = Registry.Users.OpenSubKey(sid + "\\" + DesktopKey, writable: true);
            if (key is not null)
            {
                return key;
            }
        }

        return Registry.CurrentUser.OpenSubKey(DesktopKey, writable: true);
    }

    private static string? InteractiveUserSid()
    {
        int session = Process.GetCurrentProcess().SessionId;
        foreach (Process explorer in Process.GetProcessesByName("explorer"))
        {
            using (explorer)
            {
                if (explorer.SessionId != session)
                {
                    continue;
                }

                IntPtr token = IntPtr.Zero;
                try
                {
                    if (OpenProcessToken(explorer.Handle, TokenQuery | TokenDuplicate, out token))
                    {
                        using var identity = new WindowsIdentity(token);
                        return identity.User?.Value;
                    }
                }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or UnauthorizedAccessException)
                {
                    // Not allowed to look at this Explorer; fall back to HKCU.
                }
                finally
                {
                    if (token != IntPtr.Zero)
                    {
                        CloseHandle(token);
                    }
                }
            }
        }

        return null;
    }

    private const uint TokenQuery = 0x0008;
    private const uint TokenDuplicate = 0x0002;

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);

    private static bool IsStale(string path) => IsOurScreensaver(path) is false && path.IndexOf("SysWOW64", StringComparison.OrdinalIgnoreCase) >= 0;

    private static bool HasVersion(RegistryKey root, string path)
    {
        using RegistryKey? key = root.OpenSubKey(path);
        return key?.GetValue("pv") is string pv && pv.Length > 0 && pv != "0.0.0.0";
    }

    private static string? TryGetPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    private static string UniquePath(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return path;
        }

        return path + "." + DateTime.Now.ToString("yyyyMMddHHmmss");
    }
}
