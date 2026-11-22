# AGENTS.md — 译刻 YiKe 协作者指南

本文件面向人类协作者与 AI 编码 Agent（Codex、Claude Code、Gemini CLI、Kimi Work 等均读取本文件；分层加载时离工作目录最近的同名文件优先）。改动前请完整阅读；与本文件冲突的惯例以代码和 `scripts/build-release.ps1` 的实际行为为准。

远程仓库：`https://github.com/qiming-123/YiKe`（`main` 分支，PR 用 merge commit 合入）。

## 每次修改前：云端对齐检查（最高优先级，先于一切操作）

**对仓库做任何修改之前（包括每次接续工作、每个新指令的第一步），必须先验证本地与 GitHub 云端完全对齐：**

```powershell
git fetch origin
git status --short --branch   # 期望：## main...origin/main 之后没有 [behind]/[ahead]，且无已跟踪文件的改动行
git log main..origin/main --oneline   # 期望：无输出（云端没有本地缺的提交）
git log origin/main..main --oneline   # 期望：无输出（本地没有未推送的提交）
```

任一检查不满足（本地落后、领先、有分叉、已跟踪文件有未提交改动）时：

1. **立即停止执行当前指令，不要自行 pull/rebase/merge/checkout/提交**
2. 向用户报告差异的具体情况：方向（本地缺云端提交 / 云端缺本地提交 / 工作区脏文件列表）与涉及文件
3. **等待用户明确答复后**才能继续；用户确认同步方案后再执行

唯一例外：用户当前指令本身就是处理这种不同步（如"拉取最新代码""推送我的提交"）。

确定开发范围与排期时，优先参考 [YiKe 改进清单](docs/IMPROVEMENT-BACKLOG.md)，在本次范围内优先考虑其中相关条目。每次落实改进后，在同一批变更中更新清单，记录相关条目的状态、实际改动和验证结果。

## 项目速览

译刻 YiKe：Windows 划词翻译工具（C# / .NET 10 WPF，单文件 x64 发布）。主程序入口 `src/YiKe/Program.cs`。

- 本地离线翻译：llama.cpp 引擎 + Qwen3 GGUF 模型；另有 OPUS-MT 英译中「节能引擎」（`translation-runtime/` 内的便携 Python）
- 在线翻译：OpenAI Chat Completions 兼容 API（含流式 SSE、DeepL 预设），最多保存 32 组配置
- 选区读取：UI Automation + WPS 文字/PDF、VS Code 适配、终端 Ctrl+Alt+Y；不读剪贴板（PDF 回退除外）
- 隐私红线：不记日志、不遥测、不发送窗口标题/截图；密钥只走 Windows DPAPI

不要写入瞬态机器状态（当前空闲内存、临时目录内容、依赖解压位置的绝对路径等）——这些由脚本和 lockfile 在运行时探测与固化。

## 目录速查

| 路径 | 内容 |
| --- | --- |
| `src/YiKe/` | 主程序。`Core/`（契约与 AppSettings）、`Data/`（设置/凭据存储）、`Selection/`（选区监控与各应用适配器）、`Translation/`（本地/在线/NMT 提供者、词库、术语）、`UI/`（主题与窗口） |
| `src/YiKe.VSCode/` | VS Code 划词适配扩展（打包进主程序资源） |
| `tests/YiKe.Tests/` | 控制台测试项目（无第三方测试框架），含 UI 回归与真实模型实测 |
| `scripts/` | 全部构建/测试/发布脚本（PowerShell 7） |
| `installer/` | Inno Setup 脚本 |
| `assets/` | 嵌入资源：词库、图标、许可、运行时锁文件（`*.lock.json`） |
| `docs/` | 架构文档与各版本测试报告（发布时自动更新） |
| `local-runtime/`、`translation-runtime/` | 本地模型与 NMT 运行时（均被 gitignore，由脚本生成） |
| `dist/`、`artifacts/` | 发布产物与构建日志（gitignore） |

## 环境要求（版本是硬约束）

- **PowerShell 7**（脚本用了 `GetRelativePath` 等 .NET Core API，Windows PowerShell 5.1 会失败）
- **.NET SDK 10.0.402**（`global.json` / `build-tools.json` 固定；脚本装到 `.tools/`，不改系统 PATH）
- **Node.js 20+**：仅 VS Code 适配与词库示例测试需要
- **Inno Setup 7.1.0**：打包需要，脚本装到 `.tools/inno/`
- 主线目标系统：Windows 10 22H2+ / Windows 11 x64（安装器 `MinVersion=10.0.19045`，即 Win10 22H2 起）；Windows 7/8.1 需 .NET Framework 4.8 独立分支、macOS 需跨平台 UI 移植（WPF 为 Windows 专属），均为规划中的重大工程，见 docs/IMPROVEMENT-BACKLOG.md；不提供 ARM64
- 验证策略：构建时只验证当前开发系统；其他目标系统一律用 Hyper-V 虚拟机做安装与验收

