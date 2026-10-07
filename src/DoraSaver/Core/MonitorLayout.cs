namespace DoraSaver.Core;

/// <param name="IsPrimary">The primary monitor's window plays sound and takes focus.</param>
internal sealed record MonitorArea(string Name, Rectangle Bounds, bool IsPrimary);

/// <summary>
/// The screen areas that each get a full-screen window, primary first.
/// </summary>
internal static class MonitorLayout
{
    /// <summary>
    /// Testing aid for machines with one monitor: set it to 2..4 to split the primary monitor into
    /// that many side-by-side areas, each handled like a separate monitor.
    /// </summary>
    public const string SimulateVariable = "DORASAVER_SIMULATE_MONITORS";

    private const int MaxSimulated = 4;

    public static IReadOnlyList<MonitorArea> Current()
    {
        Screen primary = Screen.PrimaryScreen ?? Screen.AllScreens[0];
        int simulated = ParseSimulatedCount(Environment.GetEnvironmentVariable(SimulateVariable));
        if (simulated > 1)
        {
            return Split(primary.Bounds, simulated);
        }

        return FromScreens(primary.DeviceName, Screen.AllScreens.Select(s => (s.DeviceName, s.Bounds)));
    }

    public static IReadOnlyList<MonitorArea> FromScreens(string primaryName, IEnumerable<(string Name, Rectangle Bounds)> screens)
    {
        return screens
            .Select(s => new MonitorArea(s.Name, s.Bounds, s.Name == primaryName))
            .OrderByDescending(a => a.IsPrimary)
            .ToList();
    }

    public static IReadOnlyList<MonitorArea> Split(Rectangle bounds, int count)
    {
        var areas = new List<MonitorArea>(count);
        for (int i = 0; i < count; i++)
        {
            int left = bounds.Left + bounds.Width * i / count;
            int right = bounds.Left + bounds.Width * (i + 1) / count;
            areas.Add(new MonitorArea($"simulated{i + 1}", new Rectangle(left, bounds.Top, right - left, bounds.Height), i == 0));
        }

        return areas;
    }

    public static int ParseSimulatedCount(string? value)
    {
        return int.TryParse(value, out int count) && count >= 2 && count <= MaxSimulated ? count : 0;
    }
}
