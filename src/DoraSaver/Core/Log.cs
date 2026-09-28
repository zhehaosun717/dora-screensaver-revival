namespace DoraSaver.Core;

/// <summary>
/// Best-effort error log in %LOCALAPPDATA%\DoraSaver. A screensaver has no console and
/// must never crash because logging failed.
/// </summary>
internal static class Log
{
    private const long MaxBytes = 512 * 1024;
    private static readonly object Gate = new();

    private static readonly int ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;

    public static string Directory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DoraSaver");

    public static string FilePath { get; } = Path.Combine(Directory, "dorasaver.log");

    public static void Error(string message, Exception? ex = null)
    {
        Write("ERROR", ex is null ? message : $"{message}: {ex}");
    }

    public static void Info(string message) => Write("INFO", message);

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                System.IO.Directory.CreateDirectory(Directory);
                var file = new FileInfo(FilePath);
                if (file.Exists && file.Length > MaxBytes)
                {
                    file.Delete();
                }

                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{ProcessId}] {level} {message}{Environment.NewLine}";
                File.AppendAllText(FilePath, line);
            }
        }
        catch (Exception)
        {
            // Logging is best-effort only.
        }
    }
}