## 命令速查：最小验证 → 完整发布

```powershell
# 首次（需要网络，下载固定版本工具与运行时）
./scripts/bootstrap-tools.ps1        # .NET SDK + Inno Setup → .tools/
./scripts/bootstrap-nmt.ps1          # NMT 运行时（Python+ctranslate2+模型）→ translation-runtime/
./scripts/bootstrap-dictionary.ps1   # 词库 assets/word-dictionary.dat（需要 ecdict.csv）

# 日常编译（环境变量必须设置）
$env:DOTNET_CLI_HOME = "$PWD/.tools/cli"
$env:NUGET_PACKAGES  = "$PWD/.tools/packages"
dotnet restore src/YiKe/YiKe.csproj --locked-mode
dotnet build   src/YiKe/YiKe.csproj -c Release --no-restore

# 测试（控制台可执行，参数即测试集）——按改动范围选最小集
$tests = "./tests/YiKe.Tests/bin/Release/net10.0-windows/win-x64/YiKe.Tests.exe"
& $tests                      # 核心回归：任何改动至少跑这个
& $tests --ui-regression      # 动到 UI/弹窗/设置窗口必跑
& $tests --visual             # 动到布局必跑
& $tests --local-live <local-runtime> <out.json>            # 改选区/翻译链路：真实标准模型实测
& $tests --local-live-light <local-runtime> <out.json>      # 同上，轻量模型
node --test tests/vscode-selection.test.cjs                 # 改 VS Code 适配必跑

# 完整发布（测试失败会中止；产物 + 清单 + SHA256 到 dist/）
./scripts/build-release.ps1 -Version 1.8.3
```

注意：

- 不加 `-Version` 时默认版本见 `build-release.ps1` 参数默认值；版本号三段式 `x.y.z`
- `--local-*` 实测要求 `translation-runtime/` 已复制到测试输出目录（构建时自动）
- NuGet 恢复必须 `--locked-mode`；改依赖要同步更新 `packages.lock.json`
- 中国网络环境下 Hugging Face 可能需要镜像；所有下载都有 SHA-256 校验，不要用未校验的替代文件

## 代码约定

- C# 全程 `Nullable` enable、`ImplicitUsings` disable（主程序）；新代码保持现有文件风格（紧凑、少样板）
- 几乎所有控件在 C# 里代码构建（非 XAML），颜色一律走 `Theme.Apply` 的资源键（`Surface/Canvas/Soft/Line/Text/Muted/Accent/AccentSoft/OnAccent/Good/Error`），**不要硬编码颜色**——暗色主题与高对比度回落都依赖这套键
- 新增可见 UI 必须同时考虑：`SystemParameters.HighContrast`、`SystemParameters.ClientAreaAnimation`（减少动态效果）、多屏/DPI
- 选区与翻译链路禁止读取剪贴板（`PdfCopyFallback` 是唯一例外，且必须能区分"本次新复制"）；禁止自动重发可能已计费的在线请求
- 字符串面向中文用户；错误消息要说明"原文是否已发送"
- `AppSettings` 新增字段：给出默认值（旧配置自动兼容）、在 `Validate()` 中加校验、确认导出/导入/迁移路径不丢字段

## 权限边界：自动执行 vs 先问再做

**无需确认即可做**：读取任何源码与文档、编辑工作区文件、编译、运行文件级/单测级检查、查看 git 状态与历史。

**必须先征得用户明确同意**：`git commit`/`push`/`reset`/`rebase` 等改写历史的操作、删除文件或目录、安装系统级工具、运行完整发布或安装测试、下载大体积运行时/模型、读取或改动 `settings.json`/`credentials/`（用户私有数据，且禁止提交）、向仓库添加新依赖。

## 测试纪律（不可妥协）

1. 任何改动至少跑 `& $tests` 和 `& $tests --ui-regression`；动到 UI 的还要跑 `--visual`
2. 改了选区/翻译链路：跑对应的 live 实测（有模型环境时）
3. 改了设置结构：确认 `Upgrade180Checks` 等升级测试仍过，旧 `settings.json` 能读
4. **所有 UI 行为变化都要在 UI 回归测试里留下断言**——外观、弹窗、设置窗口的现有用例就是范例
5. **严禁伪造测试结果**：没跑就是没跑，在交付说明里如实列出未验证项与风险

