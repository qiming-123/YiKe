# 译刻 1.7.4 · 文件拖动误触发修复验收

2026-10-25（America/Denver，按客户端日期）。本轮针对桌面移动文件、拖动图标后反复出现“未能读取 explorer 的文字选区”错误卡片修复。此前翻译及计算机说明资料见 HISTORY_1.7.3/TEST_REPORT.md。

## 原因及修复

原鼠标跟踪器仅判断同一窗口内有至少 4 像素移动或双击，未区分文件项目与文字。桌面图标拖动 / 框选也触发选区读取；资源管理器缺少文字接口时，主程序把 Unsupported / Timeout / AccessDenied 全部显示为失败卡片。该提示不表示移动文件失败，也没有发送文件内容。

新版在鼠标按下时识别 Progman / WorkerW 桌面及原生编辑框，桌面非文本按下清除手势与双击历史，不启动取词。MouseDown 仍可关闭未固定旧浮窗。文件资源管理器按实际手势起点检查有界的 UIA 控件祖先链：文件列表、树、项目、表头、滚动条等为非文本；Edit 优先允许，保留重命名等嵌在项目中的编辑框，可读取的文档 / 预览仍可进入原选区流程。

确认的非文本返回 NotTextSelection，文本为空，不进行延迟重试、WPS 或 IA2 回退，也不读取文件名或旧焦点控件内容。Shell 非原生文本区域的接口失败保持安静，诊断仍可查看；原生重命名 / 地址 / 搜索输入框以及其他应用的失败提示保留。成功文本及超长选区继续按原规则处理，没有整体排除 explorer 进程。

不操作文件、剪贴板或系统设置，不改变模型、API、计算机说明、VS Code 或 WPS 功能。读取仍只取真实选区，不扩展到全文。

## 本轮验证

| 检查 | 结果及证据 |
| --- | --- |
| 核心回归 | 146 通过，0 失败；AUTOMATED_TESTS.txt |
| WPF 界面及生产选区通道 | 31 通过，0 失败，0 跳过；UI_TESTS.txt |
| VS Code 扩展协议 | 17 通过，0 失败；VSCODE_TESTS.txt |
| JavaScript 示例 | 6 通过，0 失败；EXAMPLE_CODE_TESTS.txt |
| 两档真实本地引擎 | 各 5 组语言链路，取消 / 重试、内存释放恢复、正常及父进程异常退出清理；LOCAL_MODEL_LIVE.json、LOCAL_MODEL_LIGHT_LIVE.json |
| 专业资料与模型补充 | 两档各 8 组；TECHNICAL_LIVE_STANDARD.json、TECHNICAL_LIVE_LIGHT.json |
| 普通词与扫雷句子 | 两档各 13 个词库词及 2 句；WORD_LIVE_STANDARD.json、WORD_LIVE_LIGHT.json；轻量句子仍有 2 项内容失败 |
| 任意词计算机说明 | 两档各 4 词，先完成译文再补充，轻量档不为查词 / 说明启动模型；ANY_WORD_LIVE_STANDARD.json、ANY_WORD_LIVE_LIGHT.json |
| opens 签名 | 应用、小安装器、实际卸载器与篡改拒绝等 9 项通过；SIGNATURE_RESULTS.json |
| 当前用户升级 | 1.7.3 → 1.7.4，退出码 0；设置 / 密钥逐字节、启动项和模型选择保留；SIGNED_UPGRADE_RESULTS.json |
| 已安装文件 | 与构建记录一致，运行文件散列通过，标准本地引擎仅监听回环；INSTALLED_LOCAL_RESULTS.json、INSTALLED_WORD_DICTIONARY.json |
| 完整离线安装器 | 隔离身份的同源码安装验收 13 通过、0 失败；OFFLINE_INSTALLER_1.7.4_RESULTS.json |

新增 4 项核心检查覆盖桌面拖动 / 框选 / 双击过滤、文件与空白列表不能复用旧文本、重命名与地址 / 搜索编辑保留、确定非文本仅查询一次且不返回旧文字。它们使用窗口类别、控件路径和运输层夹具，不是实际 Explorer 文件操作。

本轮生产 UIA 辅助进程成功读取真实自有 WPF 窗口中的选中文字，不使用剪贴板。其余界面检查含焦点保留、滚动、翻译与说明分阶段状态、取消和固定、设置及密钥保留。可视布局夹具见 visual/。

## 真实桌面检查限制

按 computer-use 技能尝试寻找资源管理器窗口，但 list_windows / list_apps 未返回可操作窗口；调用现有 explorer.exe 的启动请求返回“Computer Use app approval timed out”。因此没有执行真实文件拖放，也没有移动用户文件。没有用别的 UI 自动化绕过该限制。本报告不把策略测试、WPF 选区或安装测试称为真实 Explorer / 桌面图标拖放通过。

重命名与地址 / 搜索保留已通过策略回归；特定 Windows Explorer 版本和预览处理器的实际控件树仍有差异。非文本 Shell 区域接口失败现在安静记录，因此不支持的预览选区可能不弹错误卡片；支持且成功的真实文字继续翻译。非原生 XAML 文本输入在接口失败时也可能仅留下诊断，原生 Edit / RichEdit 则保留失败提示。

## 原有质量边界与交付

保留 764,830 个离线英汉词库键、165 项人工词义修订、219 组专业资料及 70 种命令 / 子命令。模型说明和词库不保证消歧或语义全部正确。轻量模型两条扫雷句子的反向动作 / mine 词义问题仍保存在 WORD_LIVE_LIGHT.json，未列作语义通过。任意词说明的格式检查不验证知识正确性，模型来源提示保留。

完整离线单 EXE 内含两档模型与 CPU / .NET / C++ 运行组件，模型不变。安装验收采用同一生成 Inno 源码的独立 InstallerSmoke 身份，覆盖选择、提取、散列、损坏修复、复用、偏好保留、真实模型运行与正式用户数据不变。没有关闭网络或防火墙，没有在另一台全新 Windows 上做完整 GUI 安装。

opens 为带 SHA-256 / RFC3161 时间戳的自签名身份，Windows 默认不信任，未增加信任根或分发私钥。首次资料归档时报告仍在整理，触发 docs/TEST_REPORT.md 源码散列不一致并停止归档校验；应用、安装器及其已验证可执行输入没有改变。最终在文档冻结后使用 PackageOnly 重新归档，复核可执行输入、签名应用、安装器与全部资料散列。
