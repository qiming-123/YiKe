# 译刻 1.2.1 · 浏览器划词修复验收

2026-10-24（Asia/Shanghai）。Windows x64 build 26300，Intel Core Ultra 9 275HX，约 32 GB 内存，24 个逻辑处理器。真实桌面测试使用本机 Edge、固定本地 HTML 和 150% 显示缩放，不读取或发送用户网页内容。

用户截图中的普通网页已经选中文字，1.2.0 仍返回 Unsupported；这属于取词阶段失败，尚未进入翻译。仅有 UIA TextPattern 不足以覆盖这类浏览器界面，原有通用提示错误地附带 VS Code 建议。1.2.0 的桌面记录曾先使用辅助树工具，因此不能证明浏览器首次取词可靠；本轮增加独立浏览器进程、未预读辅助树的验证。

本轮改动：UIA 接受同一原生窗口的渲染子进程，使用有界 RawView 搜索；UIA 没有可读选区时尝试 MSAA / IAccessible2。兼容通道只读取明确的正向选区偏移，先核对两个鼠标端点、当前窗口、密码状态和长度，再读取所选文本；拒绝全文哨兵、嵌入对象占位符、返回长度不符或只能取得部分段落的情况。初次接口不支持或暂时空选区时各限一次短暂复查，保留独立进程、超时和取消。失败文案显示实际进程，仅 Code 使用编辑器建议，空原文不允许查看或重试。

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| 构建 | 0 警告，0 错误 | artifacts/build-1.2.1.log |
| 核心、协议、硬件策略和选区兼容边界 | 86 通过，0 失败 | AUTOMATED_TESTS.txt |
| WPF 界面 | 27 通过，0 失败，1 跳过 | UI_TESTS.txt |
| WPF 跳过项 | 测试窗口无法取得前台，未执行该窗口的 UIA 选区断言；不计通过 | UI_TESTS.txt |
| VS Code 扩展协议 | 13 通过，0 失败；扩展未修改 | VSCODE_TESTS.txt |
| IAccessible2 真实读取 | 完整读到固定英文句子，未带入周围段落 | artifacts/legacy-live-result.json |
| 独立浏览器进程首次读取 | 未预读辅助树；生产 SelectionReader / 辅助进程读到截图原句，约 233 ms | BROWSER_COLD_WORKER_RESULTS.json |
| 正式安装版自动划词 | 另一个独立浏览器进程，首次拖选截图原句，浮窗显示“固定合成文本用于选择测试。”和“翻译完成”，原选区保持；随后双击 meeting 显示“会议” | DESKTOP_1.2.1_RESULTS.json |
| 标准 / 轻量真实本地推理 | 各 5 组固定多语言样例，取消后重试、内存压力释放 / 恢复、正常 / 异常父进程退出清理通过 | LOCAL_MODEL_LIVE.json / LOCAL_MODEL_LIGHT_LIVE.json |
| 实际覆盖升级 1.2.0 → 1.2.1 | 安装成功；3 个设置 / 密钥文件字节相同，自启动不变；安装 EXE 匹配构建记录 | SIGNED_UPGRADE_RESULTS.json |
| 安装后离线运行组件 | 33 个文件逐个校验；引擎归应用所有，仅回环监听 | INSTALLED_LOCAL_RESULTS.json |
| opens 签名 | 9 通过，0 失败；含应用、安装器、实际卸载器及篡改拒绝 | SIGNATURE_RESULTS.json / SIGNATURE_TESTS.txt |
| 发布完整性 | 校验和、归档安全、源码清单、可执行输入及签名身份一致 | build-release / verify-release 输出 |

约 233 ms 仅为该机器的一次取词辅助进程调用，不包括 250 ms 防抖、模型加载或翻译时间，不能等同于所有电脑的端到端延迟。标准 / 轻量首次引擎加载本轮约 2.0 / 1.3 秒，文件可能已在系统缓存；低配置及内存压力使用注入快照，未冒充低配实体机器性能。

协议成功不代表翻译质量合格。轻量日文样例遗漏“关闭窗口前”的条件，部分简体中文目标出现繁体；标准样例也有省略。未宣称所有 22 项语言、复杂长句均通过质量验收。零开发环境、空配置成品测试保留为 1.2.0 的历史证据（ZERO_ENVIRONMENT_RESULTS.json），本轮不冒充重新执行该完整环境隔离流程。两版使用相同固定的离线模型、引擎及运行库。

浏览器会延迟提供辅助功能接口，参见 [Chromium Windows accessibility](https://www.chromium.org/developers/accessibility/windows-accessibility/)。兼容接口依据 [IAccessible2 AccessibleText.idl](https://github.com/LinuxA11y/IAccessible2/blob/master/api/AccessibleText.idl)；本工具的读取限制比接口能力更保守。没有开启读屏模式、改变浏览器安全 / 隐私设置或自动操作剪贴板。

本轮未覆盖所有浏览器 / 应用、跨段落复杂布局、真实多屏混合 DPI、干净 Windows、整机物理断网、实体低配机和长时间压力。VS Code 本轮仅重新执行协议自动测试，未把历史桌面结果算作新版本实测。opens 仍为自签名，Windows 默认不信任，不保证消除安全提示。历史 1.2.0 报告位于 HISTORY_1.2.0/。