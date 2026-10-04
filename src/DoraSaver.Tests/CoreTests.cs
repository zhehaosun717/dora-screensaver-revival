using System.Drawing;
using DoraSaver.Core;
using Xunit;

namespace DoraSaver.Tests;

public class ScreenSaverArgsTests
{
    private static string[] Split(string commandLine) =>
        commandLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

    [Theory]
    [InlineData("", "Configure", 0L)]
    [InlineData("/s", "Show", 0L)]
    [InlineData("/S", "Show", 0L)]
    [InlineData("-s", "Show", 0L)]
    [InlineData("/p 12345", "Preview", 12345L)]
    [InlineData("/p:4242", "Preview", 4242L)]
    [InlineData("/P 99", "Preview", 99L)]
    [InlineData("/c", "Configure", 0L)]
    [InlineData("/c:5555", "Configure", 5555L)]
    [InlineData("/c 777", "Configure", 777L)]
    public void Parses_standard_screensaver_arguments(string commandLine, string mode, long handle)
    {
        ScreenSaverArgs parsed = ScreenSaverArgs.Parse(Split(commandLine));

        Assert.Equal((SaverMode)Enum.Parse(typeof(SaverMode), mode), parsed.Mode);
        Assert.Equal(new IntPtr(handle), parsed.WindowHandle);
    }

    [Theory]
    [InlineData("/p")]
    [InlineData("/p abc")]
    [InlineData("/p -5")]
    [InlineData("/a")]
    [InlineData("s")]
    [InlineData("/")]
    public void Rejects_unsupported_or_malformed_arguments(string commandLine)
    {
        Assert.Equal(SaverMode.Unsupported, ScreenSaverArgs.Parse(Split(commandLine)).Mode);
    }
}

public class SaverCatalogTests
{
    [Fact]
    public void Has_fourteen_savers_with_unique_ids_files_and_names()
    {
        int count = SaverCatalog.All.Count;
        Assert.Equal(14, count);
        Assert.Equal(count, SaverCatalog.All.Select(s => s.Id).Distinct().Count());
        Assert.Equal(count, SaverCatalog.All.Select(s => s.SwfFile).Distinct().Count());
        Assert.Equal(count * 3, SaverCatalog.All.SelectMany(s => s.Names()).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Swf_file_names_are_safe_for_the_player_page()
    {
        foreach (SaverInfo saver in SaverCatalog.All)
        {
            Assert.Matches("^[A-Za-z0-9_-]+\\.swf$", saver.SwfFile);
        }
    }

    [Theory]
    [InlineData("哆啦A梦电影历史1", "movie1")]
    [InlineData("映画ドラヒストリー2", "movie2")]
    [InlineData("ドラえもん誕生日記念SS", "birthday")]
    public void Finds_saver_by_either_file_name(string fileName, string id)
    {
        Assert.Equal(id, SaverCatalog.FindByFileName(fileName)?.Id);
    }

    [Fact]
    public void Unknown_names_and_ids_are_not_found()
    {
        Assert.Null(SaverCatalog.FindByFileName("Bubbles"));
        Assert.Null(SaverCatalog.FindByFileName(null));
        Assert.Null(SaverCatalog.FindById("nope"));
    }

    [Fact]
    public void Resolve_prefers_build_metadata_over_file_name()
    {
        // The test assembly references the default build (SaverId=birthday).
        SaverInfo saver = SaverCatalog.Resolve(typeof(SaverCatalog).Assembly, @"C:\Windows\System32\哆啦A梦电影历史2.scr");

        Assert.Equal("birthday", saver.Id);
    }

    [Fact]
    public void Resolve_falls_back_to_file_name_without_metadata()
    {
        SaverInfo saver = SaverCatalog.Resolve(typeof(SaverCatalogTests).Assembly, @"C:\x\映画ドラヒストリー1.scr");

        Assert.Equal("movie1", saver.Id);
    }

    [Theory]
    [InlineData("fit", "Fit")]
    [InlineData("EXTEND", "Extend")]
    [InlineData(" fill ", "Fill")]
    [InlineData("stretch", "Stretch")]
    public void Parses_layout_names(string text, string expected)
    {
        Assert.True(SaverCatalog.TryParseLayout(text, out LayoutMode layout));
        Assert.Equal((LayoutMode)Enum.Parse(typeof(LayoutMode), expected), layout);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("zoom")]
    [InlineData("7")]
    public void Rejects_unknown_layouts(string? text)
    {
        Assert.False(SaverCatalog.TryParseLayout(text, out _));
    }
}

public class RepositoryConsistencyTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "assets")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("assets folder not found above the test directory");
    }

    [Fact]
    public void Sources_point_at_raw_wayback_captures_with_full_fingerprints()
    {
        foreach (SaverInfo saver in SaverCatalog.All)
        {
            SaverSource source = saver.Source;
            Assert.StartsWith("https://web.archive.org/web/", source.WaybackUrl);
            Assert.Matches("/web/[0-9]{14}id_/https?://", source.WaybackUrl);
            Assert.Matches("^[0-9a-f]{64}$", source.ArchiveSha256);
            Assert.Matches("^[0-9a-f]{64}$", source.SwfSha256);
            Assert.True(source.SwfLength > 1000, saver.Id);
        }

        Assert.Equal(SaverCatalog.All.Count, SaverCatalog.All.Select(s => s.Source.WaybackUrl).Distinct().Count());
    }

    /// <summary>The Flash movies are not in the repository; on a dev machine that has them, they must match.</summary>
    [Fact]
    public void Local_swf_copies_match_the_catalog_fingerprints()
    {
        string swfDir = Path.Combine(RepoRoot(), "assets", "swf");
        using var sha = System.Security.Cryptography.SHA256.Create();
        foreach (SaverInfo saver in SaverCatalog.All)
        {
            string path = Path.Combine(swfDir, saver.SwfFile);
            if (!File.Exists(path))
            {
                continue;
            }

            byte[] data = File.ReadAllBytes(path);
            Assert.Equal(saver.Source.SwfLength, data.Length);
            Assert.Equal(saver.Source.SwfSha256, BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant());
        }
    }

    [Fact]
    public void Names_are_valid_windows_file_names()
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        foreach (SaverInfo saver in SaverCatalog.All)
        {
            foreach (string name in saver.Names())
            {
                Assert.True(name.IndexOfAny(invalid) < 0, name);
                Assert.Equal(name.Trim(), name);
            }
        }
    }
}

