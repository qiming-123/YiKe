# 离线研习词典

五个 .ykdict 文件由中文维基词典贡献者的词条经 Wiktextract / kaikki.org 提取。数据快照为 2026-11-01。译刻将释义、词形和例句重新整理并建立离线索引，词典数据按 CC BY-SA 4.0 分发。
- 上游数据与提取说明：https://kaikki.org/zhwiktionary/rawdata.html
- 条目及作者历史：https://zh.wiktionary.org/wiki/ ，按词头打开页面并查看历史。
- 许可证：https://creativecommons.org/licenses/by-sa/4.0/
- 构建方式：node scripts/build-study-dictionaries.cjs <中文词典JSONL.gz> <输出目录>

可在研习收藏本中从文件安装对应语言的 .ykdict，或使用该文件的 GitHub Raw HTTPS 直链。

## 从源码包验证研习功能

源码包内附 `de`、`es`、`fr`、`ja`、`ko` 五种语言的 `*-starter.json` 小型样例。完整 `.ykdict` 词典独立分发。

用 PowerShell 7 在解压后的根目录设置 `DOTNET_CLI_HOME` 和 `NUGET_PACKAGES`，使用 `global.json` 固定的 .NET SDK 运行：

```powershell
dotnet restore ./tests/YiKe.Tests/YiKe.Tests.csproj --locked-mode
dotnet build ./tests/YiKe.Tests/YiKe.Tests.csproj -c Release --no-restore
$tests = './tests/YiKe.Tests/bin/Release/net10.0-windows/win-x64/YiKe.Tests.exe'
& $tests --ui-regression 'Japanese pack is optional'
```

该用例从解压目录读取五种语言的样例，验证安装和查词。
