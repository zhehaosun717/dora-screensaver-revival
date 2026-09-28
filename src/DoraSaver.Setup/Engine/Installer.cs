using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using DoraSaver.Core;

namespace DoraSaver.Setup.Engine;

internal sealed record InstallRequest(IReadOnlyList<SaverInfo> Savers, IReadOnlyList<UiLanguage> NameLanguages);

internal sealed record SaverFailure(SaverInfo Saver, string Reason);

internal sealed record InstallReport(IReadOnlyList<SaverInfo> Installed, IReadOnlyList<SaverFailure> Failed, string? ActiveScreensaver);

internal interface IReporter
{
    void Log(string message);

    void Progress(int value, int maximum);

    /// <summary>From here on the system is being changed and the run must not be interrupted.</summary>
    void EnteringCommitPhase();
}

/// <summary>Install and uninstall, independent of the window or command line that drives them.</summary>
internal sealed class Installer
{
    public const string SetupFileName = "DoraSaverSetup.exe";

    private const string WebView2BootstrapperUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";
    private const string InstalledListFile = "installed.txt";
    private static readonly TimeSpan WebView2Timeout = TimeSpan.FromMinutes(10);

    private readonly SetupText _text;
    private readonly IReporter _reporter;

    public Installer(SetupText text, IReporter reporter)
    {
        _text = text;
        _reporter = reporter;
    }

    public static string Version => typeof(Installer).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    public async Task<InstallReport> InstallAsync(InstallRequest request, CancellationToken cancel)
    {
        SecureFolder.Ensure(SystemIntegration.DataFolder);
        string work = SecureFolder.CreateFresh(SystemIntegration.DataFolder, "work-");
        try
        {
            using var downloader = new Downloader(Path.Combine(SystemIntegration.DataFolder, "cache"));
            await EnsureWebView2Async(work, cancel).ConfigureAwait(false);

            string staging = Path.Combine(work, "staging");
            ExtractPayload(staging);
            var extractor = new Extractor(downloader, work);
            (List<SaverInfo> installed, List<SaverFailure> failed) =
                await FetchMoviesAsync(request.Savers, extractor, downloader, Path.Combine(staging, "swf"), cancel).ConfigureAwait(false);
            if (installed.Count == 0)
            {
                return new InstallReport(installed, failed, null);
            }

            cancel.ThrowIfCancellationRequested();
            _reporter.EnteringCommitPhase();
            _reporter.Log(_text.Installing);
            string? active = Commit(staging, installed, request.NameLanguages);
            return new InstallReport(installed, failed, active);
        }
        finally
        {
            DeleteDirectory(work);
        }
    }

    /// <summary>Returns the number of items that could not be removed.</summary>
    public int Uninstall()
    {
        _reporter.Log(_text.Uninstalling);
        SystemIntegration.StopRunningScreensavers();
        int leftovers = 0;
        foreach (string scr in ReadInstalledList().Concat(SystemIntegration.FindInstalledScreensavers()).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            leftovers += TryDeleteFile(scr) ? 0 : 1;
        }

        SystemIntegration.ClearActiveScreensaverIfMissing();
        SystemIntegration.RemoveUninstallEntry();
        DeleteDirectory(Path.Combine(SystemIntegration.DataFolder, "cache"));
        if (!DeleteDirectory(SystemIntegration.InstallFolder) && !EvacuateAndDelete(SystemIntegration.InstallFolder))
        {
            leftovers++;
        }

        _reporter.Log(leftovers == 0 ? _text.UninstallDone : string.Format(_text.UninstallLeftovers, leftovers));
        return leftovers;
    }

    private string? Commit(string staging, IReadOnlyList<SaverInfo> installed, IReadOnlyList<UiLanguage> languages)
    {
        SystemIntegration.StopRunningScreensavers();
        SwapInstallFolder(staging);
        string setupCopy = CopySetupIntoInstallFolder();
        SystemIntegration.WriteUninstallEntry(setupCopy, Version, FolderSizeKb(SystemIntegration.InstallFolder));

        Dictionary<string, string> primaryPaths = InstallScreensavers(installed, languages);
        foreach (string item in SystemIntegration.BackUpBrokenOriginals(installed, w => _reporter.Log(string.Format(_text.Warning, w))))
        {
            _reporter.Log(string.Format(_text.BackedUp, item));
        }

        try
        {
            return SystemIntegration.RepointActiveScreensaver(primaryPaths);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            _reporter.Log(string.Format(_text.Warning, ex.Message));
            return null;
        }
    }

