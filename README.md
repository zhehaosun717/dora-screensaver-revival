# Doraemon Screensaver Revival

**English** | [中文](README.zh.md) | [日本語](README.ja.md)

In the 2000s, the official Japanese Doraemon websites (Doraemon Channel, the Doraemon movie site and others) gave away a series of Flash screensavers. Since Flash was retired, they no longer run on current Windows. They fail with a missing `flash.ocx` error, and on non-Japanese systems their names turn into garbled text.

This project brings them back to life on Windows 10 and 11:

- The original animations play in [Ruffle](https://ruffle.rs), an open-source Flash emulator. **No Flash Player needed.**
- Widescreen support: keep the original 4:3, extend the picture, fill the screen, or stretch.
- Screensaver names come in English, Japanese and Chinese, and each screensaver's Settings dialog follows the language of its name.
- **This repository and the installer contain no Doraemon assets.** At install time, the installer downloads the original files the official websites released from the Internet Archive's Wayback Machine and extracts the animations on your own PC.

## Included screensavers (14)

| Year | Name | Original Japanese title | Released on |
|---|---|---|---|
| 2001 | Doraemon Movie History 1 (2001 Edition) | 映画ドラヒストリー1 (Doraemon World edition) | dora-world.com |
| 2002 | Doraemon Robot Kingdom | 映画ドラえもん ロボット王国 | dora-movie.com |
| 2003 | Doraemon Birthday Clock | ドラえもん誕生日記念SS | dora-world.com |
| 2003 | Doraemon Fuko | フー子スクリーンセーバー | dora-movie.com |
| 2003 | Doraemon Windmasters | 映画ドラえもん ふしぎ風使い | dora-movie.com |
| 2004 | Doraemon Channel 2nd Anniversary | ドラチャン2周年記念 | dora-world.com |
| 2004 | Doraemon Movie History 1 | 映画ドラヒストリー1 | dora-movie.com |
| 2004 | Doraemon Movie History 2 | 映画ドラヒストリー2 | dora-movie.com |
| 2004 | Doraemon Movie 25th Anniversary | 映画ドラえもん25周年 | dora-movie.com |
| 2004 | Perman The Movie 2004 | パーマン ザ★ムービー2004 | dora-movie.com |
| 2006 | Doraemon Dinosaur 2006 Card Match | のび太の恐竜2006 絵合わせ | doraeiga.com |
| 2006 | Doraemon Dinosaur 2006 Quiz | のび太の恐竜2006 クイズ | dora2006.com |
| 2007 | Doraemon New Great Adventure into the Underworld | のび太の新魔界大冒険 | doraeiga.com |
| 2010 | Doraemon Great Battle of the Mermaid King | のび太の人魚大海戦 | doraeiga.com |

## Install

1. Download `DoraSaverSetup.exe` (about 10 MB) from [Releases](https://github.com/zhehaosun717/dora-screensaver-revival/releases/latest).
2. Run it. The installer is not code-signed, so Windows may show "Windows protected your PC". Click "More info", then "Run anyway". You will also see an administrator (UAC) prompt, because screensavers are installed into `C:\Windows\System32`.
3. Pick the screensavers and name languages you want, then click Install. Each one is downloaded, verified and extracted in turn, usually in under a minute.
4. Click "Open Screen Saver Settings" and choose one from the list.

**Requirements:** Windows 10 (version 1903 or later) or Windows 11, and an internet connection. Nothing else needs to be installed. .NET Framework 4.8 is built into Windows. The Microsoft Edge WebView2 Runtime is built into Windows 11; if it is missing, the installer downloads it from Microsoft.

**Silent install from the command line** (for example, when deploying to several PCs):

```bat
DoraSaverSetup.exe /install /quiet /names=en
DoraSaverSetup.exe /uninstall /quiet
```

Options:

- `/names=en,ja,zh` chooses which name languages to install.
- `/savers=birthday,dch2` installs only the listed screensavers.
- `/lang=en|ja|zh` sets the log language.

Exit codes: 0 means success, 1 means partial success, 2 means failure.

## Using them

- After selecting one in Screen Saver Settings, click Settings to:
  - turn sound effects and music on or off (off by default);
  - choose the widescreen mode: keep 4:3, extend, fill the screen (cropping top and bottom), or stretch. Each screensaver has a recommended default.
- Move the mouse, click or press any key to exit.
- With several monitors, the animation plays on the main one and the others go black.

## Uninstall

Uninstall "Doraemon Screensaver Revival" from Settings → Apps → Installed apps. Alternatively, run `DoraSaverSetup.exe` again and click Uninstall.

If the old, broken versions were still on your PC, they are moved to `C:\ProgramData\DoraSaver\original-backup` during installation, never deleted.

## How it works, and why it is safe

- For every original file, the Internet Archive location and its SHA-256 hash are pinned. A download that does not match is rejected.
- **None of the old installers is ever run.** Their downloads are only read as archives:
  - zip archives are opened with .NET's built-in support, and LHa archives with Windows' built-in `tar.exe`;
  - only the 2001 edition (RAR 2.0) and Perman (StuffIt) also fetch the open-source [unar](https://theunarchiver.com/command-line) tool, again verified by SHA-256.

  The installer then keeps only the Flash animation whose length and SHA-256 exactly match the record.
- The screensavers play in Ruffle inside WebView2. They can only read the local animation files; all outside network access and navigation are blocked.

## Missing screensavers: can you help?

The official screensavers below are known to have existed, but no copy has turned up in the Internet Archive or anywhere else. If you still have one, please let us know in [Issues](https://github.com/zhehaosun717/dora-screensaver-revival/issues).

| Year | Screensaver | Original file name |
|---|---|---|
| 2008 | 映画ドラえもん のび太と緑の巨人伝 (prize-draw gift) | unknown |
| 2009 | 映画ドラえもん 新・のび太の宇宙開拓史 ×2 | `dm09ss1w.zip`, `dm09ss2w.zip` |
| c. 2008 | TV Asahi global warming prevention project ×2 | `dora_clock_win_setup.exe.zip`, `dora_leaf_win_setup.exe.zip` |
| c. 2005 | ドラえもん のび太のインターネット大冒険 | `dorarule_ss_setup` |
| 2012 | Doraemon Channel 10th anniversary | unknown |

## Building from source

Requires the .NET SDK 8 or later.

```powershell
./build.ps1
```

The script downloads a pinned Ruffle build and checks its SHA-256, runs the tests, builds every screensaver, and writes `dist\DoraSaverSetup.exe`.

## Copyright

Doraemon, Perman and their characters, artwork and animations are © Fujiko-Pro, Shogakukan, TV Asahi, Shin-Ei, ADK and other rights holders. This is an unofficial fan preservation project. It is not affiliated with or endorsed by them.

This project does not distribute any of the screensaver animations. Users download them themselves from public web archives, for personal enjoyment only. The official pages of the time said each screensaver was for use only on the PC it was downloaded to; please respect that. Rights holders with concerns are welcome to contact us through Issues.

The source code is released under the [MIT License](LICENSE). For third-party components, see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Thanks

- [Ruffle](https://ruffle.rs), for making Flash content run again
- [The Internet Archive](https://archive.org), for preserving the original websites and files
- [The Unarchiver](https://theunarchiver.com), for keeping old archive formats readable
