# 1.7.3 任意选区的计算机用法说明

开启“为所有选区显示计算机用法解释”后，任意单词、短语或句子均显示说明、示例和效果。有明确专业含义的优先显示本机专业资料；how、unmark、square / squares 等普通词有专门的离线软件 / 文档用例。未收录词如无可确认专业含义，会明确资料范围并给出通用文本示例，不杜撰它是某种语言的关键字或命令。

翻译优先：本地模式、简体中文目标下，约 76 万条英汉词库继续提供精确查词；词义和计算机说明分别显示，复制仅复制译文。不读取选区外上下文。多义词会列出常见义项，不能只凭孤立词确定具体软件或程序语境。

开启模型补充后，标准本地模型或在线服务对普通选区在译文完成后最多增加一次独立说明请求，预算最多 30 秒。浮窗显示“译文已完成 · 正在补充计算机说明”，译文可立即复制。模型说明不完整、失败或超时保留完成的译文与离线说明，不重新翻译或循环重试。完成的说明随本次运行缓存，取消或换选区不回流旧说明。在线说明请求可能单独计费；固定连接测试不发说明请求。

已有专业语境的组合翻译 / 说明流程保留，已知资料不会重复请求。轻量或没有可用模型时使用离线说明，不为解释自动下载、提升模型或切换在线服务。所有选区都有说明不等于所有词都有固定计算机专义，更不代表小模型的知识和例子全部准确，生成内容始终标明“模型补充 · 请结合语境核对”。

本地翻译异常最多一次纯翻译恢复，说明异常不再重试；译文语言 / 非空检查仍生效。轻量整句误译限制仍保留在测试报告与 WORD_LIVE_LIGHT.json。词库来源为 ECDICT 固定提交的 MIT 授权数据，索引嵌入单文件应用，用户无需 Python 或数据库。

# 计算机术语与终端命令说明

## 终端命令详细用法（1.7.0）

新增 70 种常见命令和子命令的本机资料，独立于原 219 组术语。每项包含适用 shell / 工具、用途、语法、常用参数、本次选中参数、示例、效果和必要的修改影响。选区本身不会作为命令执行，也不调用外部 man / Get-Help，不读取终端环境或历史。内置资料在轻量模型下仍可用。

示例选中 grep -n -i 'error' ./demo/app.log：说明 -n 显示行号、-i 忽略大小写、error 是模式、后者是待搜索文件，并给出同类用例及预期效果。选中 g++ -std=c++20 -Wall main.cpp -o demo-app：解释语言标准、警告、输入源码、输出路径及编译写入但不自动运行程序。

支持常见提示符 PS C:\demo>、CMD 路径提示符、user@host:~$，有界处理引号、短选项组合（ls -lah、tar -tf）、选项值、普通管道和重定向。最多说明前 4 个命令，每项最多展开前 16 个参数；不展开变量、通配符、子命令或脚本结构，不是 shell 语法验证器。未收录的选项明确说明需核对目标版本，不虚构。资料覆盖常用形式，并非系统中所有可执行程序或全部参数。

终端界面可运行的命令由实际 shell、系统、已安装工具与版本决定。同名 ls、cat、rm、dir 在 PowerShell、CMD 和 Linux 中可能不同，资料明确区分。不能因为选区来自 VS Code 就认定它是 Bash。一般工程命令可能联网、执行构建脚本并写入文件，示例对这些影响作说明。

VS Code Terminal 使用 Ctrl+Alt+Y“复制并翻译 / 解释”，会将选区复制到剪贴板，不恢复旧内容或清除系统剪贴板历史。自动编辑区划词不使用这个入口。详细使用见 [VS Code 适配说明](VSCODE_TERMINAL.md)。新版 VS Code 扩展需重新加载窗口，译刻已暂停、浮窗固定或 Code 被排除时不会取词。

