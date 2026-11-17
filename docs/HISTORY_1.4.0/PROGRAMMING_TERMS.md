# 编程术语解释

译刻 1.4.0 默认开启。选中 `static`、`const`、`var`、`await`、`pointer`、`泛型` 等单独术语，或翻译包含常见术语的代码 / 编程说明，浮窗在译文下方显示“编程术语 · 本机释义”。可折叠面板；滚动可读完整释义。“复制译文”只复制译文，不混入术语资料。

设置 → 翻译服务 → “显示编程术语在常见语言中的作用”可关闭。偏好会保存、导出和迁移；旧配置缺少该字段时默认开启。中文释义跟随程序界面使用中文，与翻译目标语言分开，不改变所选目标语言。

首版 60 组关键词 / 概念包括类型、绑定、静态成员、异步、类与接口、泛型、访问控制、空值、导入、资源释放、指针与引用、闭包、协程、流程控制、集合和内存。每组按适用语言说明，覆盖常见 C、C++、C#、Java、Python、JavaScript、TypeScript、Go、Rust、Kotlin、Swift，少数对照涉及 Scala。各组仅列出常见相关语言，不会为每种语言杜撰同名关键词。

资料编入程序，独立于模型响应，在本机生成，无网络请求、额外生成等待或选区周边采集；翻译服务失败时已识别释义仍可显示。普通语句里的 return、class、new 等不会仅因出现单词就自动解释：优先接受独立术语、反引号标记、代码符号、明确编程描述或辨识度较高的关键词。按标识符边界匹配，避免把 awaitable、my_static 等名称解释成关键字。关键词匹配区分大小写。

这是一份常见用法词库与语境启发式，不是编译器：不解析实际程序、不确认具体语言或语言版本，也不能消除全部误识别。浮窗明确说明“未判断所选代码的语言”。单次最多显示最先出现的 4 组，重复术语只显示一次；更多术语可缩小选区查看。词库未知内容继续正常翻译，不作虚构解释。

资料按下列官方语言文档核对后自行概括，说明的是常见用法，并不替代完整语言规范：

- [C++ 关键词（Microsoft）](https://learn.microsoft.com/en-us/cpp/cpp/keywords-cpp?view=msvc-170)
- [C# 关键词](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/) 与 [异步编程](https://learn.microsoft.com/en-us/dotnet/csharp/asynchronous-programming/async-scenarios)
- [Java 语言关键字](https://docs.oracle.com/javase/tutorial/java/nutsandbolts/_keywords.html)（该页为历史教程；本词库不把新特性或 null 字面量误称该表中的关键字）
- [Python 简单语句](https://docs.python.org/3/reference/simple_stmts.html) 与 [复合语句](https://docs.python.org/3/reference/compound_stmts.html)
- [ECMAScript 语句与声明规范](https://tc39.es/ecma262/multipage/ecmascript-language-statements-and-declarations.html)
- [TypeScript Handbook](https://www.typescriptlang.org/docs/handbook/2/everyday-types.html)
- [Go 语言规范](https://go.dev/ref/spec)
- [Rust 关键字](https://doc.rust-lang.org/book/appendix-01-keywords.html) 与 [变量 / 可变性](https://doc.rust-lang.org/book/ch03-01-variables-and-mutability.html)
- [Kotlin 关键词](https://kotlinlang.org/docs/keyword-reference.html)
- [Swift 声明参考](https://docs.swift.org/swift-book/documentation/the-swift-programming-language/declarations/)
