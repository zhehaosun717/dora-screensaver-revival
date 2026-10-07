using System.Drawing;
using DoraSaver.Core;
using Xunit;

namespace DoraSaver.Tests;

public class MonitorLayoutTests
{
    [Fact]
    public void Puts_the_primary_monitor_first()
    {
        IReadOnlyList<MonitorArea> areas = MonitorLayout.FromScreens(
            @"\\.\DISPLAY2",
            [(@"\\.\DISPLAY1", new Rectangle(-1920, 0, 1920, 1080)), (@"\\.\DISPLAY2", new Rectangle(0, 0, 2560, 1440))]);

        Assert.Equal(2, areas.Count);
        Assert.Equal(@"\\.\DISPLAY2", areas[0].Name);
        Assert.True(areas[0].IsPrimary);
        Assert.False(areas[1].IsPrimary);
        Assert.Equal(new Rectangle(-1920, 0, 1920, 1080), areas[1].Bounds);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void Splits_a_screen_into_adjacent_areas_that_cover_it_exactly(int count)
    {
        var bounds = new Rectangle(0, 0, 3200, 2000);

        IReadOnlyList<MonitorArea> areas = MonitorLayout.Split(bounds, count);

        Assert.Equal(count, areas.Count);
        Assert.True(areas[0].IsPrimary);
        Assert.All(areas.Skip(1), a => Assert.False(a.IsPrimary));
        Assert.Equal(bounds.Left, areas[0].Bounds.Left);
        Assert.Equal(bounds.Right, areas[count - 1].Bounds.Right);
        for (int i = 1; i < count; i++)
        {
            Assert.Equal(areas[i - 1].Bounds.Right, areas[i].Bounds.Left);
        }

        Assert.All(areas, a => Assert.Equal(bounds.Height, a.Bounds.Height));
        Assert.Equal(count, areas.Select(a => a.Name).Distinct().Count());
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("1", 0)]
    [InlineData("2", 2)]
    [InlineData("4", 4)]
    [InlineData("5", 0)]
    [InlineData("-2", 0)]
    [InlineData("two", 0)]
    public void Only_accepts_a_small_simulated_monitor_count(string? value, int expected)
    {
        Assert.Equal(expected, MonitorLayout.ParseSimulatedCount(value));
    }
}
