# 实现说明

一个 WPF 应用项目、一个可执行测试项目。主程序与 `--uia-worker` 取词模式使用同一自包含 EXE，不额外部署依赖。

- `SelectionMonitor`：UI 线程低级鼠标钩子及消息循环；回调只记录按下/释放、来源 HWND/PID/坐标、拖动或双击与序号，投递 Dispatcher。要求同一来源窗口的左键按下/释放配对，右键/中键取消旧坐标；双击使用单调时钟，不把一次拖选算作前一次点击。普通点击不生成选区任务。退出释放钩子。
- `SelectionReader`：后台调度整个入口，避免异步方法首次等待同步完成时在 UI 线程启动进程；串行访问一个本工具拥有的辅助进程，通过重定向标准输入/输出传 JSON，不落原文日志。UIA 在辅助进程专用 MTA 线程执行。1,800 ms 逻辑超时或新事件取消时回收该辅助进程；最多一个查询进程，无无限线程。父进程 PID 与启动时间共同绑定，退出后辅助进程跟随退出。EOF/损坏数据重置进程；释放失败时保留所有权，不无限创建替代进程。
- 通用取词路径用 `TextPattern.GetSelection()`，检查密码、同 PID、有限 7 层祖先、来源窗口和选区几何位置；不访问 `DocumentRange`、不复制剪贴板、不查整个桌面。空范围失败无旧文本。超过上限只报告过长，不发送部分选区。
- VS Code 路径先查询内嵌扩展：仅当前前台 Code 的同文件进程树，CurrentUserOnly 管道和实际服务 PID 校验；随机 nonce、按下/释放时间，连接/响应/消息均限长限时。扩展仅保留鼠标事件元数据，核对当前焦点编辑器、版本、选区签名及时间后读取 `getText(range)`；键盘、命令、编辑、失焦清理旧元数据。单宿主连接上限 8，选区上限 32，结果上限 2,000 码点。未命中继续通用 UIA 路径。安装包与设置通过已安装 Code 的官方 CLI 安装内嵌 VSIX，不修改编辑器设置。扩展源码在 src/YiKe.VSCode，Node.js 只用于构建测试。
- `TranslationCoordinator`：锁保护序号、一个运行项与一个可替换待处理项；发布片段和 UI Dispatcher 回调各检查序号。新请求取消旧客户端工作，关闭/暂停令旧序号失效。无论服务是否配合取消，都不并发执行两个翻译；服务自身另有总超时。流式 UI 最快约 65 ms 合并一次。
- 内存缓存最多 32 项，包含原文、语言、配置/服务 ID、模型、端点、提示词版本、流式与指令角色。仅已确认真实选区后查缓存，明确标记缓存，无磁盘历史。
- `OpenAiCompatibleProvider`：每次仅固定指令 + 当前原文两条消息。手动模型，HTTPS 根地址规范化，Bearer header，禁重定向，不自定义证书验证。有行/事件/流总长度限制的严格 UTF-8 SSE 解析（支持 BOM、CR/LF/CRLF、多行 data）；只接受 content，忽略 reasoning 字段。stop 即完成，无需继续等待 DONE；只返回空白、提前 DONE 或未终止事件不当作正常结束；完成 reason 必须正常，空文、拒绝、截断、断流报错。不自动重试，429 冷却 10 秒。
- `ConfigurationService`：更新密钥时先写新服务 ID 的 DPAPI 密文，再原子提交配置指针。配置提交失败，旧配置和旧凭据仍可用；可能遗留未引用的本工具密文，可随主动删除全部用户数据清理。
- `SettingsStore`：用户目录是正常提交位置，便携副本用稳定配置 ID/修订协调；只读便携位置回退后提示。迁移解密和配置验证全部在写入前完成，错误包不改配置。普通导入使用新服务 ID，不混用旧端点密钥。
- `PopupWindow`：单一无激活、置顶工具浮窗，正文与原文分别限制滚动区域；以物理坐标和浮窗自身实际 DPI 限制到工作区，DPI 改变后重新定位。固定阻止自动取词，当前网络请求仍可完成；关闭/暂停无论固定状态均失效。文本控件不会执行服务输出。设置/粘贴窗口按当前工作区与实际 DPI 限制尺寸，最小尺寸随可用空间缩小，设置侧栏可滚动。

安装器只按当前用户安装；没有服务或全局环境变量。小型安装器按用户所选模型联网下载，完整离线安装器从内置载荷提取；运行本地模式不自动下载。AppId 固定，正常退出命令先释放译刻资源，再覆盖文件。安装任务页恢复当前 Run 注册项的真实状态，静默升级保留实际选择；显式 /TASKS、含启动项的 /MERGETASKS 优先，卸载前等待正常退出，失败时中止；卸载显式清理 Run 项。交互删除数据仅针对 settings.json 与 credentials/*.bin，不递归删除用户导出文件。

签名通过构建机钩子执行。当前以 opens 自签名并附 RFC3161 时间戳；只分发公开证书，不分发私钥或安装信任根。官方运行时许可与声明随源码、安装器和便携包附带。Inno Setup 商业使用需发布者自行满足其许可条件。

规范参考：[WHATWG SSE](https://html.spec.whatwg.org/multipage/server-sent-events.html#event-stream-interpretation)、[WPF 进程渲染模式](https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.renderoptions.processrendermode?view=windowsdesktop-10.0)、[显示器工作区](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.screen.workingarea?view=windowsdesktop-10.0)。实现结果以本项目验收报告为准。

## 1.7.8 WPS PDF 显式复制入口

PdfCopyFallback 使用消息窗口接收剪贴板变化；仅在 WPS PDF 的真实鼠标划选、直接接口不可用后等待本次复制。15 秒限时、当前窗口 / 标题 / 进程、起点命中及新序列共同绑定来源。允许持有窗口的原进程 / 所有者窗口，或同安装目录、晚于主进程启动且属于其进程树的 wpspdf.exe。其他进程、文件复制和旧剪贴板不会读取。后台以 OpenClipboard / CF_UNICODETEXT 有界读取，40 ms 合并变更并有限重试延迟数据，读取前后核对序列及来源，异步回调核对代次。不会发送复制快捷键，不使用 SetClipboardData、EmptyClipboard 或恢复剪贴板。

前台轮询使切换窗口、过期和停用失效；新手势、关闭、固定或暂停清理候选。结果保持原始段落，不截断，2,000 Unicode 码点上限。TranslateCopiedPdf 默认开启，普通设置导出 / 导入及迁移均保留该偏好。自建 PDF 位于 tests/fixtures/wps-selection-test.pdf；人工操作配合 --pdf-copy-live，仅对该夹具记录状态与精确相符标记，不写真实原文。
