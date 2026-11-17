# 译刻 1.2.0 · 响应与硬件适配验收

2026-10-24（Asia/Shanghai）。Windows x64 build 26300，Intel Core Ultra 9 275HX，32 GB 标称内存 / 系统报告约 31.4 GiB，24 个逻辑处理器。低配置及内存压力通过注入硬件快照验证，实际模型推理仍在这台电脑执行，不冒充真实低配机器性能测试。

改动：鼠标监听从 UI 线程迁移到独立消息循环；无选区时短暂复查一次，UIA 查询去重，失败显示原因及粘贴入口；生成界面约每 120 ms 合并更新。双模型按硬件与空闲内存选档，低于门槛阻止加载；限制线程、上下文和批处理，降低进程优先级并关闭轮询。闲置重载复用未改变文件的校验记录。

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| 构建 | 0 警告，0 错误 | artifacts/build-1.2.0.log |
| 核心、协议和硬件策略 | 79 通过，0 失败 | AUTOMATED_TESTS.txt |
| WPF 界面 | 27 通过，0 失败，0 跳过 | UI_TESTS.txt |
| VS Code 扩展 | 13 通过，0 失败 | VSCODE_TESTS.txt |
| 标准 / 轻量真实推理 | 各 5 组多语言固定样例；流式、整段、取消后重试通过 | LOCAL_MODEL_LIVE.json / LOCAL_MODEL_LIGHT_LIVE.json |
| 内存压力与恢复 | 注入低空闲快照后，真实引擎释放；恢复后重新加载，未重复完整散列 | 同上 |
| 进程生命周期 | 正常退出及只终止父进程后引擎自动退出 | 同上 |
| 独立监听消息循环 | UI 忙碌 1150 ms 时，独立线程仍可处理消息 | UI_TESTS.txt |
| 实际桌面 | Edge 拖选英文句子出现中文浮窗；VS Code 默认编辑模式双击 changes 显示“更改” | DESKTOP_1.2.0_RESULTS.json |
| 成品空配置 / 无开发环境路径 | 实际发布 EXE 从包内运行，33 个离线文件校验，本地翻译成功 | ZERO_ENVIRONMENT_RESULTS.json |
| 实际覆盖升级 1.1.0 → 1.2.0 | 安装成功；3 个用户数据文件字节相同，自启动不变；安装 EXE 匹配构建记录 | SIGNED_UPGRADE_RESULTS.json |
| 安装后引擎 | 33 个运行文件校验，应用自己启动引擎且仅回环监听 | INSTALLED_LOCAL_RESULTS.json |
| opens 签名 | 应用、安装器及实际卸载器校验 | SIGNATURE_RESULTS.json / SIGNATURE_TESTS.txt |
| 发布完整性 | 校验和、源码清单、可执行输入、签名身份及全部离线文件一致 | build-release / verify-release 输出 |

标准档本轮首次加载约 2.1 秒，6 个线程，短句约 0.52–0.88 秒；轻量档约 1.1 秒，2 个线程，短句约 0.41–0.69 秒。文件可能已在系统缓存中，这些不代表冷启动或低配电脑速度。首次划词还包含 250 ms 防抖及源应用 UIA 响应，未测量可靠的整段端到端延迟。

协议成功不等于翻译质量合格。轻量日文样例遗漏“关闭窗口前”的条件，部分中文输出为繁体；标准样例也有遗漏对象和措辞生硬。未宣称全部 22 项语言通过质量验收。重要或复杂文本需结合原文核对。

独立监听改动遵循 [Microsoft LowLevelMouseProc 说明](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc)：安装钩子的线程需要消息循环，超时可能导致钩子被移除。硬件快照使用 [GlobalMemoryStatusEx](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-globalmemorystatusex)，空闲量会随系统负载变化；门槛属于本工具的保守策略。

仍使用 CN=opens 自签名，不向 Windows 受信任根添加证书，不能保证消除系统安全提示。应用、安装器、卸载器使用 SHA256 / RFC3161 时间戳；第三方引擎和模型按固定 SHA256 校验，Microsoft DLL 保留上游签名。

## 验收边界

本轮没有进行真实低配设备、另一台干净 Windows、整机断网、多屏 / 高 DPI 完整矩阵、长时间压力或全部桌面应用验证。VS Code 本轮确认双击选词，拖动选句未完成本轮验收；历史拖选桥接结果保留于 1.0.2 报告。没有独立重建新源码 ZIP。未开启读屏模式、未改变工作区信任或其他安全设置。全部配置和密钥保留，在线服务原有连接问题未纳入本轮修复。