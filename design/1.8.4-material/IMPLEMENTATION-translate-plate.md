# 双片玻璃＋金属译字试用版

按用户确认的右侧 02 / FACET 方向实装。划词图标已替换为原生 WPF 矢量双片玻璃底形，前片带斜向反光和浅金属“译”字，字体轮廓采用 Microsoft YaHei UI Bold。图标入口尺寸 54 DIP，点击处理与无焦点窗口行为沿用原实现；主界面 YK 标识保留。

实际截图：translate-plate-wpf-v1.png。矢量材质是概念图的简化实现，不包含棚拍效果和真实玻璃折射。

验证：Release 构建 0 警告、0 错误；现有 --visual 排版检查通过；发布单文件的 --verify-dictionary 检查通过。首次在受限沙箱验证时因 .NET 临时目录解包失败，随后在正常桌面环境验证成功。

已替换 E:/translator/YiKe/YiKe.exe 并启动。安装前旧程序保存在 artifacts/translate-plate-install-receipt.json 指定的 Backup 目录中。仅替换可执行文件，未覆盖用户配置、密钥、模型、翻译运行时或卸载信息。程序集版本仍为 1.8.3，本次是本地视觉试用，不是完整发布包。

完整构建与安装哈希见 artifacts/translate-plate-install-receipt.json。尚未声明通过真实划选点击翻译的端到端验收。

## 桌面、开始菜单和程序图标同步

已补齐外壳图标：assets/yike.ico 与 yike-icon-preview.png 使用 MaterialMark 同一原生绘制导出，ICO 包含 16、20、24、32、40、48、64、128、256 像素共九档，已验证每档 PNG 尺寸与 ICO 索引一致。安装程序侧栏插画也使用同一绘制；scripts/generate-brand.ps1 调用 tools/YiKe.Brand，可复现这些资源。

重新发布并替换已安装的 YiKe.exe，使 EXE 内嵌图标与托盘同步。桌面与开始菜单快捷方式的独立图标引用已改为 E:/translator/YiKe/YiKe-Translate-Plate.ico，目标与其他快捷方式字段保留，旧 YiKe-Prism.ico 也同步更新。已发送 Shell 图标/关联更新通知并重新启动应用。

备份、安装哈希和运行进程见 artifacts/desktop-icon-install-receipt.json。发布前词库检查通过；构建时出现 NU1900 提示，原因是无法访问 NuGet 漏洞服务，本轮没有完成在线依赖漏洞检查。
