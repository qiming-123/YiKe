# 两人协作与当前开发版

共同仓库：qiming-123/YiKe。main 保留共同确认的代码；每个功能或修复新建分支，提交后通过 Pull Request 检查、测试和合并。合并后另一位开发者拉取 main。不要用整目录覆盖对方的工作。

本次从导入的 1.7.8 源码升级到本地 1.8.3 开发状态，包括英译中节能引擎、PDF 整理与复制改进、双片玻璃“译”图标、YK 主界面和节能测试误用通用模型内存门槛的修复。界面迭代目录 1.8.4-material 是设计编号；当前程序集版本仍为 1.8.3，尚未建立新的正式发布包。

## 准备环境

Windows x64；安装 Node.js 后，按 README 的顺序运行 bootstrap-tools、bootstrap-dictionary、bootstrap-local、bootstrap-nmt。下载组件均有来源锁定与哈希校验。通用 Qwen 模型可以暂缓准备；英译简中日常开发使用节能引擎。

assets/word-dictionary.dat 是约 100 MiB 的生成资源，超过普通 Git 单文件限制，不纳入 Git。首次构建前运行 scripts/bootstrap-dictionary.ps1，它根据固定 ECDICT 版本下载源码并生成当前结构化词库；也可以用 -Csv 指定已有的匹配 CSV。不要使用旧格式词库构建新版。词库元数据和生成脚本仍受版本控制。

模型、运行环境、工具安装目录和编译产物不纳入 Git。个人配置、凭据和签名私钥也不上传。模型和词库保存在本机，发布产物另行制作。

## 验证

使用 .tools/dotnet/dotnet.exe build tests/YiKe.Tests/YiKe.Tests.csproj -c Release 构建。可执行 YiKe.Tests.exe --ui-regression "NMT low memory" 检查节能测试的内存分支；--visual 生成布局截图；--nmt-180 后接 translation-runtime 目录，运行固定样例的实际离线翻译。

本次不把已安装程序、机器相关安装回执和本机快捷方式维护脚本发布为项目源码。图标通过 scripts/generate-brand.ps1 从 tools/YiKe.Brand 的共享矢量绘制重新生成。
