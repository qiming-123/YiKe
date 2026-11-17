# 节能引擎测试误报内存不足修复

用户反馈选择本地离线翻译后，点击测试即显示内存不足。检查现有配置，已选择 LocalEngine=nmt（英译中节能引擎），目标简体中文。

根因：SettingsWindow.TestConnection 无条件对所有本地引擎应用 LocalHardware.Evaluate 的通用 Qwen 内存门槛（至少 1.75 GiB 当前空闲）。设置卡片已按 nmt 分支显示可用，测试按钮却仍被这个旧检查拦截。

修复：仅在测试通用模型时应用该门槛。节能引擎交给其原有组件校验和工作进程执行，不修改通用模型的保护门槛，不自动切换在线服务。

验证：新增可筛选的 WPF 回归检查。在 16 GiB 总内存、512 MiB 空闲的模拟硬件快照下，通过实际 TranslationProvider 测试本地节能翻译成功；切换通用引擎后仍提示 1.75 GB 内存不足。命令：YiKe.Tests.exe --ui-regression "NMT low memory"。这是模拟硬件快照，不是实际占满物理内存的压力测试。

另外使用安装目录 E:/translator/YiKe/translation-runtime 实际完成固定句子翻译，保留否定与时间条件。模型文件仍约 76 MiB，无需下载或换模型。历史内存测量见 MEMORY_1.8.0.md；本轮未重新测量峰值。

构建成功。NU1900 警告源于无法访问 NuGet 在线漏洞数据服务。安装备份与哈希记录见 artifacts/nmt-memory-fix-install-receipt.json。