## 提交与协作流程

- **每次动手前先完成「云端对齐检查」（见文件顶部第一节），未对齐时停止并等用户确认**
- **每次提交必须经用户确认：先列出待提交文件清单与改动摘要，用户明确同意后才能 `git commit`；用户未确认前不得提交任何文件、不得用 `git add -A` 之类的宽匹配**
- **禁止直接向 `main` 推送任何提交**：所有改动从 `main` 切 `feature/<主题>` 或 `fix/<主题>` 分支开发，push 到远端同名分支后开 PR 合回 `main`（仓库用 merge commit）；唯一例外是仓库所有者明确指示直接推 `main`
- 提交信息用中文一句话概括"做了什么、为什么"；一个提交只做一件事
- 禁止提交：密钥/证书私钥、`settings.json`/`credentials/`、模型与运行时（`local-runtime/`、`translation-runtime/`）、构建产物（`dist/`、`artifacts/`、`bin/`、`obj/`）
- 发布版本相关的文档（`RELEASE_NOTES.md`、`docs/*RESULTS*`、视觉截图）由 `build-release.ps1` 自动刷新，**不要手工伪造**
- 版本号提升 = 走完整 `build-release.ps1 -Version x.y.z` 并更新 `RELEASE_NOTES.md`；不要把未发布代码标成已发布版本

## 安全与隐私红线

- 密钥只能经 `CredentialStore`（DPAPI CurrentUser）；新服务的密钥必须绑定规范化端点
- 原文/译文/请求体/密钥**永不写入日志或磁盘**；诊断信息不得包含选区文本
- 不引入第三方遥测/分析 SDK；新增网络请求要在 PR 描述里说明目的地与内容
- 安装/卸载逻辑改动必须用 `scripts/test-installer.ps1` 验证，且不得在无隔离环境跑安装测试（脚本自身会拒绝覆盖现有用户数据）

## 版权与许可

- 引入任何第三方代码、依赖、模型、词库或素材前，先确认许可证与本项目兼容；GPL/AGPL 系传染性许可慎用；标注 NC（非商业）或禁用转售的素材不进发布包
- 新依赖必须：固定版本与哈希（相应 `*.lock.json`）、在 `THIRD_PARTY_NOTICES.md` 或 `assets/` 下对应许可目录补许可文件
- 复制他人代码片段必须保留原许可声明与出处，不得去除版权头
- 模型/权重受各自许可约束（Qwen、OPUS-MT Apache-2.0 等），随包许可目录为 `assets/local-licenses/`、`assets/nmt-licenses/`、`translation-runtime/licenses/`，改动打包内容时同步核对
- 商用分发前核对 Inno Setup 商业许可（README 已注明）

## 完成定义（Definition of Done）

交付前逐项自查，并在回复中给出证据而非断言：

1. 该跑的最小测试集已跑且通过；失败项修复或明确标注遗留
2. 改动 diff 已自查：无调试残留、无硬编码颜色/密钥、无关文件零改动
3. 如实报告：跑了什么命令、结果如何、哪些没验证及原因
4. 文档与行为一致（本文件、清单、README 随流程变化同步更新——过时的指令比没有指令更糟）
5. 每次修改程序后，必须在同一批变更中同步更新 `docs/USER_GUIDE.md` 和 `docs/QUICK_START.md`；详细版逐项说明功能与限制，精简版只保留核心用法，话语尽量精简

## 卡住或不确定时

- 拿不准 API 行为时优先读 `tests/` 里的现有断言，而不是凭记忆写
- 对缺少复现依据的低概率风险，新增锁、重试、额外状态或依赖之前，说明触发条件、影响、概率依据及预计代码量，**获得用户同意**；未知概率写明未知
- 连续两次纠正同一问题仍错时，停下来向用户复述你对任务的理解，而不是第三次重试

## 给 AI Agent 的专项提醒

- 修改 UI 外观前先读 `src/YiKe/UI/Theme.cs`（资源键与 `Apply`）、`CyberPrism.cs`（`PrismPanel` 卡片/霓虹效果）、`MaterialMark.cs`（自绘图标）、`SettingsWindow.cs`（窗口内容在构造函数里代码构建），理解主题与玻璃质感如何实现
- WPF 模板工厂（`FrameworkElementFactory`）只接受可视元素；Transform 等需用 `EventSetter` 或代码后置处理
- 窗口显示后不可再改 `AllowsTransparency`/`WindowStyle`；重建窗口内容前先摘除元素的逻辑父级（参考 `Detach`）
- 开发提交标题及桌面试用版编号遵循 docs/COLLABORATION.md 的「首次正式发行目标与开发编号」
