extern alias setup;

using setup::DoraSaver.Core;
using setup::DoraSaver.Setup;
using setup::DoraSaver.Setup.Engine;
using Xunit;

namespace DoraSaver.Tests.SetupSide;

public class SwfCarverTests
{
    private static byte[] FakeSwf(char kind, int length)
    {
        var swf = new byte[length];
        new Random(length).NextBytes(swf);
        swf[0] = (byte)kind;
        swf[1] = (byte)'W';
        swf[2] = (byte)'S';
        swf[3] = 8;
        return swf;
    }

    private static byte[] Embed(byte[] swf, int before, int after)
    {
        var blob = new byte[before + swf.Length + after];
        new Random(before).NextBytes(blob);
        Buffer.BlockCopy(swf, 0, blob, before, swf.Length);
        return blob;
    }

    [Theory]
    [InlineData('F')]
    [InlineData('C')]
    public void Finds_the_movie_whose_length_and_hash_match(char kind)
    {
        byte[] swf = FakeSwf(kind, 5000);
        byte[] decoy = FakeSwf('F', 5000);
        byte[][] blobs = [Embed(decoy, 100, 50), Embed(swf, 12345, 777)];

        byte[]? found = SwfCarver.Find(blobs, swf.Length, Hash.Sha256(swf));

        Assert.NotNull(found);
        Assert.Equal(swf, found);
    }

    [Fact]
    public void Returns_null_when_nothing_matches()
    {
        byte[] swf = FakeSwf('F', 3000);
        Assert.Null(SwfCarver.Find([Embed(swf, 10, 10)], swf.Length, new string('0', 64)));
        Assert.Null(SwfCarver.Find([Embed(swf, 10, 10)], swf.Length + 100, Hash.Sha256(swf)));
        Assert.Null(SwfCarver.Find([], 10, Hash.Sha256(swf)));
    }

    [Fact]
    public void A_movie_at_the_very_end_of_a_blob_is_found()
    {
        byte[] swf = FakeSwf('C', 2048);
        Assert.NotNull(SwfCarver.Find([Embed(swf, 64, 0)], swf.Length, Hash.Sha256(swf)));
    }
}

public class CommandLineTests
{
    [Fact]
    public void No_arguments_means_interactive_install()
    {
        CommandLine c = CommandLine.Parse([]);
        Assert.True(c.Install);
        Assert.False(c.Uninstall);
        Assert.False(c.Quiet);
        Assert.Null(c.Language);
    }

    [Fact]
    public void Parses_quiet_install_with_options()
    {
        CommandLine c = CommandLine.Parse(["/install", "/quiet", "/names=zh,ja", "/savers=birthday,dch2", "/lang:en"]);
        Assert.True(c.Quiet);
        Assert.Equal(new[] { UiLanguage.Chinese, UiLanguage.Japanese }, c.NameLanguages);
        Assert.Equal(new[] { "birthday", "dch2" }, c.SaverIds);
        Assert.Equal(UiLanguage.English, c.Language);
    }

    [Fact]
    public void Parses_uninstall()
    {
        CommandLine c = CommandLine.Parse(["-uninstall", "-q"]);
        Assert.True(c.Uninstall);
        Assert.False(c.Install);
        Assert.True(c.Quiet);
    }

    [Theory]
    [InlineData("zh", "Chinese")]
    [InlineData("JA", "Japanese")]
    [InlineData("english", "English")]
    public void Parses_languages(string text, string expected)
    {
        Assert.Equal((UiLanguage)Enum.Parse(typeof(UiLanguage), expected), CommandLine.ParseLanguage(text));
    }

    [Fact]
    public void Unknown_languages_are_ignored()
    {
        Assert.Null(CommandLine.ParseLanguage("fr"));
        Assert.Empty(CommandLine.Parse(["/names=fr,xx"]).NameLanguages);
    }
}

