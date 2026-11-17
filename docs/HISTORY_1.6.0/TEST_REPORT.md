# 译刻 1.6.0 · 术语用法示例验收

2026-10-24（Asia/Shanghai），Windows x64 build 26300，约 32 GB 内存、24 个逻辑处理器。

全部 219 组内置词条新增用法或场景示例及效果说明，附在本机作用解释之后。代码标明语言，概念词使用场景演示，Linux 主要采用查询、预演和练习路径；少数写入、权限或结构修改示例明确说明影响与前提。示例是学习材料，部分代码是片段，并非承诺在任何环境都可独立运行。示例为纯文本，不执行命令、脚本或用户选区；复制译文仍只复制译文。

标准本地模型及在线服务的专业解释提示改为作用、示例、效果三部分，仍使用同一次翻译请求；轻量模型保留完整词库示例，不启用不稳定的模型知识生成。过滤模型重复的已知术语时，其示例及效果一起去除，保留新词的例子。流式解析、长度限制、取消、缓存和主开关继续生效。模型生成的用例仍可能不完整或错误。

| 检查 | 本轮结果 | 证据 |
| --- | --- | --- |
| C# 构建 | 0 警告、0 错误；可执行输入与发布产物绑定 | artifacts/build-1.6.0-final.log |
| 核心回归 | 121 通过，0 失败；219 组示例覆盖、长度、语言 / 效果字段、显示顺序、去重、危险默认示例限制 | AUTOMATED_TESTS.txt |
| JavaScript 示例语义 | 6 通过，0 失败；实际检查 const 属性修改与绑定、let、var 块作用域、闭包、Promise、数组增长 | EXAMPLE_CODE_TESTS.txt |
| WPF / UIA | 29 通过，0 失败，1 跳过；本次剪贴板序列未初始化，相关序列检查跳过，未读取或改写剪贴板 | UI_TESTS.txt |
| 示例排版 | 浅色 / 深色 Linux 与 C++ 示例滚动区已渲染并查看；操作栏可见 | visual/light-linux-example.png / visual/dark-programming-example.png |
| VS Code 协议 | 13 通过，0 失败；选区适配未改 | VSCODE_TESTS.txt |
| 真实专业解释 | 标准 / 轻量各 6 组；本机词条均有示例和效果，标准模型为未收录 memoization 提供缓存计算结果的场景及效果；每次仅一个 provider 调用 | TECHNICAL_LIVE_STANDARD.json / TECHNICAL_LIVE_LIGHT.json |
| 真实翻译基线 | 两档各 5 组语言样例、取消重试、内存压力释放恢复、正常 / 异常退出清理通过 | LOCAL_MODEL_LIVE.json / LOCAL_MODEL_LIGHT_LIVE.json |
| 实际升级 | 实际当前用户目录安装从 1.5.0 → 1.6.0；退出码 0；3 个设置 / 密钥文件逐字节相同，模型选择与启动值保留 | SIGNED_UPGRADE_RESULTS.json |
| 安装后组件 | 标准模型与 31 个引擎 / 运行文件（共 32 个）全部校验通过，自有引擎仅回环监听 | INSTALLED_LOCAL_RESULTS.json |
| opens 签名 | 9 通过，0 失败；应用、安装器、实际卸载器、时间戳与篡改拒绝 | SIGNATURE_RESULTS.json / SIGNATURE_TESTS.txt |
| 成品完整性 | 发布核验通过；资料更新后通过 PackageOnly 重建并核对源码、归档、散列和签名身份 | BUILD-MANIFEST.json / SHA256SUMS.txt |

示例语义检查仅执行六个可信、内置的 JavaScript 片段，使用受限的 Node VM；没有把用户选区或 Linux 示例送入执行器。其余语言片段及场景经过资料核对和人工检查，没有声称所有语言都实际编译或所有命令都执行。真实模型检查证明本轮样例格式与流程，不是所有专业知识或生成代码的正确性认证。

来源和使用范围见 PROGRAMMING_TERMS.md；新用例核对了 curl 官方手册、systemctl、PostgreSQL 查询教程和 JavaScript const 文档，编程与计算机基础资料沿用已列来源。单次词库仍最多显示 4 组，每组例子可在解释区滚动查看；不增加额外模型请求或下载。

安装模型流程、硬件策略、WPS / Edge / VS Code 选区代码未改；历史验收不冒充本轮重新执行的完整应用矩阵。本轮没有干净 Windows、实体低配机器或整机物理断网验收，轻量档使用硬件快照选择真实 0.6B 模型。1.5.0 报告与说明保存在 HISTORY_1.5.0/。

opens 仍为自签名；签名完整性和时间戳通过不等于 Windows 默认信任。私钥不进入任何交付包，未修改系统信任根、安全或隐私设置。