# 译刻 1.1.0 · 本地离线整合版验收

2026-10-23（Asia/Shanghai），Windows x64 build 26300；Intel Core Ultra 9 275HX，32 GB 内存。推理仅使用 CPU，8 个生成线程。

本版完整安装包与便携包内置 Qwen3-1.7B Q4_K_M、llama.cpp b11796 CPU 引擎、.NET 和应用目录 Microsoft C++ DLL。首次启动默认进入本地模式并预加载模型，无需地址、API Key、模型名称、Ollama 或显卡配置。已有在线配置与加密密钥保留，可手动切换在线模式。

## 本轮结果

| 验证 | 结果 | 证据 |
| --- | --- | --- |
| 核心、协议与数据 | 73 通过，0 失败 | AUTOMATED_TESTS.txt |
| VS Code 扩展 | 13 通过，0 失败 | VSCODE_TESTS.txt |
| WPF 界面 | 24 通过，0 失败，1 跳过 | UI_TESTS.txt |
| 真实本地 CPU 推理 | 5 组固定样例，包含流式与整段输出 | LOCAL_MODEL_LIVE.json |
| 本地取消与恢复 | 中途取消后可再次翻译 | LOCAL_MODEL_LIVE.json |
| 引擎生命周期 | 正常释放及仅终止父进程后的自动清理均通过 | LOCAL_MODEL_LIVE.json |
| 应用目录运行库 | 3 个核心 Microsoft CRT 模块实际从随包目录加载 | LOCAL_MODEL_LIVE.json |
| 实际安装 1.0.4 → 1.1.0 | 安装成功；3 个用户数据文件逐字节相同；自启动未变 | SIGNED_UPGRADE_RESULTS.json |
| 实际安装后启动 | 32 个模型 / 引擎文件校验通过；主程序自动创建引擎；仅回环监听 | INSTALLED_LOCAL_RESULTS.json |
| 成品空环境首次运行 | 实际便携包在空配置、仅系统 PATH、禁用外部 .NET 查找下启动；界面本地翻译成功 | ZERO_ENVIRONMENT.md / ZERO_ENVIRONMENT_RESULTS.json |
| 闲置观察 | 应用启动约 310 秒后仍运行，所属引擎数量为 0 | LOCAL_IDLE_OBSERVATION.json |
| opens 签名 | 9 通过，0 失败 | SIGNATURE_TESTS.txt / SIGNATURE_RESULTS.json |
| 发布完整性与篡改拒绝 | 7 通过，0 失败 | RELEASE_VERIFICATION_TESTS.txt |

构建 0 警告、0 错误。原有生产 UIA 夹具因当前桌面未允许测试窗口成为前台而跳过，未强制抢占焦点；其余界面检查通过，包括剪贴板序号保留。深浅主题、本地设置页、在线设置页、语言与模型下拉框均生成真实 WPF 布局图片并查看，见 visual/。

## 本地实测与边界

本轮初始化约 2.1 秒；已加载后的短句首字约 0.2–0.3 秒，总计约 0.4–0.6 秒。测试文件可能处于系统缓存中，这些是本机样例结果，不能代表其他 CPU、低内存设备、长段落或冷启动。

真实样例包含英文到中文、中文到英文、英文到日文和法文，以及多行中文技术提示。英文会议句译为“会议明天上午九点开始。”，法文感谢句译为“Merci pour votre aide.”。也观察到措辞生硬及省略：日文样例省略了 window 的具体对象词，中文技术句的“不关闭窗口”不够自然。本地小模型更适合日常短句，不宣称全部 22 项语言已完成翻译质量验收。

本地模式通过应用创建的随机回环端口与临时鉴权连接自己的引擎，发送文本前核对监听 PID。禁用代理、重定向、网页界面、模型在线下载与生成日志；没有在线服务失败回退。未配置密钥时也能使用本地模式。真实检查验证了流式、整段、取消后重试、正常退出和异常父进程退出；后台闲置时释放模型，下次自动重新加载。

主程序、安装器和卸载器继续使用既有 CN=opens 自签名，RSA 3072、SHA-256 文件摘要与 DigiCert RFC3161 / SHA-256 时间戳。Windows 默认不信任该身份，状态为 self-signed-untrusted / UnknownError / 0x800B0109，未将 opens 加入受信任根。第三方引擎保持上游文件，通过固定 SHA-256 核验；Microsoft DLL 保留其原始 Microsoft 签名。私钥不随包提供。详情见 SIGNING.md、LOCAL_MODEL.md 和 THIRD_PARTY_NOTICES.md。

## 尚未覆盖

没有重新执行全部真实应用鼠标矩阵、整机禁网、干净系统、跨机器、多屏和完整读屏检查。VS Code 扩展仍为 1.0.2，历史真实选区结果保留在 HISTORY_1.0.2_TEST_REPORT.md。本轮源码包未再做独立重建；其构建脚本根据固定来源和摘要下载运行组件，成品包运行时不需要下载。在线服务原有的连接问题不属于本地模式依赖，未因此修改个人服务地址或密钥。
