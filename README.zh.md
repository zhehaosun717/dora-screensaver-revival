# 哆啦A梦屏保复活计划

[English](README.md) | **中文** | [日本語](README.ja.md)

2000 年代，哆啦A梦的日本官方网站（ドラえもんチャンネル、映画ドラえもん等）免费发布过一批 Flash 屏保。Flash 退役以后，它们在今天的 Windows 上已经无法运行：会弹出找不到 `flash.ocx` 的错误，在中文系统上名字还会变成乱码。

这个项目让它们在 Windows 10 / 11 上重新动起来：

- 用开源 Flash 模拟器 [Ruffle](https://ruffle.rs) 播放原版动画，**不需要安装 Flash Player**。
- 支持宽屏：可以保持 4:3，也可以扩展画面、填满屏幕或拉伸。
- 屏保名字有中文、日文、英文三种，「设置」界面会跟着名字的语言走。
- **本仓库和安装程序都不包含任何哆啦A梦素材。** 安装时，程序会从互联网档案馆（Wayback Machine）下载官方网站当年发布的原始文件，在你的电脑上把动画提取出来。

## 收录的屏保（14 个）

| 年份 | 名称 | 日文原名 | 原发布网站 |
|---|---|---|---|
| 2001 | 哆啦A梦电影历史1 2001初版 | 映画ドラヒストリー1（ドラえもんワールド版） | dora-world.com |
| 2002 | 哆啦A梦机器人王国 | 映画ドラえもん ロボット王国 | dora-movie.com |
| 2003 | 哆啦A梦诞生日纪念（时钟） | ドラえもん誕生日記念SS | dora-world.com |
| 2003 | 哆啦A梦风子屏保 | フー子スクリーンセーバー | dora-movie.com |
| 2003 | 哆啦A梦风之使者 | 映画ドラえもん ふしぎ風使い | dora-movie.com |
| 2004 | 哆啦A梦频道2周年纪念 | ドラチャン2周年記念 | dora-world.com |
| 2004 | 哆啦A梦电影历史1 | 映画ドラヒストリー1 | dora-movie.com |
| 2004 | 哆啦A梦电影历史2 | 映画ドラヒストリー2 | dora-movie.com |
| 2004 | 哆啦A梦电影25周年 | 映画ドラえもん25周年 | dora-movie.com |
| 2004 | 帕门 剧场版2004 | パーマン ザ★ムービー2004 | dora-movie.com |
| 2006 | 哆啦A梦恐龙2006 翻牌 | のび太の恐竜2006 絵合わせ | doraeiga.com |
| 2006 | 哆啦A梦恐龙2006 问答 | のび太の恐竜2006 クイズ | dora2006.com |
| 2007 | 哆啦A梦新魔界大冒险 | のび太の新魔界大冒険 | doraeiga.com |
| 2010 | 哆啦A梦人鱼大海战 | のび太の人魚大海戦 | doraeiga.com |

## 安装

1. 从 [Releases](https://github.com/zhehaosun717/dora-screensaver-revival/releases/latest) 下载 `DoraSaverSetup.exe`，约 10 MB。
2. 双击运行。因为程序没有代码签名，Windows 可能会提示「Windows 已保护你的电脑」，点「更多信息」→「仍要运行」即可。之后会弹出管理员确认（UAC），因为屏保要装进 `C:\Windows\System32`。
3. 勾选想要的屏保和名字语言，点「安装」。程序会逐个下载、校验、提取，通常一分钟以内就能完成。
4. 点「打开屏保设置」，在列表里选一个就行。

**系统要求**：Windows 10（1903 或更新）或 Windows 11，需要联网。不需要另外安装任何运行时：.NET Framework 4.8 是系统自带的；Microsoft Edge WebView2 运行时在 Windows 11 上也是自带的，缺少时安装程序会自动从微软官网下载。

**命令行静默安装**（例如批量部署）：

```bat
DoraSaverSetup.exe /install /quiet /names=zh,ja
DoraSaverSetup.exe /uninstall /quiet
```

可用参数：`/names=zh,ja,en` 选择安装哪几种语言的名字；`/savers=birthday,dch2` 只安装指定的屏保；`/lang=zh|ja|en` 设置日志语言。退出码：0 表示成功，1 表示部分成功，2 表示失败。

## 使用

- 在「屏幕保护程序设置」里选中屏保后，点「设置」可以：
  - 打开或关闭声音（默认关闭）；
  - 选择宽屏显示方式：保持 4:3、扩展画面、填满屏幕（裁掉上下）或拉伸。每个屏保都有推荐的默认值。
- 动一下鼠标、点击或按任意键就会退出。
- 有多个显示器时，动画在主显示器上播放，其他显示器显示黑屏。

## 卸载

在「设置 → 应用 → 已安装的应用」里卸载「Doraemon Screensaver Revival」，或者再次运行 `DoraSaverSetup.exe` 并点「卸载」。

如果你的电脑上原来就装着那几个出错的旧版屏保，安装时它们会被移到 `C:\ProgramData\DoraSaver\original-backup`，不会被删除。

## 工作原理与安全性

- 每个原始文件都固定了互联网档案馆里的存档地址和 SHA-256 校验值。下载后校验不一致就会拒绝使用。
- **当年的安装程序一个都不运行。** 安装程序只把原始文件当作压缩包读取：zip 用 .NET 自带功能，LHa 用 Windows 自带的 `tar.exe`，只有 2001 初版的 RAR 2.0 和帕门的 StuffIt 格式会额外下载开源工具 [unar](https://theunarchiver.com/command-line)（同样校验 SHA-256）。随后按长度和 SHA-256 从中找出和记录完全一致的 Flash 动画。
- 屏保在 WebView2 中通过 Ruffle 播放，只允许访问本地的动画文件，所有外部网络访问和跳转都被禁止。

## 找不回来的屏保（征集）

以下官方屏保确认发布过，但在互联网档案馆和其他地方都没有找到文件。如果你还保存着，欢迎在 [Issues](https://github.com/zhehaosun717/dora-screensaver-revival/issues) 告诉我们：

| 年份 | 屏保 | 当年的文件名 |
|---|---|---|
| 2008 | 映画ドラえもん のび太と緑の巨人伝（福引プレゼント） | 不明 |
| 2009 | 映画ドラえもん 新・のび太の宇宙開拓史 ×2 | `dm09ss1w.zip`, `dm09ss2w.zip` |
| 2008 左右 | テレビ朝日 地球温暖化防止プロジェクト ×2 | `dora_clock_win_setup.exe.zip`, `dora_leaf_win_setup.exe.zip` |
| 2005 左右 | ドラえもん のび太のインターネット大冒険 | `dorarule_ss_setup` |
| 2012 | ドラえもんチャンネル 10周年 | 不明 |

## 从源代码构建

需要 .NET SDK 8 或更新版本。

```powershell
./build.ps1
```

构建脚本会下载固定版本的 Ruffle（校验 SHA-256）、运行测试、编译每个屏保，最后生成 `dist\DoraSaverSetup.exe`。

## 版权声明

哆啦A梦、帕门及相关角色、图像和动画的版权属于 © 藤子プロ・小学館・テレビ朝日・シンエイ・ADK 等权利人。本项目是粉丝自发的保存项目，与上述权利人无关，也没有得到他们的授权。本项目不分发任何屏保动画；动画由用户自己从公开的网页存档下载，仅供个人欣赏。当年的官方页面写着这些屏保仅供下载者本人在自己的电脑上使用，请遵守。如果权利人对本项目有异议，请通过 Issues 联系。

本项目的源代码以 [MIT 许可证](LICENSE) 发布。第三方组件见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

## 致谢

- [Ruffle](https://ruffle.rs)：让 Flash 内容在今天重新运行
- [互联网档案馆](https://archive.org)：保存了当年的网站和文件
- [The Unarchiver](https://theunarchiver.com)：让老式压缩格式仍然能打开
