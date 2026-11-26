<p align="center">
  <img src="../assets/yike-icon-preview.png" width="96" alt="YiKe app icon">
</p>
<h1 align="center">YiKe · 译刻</h1>
<p align="center"><strong>Translate selected text on Windows with offline models or optional online services.</strong></p>
<p align="center">
  <a href="../README.md">简体中文</a> · <strong>English</strong>
</p>
<p align="center">
  <a href="https://github.com/qiming-123/YiKe/releases/latest"><strong>Download latest release</strong></a> ·
  <a href="#getting-started">Getting started</a> ·
  <a href="USER_GUIDE.md">User guide (Chinese)</a> ·
  <a href="https://github.com/qiming-123/YiKe/issues">Report an issue</a>
</p>

---

## Getting started

1. Download and run the Windows x64 installer from [GitHub Releases](https://github.com/qiming-123/YiKe/releases/latest).
2. Open YiKe and select text in a supported application. YiKe can translate immediately or show a small icon you click to translate.
3. For typed or pasted text, open Manual Translation from the main window or tray menu and choose the source and target languages.

The published 2.0.0 installer requires Windows 11 x64, build 22631 or later. The installer in the main branch targets Windows 10 22H2 x64, build 19045, and later Windows versions.

## Translation options

| Service | When to use it |
| --- | --- |
| Built-in offline engine | Translate English to Simplified Chinese without downloading an additional model. |
| Optional local multilingual model | Translate other language pairs offline. This option needs more available RAM. |
| DeepL or an OpenAI-compatible service | Use your own provider credentials online. YiKe does not automatically switch to an online service when local translation fails. |

## Dictionaries and study

- Enable English Study to look up pronunciations, definitions, and phrases, play supported Windows voices, or save words for review.
- Download Japanese, Korean, French, German, or Spanish study dictionaries when you need them.
- Open the vocabulary notebook to review, remove, or export saved words. YiKe does not keep a translation history by default.
- Enable Technical Notes for local computing terminology, commands, and examples.

## Documentation and source

Regular `dotnet build` builds show `2.0.0-dev` in Settings. Include the short Git commit hash when reporting issues with a development build. The release script uses the supplied three-part version.

- [Quick start in Simplified Chinese](QUICK_START.md)
- [Full user guide in Simplified Chinese](USER_GUIDE.md)
- [Local model details](LOCAL_MODEL.md)
- [Build instructions in the Simplified Chinese README](../README.md#源码构建)
- [License](../LICENSE) and [third-party notices](../THIRD_PARTY_NOTICES.md)
