using System.Globalization;

namespace DoraSaver.Core;

internal enum UiLanguage
{
    Chinese,
    Japanese,
    English,
}

/// <summary>All user-visible text of the settings dialog, in one language.</summary>
internal sealed record UiText(
    UiLanguage Language,
    string FontName,
    string TitleFormat,
    string PlaySound,
    string LayoutGroup,
    string Fit,
    string Extend,
    string Fill,
    string Stretch,
    string Recommended,
    string Footer,
    string Ok,
    string Cancel,
    string MissingAssetsFormat,
    string SaveFailedFormat)
{
    public static UiText Chinese { get; } = new(
        UiLanguage.Chinese,
        "Microsoft YaHei UI",
        "{0} 设置",
        "播放声音和背景音乐",
        "宽屏显示方式",
        "保持原比例 4:3（两侧黑边）",
        "扩展画面（显示原画面外的背景，不裁切）",
        "填满屏幕（放大并裁掉上下）",
        "拉伸填满（画面会变形）",
        "★推荐",
        "动画由开源 Flash 模拟器 Ruffle 播放，无需安装 Flash\u00A0Player。",
        "确定",
        "取消",
        "找不到动画文件，请重新安装（需要 {0}）。",
        "保存设置失败：{0}");

    public static UiText Japanese { get; } = new(
        UiLanguage.Japanese,
        "Yu Gothic UI",
        "{0} の設定",
        "効果音・BGM を再生する",
        "ワイド画面での表示",
        "元の比率 4:3 のまま（左右に黒い帯）",
        "画面を広げる（元の画面の外側の背景も表示、切り取りなし）",
        "画面いっぱいに拡大（上下を切り取り）",
        "引き伸ばして全画面（絵がゆがみます）",
        "★おすすめ",
        "アニメーションはオープンソースの Flash\u00A0エミュレーター Ruffle で再生しています。Flash\u00A0Player は不要です。",
        "OK",
        "キャンセル",
        "アニメーションのファイルが見つかりません。再インストールしてください（{0} が必要です）。",
        "設定を保存できませんでした：{0}");

    public static UiText English { get; } = new(
        UiLanguage.English,
        "Segoe UI",
        "{0} Settings",
        "Play sound effects and music",
        "Widescreen display",
        "Keep the original 4:3 (black bars at the sides)",
        "Extend (show the background beyond the original frame, no cropping)",
        "Fill the screen (zoom in, crop top and bottom)",
        "Stretch to fill (distorts the picture)",
        "★Recommended",
        "Played by Ruffle, an open-source Flash emulator. No Flash\u00A0Player needed.",
        "OK",
        "Cancel",
        "Animation files not found. Please reinstall (expected in {0}).",
        "Could not save settings: {0}");

    public static UiText For(UiLanguage language) => language switch
    {
        UiLanguage.Japanese => Japanese,
        UiLanguage.English => English,
        _ => Chinese,
    };

    public string LayoutLabel(LayoutMode mode) => mode switch
    {
        LayoutMode.Extend => Extend,
        LayoutMode.Fill => Fill,
        LayoutMode.Stretch => Stretch,
        _ => Fit,
    };

    /// <summary>
    /// The .scr's own name decides the language (a Japanese-named saver speaks Japanese even on a
    /// Chinese system); otherwise follow the Windows display language, falling back to English.
    /// </summary>
    public static UiLanguage Detect(string? fileNameWithoutExtension, CultureInfo uiCulture)
    {
        if (!string.IsNullOrWhiteSpace(fileNameWithoutExtension))
        {
            foreach (SaverInfo saver in SaverCatalog.All)
            {
                if (string.Equals(saver.JapaneseName, fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return UiLanguage.Japanese;
                }

                if (string.Equals(saver.ChineseName, fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return UiLanguage.Chinese;
                }

                if (string.Equals(saver.EnglishName, fileNameWithoutExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return UiLanguage.English;
                }
            }
        }

        return uiCulture.TwoLetterISOLanguageName switch
        {
            "ja" => UiLanguage.Japanese,
            "zh" => UiLanguage.Chinese,
            _ => UiLanguage.English,
        };
    }
}
