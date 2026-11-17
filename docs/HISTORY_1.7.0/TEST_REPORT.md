# 译刻 1.7.0 · VS Code 终端与命令用法验收

2026-10-24（Asia/Shanghai），Windows x64 build 26300，约 32 GB 内存、24 个逻辑处理器；终端目标为 VS Code 1.143.1。

新增显式 Ctrl+Alt+Y 终端复制入口和 70 种命令 / 子命令的离线详细用法，保留 219 组原术语与示例。命令资料包括适用环境、用途、语法、常用参数、选中参数、示例、效果及修改影响；未知参数明确标记，不猜测。对提示符、引号、短选项组合、值、简单管道和重定向作有界说明，不是完整 shell 解析器，不展开变量、不执行用户输入，也不调用外部帮助程序。

终端入口会使用 Code 原生“复制选区”，更新剪贴板，不还原旧内容或清除系统历史。复制前后校验当前窗口、认证命名管道、nonce / 时效及终端身份；读取前检查新的剪贴板序列与 Code 所属 owner，按 2,000 个 Unicode 码点限长。无选区不读取旧剪贴板，不读取完整输出、滚屏、历史、文件或环境。自动编辑区划词仍不修改剪贴板。暂停、固定、排除、取消和过时结果丢弃继续生效。

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| 构建 | 0 警告、0 错误 | artifacts/build-1.7.0-final.log |
| 核心回归 | 126 通过，0 失败；包含全部命令资料、引用参数、提示符、管道、重定向、未知参数、认证应答和剪贴板新鲜度 | AUTOMATED_TESTS.txt |
| VS Code 协议 | 17 通过，0 失败；编辑区原规则、显式终端复制、过期请求、失焦、终端切换和不读取编辑器 / 剪贴板 | VSCODE_TESTS.txt |
| 可信 JavaScript 示例语义 | 6 通过，0 失败 | EXAMPLE_CODE_TESTS.txt |
| WPF / UIA | 30 通过，0 失败，0 跳过；选区、焦点、自动浮窗剪贴板序列、设置及滚动行为 | UI_TESTS.txt |
| 命令排版 | 浅色 / 深色命令正文和底部示例已渲染并查看；滚动区有界，操作栏保持可用 | visual/light-terminal-command.png / visual/dark-terminal-example.png |
| 真实终端链路 | VS Code 1.143.1；原生 Ctrl+Alt+Y 消息、原生复制、认证管道、选中参数解释、空选区不复用旧剪贴板、超长拒绝、前台保持 | VSCODE_TERMINAL_LIVE.json |
| 安装后终端入口 | 使用真实安装版及隔离合成终端按快捷键，应用诊断显示 Success；设置只读查看，关闭未保存 | INSTALLED_TERMINAL_RESULTS.json |
| 真实专业解释 | 标准与轻量各 8 组，包括 chmod、g++、PowerShell、DNS、未知词和普通句；每次仅一条模型请求 | TECHNICAL_LIVE_STANDARD.json / TECHNICAL_LIVE_LIGHT.json |
| 本地翻译基线 | 两档各 5 组语言样例及取消、重试、内存释放 / 恢复、正常 / 异常退出清理 | LOCAL_MODEL_LIVE.json / LOCAL_MODEL_LIGHT_LIVE.json |
| 实际升级 | 1.6.0 → 1.7.0，安装退出码 0；3 个设置 / 密钥文件逐字节相同，模型选择与启动值保留 | SIGNED_UPGRADE_RESULTS.json |
| 已安装组件 | 标准模型与 31 个引擎 / 运行文件共 32 个全部通过散列校验，自有引擎仅回环监听 | INSTALLED_LOCAL_RESULTS.json |
| 已安装适配 | 1.1.0，脚本与源码逐字节一致；已有窗口需重新加载扩展宿主 | vscode/installed-extension.json |
| opens 签名 | 9 通过，0 失败；应用、安装器、实际卸载器、时间戳、篡改拒绝及生产 worker | SIGNATURE_RESULTS.json / SIGNATURE_TESTS.txt |
| 交付完整性 | 原始构建发布验证通过；资料补齐后通过 PackageOnly 重新归档并验证源码、散列、归档和签名身份 | BUILD-MANIFEST.json / SHA256SUMS.txt；artifacts/package-1.7.0-final.log |

真实终端检查在独立配置的空白 Code 窗口创建 Pseudoterminal，只输出预设测试文字，没有 shell 进程，不在终端输入或执行命令。原生复制得到 grep 例子，清空选区后保持原剪贴板序列，超长选区被拒绝。没有操作用户原 Code 项目或配置，没有启用试验 API、读屏模式或调试端口。正式安装版另外验证实际快捷键和取词诊断；其浮窗没有作为独立真实屏幕视觉认证，排版由实际 WPF 渲染图和 30 项 UI 检查验证。

没有在所有 shell、发行版、语言和工具版本上逐条执行示例；命令可用性仍依赖实际环境。CMD / PowerShell / Bash 同名命令的差异在资料中说明，部分命令会写文件、联网或执行项目脚本，示例注明影响。示例仅展示，普通文字仍按当前目标语言翻译；模型补充仍可能不完整或错误。标准模型或在线服务按设置补充未知内容，轻量模型保留本机资料，不为此自动下载模型、切换到在线服务或增加第二次请求。

本轮没有干净 Windows、实体低配、多种远程工作区或全部第三方应用的完整矩阵验收。安装模型选择、硬件策略、WPS / Edge 逻辑未变；历史验收不冒充本轮重新执行。1.6.0 报告和结果见 HISTORY_1.6.0/，使用及来源见 PROGRAMMING_TERMS.md 和 VSCODE_TERMINAL.md。

opens 仍为自签名，完整性和时间戳通过不等于 Windows 默认信任。私钥未进入交付物，未修改系统信任根或安全设置。