    private async Task<(List<SaverInfo> Installed, List<SaverFailure> Failed)> FetchMoviesAsync(
        IReadOnlyList<SaverInfo> savers, Extractor extractor, Downloader downloader, string swfFolder, CancellationToken cancel)
    {
        Directory.CreateDirectory(swfFolder);
        var installed = new List<SaverInfo>();
        var failed = new List<SaverFailure>();
        for (int i = 0; i < savers.Count; i++)
        {
            cancel.ThrowIfCancellationRequested();
            SaverInfo saver = savers[i];
            _reporter.Progress(i, savers.Count);
            _reporter.Log(string.Format(_text.Fetching, saver.NameIn(_text.Language), saver.Year));
            try
            {
                byte[]? movie = ReuseInstalledMovie(saver);
                bool reused = movie is not null;
                movie ??= await DownloadMovieAsync(saver, extractor, downloader, cancel).ConfigureAwait(false);
                File.WriteAllBytes(Path.Combine(swfFolder, saver.SwfFile), movie);
                installed.Add(saver);
                _reporter.Log(reused ? _text.Reused : _text.Verified);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed.Add(new SaverFailure(saver, ex.Message));
                _reporter.Log(string.Format(_text.Failed, ex.Message));
            }
        }

        _reporter.Progress(savers.Count, savers.Count);
        return (installed, failed);
    }

    private static byte[]? ReuseInstalledMovie(SaverInfo saver)
    {
        string existing = Path.Combine(SystemIntegration.InstallFolder, "swf", saver.SwfFile);
        if (!File.Exists(existing))
        {
            return null;
        }

        byte[] data = File.ReadAllBytes(existing);
        return data.Length == saver.Source.SwfLength && Hash.Sha256(data) == saver.Source.SwfSha256 ? data : null;
    }

    private async Task<byte[]> DownloadMovieAsync(SaverInfo saver, Extractor extractor, Downloader downloader, CancellationToken cancel)
    {
        SaverSource source = saver.Source;
        if (Extractor.NeedsUnar(source.Kind))
        {
            _reporter.Log(_text.NeedsUnar);
        }

        byte[] archive = await downloader.GetAsync(source.WaybackUrl, source.ArchiveSha256, cancel).ConfigureAwait(false);
        IReadOnlyList<byte[]> blobs = await extractor.ExpandAsync(archive, source.Kind, saver.Id, cancel).ConfigureAwait(false);
        return SwfCarver.Find(blobs, source.SwfLength, source.SwfSha256)
            ?? throw new InvalidDataException(_text.MovieNotFound);
    }

    private async Task EnsureWebView2Async(string work, CancellationToken cancel)
    {
        if (SystemIntegration.IsWebView2Installed())
        {
            return;
        }

        _reporter.Log(_text.InstallingWebView2);
        string bootstrapper = Path.Combine(work, "MicrosoftEdgeWebview2Setup.exe");
        using (var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromMinutes(5) })
        {
            byte[] data = await http.GetByteArrayAsync(WebView2BootstrapperUrl).ConfigureAwait(false);
            File.WriteAllBytes(bootstrapper, data);
        }

        // The work folder is admin-only, so the file checked here is the file that runs.
        if (!TrustVerifier.IsSignedByMicrosoft(bootstrapper))
        {
            throw new InvalidDataException(_text.WebView2NotTrusted);
        }

