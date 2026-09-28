namespace DoraSaver.Core;

internal enum SaverMode
{
    Configure,
    Show,
    Preview,
    Unsupported,
}

/// <summary>
/// Parsed Windows screensaver command line: /s, /p HWND, /p:HWND, /c, /c:HWND, or nothing.
/// </summary>
internal sealed record ScreenSaverArgs(SaverMode Mode, IntPtr WindowHandle)
{
    public static ScreenSaverArgs Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0)
        {
            return new ScreenSaverArgs(SaverMode.Configure, IntPtr.Zero);
        }

        string first = args[0].Trim();
        if (first.Length < 2 || (first[0] != '/' && first[0] != '-'))
        {
            return new ScreenSaverArgs(SaverMode.Unsupported, IntPtr.Zero);
        }

        char command = char.ToLowerInvariant(first[1]);
        string inlineValue = first.Length > 2 ? first.Substring(2).TrimStart(':', ' ') : string.Empty;
        string handleText = inlineValue.Length > 0 ? inlineValue : (args.Count > 1 ? args[1] : string.Empty);
        IntPtr handle = ParseHandle(handleText);

        return command switch
        {
            's' => new ScreenSaverArgs(SaverMode.Show, IntPtr.Zero),
            'c' => new ScreenSaverArgs(SaverMode.Configure, handle),
            'p' or 'l' when handle != IntPtr.Zero => new ScreenSaverArgs(SaverMode.Preview, handle),
            _ => new ScreenSaverArgs(SaverMode.Unsupported, IntPtr.Zero),
        };
    }

    private static IntPtr ParseHandle(string text)
    {
        return long.TryParse(text.Trim(), out long value) && value > 0
            ? new IntPtr(value)
            : IntPtr.Zero;
    }
}
