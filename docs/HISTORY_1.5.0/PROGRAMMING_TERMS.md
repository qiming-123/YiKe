# 计算机术语解释

译刻 1.5.0 将原编程词库扩展为 **219 组计算机专业词条**。选中文字后，浮窗“计算机术语 · 作用说明”显示中文解释，译文仍按选择的目标语言生成。“复制译文”只复制译文，不混入术语资料。

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

**智能补充：**未收录的短词、短语或有计算机专业语境的内容，可由当前标准本地模型或在线服务随本次翻译一起生成补充。不会另发第二次翻译请求，不增加读取范围；在线服务的生成量仍可能增加计费，本地生成也可能增加等待。补充标为“模型补充 · 请结合语境核对”，与本机资料分开。使用标准本地模型时，补充也在本机完成。

轻量 0.6B 模型实测不能稳定遵循解释格式且专业知识不可靠，因此只使用本机词库解释与原翻译流程，不自动生成知识补充。实际模型按安装选择、硬件和可用内存决定；没有标准模型时不会为此自动下载、提升模型或切换到在线服务。覆盖尽量广不代表能正确解释所有专业词，模型可能遗漏、误解或没有输出补充。

设置 → 翻译服务有两个独立偏好：

- “显示计算机术语解释（含 Linux 与编程）”：控制解释总开关。
- “让当前模型补充未收录的专业术语”：关闭后保留词库解释，减少生成等待；主开关关闭时该项不可操作。

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