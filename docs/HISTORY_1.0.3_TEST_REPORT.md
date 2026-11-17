# 译刻 1.0.3 · opens 签名验收

2026-10-23（Asia/Shanghai），Windows x64 build 26300。

本版主程序、安装器和实际安装的卸载器均为 CN=opens 自签名，RSA 3072 位，文件摘要 SHA-256，DigiCert RFC3161 / SHA-256 时间戳。安装器和主程序的发布者/公司元数据为 opens。私钥留在本机当前用户证书库，CNG 导出策略为 None；随包仅含 .cer 公钥证书。

Windows 默认不信任该自签名。实际状态为 UnknownError / 0x800B0109 / self-signed-untrusted，原因是证书根不受信任；没有伪报 Valid，没有把 opens 安装为受信任根。时间戳不会把自签名变成 CA 认证身份，SmartScreen 等提示可能仍出现。详见 [签名说明](SIGNING.md)。

## 本轮结果

| 验证 | 结果 | 证据 |
| --- | --- | --- |
| 核心功能/协议/数据 | 60 通过，0 失败 | AUTOMATED_TESTS.txt |
| VS Code 扩展 | 13 通过，0 失败 | VSCODE_TESTS.txt |
| WPF 界面 | 14 通过，0 失败，0 跳过 | UI_TESTS.txt |
| 签名专项 | 9 通过，0 失败 | SIGNATURE_TESTS.txt / SIGNATURE_RESULTS.json |
| 发布完整性与故意篡改 | 6 通过，0 失败 | RELEASE_VERIFICATION_TESTS.txt |
| 实际安装 1.0.2 → 1.0.3 | 成功；2 个用户数据文件逐字节相同；自启动未变；应用匹配构建哈希 | SIGNED_UPGRADE_RESULTS.json |
| 私钥/公钥边界 | 私钥不可导出；分发证书不含私钥；opens 未加入受信任根 | SIGNING_KEY_POLICY.json |

构建 0 警告、0 错误。签名专项覆盖主程序/安装器/实际卸载器的固定证书、Windows 文件完整性、CMS 签名、SHA-256 与 RFC3161、时间戳与主签名绑定；拒绝错证书、未明确允许的自签名、改动后的 EXE 以及含私钥格式的归档。已签名单文件应用实际启动生产 UIA 辅助进程，返回受保护的 TargetGone 结果并在 EOF 后正常退出，未读取选区或调用在线服务。

签名后的程序已恢复本机托盘运行。公钥和所有发布包的最终哈希见 BUILD-MANIFEST.json / SHA256SUMS.txt。安装版与便携包来自同一份已签名应用；源码包没有私钥，在其他机器可重建未签名应用，无法复现本机同一身份的签名。

## 已知边界

本次是代码签名与发布校验，没有重新执行全部真实应用鼠标矩阵。VS Code 扩展仍为 1.0.2，功能代码未改；此前双击、跨行与 Unicode 选区的实际结果保留在 HISTORY_1.0.2_TEST_REPORT.md 和 vscode/。本轮 WPF 生产 UIA 夹具通过。

已有翻译服务的 TLS 握手问题未由代码签名解决。本轮没有发送真实用户文本或修改服务配置。真实在线译文/质量、干净系统、跨机器、多屏、完整读屏等未覆盖项目仍保留，不将本版描述为全面验收或 Windows 受信任发布者版。历史源码重建结果带原版本号，不等同于本轮重建。