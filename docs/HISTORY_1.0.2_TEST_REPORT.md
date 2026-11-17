# 译刻 1.0.2 · VS Code 兼容修复验收

2026-10-23（Asia/Shanghai），Windows x64 build 26300。当前仍为未签名验收候选版。

## 本次问题与修复

真实 VS Code 1.142.0 的默认编辑模式显示“The editor is not accessible at this time”，未向 UIA 提供可用代码选区。1.0.1 的纯 TextPattern 读取因此无法覆盖该场景。没有通过开启读屏模式来掩盖默认模式兼容问题。

1.0.2 内嵌可安装的 VS Code 本机扩展，通过官方 `onDidChangeTextEditorSelection` 与 `TextDocument.getText(range)` 获取本次鼠标选区。译刻确认拖选/双击、未暂停/排除/固定后才发本机请求。请求携带本次按下/释放时间和随机 nonce，不携带文本、文件名或路径。

适配仅连接当前前台 Code 进程树中同一可执行文件的扩展宿主；Windows 管道验证当前用户及实际服务器 PID。扩展核对焦点、编辑器、文档版本、选区位置和本次鼠标事件时间。键盘、命令、文件变化或窗口切换使旧元数据失效。读取前以选区偏移限制大小；最多 32 个选区、2,000 Unicode 码点。消息、连接、响应等待均有上限。失败继续按 UIA 支持程度判断，不复制剪贴板，不读取 DocumentRange。

扩展随单文件应用内嵌，无需用户另装 Node.js。设置提供安装/更新入口；安装器提供默认不勾选的适配任务，失败会明确提示重试。单独卸载译刻保留 VS Code 扩展；无译刻请求时扩展不读取选区。

官方接口依据：[VS Code API](https://code.visualstudio.com/api/references/vscode-api#window.onDidChangeTextEditorSelection)、[本机 UI 扩展宿主](https://code.visualstudio.com/api/advanced-topics/extension-host)、[VS Code 无障碍设置](https://code.visualstudio.com/docs/configure/accessibility/accessibility)。

## 当前真实交互证据

使用项目内合成文本，不读取用户代码内容。默认 `editor.accessibilitySupport` 未改动，VS Code 继续显示不可访问提示，处于 Restricted Mode。

| 操作 | 生产读取结果 | 耗时（不含 250 ms 防抖） | 焦点/选区 |
| --- | --- | --- | --- |
| 双击 translation | Success，11 码点，匹配合成原文 | 236 ms | 浮窗前后仍在 Code，选区保留 |
| 跨三行拖选，含中文、表情、数字 | Success，169 码点，匹配合成原文 | 226 ms | 被动浮窗出现，选区保留 |

证据位于 `vscode/default-double-click.json`、`vscode/default-cross-line-retry.json`。前一次从行首开始的输入没有形成实际新选区，返回 NoSelection，保留 `default-cross-line.json`，未计作通过。随后在编辑器内形成真实跨行选区后通过。

验收器使用生产 SelectionMonitor、SelectionReader、辅助进程和 PopupWindow，浮窗明确标记“适配验收，未调用在线翻译”。这是取词链路验收，不冒充真实在线译文。最终冻结版本的补测和本机升级结果另见 `vscode/` 中新增记录。

最终应用 SHA-256：`e6963748a170701d86a871d92813aa24b5ff48caa289abc1f5d0fa3c9e874d36`。冻结字节再次双击 preserve 成功（8 码点 / 261 ms），跨三行成功（169 码点 / 243 ms），均匹配合成原文并保持前台。见 `final-double-click.json`、`final-cross-line.json`。重新加载窗口后，VS Code 的默认 auto 模式自动检测到辅助技术，状态栏显示 Screen Reader Optimized；没有把 editor.accessibilitySupport 写为 on，初次双击/跨行成功证据来自尚未进入优化模式的窗口。

实际已安装 1.0.1 → 1.0.2 升级返回 0，配置及加密凭据共两个文件逐字节相同，自启动状态不变，安装应用匹配构建哈希。最终扩展 JS 与源码逐字节相同，包身份匹配；VS Code 安装时会向 package.json 加入自己的元数据，因此该文件以身份字段核对。见 `installed-upgrade.json`、`installed-extension.json`。扩展已重新加载，译刻已恢复托盘运行，合成测试标签已关闭，原有用户文档保留。

实际应用对合成单词触发了一次配置服务请求，但 TLS 握手失败，未取得译文。随后不带密钥或选区的服务根地址检查也以 AuthenticationException 失败；同一环境公开 Microsoft Learn HTTPS 对照返回 200。没有绕过证书检查、修改系统信任、服务配置或密钥。`service-connectivity.json` / `tls-diagnostic.json` 未记录服务地址、原文或凭据。这是服务连接的未解决限制，不计为在线翻译成功。

## 自动回归与构建

- 核心测试：60 项；新增进程树/循环隔离、管道协议与 nonce、Unicode/失败文本隔离、内嵌 VSIX 完整性测试。最终输出见 `AUTOMATED_TESTS.txt`。
- 扩展测试：13 项；真实选区范围、焦点/编辑器/版本/签名变化、时间新鲜度、长拖选、Unicode 边界、空白/超长/多选区、键盘与窗口事件清理、分片/畸形/超长本机请求。见 `VSCODE_TESTS.txt`。
- WPF 回归：13 通过、0 失败、1 跳过；执行桌面不允许自建 UIA 测试窗口成为前台。剪贴板序号检查本轮通过。见 `UI_TESTS.txt`。
- 完整发布校验和故意篡改检查见 `RELEASE_VERIFICATION_TESTS.txt`。源码输入包含内嵌扩展及许可证，PackageOnly 不允许在改变可执行输入后复用旧构建。
- 完整包的应用、安装器、源码与便携包绑定同一构建记录，哈希及签名状态以 `dist/BUILD-MANIFEST.json`、`SHA256SUMS.txt` 为准。
- 源码 ZIP 在同机另一中文空格目录重新恢复、构建、60 核心测试、13 扩展测试、自包含发布及 Inno 编译通过；重建 EXE 哈希不同，属于功能重建验证，不能宣称字节级可复现。见 `SOURCE_BUILD_RESULTS.json`。

历史 1.0.1 的 56 核心、12 UI 通过/2 跳过、安装矩阵、4 项便携场景、源码 ZIP 重建和 Edge/记事本证据保留在 `HISTORY_1.0.1_TEST_REPORT.md` 及对应带版本记录中；这些历史结果不等同于本轮全部重新执行。

## 边界

本次验收范围是 Windows 桌面 VS Code 的普通文本编辑器。网页版、终端、自定义编辑器、PDF、Insiders、远程工作区、多编辑组多选区的真实交互尚未验收；多选区及窗口隔离逻辑已有自动回归。其他应用仍取决于其 UIA 提供者。

真实在线服务与翻译质量、干净机器、重启 Windows、跨账户/电脑、真实混合 DPI、完整键盘/读屏、交互安装器全页面等仍未覆盖。未签名状态未改变。不宣称所有软件或任务书全部验收通过。
