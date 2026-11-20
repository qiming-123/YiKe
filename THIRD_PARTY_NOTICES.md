# 第三方组件与素材

应用使用 Microsoft .NET / Windows Desktop Runtime 10.0.13（以构建清单中的实际版本为准），这些运行时随自包含程序分发。运行时的 MIT 许可和第三方声明由 Microsoft 发布于 https://github.com/dotnet/runtime 和 https://github.com/dotnet/wpf 的 LICENSE.TXT、THIRD-PARTY-NOTICES.TXT；发布脚本另附实际 SDK 中的运行时许可副本。Windows UI Automation、DPAPI 和系统字体由 Windows 提供。

安装器由 Inno Setup 7.1.0 编译。Inno Setup 的许可与商业使用说明见 https://jrsoftware.org/isinfo.php 和 https://jrsoftware.org/isdl.php。编译器只用于构建，不随便携程序分发。对外商业发布前，发布者应核对并满足编译器的商业许可要求。

译刻图标、安装侧栏和界面中的向量路径由本项目自行绘制，可依据本仓库 MIT 许可修改。生成源文件为 scripts/generate-brand.ps1。未引入第三方图标包、图片素材或换肤 DLL。

未提供真实发布者公司或联系信息；安装器未填写虚构的公司、网站或已验证发布者标识。当前构建的代码签名状态见验收报告。


本地翻译随完整包分发 Qwen3-1.7B 的 Q4_K_M 和 Qwen3-0.6B 的 Q4_0 GGUF 量化文件（Apache License 2.0），原始模型由 Qwen 发布，量化文件由 ggml-org 发布。来源、固定提交、SHA-256 和文件大小记录在 local-model.lock.json 和 assets/local-package.json。没有修改模型权重，不将其作为本项目原创模型。轻量模型来源：https://huggingface.co/ggml-org/Qwen3-0.6B-GGUF 。

本地推理使用 llama.cpp b11796（MIT），CPU 后端及 OpenMP 依赖随包提供。OpenMP 使用随官方引擎发布的 LLVM 许可说明。微软 Visual C++ Runtime 14.44.35211.0 以应用目录 DLL 形式分发，来源是已验证 Microsoft 签名和 SHA-256 的官方 VC_redist.x64.exe，不运行系统安装程序。模型、引擎、OpenMP、Microsoft 运行库和各依赖的许可原文在安装目录 / 便携包的 licenses/，源码副本在 assets/local-licenses/。

来源：https://huggingface.co/ggml-org/Qwen3-1.7B-GGUF 、https://huggingface.co/Qwen/Qwen3-1.7B 、https://github.com/ggml-org/llama.cpp/tree/0a1ef309c 。软件发布者字段 opens 是自签名身份，不表示微软或模型作者认证 / 背书。

选区兼容通道使用 Windows MSAA / oleacc.dll，并依据 Linux Foundation IAccessible2 的 AccessibleText.idl 声明只读 COM 接口；接口定义适用 BSD 许可证。许可证原文随包位于 licenses/IAccessible2-BSD.txt，来源：https://github.com/LinuxA11y/IAccessible2/blob/master/api/AccessibleText.idl 。未分发额外的 IAccessible2 二进制文件。

1.7.1 内置离线英汉词库来自 ECDICT（skywind3000/ECDICT），按该项目 MIT 许可分发。固定提交 bc015ed2e24a7abef49fc6dbbb7fe32c1dadaf8b，来源、原始 CSV 与转换产物 SHA-256 及条目数量见 assets/word-dictionary-source.json / assets/word-dictionary.json；许可全文在 licenses/ECDICT-MIT.txt（源码路径 assets/local-licenses/ECDICT-MIT.txt）。只提取符合范围的英文词键及含中文的译义，转换为只读索引；保留另行整理的常用词覆盖，不把上游词条声称为本项目原创。来源：https://github.com/skywind3000/ECDICT 。词库不是所有语境的权威译法，单词多义与上游数据错误仍可能存在。Python 仅用于构建时转换，不是应用运行依赖。

## 1.8.0 节能英译中组件

Helsinki-NLP OPUS-MT en-zh（Apache-2.0），INT8转换文件来自 Second-Dawn-Lab/owll-language-packs，固定地址与哈希见 assets/nmt-source-lock.json。CTranslate2 4.8.4（MIT，OpenNMT/SYSTRAN）、SentencePiece 0.2.0（Apache-2.0）、CPython 3.12.10（PSF）、NumPy 2.2.6（BSD及其附带声明）、PyYAML 6.0.2（MIT）均在应用目录隔离分发；Python现在是节能组件的内部运行依赖，无需用户安装。CTranslate2 随附 Intel OpenMP 运行库使用 Intel Simplified Software License。未分发 CUDA/cuDNN。Microsoft CRT 沿用上文来源与许可。

组件许可全文位于 translation-runtime/LICENSE.txt、translation-runtime/licenses/ 以及 Lib/site-packages 下的 dist-info 许可文件；模型许可位于 translation-runtime/model/APACHE-2.0.txt。Intel声明来源：https://github.com/intel/ScalableVectorSearch/blob/main/THIRD-PARTY-PROGRAMS 。CT2/SPM许可来源为各自官方仓库同版本标签。运行文件清单与SHA见 assets/nmt-package.json。

ECDICT 1.8.0 除中文译义还保留音标、英文定义与词形数据，仍使用同一固定提交和MIT许可。只按索引读取命中的词条，不把整个词库加载进内存。朗读使用 Windows SAPI 和现有系统英语语音，不分发第三方语音模型。

## 离线研习词典

五个 .ykdict 文件由中文维基词典贡献者的词条经 Wiktextract / kaikki.org 提取。数据快照为 2026-11-01。译刻将释义、词形和例句重新整理并建立离线索引，词典数据按 CC BY-SA 4.0 分发。
- 上游数据与提取说明：https://kaikki.org/zhwiktionary/rawdata.html
- 条目及作者历史：https://zh.wiktionary.org/wiki/ ，按词头打开页面并查看历史。
- 许可证：https://creativecommons.org/licenses/by-sa/4.0/
- 构建方式：node scripts/build-study-dictionaries.cjs <中文词典JSONL.gz> <输出目录>


