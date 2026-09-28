using System.Reflection;

namespace DoraSaver.Core;

internal enum LayoutMode
{
    /// <summary>Original 4:3 with black bars.</summary>
    Fit,
    /// <summary>Widen the stage and show whatever lies outside the original 4:3 area.</summary>
    Extend,
    /// <summary>Scale up until the screen is covered, cropping the overflow.</summary>
    Fill,
    /// <summary>Distort to exactly cover the screen.</summary>
    Stretch,
}

/// <summary>How the original download wraps the Flash movie.</summary>
internal enum SourceKind
{
    /// <summary>Zip archive holding a Windows installer that embeds the SWF.</summary>
    Zip,
    /// <summary>Self-extracting LHa archive (unpacked with the tar.exe that ships with Windows).</summary>
    LhaSfx,
    /// <summary>Self-extracting RAR 2.0 archive.</summary>
    RarSfx,
    /// <summary>Classic Mac StuffIt 5 archive (only the Mac version survives).</summary>
    StuffIt,
}

/// <summary>
/// Where the official file survives in the Internet Archive, and fingerprints proving that both the
/// download and the extracted Flash movie are exactly the original.
/// </summary>
internal sealed record SaverSource(
    string WaybackUrl,
    SourceKind Kind,
    string ArchiveSha256,
    string SwfSha256,
    int SwfLength);

internal sealed record SaverInfo(
    string Id,
    string SwfFile,
    string ChineseName,
    string JapaneseName,
    string EnglishName,
    int Year,
    LayoutMode DefaultLayout,
    SaverSource Source);

internal static class SaverCatalog
{
    public const string SaverIdMetadataKey = "SaverId";

    private const string Wayback = "https://web.archive.org/web/";

