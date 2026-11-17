# 译刻 1.2.2 · WPS 文字选区适配验收

2026-10-24（Asia/Shanghai）。Windows x64 build 26300，WPS Office 12.1.0.28488，Intel Core Ultra 9 275HX，约 32 GB 内存、24 个逻辑处理器、150% 显示缩放。本轮只使用自行创建的固定 RTF / DOCX 测试文档，未修改用户原文档。

用户截图属于 WPS 文字的真实选区，UIA / IAccessible2 通用路径未能读取。本轮新增只读 COM 适配，仅连接已有 Writer 实例，绝不创建另一个 WPS。实测 WPS 会在松开鼠标后从辅助进程显示浮动工具栏，覆盖选区端点；适配通过原生窗口所有者链确认该浮层属于源窗口，再验证选区的实际屏幕范围，避免误判。读取前后核对前台、进程、活动窗口 COM 身份、文档 COM 身份、选区类型、起止偏移与 story，变化时丢弃文本。

选区必须是正常连续文本，两个鼠标端点都需落在对应范围附近；插入点、空选区、列 / 整表行列 / 图形选择不读取 Text。读取前限制长度，返回长度与偏移不符、内嵌对象占位符或空字符均拒绝；按 2,000 Unicode 码点限制完整保留或拒绝，不截断。WPS 段落 / 软换行转换为换行。保留独立辅助进程、超时、取消和来源校验，不操作剪贴板、不改配置、不读全文。

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| 构建 | 0 警告，0 错误 | artifacts/build-1.2.2.log |
| 核心、协议、硬件和选区边界 | 95 通过，0 失败；包含新增 9 项 WPS 检查 | AUTOMATED_TESTS.txt |
| WPF 界面 | 27 通过，0 失败，1 跳过 | UI_TESTS.txt |
| 跳过项 | 自建 WPF 测试窗无法取得前台，未执行其 UIA 选区断言；WPS 真实桌面取词另行验证 | UI_TESTS.txt |
| VS Code 扩展协议 | 13 通过，0 失败；扩展未修改 | VSCODE_TESTS.txt |
| RTF 实际取词 | 完整英文句子，保留结尾换行 | WPS_SELECTION_RESULTS.json |
| DOCX 实际取词 | pair programmer、跨两个段落、汉字和表情均返回精确选区，无周围段落或截断 | WPS_SELECTION_RESULTS.json |
| 正式安装版自动翻译 | DOCX 拖选英文句子自动显示“会议明天上午九点开始。”和“翻译完成”，选区保留；pair programmer 也自动显示译文 | WPS_DESKTOP_1.2.2_RESULTS.json |
| 双模型真实推理 | 标准 / 轻量各 5 组样例，取消后重试、内存释放 / 恢复、正常 / 异常退出清理通过 | LOCAL_MODEL_LIVE.json / LOCAL_MODEL_LIGHT_LIVE.json |
| 实际升级与数据 | D:\YiKe 从 1.2.1 → 1.2.2；3 个设置 / 密钥文件字节相同，自启动值不变 | SIGNED_UPGRADE_RESULTS.json |
| 安装后离线组件 | 33 个运行文件逐个校验，引擎归应用所有且仅回环监听 | INSTALLED_LOCAL_RESULTS.json |
| opens 签名 | 9 通过，0 失败；含应用、安装器、实际卸载器及篡改拒绝 | SIGNATURE_RESULTS.json / SIGNATURE_TESTS.txt |
| 发布完整性 | 校验和、源码清单、归档安全、可执行输入和签名身份一致 | build-release / verify-release 输出 |

WPS 的固定样例独立辅助进程调用约 0.47–0.53 秒，包含首次 COM 客户端启动开销，不包含 250 ms 防抖、模型加载和翻译时间，不代表所有电脑速度。低配及内存压力仍使用注入快照，不冒充实体低配性能测试。小型本地模型有省略、繁简混用等质量限制，协议成功不代表所有 22 项语言均通过质量验收。

适配依据 WPS 官方的 [Selection 对象](https://open.wps.cn/documents/app-integration-dev/wps365/client/wpsoffice/jsapi/wps/Selection/obj) 和 [Window 对象](https://open.wps.cn/documents/app-integration-dev/wps365/client/wpsoffice/jsapi/wps/Window/obj)。官方特别说明插入点的 Text 可能返回旁边字符，因此必须先核对选择类型和端点。实现使用 Windows 自带 COM，不随包分发 WPS 二进制或要求用户安装开发环境。

边界：已验证本机 WPS 文字的普通 DOCX / RTF 连续文本。未验证 WPS PDF、演示、表格、图形、矩形列选区、复杂域 / 修订、多个独立 WPS 实例、其他 WPS 版本、另一台干净 Windows、物理断网和实体低配机。Edge 的冷启动划词实测来自 1.2.1（DESKTOP_1.2.1_RESULTS.json / BROWSER_COLD_WORKER_RESULTS.json）；本轮未冒充浏览器重新实测。完整零开发环境隔离测试来自 1.2.0，离线运行组件不变，本轮重新执行两档真实引擎及安装目录校验。历史 1.2.1 报告在 HISTORY_1.2.1/。

opens 仍为自签名，Windows 默认不信任；签名完整性通过不等于受信任发布者，也不保证消除系统安全提示。没有改变 Windows 或 WPS 的安全 / 隐私设置。