public class PlayerPathsTests
{
    [Fact]
    public void Builds_player_uri_on_the_virtual_host()
    {
        SaverInfo saver = SaverCatalog.FindById("movie1")!;

        Uri uri = PlayerPaths.BuildPlayerUri(saver, LayoutMode.Fill);

        Assert.Equal("https://dorasaver.example/player.html?swf=dm01_ss.swf&layout=fill", uri.ToString());
        Assert.True(PlayerPaths.IsPlayerUri(uri.ToString()));
    }

    [Theory]
    [InlineData("https://example.com/")]
    [InlineData("http://dorasaver.example/player.html")]
    [InlineData("file:///C:/Windows/")]
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    [InlineData(null)]
    public void Only_the_local_player_host_is_allowed(string? uri)
    {
        Assert.False(PlayerPaths.IsPlayerUri(uri));
    }

    [Fact]
    public void Finds_portable_folder_before_program_files()
    {
        string root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "dorasaver-test-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            string portable = Path.Combine(root, "app", PlayerPaths.DataFolderName);
            string installed = Path.Combine(root, "pf", "DoraSaver");
            Directory.CreateDirectory(portable);
            Directory.CreateDirectory(installed);
            File.WriteAllText(Path.Combine(installed, "player.html"), "x");

            Assert.Equal(installed, PlayerPaths.FindAssetFolder(Path.Combine(root, "app"), Path.Combine(root, "pf")));

            File.WriteAllText(Path.Combine(portable, "player.html"), "x");
            Assert.Equal(portable, PlayerPaths.FindAssetFolder(Path.Combine(root, "app"), Path.Combine(root, "pf")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Returns_null_when_no_assets_exist()
    {
        Assert.Null(PlayerPaths.FindAssetFolder(@"C:\does\not\exist", @"C:\also\missing"));
    }
}

public class WakePolicyTests
{
    private static readonly TimeSpan Grace = TimeSpan.FromMilliseconds(750);

    [Fact]
    public void Ignores_movement_during_grace_period()
    {
        var policy = new WakePolicy(new Point(100, 100), 16, Grace);

        Assert.False(policy.ShouldWakeOnMove(new Point(900, 900), TimeSpan.FromMilliseconds(100)));
    }

    [Fact]
    public void Ignores_small_jitter_after_grace_period()
    {
        var policy = new WakePolicy(new Point(100, 100), 16, Grace);

        Assert.False(policy.ShouldWakeOnMove(new Point(110, 108), TimeSpan.FromSeconds(5)));
        Assert.False(policy.ShouldWakeOnMove(new Point(116, 100), TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void Wakes_on_real_movement_after_grace_period()
    {
        var policy = new WakePolicy(new Point(100, 100), 16, Grace);

        Assert.True(policy.ShouldWakeOnMove(new Point(117, 100), TimeSpan.FromSeconds(1)));
        Assert.True(policy.ShouldWakeOnMove(new Point(0, 0), TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Movement_during_grace_period_is_forgotten_not_postponed()
    {
        var policy = new WakePolicy(new Point(100, 100), 16, Grace);

        Assert.False(policy.ShouldWakeOnMove(new Point(900, 900), TimeSpan.FromMilliseconds(300)));
        Assert.False(policy.ShouldWakeOnMove(new Point(900, 900), TimeSpan.FromSeconds(2)));
        Assert.True(policy.ShouldWakeOnMove(new Point(950, 900), TimeSpan.FromSeconds(3)));
    }

    [Fact]
    public void Unknown_origin_is_taken_from_the_first_observed_point()
    {
        var policy = new WakePolicy(null, 16, Grace);

        Assert.False(policy.ShouldWakeOnMove(new Point(500, 500), TimeSpan.FromSeconds(5)));
        Assert.False(policy.ShouldWakeOnMove(new Point(505, 500), TimeSpan.FromSeconds(6)));
        Assert.True(policy.ShouldWakeOnMove(new Point(600, 500), TimeSpan.FromSeconds(7)));
    }

    [Fact]
    public void Rejects_non_positive_threshold()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WakePolicy(Point.Empty, 0, Grace));
    }
}

public class SaverSettingsTests
{
    [Fact]
    public void Defaults_are_silent_with_the_recommended_layout()
    {
        foreach (SaverInfo saver in SaverCatalog.All)
        {
            SaverSettings defaults = SaverSettings.DefaultsFor(saver);

            Assert.False(defaults.PlaySound);
            Assert.Equal(saver.DefaultLayout, defaults.Layout);
            Assert.True(defaults.AllMonitors);
        }
    }

    [Fact]
    public void Registry_store_round_trips_and_falls_back_to_defaults()
    {
        var saver = SaverCatalog.All[0] with { Id = $"test-{Guid.NewGuid():N}", DefaultLayout = LayoutMode.Extend };
        var store = new RegistrySettingsStore();
        try
        {
            Assert.Equal(SaverSettings.DefaultsFor(saver), store.Load(saver));

            store.Save(saver, new SaverSettings(true, LayoutMode.Stretch, AllMonitors: false));
            Assert.Equal(new SaverSettings(true, LayoutMode.Stretch, AllMonitors: false), store.Load(saver));
        }
        finally
        {
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree($@"Software\DoraSaver\{saver.Id}", throwOnMissingSubKey: false);
        }
    }
}

public class UiTextTests
{
    private static readonly System.Globalization.CultureInfo Chinese = new("zh-CN");
    private static readonly System.Globalization.CultureInfo Japanese = new("ja-JP");
    private static readonly System.Globalization.CultureInfo English = new("en-US");

    [Theory]
    [InlineData("ドラえもん誕生日記念SS", "Japanese")]
    [InlineData("パーマン ザ★ムービー2004", "Japanese")]
    [InlineData("映画ドラヒストリー1 2001年版", "Japanese")]
    [InlineData("哆啦A梦诞生日纪念", "Chinese")]
    [InlineData("帕门 剧场版2004", "Chinese")]
    [InlineData("Doraemon Birthday Clock", "English")]
    [InlineData("Perman The Movie 2004", "English")]
    public void Saver_file_name_decides_language_even_on_a_chinese_system(string fileName, string expected)
    {
        Assert.Equal((UiLanguage)Enum.Parse(typeof(UiLanguage), expected), UiText.Detect(fileName, Chinese));
        Assert.Equal((UiLanguage)Enum.Parse(typeof(UiLanguage), expected), UiText.Detect(fileName, English));
    }

    [Fact]
    public void Unknown_file_name_follows_the_display_language()
    {
        Assert.Equal(UiLanguage.Japanese, UiText.Detect("birthday", Japanese));
        Assert.Equal(UiLanguage.Chinese, UiText.Detect("birthday", Chinese));
        Assert.Equal(UiLanguage.Chinese, UiText.Detect(null, new("zh-TW")));
        Assert.Equal(UiLanguage.English, UiText.Detect("", English));
        Assert.Equal(UiLanguage.English, UiText.Detect("birthday", new("fr-FR")));
    }

    [Fact]
    public void Every_language_has_complete_text()
    {
        foreach (UiLanguage language in Enum.GetValues(typeof(UiLanguage)).Cast<UiLanguage>())
        {
            UiText text = UiText.For(language);
            Assert.Equal(language, text.Language);
            foreach (var property in typeof(UiText).GetProperties().Where(p => p.PropertyType == typeof(string)))
            {
                Assert.False(string.IsNullOrWhiteSpace((string?)property.GetValue(text)), $"{language}.{property.Name}");
            }

            Assert.Contains("{0}", text.TitleFormat);
            Assert.Contains("{0}", text.MissingAssetsFormat);
            Assert.Contains("{0}", text.SaveFailedFormat);
            Assert.Equal(4, Enum.GetValues(typeof(LayoutMode)).Cast<LayoutMode>().Select(text.LayoutLabel).Distinct().Count());
        }
    }
}