    public static IReadOnlyList<SaverInfo> All { get; } =
    [
        new("birthday", "clock_ss.swf", "哆啦A梦诞生日纪念", "ドラえもん誕生日記念SS", "Doraemon Birthday Clock", 2003, LayoutMode.Extend,
            new(Wayback + "20101125024055id_/http://dora-world.com/8th_anniv/ss/dch_ss.zip", SourceKind.Zip,
                "589fdb0135ef53a3e150b871838f91903e2b3982b4ccac729d5cb9ab7860b098",
                "d951149130b5d350ab5c9a1cacce4f200f638a3428094cc47eb3f8b17414bc60", 385507)),
        new("dch2", "dch2_ss.swf", "哆啦A梦频道2周年纪念", "ドラチャン2周年記念", "Doraemon Channel 2nd Anniversary", 2004, LayoutMode.Extend,
            new(Wayback + "20101125024405id_/http://dora-world.com/8th_anniv/ss/dch2_ss.zip", SourceKind.Zip,
                "4322c681aeb8bd5df728db2ac8a15172726ee88ef20c919bdb937b57ac89f988",
                "a0f241482a7f001bf88e89a3c6a1e66369d910eba1eae8235047fd2b05d0f1f8", 205096)),
        new("movie1", "dm01_ss.swf", "哆啦A梦电影历史1", "映画ドラヒストリー1", "Doraemon Movie History 1", 2004, LayoutMode.Fill,
            new(Wayback + "20101125024209id_/http://dora-world.com/8th_anniv/ss/dm01_ss.zip", SourceKind.Zip,
                "b0567e1faa6ec2c3c4c6b6c3bafee4719bbf81373227c3de99a47e9c9a57003e",
                "7bc6d63d4819ab4a47d5879f2fe1c3333b75e0003be9b432e0b856723fcd30ca", 2213789)),
        new("movie2", "dm02_ss.swf", "哆啦A梦电影历史2", "映画ドラヒストリー2", "Doraemon Movie History 2", 2004, LayoutMode.Fill,
            new(Wayback + "20101125024708id_/http://dora-world.com/8th_anniv/ss/dm02_ss.zip", SourceKind.Zip,
                "e1fb0a72fab8415299934068635d1b7e0572b22c41d8e8ac2de495975cb5f6fb",
                "8a3a0cb58729201cc2bdf271d6819e95f6cb27f90d3dc9b316827ab16f06e4dd", 1226246)),
        new("doraworld2001", "doraworld2001_ss.swf", "哆啦A梦电影历史1 2001初版", "映画ドラヒストリー1 2001年版", "Doraemon Movie History 1 (2001 Edition)", 2001, LayoutMode.Fill,
            new(Wayback + "20011003204623id_/http://dora-world.com:80/download/saver/doraemon.exe", SourceKind.RarSfx,
                "1677f379a932ce841589b5480d0678201137e579cf832335d48b557cd020a248",
                "16760c2c7855c75d44167be7c72d7d82adcebaccbeab43296452d8cbe606a486", 1007164)),
        new("robot2002", "robot2002_ss.swf", "哆啦A梦机器人王国", "映画ドラえもん ロボット王国", "Doraemon Robot Kingdom", 2002, LayoutMode.Extend,
            new(Wayback + "20031212102524id_/http://dora-movie.com:80/movie_23/quiz_rally/question/doramovie23.exe", SourceKind.LhaSfx,
                "0497cf8a3e70fa7909c936d0a33ad0f17fcaa7350ef003c19cc80a6d47bc3e75",
                "c7ec2ac998b9ce81c6401918bff3afcb0f8487d6bb3aee04d47566e548f156c5", 431272)),
        new("fuuko2003", "fuuko2003_ss.swf", "哆啦A梦风子屏保", "フー子スクリーンセーバー", "Doraemon Fuko", 2003, LayoutMode.Extend,
            new(Wayback + "20030805073145id_/http://dora-movie.com:80/movie_24/ss/dm24_ss2.exe", SourceKind.LhaSfx,
                "26576518c8c38b650e25a680643991844f8b61336215eeb62fdc751129f715fa",
                "3e37a3a6d3deee67f855b9b8c48ce046e640ed072be2d1ccf5502e31ccc35e9f", 203446)),
        new("kaze2003", "kaze2003_ss.swf", "哆啦A梦风之使者", "映画ドラえもん ふしぎ風使い", "Doraemon Windmasters", 2003, LayoutMode.Fill,
            new(Wayback + "20031016131736id_/http://dora-movie.com:80/movie_24/kaze_game/dm24_ss.exe", SourceKind.LhaSfx,
                "eb859bfbd66b5adde3fefd5dde2a02a4a5a67c56e4b2b4dde0cf95463f4d698e",
                "02b1ed08f3b6ad2bb332d999583e95f5d1eb53a609f8d0c1e5d483c4f328404e", 899841)),
        new("quiz25th2004", "quiz25th2004_ss.swf", "哆啦A梦电影25周年", "映画ドラえもん25周年", "Doraemon Movie 25th Anniversary", 2004, LayoutMode.Extend,
            new(Wayback + "20040209113640id_/http://www.dora-movie.com:80/quiz/ss/d_m_25th.exe", SourceKind.LhaSfx,
                "0c69c2d5e29f5fbb6ae945bc81bc5cc060ef3f708843198c1bc4a4c6380a2e55",
                "2397c9efced357a012eba8a37482044d078ad1d6b44e79b5143f30ee81a11420", 819546)),
        new("perman2004", "perman2004_ss.swf", "帕门 剧场版2004", "パーマン ザ★ムービー2004", "Perman The Movie 2004", 2004, LayoutMode.Extend,
            new(Wayback + "20150301183949id_/http://dora-movie.com/movie_25/perman/perman2004_ss.sit", SourceKind.StuffIt,
                "64ecea36d25e81c0b8abcdc0b23e74959eb7bd3374074549654c0212421e5711",
                "2275c4872aed50d82aff028b28bfc67e9c6498f61b08374333cab1838be7eef4", 140987)),
        new("dino2006card", "dino2006card_ss.swf", "哆啦A梦恐龙2006 翻牌", "のび太の恐竜2006 絵合わせ", "Doraemon Dinosaur 2006 Card Match", 2006, LayoutMode.Extend,
            new(Wayback + "20100101092050id_/http://doraeiga.com/2006/amuse/ss/game_card_ss_setup.zip", SourceKind.Zip,
                "c90a8f7aebf9f5b9f6f7c7e8b88c72eb40b2761382d637a0090ea9a06a4c57c1",
                "747b5831cc6672aa169ea05d34313f61e9106940f976ca1846a92f7ebd5f5250", 370611)),
        new("dino2006quiz", "dino2006quiz_ss.swf", "哆啦A梦恐龙2006 问答", "のび太の恐竜2006 クイズ", "Doraemon Dinosaur 2006 Quiz", 2006, LayoutMode.Fill,
            new(Wayback + "20060414223419id_/http://dora2006.com:80/quiz/quiz_ss_setup.zip", SourceKind.Zip,
                "d8ed4073c4475e50ac1a016ecf9416dfcd93fc63e5c0538b0cbd3afac5204ce6",
                "662b34bf0197229c298c338120efd7c87709279bcadfcee8a2f0127df3e093f0", 6516133)),
        new("makai2007", "makai2007_ss.swf", "哆啦A梦新魔界大冒险", "のび太の新魔界大冒険", "Doraemon New Great Adventure into the Underworld", 2007, LayoutMode.Extend,
            new(Wayback + "20150921031654id_/http://doraeiga.com/2007/magical/ss/dm07_ss2_win.zip", SourceKind.Zip,
                "35e402d3f49a61a2084a3899899fe52a2a1cceea9865eb0af1eaa13741e20b5b",
                "776d8fc43496b2b85633fe079bd00b4123f8d604f5f437003ac55df38baf1503", 238969)),
        new("mermaid2010", "mermaid2010_ss.swf", "哆啦A梦人鱼大海战", "のび太の人魚大海戦", "Doraemon Great Battle of the Mermaid King", 2010, LayoutMode.Fill,
            new(Wayback + "20100101071201id_/http://doraeiga.com/2010/ss/dm2010ss1_win.zip", SourceKind.Zip,
                "80853b67f0a28fa23e791a66c7aad2f77562c2ac77e678566fd861844599aa6b",
                "735fbfb681e130df13753c5b6e0d74febaebe4580e952dd2585b65cc254706d0", 1787200)),
    ];

