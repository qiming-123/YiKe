# opens 签名说明

译刻 1.1.0 的主程序、安装器和卸载器以 `CN=opens` 自签名。主程序文件详情的公司字段、安装器发布者也为 opens。自签名仅声明本次选定的签名身份，不是 CA 对 opens 身份的认证；Windows 默认仍不信任该证书，SmartScreen 等提示可能继续出现。

使用 RSA 3072 位代码签名证书、SHA-256 文件摘要、DigiCert 的 RFC3161 / SHA-256 时间戳。时间戳不会把 opens 的自签名变成受信任发布者签名。验证同时检查 Windows 文件完整性、固定公钥证书身份、CMS 签名、时间戳摘要算法及其与主签名的绑定。

公钥证书 SHA-256：`c94ffb6fd7b4c36281683e8b20a5cc51bf73c61377033dcfe66ee8359203c51f`。

证书指纹：`06019D3F63B3F2B084126B9C3EB883283E376FEE`。

证书到期日：2028-10-23（UTC）。公钥证书随安装器、便携包资料和源码包提供，可用于核对身份。没有把 opens 导入受信任根证书库，没有修改系统安全设置。

私钥由本机当前 Windows 用户的个人证书库及 Microsoft Software Key Storage Provider 管理，创建时设为不可导出；私钥不在源码、EXE、ZIP 或日志中。分发目录只包含 `.cer` 公钥证书，不包含 PFX/P12/PVK/KEY 等私钥格式。另一台机器没有该私钥，不能重现同一 opens 签名；源码仍可正常重建未签名版本。

签名构建：

```powershell
./scripts/bootstrap-signing.ps1
./scripts/build-release.ps1 -Version 1.1.0 -SignScript ./scripts/sign-opens.ps1 -SignerCertificate ./assets/opens-codesign.cer -AllowSelfSigned
./scripts/test-signature.ps1 -Version 1.1.0 -UninstallerPath "$env:LOCALAPPDATA\Programs\YiKe\unins000.exe"
```

`AllowSelfSigned` 只允许固定公钥且 Windows 返回“根不受信任”的情况。摘要不匹配、错证书、过期或其他签名失败仍阻止发布。默认签名流程继续要求受信任签名；不会一律接受 UnknownError。清单如实记录 `self-signed-untrusted`、`UnknownError` 与 `0x800B0109`，不伪报 Valid。

签名工具来自微软官方 Microsoft.Windows.SDK.BuildTools 10.0.26100.9169 NuGet 包；已验证官方目录 SHA-512、NuGet 作者/仓库签名和工具的 Microsoft Authenticode 签名。固定摘要写入 build-tools.json，工具只放在项目 .tools，不随应用分发。

依据：[微软自签名说明](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_signing)、[SignTool 与 RFC3161 参数](https://learn.microsoft.com/en-us/windows/win32/seccrypto/signtool)、[Authenticode 时间戳](https://learn.microsoft.com/en-us/windows/win32/seccrypto/time-stamping-authenticode-signatures)。本次实际签名与负向检查结果见 SIGNATURE_RESULTS.json、SIGNATURE_TESTS.txt。

1.1.0 的 opens 签名覆盖本项目主程序、安装器和卸载器。随包第三方 llama.cpp 引擎保持其上游发布文件，使用固定摘要清单核验；微软 C++ DLL 保留其原始 Microsoft 签名。模型 GGUF 通过固定 SHA-256 和大小核验，不能把模型文件描述成 opens 认证的模型。全部运行文件的清单见 assets/local-package.json 与发布清单的 localPackage 字段。