public class SetupTextTests
{
    [Fact]
    public void Every_language_is_complete_with_placeholders_in_place()
    {
        foreach (UiLanguage language in Enum.GetValues(typeof(UiLanguage)).Cast<UiLanguage>())
        {
            SetupText text = SetupText.For(language);
            Assert.Equal(language, text.Language);
            foreach (var property in typeof(SetupText).GetProperties().Where(p => p.PropertyType == typeof(string)))
            {
                Assert.False(string.IsNullOrWhiteSpace((string?)property.GetValue(text)), $"{language}.{property.Name}");
            }

            Assert.Contains("{0}", text.Fetching);
            Assert.Contains("{1}", text.Fetching);
            Assert.Contains("{2}", text.DoneWithFailures);
            Assert.Contains("{0}", text.Error);
        }
    }

    [Fact]
    public void Summary_mentions_failures_and_active_saver()
    {
        SaverInfo a = SaverCatalog.All[0], b = SaverCatalog.All[1];
        var report = new InstallReport([a], [new SaverFailure(b, "offline")], @"C:\Windows\System32\X.scr");

        string summary = SetupForm.Summarize(SetupText.English, report);

        Assert.Contains(b.EnglishName, summary);
        Assert.Contains("X", summary);
    }
}

public class SystemIntegrationTests
{
    [Theory]
    [InlineData("ドラえもん誕生日記念SS", "僪儔偊傕傫抋惗擔婰擮SS")]
    [InlineData("ドラチャン2周年記念", "僪儔僠儍儞2廃擭婰擮")]
    [InlineData("映画ドラヒストリー1", "塮夋僪儔僸僗僩儕乕1")]
    [InlineData("映画ドラヒストリー2", "塮夋僪儔僸僗僩儕乕2")]
    public void Recreates_the_garbled_names_of_the_broken_originals(string japanese, string garbled)
    {
        Assert.Equal(garbled, SystemIntegration.MojibakeName(japanese));
    }

    [Fact]
    public void Only_rar_and_stuffit_need_unar()
    {
        Assert.True(Extractor.NeedsUnar(SourceKind.RarSfx));
        Assert.True(Extractor.NeedsUnar(SourceKind.StuffIt));
        Assert.False(Extractor.NeedsUnar(SourceKind.Zip));
        Assert.False(Extractor.NeedsUnar(SourceKind.LhaSfx));
    }
}

/// <summary>
/// End-to-end extraction on the real original downloads when a developer has them in originals\
/// (they are not in the repository, so elsewhere these tests have nothing to check).
/// </summary>
public class RealOriginalsTests
{
    private static string? FindOriginal(SaverInfo saver)
    {
        var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "originals")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            return null;
        }

        return Directory.EnumerateFiles(Path.Combine(dir.FullName, "originals"), "*", SearchOption.AllDirectories)
            .FirstOrDefault(f => new FileInfo(f).Length < 20_000_000 && Hash.Sha256File(f) == saver.Source.ArchiveSha256);
    }

    [Fact]
    public async Task Zip_and_lha_originals_yield_the_exact_movie()
    {
        string work = Path.Combine(Path.GetTempPath(), "dorasaver-extract-" + Guid.NewGuid().ToString("N"));
        using var downloader = new Downloader(Path.Combine(work, "cache"));
        var extractor = new Extractor(downloader, work);
        try
        {
            foreach (SaverInfo saver in SaverCatalog.All.Where(s => !Extractor.NeedsUnar(s.Source.Kind)))
            {
                string? original = FindOriginal(saver);
                if (original is null)
                {
                    continue;
                }

                IReadOnlyList<byte[]> blobs = await extractor.ExpandAsync(File.ReadAllBytes(original), saver.Source.Kind, saver.Id, CancellationToken.None);
                Assert.True(SwfCarver.Find(blobs, saver.Source.SwfLength, saver.Source.SwfSha256) is not null, saver.Id);
            }
        }
        finally
        {
            if (Directory.Exists(work))
            {
                Directory.Delete(work, recursive: true);
            }
        }
    }
}
