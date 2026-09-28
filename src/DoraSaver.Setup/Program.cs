using System.Globalization;
using System.Runtime.InteropServices;
using DoraSaver.Core;
using DoraSaver.Setup.Engine;

namespace DoraSaver.Setup;

/// <summary>
/// DoraSaverSetup.exe                       window
/// DoraSaverSetup.exe /install [/quiet] [/names=zh,ja,en] [/savers=id,id] [/lang=zh|ja|en]
/// DoraSaverSetup.exe /uninstall [/quiet]
/// Exit codes: 0 success, 1 partly installed, 2 failed.
/// </summary>
internal static class Program
{
    private const string RelaunchedFlag = "/relaunched";

    [STAThread]
    private static int Main(string[] args)
    {
        // Started from Downloads with admin rights: never load DLLs from, or resolve tools in, that folder.
        SetDefaultDllDirectories(LoadLibrarySearchSystem32 | LoadLibrarySearchApplicationDir);
        Directory.SetCurrentDirectory(Environment.SystemDirectory);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        CommandLine command = CommandLine.Parse(args);
        UiLanguage language = command.Language ?? SetupText.Detect(CultureInfo.CurrentUICulture);
        bool relaunched = args.Contains(RelaunchedFlag, StringComparer.OrdinalIgnoreCase);
        if (!relaunched && IsRunningFromInstallFolder())
        {
            if (command.Quiet)
            {
                // Let the copy write to the same console as this process.
                AttachConsole(-1);
            }

            return RelaunchFromCopy(args);
        }

        using var singleInstance = new Mutex(false, @"Global\DoraSaverSetup");
        if (!singleInstance.WaitOne(0))
        {
            MessageBoxOrConsole(command.Quiet, SetupText.For(language).AlreadyRunning);
            return 2;
        }

        try
        {
            return command.Quiet ? RunQuiet(command, language) : RunWindow(command, language);
        }
        finally
        {
            singleInstance.ReleaseMutex();
            if (relaunched)
            {
                // This temporary copy cannot delete itself while running.
                NativeFile.DeleteOnReboot(Application.ExecutablePath);
                NativeFile.DeleteOnReboot(Path.GetDirectoryName(Application.ExecutablePath)!);
            }
        }
    }

    private static int RunWindow(CommandLine command, UiLanguage language)
    {
        using var form = new SetupForm(language);
        if (command.Uninstall)
        {
            form.UninstallWhenShown();
        }

        Application.Run(form);
        return 0;
    }

    private static int RunQuiet(CommandLine command, UiLanguage language)
    {
        AttachConsole(-1);
        var reporter = new ConsoleReporter();
        SetupText text = SetupText.For(language);
        try
        {
            var installer = new Installer(text, reporter);
            if (command.Uninstall)
            {
                return installer.Uninstall() == 0 ? 0 : 1;
            }

            IReadOnlyList<SaverInfo> savers = command.SaverIds.Count == 0
                ? SaverCatalog.All
                : SaverCatalog.All.Where(s => command.SaverIds.Contains(s.Id, StringComparer.OrdinalIgnoreCase)).ToList();
            IReadOnlyList<UiLanguage> names = command.NameLanguages.Count == 0 ? [language] : command.NameLanguages;
            InstallReport report = installer.InstallAsync(new InstallRequest(savers, names), CancellationToken.None).GetAwaiter().GetResult();
            reporter.Log(SetupForm.Summarize(text, report));
            return report.Installed.Count == 0 ? 2 : report.Failed.Count == 0 ? 0 : 1;
        }
        catch (Exception ex)
        {
            reporter.Log(string.Format(text.Error, ex));
            return 2;
        }
    }

    private static bool IsRunningFromInstallFolder()
    {
        string folder = Path.GetFullPath(SystemIntegration.InstallFolder) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(Application.ExecutablePath).StartsWith(folder, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The copy in Program Files (used by Apps &amp; Features) cannot replace or remove the folder it runs
    /// from, so run a temporary copy from the admin-only data folder and pass its exit code through.
    /// </summary>
    private static int RelaunchFromCopy(string[] args)
    {
        SecureFolder.Ensure(SystemIntegration.DataFolder);
        string folder = SecureFolder.CreateFresh(SystemIntegration.DataFolder, "run-");
        string copy = Path.Combine(folder, Installer.SetupFileName);
        File.Copy(Application.ExecutablePath, copy);
        string arguments = string.Join(" ", args.Select(a => "\"" + a.Replace("\"", string.Empty) + "\"").Append(RelaunchedFlag));
        using System.Diagnostics.Process child = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(copy, arguments)
        {
            UseShellExecute = false,
            WorkingDirectory = Environment.SystemDirectory,
        })!;
        child.WaitForExit();
        return child.ExitCode;
    }

    private static void MessageBoxOrConsole(bool quiet, string message)
    {
        if (quiet)
        {
            AttachConsole(-1);
            Console.WriteLine(message);
        }
        else
        {
            MessageBox.Show(message, "DoraSaver", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private const uint LoadLibrarySearchApplicationDir = 0x00000200;
    private const uint LoadLibrarySearchSystem32 = 0x00000800;

    [DllImport("kernel32.dll")]
    private static extern bool SetDefaultDllDirectories(uint directoryFlags);

    [DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);

    private sealed class ConsoleReporter : IReporter
    {
        private readonly string _logFile = Path.Combine(SystemIntegration.DataFolder, "setup.log");

        public ConsoleReporter()
        {
            SecureFolder.Ensure(SystemIntegration.DataFolder);
        }

        public void Log(string message)
        {
            Console.WriteLine(message);
            try
            {
                Directory.CreateDirectory(SystemIntegration.DataFolder);
                File.AppendAllText(_logFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The console already has the message.
            }
        }

        public void Progress(int value, int maximum)
        {
        }

        public void EnteringCommitPhase()
        {
        }
    }
}

internal sealed record CommandLine(bool Install, bool Uninstall, bool Quiet, UiLanguage? Language, IReadOnlyList<UiLanguage> NameLanguages, IReadOnlyList<string> SaverIds)
{
    public static CommandLine Parse(IEnumerable<string> args)
    {
        bool install = false, uninstall = false, quiet = false;
        UiLanguage? language = null;
        var names = new List<UiLanguage>();
        var savers = new List<string>();
        foreach (string raw in args)
        {
            string arg = raw.Trim().TrimStart('/', '-');
            string key = arg.Split('=', ':')[0].ToLowerInvariant();
            string value = arg.Length > key.Length ? arg.Substring(key.Length + 1) : string.Empty;
            switch (key)
            {
                case "install": install = true; break;
                case "uninstall": uninstall = true; break;
                case "quiet": case "q": case "silent": quiet = true; break;
                case "lang": language = ParseLanguage(value); break;
                case "names": names.AddRange(SplitList(value).Select(ParseLanguage).OfType<UiLanguage>()); break;
                case "savers": savers.AddRange(SplitList(value)); break;
            }
        }

        return new CommandLine(install || !uninstall, uninstall, quiet, language, names.Distinct().ToList(), savers);
    }

    internal static UiLanguage? ParseLanguage(string value) => value.Trim().ToLowerInvariant() switch
    {
        "zh" or "cn" or "chinese" => UiLanguage.Chinese,
        "ja" or "jp" or "japanese" => UiLanguage.Japanese,
        "en" or "english" => UiLanguage.English,
        _ => null,
    };

    private static IEnumerable<string> SplitList(string value) =>
        value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
}
