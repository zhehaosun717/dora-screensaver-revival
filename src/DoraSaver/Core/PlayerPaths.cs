namespace DoraSaver.Core;

internal static class PlayerPaths
{
    /// <summary>Virtual host the asset folder is mapped to inside WebView2 (.example never resolves on the network).</summary>
    public const string VirtualHost = "dorasaver.example";

    public const string DataFolderName = "DoraSaverData";

    private const string MarkerFile = "player.html";

    /// <summary>
    /// Asset folder: a DoraSaverData folder next to the .scr (portable/dev layout) or %ProgramFiles%\DoraSaver.
    /// </summary>
    public static string? FindAssetFolder(string baseDirectory, string programFiles)
    {
        string[] candidates =
        [
            Path.Combine(baseDirectory, DataFolderName),
            Path.Combine(programFiles, "DoraSaver"),
        ];
        return candidates.FirstOrDefault(dir => File.Exists(Path.Combine(dir, MarkerFile)));
    }

    public static Uri BuildPlayerUri(SaverInfo saver, LayoutMode layout)
    {
        string query = $"swf={Uri.EscapeDataString(saver.SwfFile)}&layout={SaverCatalog.ToQueryValue(layout)}";
        return new Uri($"https://{VirtualHost}/{MarkerFile}?{query}");
    }

    public static bool IsPlayerUri(string? uri)
    {
        return Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsed)
            && parsed.Scheme == Uri.UriSchemeHttps
            && string.Equals(parsed.Host, VirtualHost, StringComparison.OrdinalIgnoreCase);
    }

    public static string WebViewUserDataFolder => Path.Combine(Log.Directory, "WebView2");
}