        using Process process = Process.Start(new ProcessStartInfo(bootstrapper, "/silent /install") { UseShellExecute = false, WorkingDirectory = work })
            ?? throw new InvalidOperationException("WebView2 setup did not start");
        bool exited = await Task.Run(() => process.WaitForExit((int)WebView2Timeout.TotalMilliseconds), cancel).ConfigureAwait(false);
        if (!exited || !SystemIntegration.IsWebView2Installed())
        {
            throw new InvalidOperationException(string.Format(_text.WebView2Failed, exited ? process.ExitCode.ToString() : "timeout"));
        }
    }

    private static void ExtractPayload(string destination)
    {
        using Stream payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip")
            ?? throw new InvalidOperationException("This build of the setup has no payload (run build.ps1).");
        using var zip = new ZipArchive(payload, ZipArchiveMode.Read);
        zip.ExtractToDirectory(destination);
    }

    /// <summary>
    /// Replaces the install folder with the staged one. If the second move fails, the previous folder
    /// is put back so the installed screensavers keep working.
    /// </summary>
    private static void SwapInstallFolder(string staging)
    {
        string target = SystemIntegration.InstallFolder;
        string previous = target + ".old-" + Guid.NewGuid().ToString("N");
        bool hadPrevious = Directory.Exists(target);
        if (hadPrevious)
        {
            Retry(() => Directory.Move(target, previous));
        }

        try
        {
            Retry(() => Directory.Move(staging, target));
        }
        catch when (hadPrevious)
        {
            Retry(() => Directory.Move(previous, target));
            throw;
        }

        if (hadPrevious && !DeleteDirectory(previous))
        {
            ScheduleDeleteOnReboot(previous);
        }
    }

    /// <summary>Writes the new .scr files first, then removes only old ones that are no longer wanted.</summary>
    private Dictionary<string, string> InstallScreensavers(IReadOnlyList<SaverInfo> savers, IReadOnlyList<UiLanguage> languages)
    {
        var written = new List<string>();
        var primary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string listPath = Path.Combine(SystemIntegration.InstallFolder, InstalledListFile);
        foreach (SaverInfo saver in savers)
        {
            string host = Path.Combine(SystemIntegration.InstallFolder, "scr", saver.Id + ".scr");
            foreach (UiLanguage language in languages)
            {
                string target = Path.Combine(SystemIntegration.System32, saver.NameIn(language) + ".scr");
                Retry(() => File.Copy(host, target, overwrite: true));
                written.Add(target);
                File.WriteAllLines(listPath, written);
                if (!primary.ContainsKey(saver.Id))
                {
                    primary[saver.Id] = target;
                }
            }
        }

        var keep = new HashSet<string>(written, StringComparer.OrdinalIgnoreCase);
        foreach (string old in SystemIntegration.FindInstalledScreensavers().Where(f => !keep.Contains(f)))
        {
            TryDeleteFile(old);
        }

        _reporter.Log(string.Format(_text.InstalledFiles, written.Count));
        return primary;
    }

    private static string CopySetupIntoInstallFolder()
    {
        string self = Path.GetFullPath(Application.ExecutablePath);
        string copy = Path.Combine(SystemIntegration.InstallFolder, SetupFileName);
        if (!string.Equals(self, Path.GetFullPath(copy), StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(self, copy, overwrite: true);
        }

        return copy;
    }

    private static long FolderSizeKb(string folder) =>
        Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length) / 1024;

    private static IEnumerable<string> ReadInstalledList()
    {
        string list = Path.Combine(SystemIntegration.InstallFolder, InstalledListFile);
        return File.Exists(list) ? File.ReadAllLines(list).Where(l => l.EndsWith(".scr", StringComparison.OrdinalIgnoreCase)) : [];
    }

    /// <summary>Files can stay locked for a moment after their process was stopped (e.g. WebView2 helpers).</summary>
    private static void Retry(Action action)
    {
        for (int attempt = 1; ; attempt++)
        {
            try
            {
                action();
                return;
            }
            catch (Exception ex) when ((ex is IOException or UnauthorizedAccessException) && attempt < 8)
            {
                Thread.Sleep(500 * attempt);
            }
        }
    }

    private static bool TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool DeleteDirectory(string path)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }

                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Thread.Sleep(300 * attempt);
            }
        }

        return false;
    }

    /// <summary>
    /// A running exe (the Program Files copy that launched this uninstall) cannot be deleted but can be
    /// moved. Move whatever is still locked into the admin-only data folder, delete it at the next
    /// restart, and remove the now-empty install folder.
    /// </summary>
    private static bool EvacuateAndDelete(string folder)
    {
        if (!Directory.Exists(folder))
        {
            return true;
        }

        string trash = SecureFolder.CreateFresh(SystemIntegration.DataFolder, "trash-");
        foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).ToList())
        {
            if (TryDeleteFile(file))
            {
                continue;
            }

            try
            {
                string moved = Path.Combine(trash, Guid.NewGuid().ToString("N") + "-" + Path.GetFileName(file));
                File.Move(file, moved);
                NativeFile.DeleteOnReboot(moved);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                NativeFile.DeleteOnReboot(file);
            }
        }

        NativeFile.DeleteOnReboot(trash);
        bool deleted = DeleteDirectory(folder);
        if (!deleted)
        {
            ScheduleDeleteOnReboot(folder);
        }

        return deleted;
    }

    private static void ScheduleDeleteOnReboot(string folder)
    {
        foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            NativeFile.DeleteOnReboot(file);
        }

        foreach (string dir in Directory.EnumerateDirectories(folder, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
        {
            NativeFile.DeleteOnReboot(dir);
        }

        NativeFile.DeleteOnReboot(folder);
    }
}

internal static class NativeFile
{
    private const int MoveFileDelayUntilReboot = 0x4;

    public static void DeleteOnReboot(string path) => MoveFileEx(path, null, MoveFileDelayUntilReboot);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern bool MoveFileEx(string existing, string? replacement, int flags);
}