    public static SaverInfo? FindById(string? id)
    {
        return All.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    public static SaverInfo? FindByFileName(string? fileNameWithoutExtension)
    {
        if (string.IsNullOrWhiteSpace(fileNameWithoutExtension))
        {
            return null;
        }

        return All.FirstOrDefault(s => s.Names().Any(n => string.Equals(n, fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase)));
    }

    public static IEnumerable<string> Names(this SaverInfo saver) => [saver.ChineseName, saver.JapaneseName, saver.EnglishName];

    public static string NameIn(this SaverInfo saver, UiLanguage language) => language switch
    {
        UiLanguage.Japanese => saver.JapaneseName,
        UiLanguage.English => saver.EnglishName,
        _ => saver.ChineseName,
    };

    /// <summary>
    /// The build-time id wins (it survives renaming the .scr); the file name is a fallback.
    /// </summary>
    public static SaverInfo Resolve(Assembly assembly, string? processPath)
    {
        string? buildId = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == SaverIdMetadataKey)?.Value;

        return FindById(buildId)
            ?? FindByFileName(Path.GetFileNameWithoutExtension(processPath))
            ?? All[0];
    }

    public static string ToQueryValue(LayoutMode layout) => layout.ToString().ToLowerInvariant();

    public static bool TryParseLayout(string? text, out LayoutMode layout)
    {
        layout = LayoutMode.Fit;
        return !string.IsNullOrWhiteSpace(text)
            && Enum.TryParse(text!.Trim(), ignoreCase: true, out layout)
            && Enum.IsDefined(typeof(LayoutMode), layout);
    }
}
