using System.Diagnostics;
using System.IO.Compression;
using DoraSaver.Core;

namespace DoraSaver.Setup.Engine;

/// <summary>
/// Unpacks an original download far enough to reach the installer that embeds the Flash movie.
/// Nothing from the old downloads is ever executed; archives are only read. All files are written
/// to the admin-only work folder, from already-verified bytes.
/// </summary>
internal sealed class Extractor
{
    /// <summary>
    /// The Unarchiver's command-line tool, only needed for the RAR 2.0 (2001) and StuffIt (Perman) files.
    /// Pinned by hash; it is downloaded on demand and never bundled.
    /// </summary>
    public const string UnarUrl = "https://cdn.theunarchiver.com/downloads/unarWindows.zip";
    public const string UnarSha256 = "61a6b299606282f72f51c278801eac11d3dccfac83e2d68bccce33539912e0dd";

    private static readonly TimeSpan ToolTimeout = TimeSpan.FromMinutes(2);

    private readonly Downloader _downloader;
    private readonly string _workFolder;
    private string? _unarExe;

    /// <param name="workFolder">Must be admin-only (see <see cref="SecureFolder"/>).</param>
    public Extractor(Downloader downloader, string workFolder)
    {
        _downloader = downloader;
        _workFolder = workFolder;
    }

    public static bool NeedsUnar(SourceKind kind) => kind is SourceKind.RarSfx or SourceKind.StuffIt;

    /// <summary>The verified download plus every file found inside it: all places the SWF might be.</summary>
    public async Task<IReadOnlyList<byte[]>> ExpandAsync(byte[] archive, SourceKind kind, string id, CancellationToken cancel)
    {
        var blobs = new List<byte[]> { archive };
        string folder = Path.Combine(_workFolder, id);
        string outFolder = Path.Combine(folder, "out");
        Directory.CreateDirectory(outFolder);
        string archivePath = Path.Combine(folder, "original.bin");

        switch (kind)
        {
            case SourceKind.Zip:
                blobs.AddRange(ReadZipEntries(archive));
                break;
            case SourceKind.LhaSfx:
                File.WriteAllBytes(archivePath, archive);
                RunTool(Path.Combine(Environment.SystemDirectory, "tar.exe"), $"-xf \"{archivePath}\" -C \"{outFolder}\"");
                blobs.AddRange(ReadAllFiles(outFolder));
                break;
            case SourceKind.RarSfx:
            case SourceKind.StuffIt:
                string unar = await EnsureUnarAsync(cancel).ConfigureAwait(false);
                File.WriteAllBytes(archivePath, archive);
                RunTool(unar, $"-q -f -e shift_jis -o \"{outFolder}\" \"{archivePath}\"");
                blobs.AddRange(ReadAllFiles(outFolder));
                break;
        }

        return blobs;
    }

    private static IEnumerable<byte[]> ReadZipEntries(byte[] archive)
    {
        using var zip = new ZipArchive(new MemoryStream(archive, writable: false), ZipArchiveMode.Read);
        var result = new List<byte[]>();
        foreach (ZipArchiveEntry entry in zip.Entries.Where(e => e.Length > 0))
        {
            using Stream stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            result.Add(buffer.ToArray());
        }

        return result;
    }

    private static IEnumerable<byte[]> ReadAllFiles(string folder) =>
        Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories).Select(File.ReadAllBytes).ToList();

    private async Task<string> EnsureUnarAsync(CancellationToken cancel)
    {
        if (_unarExe is not null)
        {
            return _unarExe;
        }

        byte[] package = await _downloader.GetAsync(UnarUrl, UnarSha256, cancel).ConfigureAwait(false);
        string folder = Path.Combine(_workFolder, "unar");
        Directory.CreateDirectory(folder);
        using (var zip = new ZipArchive(new MemoryStream(package, writable: false), ZipArchiveMode.Read))
        {
            foreach (ZipArchiveEntry entry in zip.Entries.Where(e => e.Name.Length > 0 && !e.FullName.StartsWith("__MACOSX", StringComparison.Ordinal)))
            {
                entry.ExtractToFile(Path.Combine(folder, entry.Name), overwrite: true);
            }
        }

        _unarExe = Path.Combine(folder, "unar.exe");
        if (!File.Exists(_unarExe))
        {
            throw new FileNotFoundException("unar.exe missing from the downloaded package", _unarExe);
        }

        return _unarExe;
    }

    private static void RunTool(string exe, string arguments)
    {
        if (!File.Exists(exe))
        {
            throw new FileNotFoundException($"Required tool not found: {exe}", exe);
        }

        var info = new ProcessStartInfo(exe, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(exe) ?? Environment.SystemDirectory,
        };
        using Process process = Process.Start(info) ?? throw new InvalidOperationException($"Could not start {exe}");
        Task<string> stdout = process.StandardOutput.ReadToEndAsync();
        Task<string> stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit((int)ToolTimeout.TotalMilliseconds))
        {
            process.Kill();
            throw new TimeoutException($"{Path.GetFileName(exe)} did not finish");
        }

        // tar may exit non-zero after warnings about file-name encoding while still extracting
        // everything; the SHA-256 check on the carved movie is the real success criterion.
        _ = stdout.Result + stderr.Result;
    }
}