新资料核对 [PowerShell 命令语法](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_command_syntax)、[Windows 命令](https://learn.microsoft.com/en-us/windows-server/administration/windows-commands/windows-commands)、[Git](https://git-scm.com/docs/git)、[npm install](https://docs.npmjs.com/cli/v11/commands/npm-install)、[Python 命令行](https://docs.python.org/3/using/cmdline.html)、[Docker ps](https://docs.docker.com/reference/cli/docker/container/ls/)、[GCC](https://gcc.gnu.org/onlinedocs/gcc/Overall-Options.html)、[CMake](https://cmake.org/cmake/help/latest/manual/cmake.1.html)、[Cargo](https://doc.rust-lang.org/cargo/commands/cargo.html)、[.NET CLI](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet)、[Java](https://docs.oracle.com/en/java/javase/25/docs/specs/man/java.html) 和 [Go build](https://pkg.go.dev/cmd/go#hdr-Compile_packages_and_dependencies)。GNU / Linux 基础手册沿用下方列出的来源。

普通扩展的终端选区接口仍属 [VS Code proposed API](https://github.com/microsoft/vscode/blob/main/src/vscode-dts/vscode.proposed.terminalSelection.d.ts)，因此提供兼容稳定版的显式复制入口，未强行启用试验接口。

## 原有术语解释

译刻 1.7.0 将原编程词库扩展为 **219 组计算机专业词条**。选中文字后，浮窗“计算机术语 · 作用与示例”显示中文解释，译文仍按选择的目标语言生成。“复制译文”只复制译文，不混入术语资料。

## 用例与效果

全部 219 组词条都在作用解释之后附“示例”和“效果”。编程代码注明语言，Linux 示例说明相关参数及对文件的影响，网络、硬件和算法等概念使用实际场景帮助理解。示例随程序内置，离线和轻量模式也有，不增加模型请求或下载。

例如 chmod 的用例为 chmod 755 ./script.sh，效果说明所有者可读写执行，组和其他用户可读执行；示例文件需已存在且有权限。JavaScript const 的用例展示可修改对象属性，但不能把绑定重新赋值。SQL 示例说明所需表和列，避免把假定的上下文误认为任何环境都已具备。

示例是学习材料，有些是局部代码或场景，并非全部可直接作为独立程序运行。Linux 例子主要采用查询、预演或练习文件路径；少数文件写入、权限、包管理和数据库示例会明确说明影响及前提。译刻不会执行所展示内容。原“复制译文”仍只复制译文，不混入示例。

模型补充也要求给出一个简例及预期效果，可能增加同次输出量；只保留原模型门槛，不为此自动使用在线服务。生成的示例仍可能不完整或不准确。去除模型重复的已知术语时，其示例和效果会一并去除；未知新词的例子保留。

新用例还核对了 [curl 官方手册](https://curl.se/docs/manpage.html)、[PostgreSQL 查询教程](https://www.postgresql.org/docs/current/tutorial-select.html)和 [JavaScript const 文档](https://developer.mozilla.org/en-US/docs/Web/JavaScript/Reference/Statements/const)。六个内置 JavaScript 例子使用隔离测试验证绑定、作用域、闭包、Promise 和数组行为；没有执行所选用户内容或 Linux 示例命令。

| 方向 | 示例 |
| --- | --- |
| Linux / Unix | ls、chmod、sudo、grep、systemctl、apt、SSH、rwx、管道、重定向、环境变量 |
| 操作系统 | 内核、进程 / 线程、系统调用、信号、IPC、死锁、虚拟内存、COW、cgroups |
| 网络 | TCP / UDP、IP、DNS、HTTP / HTTPS、TLS、代理、CDN、WebSocket、SSE |
| 数据库 | SQL、JOIN、主外键、索引、事务、ACID、MVCC、WAL、缓存与复制 |
| 开发 / 云 | 编程关键字跨语言用法、Git、Docker、Kubernetes、CI/CD、API / ABI、调试与测试 |
| 安全 | 认证与授权、公私钥、数字签名、JWT、XSS、CSRF、SQL 注入、最小权限 |
| 基础 / 算法 / AI | CPU / GPU、内存与磁盘、字符编码、复杂度、数据结构、LLM、量化、RAG |

常见词优先使用随程序携带的本机词库，解释不依赖联网或大模型。Linux 命令说明用途及常见选项，并在涉及删除、覆盖等动作时说明实际影响；译刻只显示文字，不执行命令。独立的已知术语和以已知命令开头的常见命令行优先使用词库，避免额外生成解释造成等待。

**智能补充：**有明确计算机专业语境或特定技术提示的未收录内容，可由当前标准本地模型或在线服务随本次翻译一起生成补充。已有专业语境的组合请求不另发第二次翻译；普通选区可增加一次独立说明请求，不增加读取范围；在线服务的生成量仍可能增加计费，本地生成也可能增加等待。补充标为“模型补充 · 请结合语境核对”，与本机资料分开。使用标准本地模型时，补充也在本机完成。

轻量 0.6B 模型实测不能稳定遵循解释格式且专业知识不可靠，因此只使用本机词库解释与原翻译流程，不自动生成知识补充。实际模型按安装选择、硬件和可用内存决定；没有标准模型时不会为此自动下载、提升模型或切换到在线服务。覆盖尽量广不代表能正确解释所有专业词，模型可能遗漏、误解或没有输出补充。

设置 → 翻译服务有两个独立偏好：

- “为所有选区显示计算机用法解释”：控制解释总开关。
- “让当前模型补充所有选区的计算机用法”：关闭后保留词库解释，减少生成等待；主开关关闭时该项不可操作。

默认均开启，旧配置自动补默认值，导入导出与迁移保留选择。原 ExplainProgrammingTerms 字段继续兼容，新增 SupplementTechnicalTerms 不改变已有服务、密钥或模型偏好。

单次词库最多显示最先出现的 4 组，重复词条只显示一次，长短词重叠时优先完整术语；更多内容可缩小选区查看。编程关键字和命令通常区分大小写，一般术语适当兼容大小写，部分有歧义的缩写保留大小写区别。中文专业短语可在句中识别。普通句子使用语境启发式，不能消除所有误识别；资料不是编译器，不认定实际语言、发行版或版本。模型说明不应替代目标系统手册。

解释与译文通过内部流式分隔解析。分隔符跨任意块边界也不会进入正常译文；可选结束标记被隐藏，解释长度受限。未完成的补充标明状态且不缓存，关闭或换选区继续丢弃过时结果。模型不遵循格式时仍可能出现格式或内容问题，尤其是小型模型；不会偷偷重试可能已计费的请求。

词库为自行概括的常见用途，核对资料包括：

- [GNU chmod 手册](https://man7.org/linux/man-pages/man1/chmod.1.html)、[GNU rm 手册](https://man7.org/linux/man-pages/man1/rm.1.html)、[Bash 手册](https://man7.org/linux/man-pages/man1/bash.1.html)（项目手册的 man7 渲染）
- [kill 手册](https://man7.org/linux/man-pages/man1/kill.1.html)、[systemctl 官方手册](https://www.freedesktop.org/software/systemd/man/latest/systemctl.html)
- [Linux 内存管理概念](https://www.kernel.org/doc/html/latest/admin-guide/mm/concepts.html)、[cgroup v2](https://docs.kernel.org/admin-guide/cgroup-v2.html)
- [HTTP 语义 RFC 9110](https://www.rfc-editor.org/rfc/rfc9110.html)、[TLS 1.3 RFC 8446](https://www.rfc-editor.org/rfc/rfc8446.html)
- [PostgreSQL 约束](https://www.postgresql.org/docs/current/ddl-constraints.html)与[事务隔离](https://www.postgresql.org/docs/current/transaction-iso.html)
- [Git 手册](https://git-scm.com/docs/git)、[Docker 容器概念](https://docs.docker.com/get-started/docker-concepts/the-basics/what-is-a-container/)、[Kubernetes 概览](https://kubernetes.io/docs/concepts/overview/)

编程语言资料沿用 [1.4.0 的来源清单](HISTORY_1.4.0/PROGRAMMING_TERMS.md)。资料仅覆盖常见用法，各命令实现和语言版本可能有差异。