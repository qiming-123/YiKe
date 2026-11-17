using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace YiKe.Translation;

// Reviewed, bundled explanations: no second model request, editor context or network access.
// These are common usages, not a parser or a claim about the language of the selection.
public static partial class ProgrammingGlossary
{
    public const int MaximumTerms = 4;
    sealed class Entry(string name, string aliases, string usage, bool distinctive = false, bool ignoreCase = false, bool technical = false, string[]? symbols = null, bool command = false)
    {
        public string Name { get; } = name;
        public string Usage { get; } = usage;
        public bool Distinctive { get; } = distinctive;
        public bool Technical { get; } = technical;
        public bool IgnoreCase { get; } = ignoreCase;
        public bool Command { get; } = command;
        public Regex Pattern { get; } = new(string.Join("|", aliases.Split('|').Concat(symbols ?? Array.Empty<string>()).Where(a => a.Length > 0).OrderByDescending(a => a.Length).Select(a => a.Length > 1 && a.Any(c => c >= '\u3400' && c <= '\u9fff') ? Regex.Escape(a) : @"(?<![\p{L}\p{N}\p{M}_])" + Regex.Escape(a).Replace("\\ ", @"[ \t]+") + @"(?![\p{L}\p{N}\p{M}_])")), RegexOptions.CultureInvariant | (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None));
    }
    static readonly Regex Context = new(@"编程|代码|关键字|编译|变量|函数|方法|类型推断|(?<![\p{L}\p{N}_])(?:programming|code|compiler|keyword|JavaScript|TypeScript|Python|Java|Rust|Kotlin|Swift|C#|C\+\+)(?![\p{L}\p{N}_])|=>|::|->|:=|[{}]|\b\w+\s*\([^\r\n)]*\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    static readonly System.Collections.Generic.HashSet<string> DistinctiveTokens = new(StringComparer.Ordinal)
        { "const", "var", "val", "async", "await", "struct", "enum", "virtual", "override", "readonly", "nullptr", "undefined", "def", "fn", "fun", "func", "lambda", "mut", "impl", "trait", "defer", "chan", "sizeof", "goroutine", "goroutines", "int", "bool", "void" };
    static readonly Entry[] Entries =
    {
        new("static · 静态", "static|静态", "C/C++：局部变量保留跨调用的值；文件作用域可限制链接范围。C++ 静态成员属于类。\nJava / C#：成员属于类型，不依赖某个实例；不表示值不可改变。\nJavaScript / TypeScript：类的静态成员由类本身访问。\nRust：声明具有静态生命周期的存储项。"),
        new("const · 常量 / 只读限定", "const|常量", "C/C++：限制通过该声明修改值；指针本身只读与所指对象只读有区别。\nJavaScript / TypeScript：块级绑定不能重新赋值，对象内部仍可能改变。\nC#：声明编译期常量。Go：声明常量。\nRust：声明常量项，通常需显式类型。Java 的 const 是保留字，常用 final 表示不能重新赋值。", true),
        new("let · 绑定", "let", "JavaScript / TypeScript：声明可重新赋值的块级变量。\nRust：创建默认不可变的绑定；let mut 允许修改。\nSwift：声明常量绑定；引用指向的对象仍可能具有可变属性。"),
        new("var · 变量声明", "var", "JavaScript：声明函数作用域或脚本全局作用域变量，不具有 let 的块级作用域。\nC# / Java：在允许的位置由初始化表达式推断局部变量类型，仍是静态类型。\nGo：声明变量，可显式指定类型或根据初值推断。\nKotlin / Swift：声明可重新赋值的变量。", true),
        new("val · 只读绑定", "val", "Kotlin：声明只读属性或局部变量；不能经该名称重新赋值，但对象内容未必不可变。\nScala：声明不可重新赋值的绑定。", true),
        new("async · 异步", "async|异步", "C#：允许方法内使用 await；并不自动创建后台线程。\nJavaScript / TypeScript：函数返回 Promise，可在函数内 await。\nPython：async def 定义协程函数；协程需要等待或调度才执行。\nRust：构造 Future，由执行器轮询推进。", true),
        new("await · 等待异步结果", "await", "C# / JavaScript / TypeScript / Python：等待可等待对象；未就绪时可挂起当前异步流程，不等于阻塞整个线程。\nRust：使用 future.await 等待 Future 完成，需要处于异步上下文。", true),
        new("yield · 产出 / 让出", "yield", "Python / JavaScript：生成器产出一个值，保留状态供下次继续执行。\nC#：yield return 逐项产出迭代结果；yield break 结束迭代。\nJava：在 switch 表达式的语句块中提供结果，不是生成器语法。"),
        new("return · 返回", "return|返回值", "C / C++ / Java / C# / JavaScript / Python / Go / Rust：结束当前函数的执行，并在允许时返回结果。\nPython：未指定返回值时返回 None。Rust：函数末尾的表达式也可隐式返回。"),
        new("class · 类", "class|类", "C++：定义类，成员和基类默认 private；struct 默认 public。\nJava / C#：定义类；通过实例封装状态与行为，也可声明静态成员。\nPython：执行类体并创建类对象，方法通常显式接收 self。\nJavaScript / TypeScript：声明基于原型机制的类。"),
        new("struct · 结构体", "struct|结构体", "C：将多个字段组合为一个类型。\nC++：与 class 能力接近，主要区别是默认访问权限。\nC# / Swift：声明值类型。Go / Rust：声明由字段组成的类型。", true),
        new("interface · 接口", "interface|接口", "通用：组件之间约定的交互边界；网络接口则是网络收发入口，需结合语境。\nJava / C#：声明类型契约，具体允许的实现形式依语言版本而定。\nTypeScript：描述结构化类型，通常编译后被擦除。\nGo：以方法集定义接口，满足方法集的类型隐式实现。\nC++ 无 interface 语言关键字，常用纯虚基类表达接口。"),
        new("enum · 枚举", "enum|枚举", "C / C++ / C#：定义一组具名常量；C++ 的 enum class 提供作用域和更强类型约束。\nJava：枚举实例还可有字段、方法和构造逻辑。\nRust / Swift：枚举变体可携带关联数据。TypeScript：普通 enum 通常生成运行时代码。", true),
        new("template · 模板", "template|模板", "C++：定义以类型或值为参数的函数、类等模板，编译期按规则实例化。\nJava / C# / TypeScript：相近需求通常使用泛型，机制与 C++ 模板不同。\nPython：常以动态类型或类型参数表达类似抽象，不使用 template 关键字。"),
        new("generic · 泛型", "generic|generics|泛型", "Java：以类型参数复用代码，主要采用类型擦除机制。\nC#：类型参数保留在运行时类型系统中。\nTypeScript：提供编译期类型约束，类型信息通常被擦除。\nGo / Rust：支持带约束的类型参数。C++ 通常以模板实现泛型编程。", true),
        new("virtual · 虚成员", "virtual|虚函数", "C++：虚函数支持通过基类指针或引用进行动态分派。\nC#：允许派生类通过 override 重写成员。\nJava：普通可覆盖实例方法默认采用动态分派，无 virtual 关键字。", true),
        new("override · 重写", "override|重写", "C++：标明函数应重写基类虚函数，由编译器核验。\nC# / Kotlin / Swift：显式声明重写基类成员。\nJava：@Override 注解帮助核验重写；不是 override 关键字。\nTypeScript：override 修饰符检查基类是否有相应成员。", true),
        new("abstract · 抽象", "abstract|抽象类", "Java / C# / TypeScript：抽象类不能直接实例化；抽象成员由具体派生类实现。\nC++：含纯虚函数的类可为抽象类，无 abstract 标准关键字。\nPython：可借助 abc.ABC 与 @abstractmethod 声明抽象接口。", true),
        new("final · 不再改变 / 继承", "final", "Java：变量不能重新赋值；方法不能被重写；类不能被继承。对象内容仍可能可变。\nC++：限制虚函数被进一步重写，或限制类被继承。\nKotlin：类和成员通常默认 final，需 open 等方式允许继承或重写。", true),
        new("readonly · 只读", "readonly|只读", "C#：readonly 字段可在声明或对应构造函数中赋值，引用对象内容未必只读；也用于只读结构体和成员。\nTypeScript：限制通过该类型修改属性，通常只在类型检查时生效。\nC/C++：通常使用 const 表达只读限定，没有通用 readonly 关键字。", true),
        new("public / private / protected · 可见性", "public|private|protected|访问修饰符", "C++ / Java / C#：控制成员或类型的可访问范围；protected 与包、程序集、继承的关系因语言而异。\nTypeScript：访问修饰符主要用于类型检查；JavaScript 的 #私有字段具有运行时私有语义。\nPython：下划线主要是约定，双下划线还会触发名称改写，不等于上述访问修饰符。", true),
        new("this / self · 当前接收者", "this|self", "C++：this 是指向当前对象的指针。Java / C#：this 表示当前实例。\nJavaScript：普通函数的 this 取决于调用方式；箭头函数捕获外层 this。\nPython：self 是实例方法首参数的惯用名称，不是关键字。\nRust：self 作为方法接收者，可按值或借用接收。"),
        new("super · 父类访问", "super", "Java：访问父类成员或调用父类构造函数。\nJavaScript / TypeScript：在派生类中调用父类构造函数或访问父类成员。\nPython：super() 沿方法解析顺序委托，尤其用于协作式多重继承。"),
        new("null / nullptr / None / nil · 空值", "null|nullptr|None|nil|空值", "Java / C# / JavaScript：null 表示空引用或显式空值；具体类型规则不同。\nC++：nullptr 是空指针字面量。Python：None 是表示无值的单例。\nGo：nil 用于指针、切片、映射、通道、函数和接口等可空类型；接口内含有类型的 nil 指针不等于 nil 接口。\nSwift：nil 表示可选值没有值。Rust 通常使用 Option::None。", true),
        new("undefined · 未定义值", "undefined", "JavaScript / TypeScript：常见于未赋值变量、缺失属性和没有显式返回值的函数。\n与 null 不同；TypeScript 的严格空值检查会进一步限制其可赋值性。\nPython / Java 等不能直接套用 JavaScript 的 undefined 语义。", true),
        new("import · 导入", "import|导入模块", "Python：查找并加载模块，将模块或其中的名称绑定到当前作用域。\nJava：在源码中引入类型或静态成员的简写名称，不是文本复制。\nJavaScript / TypeScript：导入模块导出的绑定；TypeScript 的类型导入可被擦除。\nGo：声明当前文件依赖的包。"),
        new("include · 头文件包含", "include|头文件", "C / C++：#include 由预处理器引入文件内容，常用于头文件声明，不等于模块导入。\nPython / Java / JavaScript 等通常用 import，不使用相同的预处理包含机制。"),
        new("using · 引用 / 资源管理", "using", "C#：using 指令引入命名空间或别名；using 语句 / 声明在离开作用域时释放 IDisposable 资源。\nC++：using 可声明类型别名、引入名字或命名空间；不会自动执行 C# 的资源释放语义。"),
        new("def / fn / function · 定义函数", "def|fn|function|fun|func|函数", "Python：def 定义函数，async def 定义协程函数。\nRust：fn 声明函数。JavaScript / TypeScript：function 声明函数或构造函数表达式。\nKotlin：fun 声明函数。Go：对应关键字为 func。", true),
        new("lambda · 匿名函数", "lambda|匿名函数", "Python：lambda 用单个表达式构造匿名函数。\nC++：用捕获列表和函数体创建闭包对象。\nJava / C#：lambda 表达式表示可传递的行为；目标类型分别常为函数式接口、委托或表达式树。\nJavaScript：箭头函数是常见写法，this 行为与普通函数不同。", true),
        new("closure · 闭包", "closure|闭包", "JavaScript / Python：函数可继续访问其词法作用域中的绑定。\nC++：lambda 的捕获列表决定按值或引用捕获。\nRust：闭包可借用或取得捕获值的所有权，并按使用方式实现 Fn、FnMut 或 FnOnce。", true),
        new("pointer · 指针", "pointer|pointers|指针", "C / C++：保存地址，解引用访问对象；生命周期和边界需要正确管理。\nGo：有指针但通常不提供 C 风格的普通指针算术。\nRust：引用受借用规则检查；原始指针的解引用需要 unsafe。\nJava / Python / JavaScript：主要通过对象引用操作，不直接采用 C 式指针操作。", true),
        new("reference · 引用", "reference|references|引用", "C++：T& 是引用，初始化后不能改绑到另一对象；不同于指针。\nRust：&T / &mut T 表示共享 / 可变借用，受生命周期规则约束。\nJava / C#：引用类型变量保存对象引用；C# 的 ref 还可按引用传递存储位置。\nPython：名称绑定到对象；赋值通常不会复制整个对象。"),
        new("mut · 可变", "mut", "Rust：在绑定、引用或指针类型中标明可变性，例如 let mut x 与 &mut T；仍需遵守借用规则。\nJavaScript 的 let、Kotlin 的 var 可表达可重新赋值，但并非 Rust 借用规则的等价物。", true),
        new("impl / trait · 实现与特征", "impl|trait", "Rust：trait 声明共享行为契约；impl 为类型添加固有方法或实现 trait。\nJava / C#：相近的行为契约常用 interface 表达，具体约束和实现机制不同。", true),
        new("match · 模式匹配", "match|模式匹配", "Rust：按模式选择分支，匹配需要穷尽。\nPython：match / case 进行结构化模式匹配，并非简单字符串相等比较。\nC#：常通过 switch 表达式或 is 模式表达相近需求。"),
        new("defer · 延后清理", "defer", "Go：把调用延后到当前函数返回时执行；参数在 defer 语句处求值，多次延后调用按后进先出顺序执行。\nSwift：在离开当前作用域时执行 defer 块。\nC++ 常用 RAII，C# 常用 using 表达资源清理需求。", true),
        new("goroutine / go · 并发任务", "goroutine|goroutines|go", "Go：go f() 启动由 Go 运行时调度的 goroutine，不等于为每次调用固定创建一个系统线程。\n与其他语言的 async / await 不是相同语法或调度模型。"),
        new("chan · 通道", "chan|channel|通道", "Go：声明用于 goroutine 间传递值与同步的通道类型，可区分双向、只发送和只接收。\nRust：标准库 mpsc 等提供消息通道，通常通过库而非 chan 关键字使用。", true),
        new("new · 创建对象", "new", "C++：new 表达式分配存储并初始化对象；需正确管理所有权，可用智能指针。\nJava / C#：创建类型实例，引用对象通常由垃圾回收管理。\nJavaScript：以构造调用方式创建对象。\nGo：new(T) 返回指向 T 零值的指针，不同于 make。"),
        new("try / catch / except / finally · 异常处理", "try|catch|except|finally|异常处理", "C++ / Java / C# / JavaScript：try 配合 catch 处理异常；C++ 没有标准 finally 语法。\nPython：try 配合 except 捕获异常，finally 负责离开时的清理。\nGo 通常显式返回 error；Rust 通常使用 Result，而非相同的异常机制。"),
        new("throw / raise · 抛出异常", "throw|raise|抛出异常", "C++ / Java / C# / JavaScript：throw 抛出异常或异常值，交给匹配的处理逻辑。\nPython：对应关键字为 raise。\nRust 的 ? 常用于传播 Result / Option 的提前返回，不等同于抛出异常。", true),
        new("sizeof · 大小", "sizeof", "C / C++：查询类型或对象表示占用的字节数，结果不是指针所指动态数组的长度。\nC#：用于获取非托管类型的大小，部分用法需要 unsafe 上下文。\nPython 的 sys.getsizeof 是库函数，且通常不递归计算所有引用对象。", true),
        new("auto · 类型推断", "auto|类型推断", "C++：现代代码中由初始化表达式推断变量类型，仍是编译期静态类型。\nC# / Java：相近局部类型推断常使用 var。Rust：常省略类型标注由编译器推断。\n这不表示变量可以任意改变为另一种类型。"),
        new("if / else · 条件分支", "if|else|条件分支", "C / C++ / Java / C# / JavaScript / Python / Go：按条件选择执行路径；布尔转换规则各不相同。\nRust / Kotlin：if 还可用作产生值的表达式。"),
        new("for / while · 循环", "for|while|循环", "C / C++ / Java / C# / JavaScript：有条件循环及各自的遍历形式。\nPython：for 遍历可迭代对象，while 按条件重复。\nGo：for 统一承担多种循环形式；Rust：for 遍历迭代器，while 按条件循环。"),
        new("break / continue · 循环控制", "break|continue", "多数语言：break 结束目标循环，continue 跳过本轮余下语句并继续下一轮。\nJavaScript / Java / Go / Rust 等还支持各自的标签规则；break 是否可带值取决于语言和所在结构。"),
        new("recursion · 递归", "recursion|recursive|递归", "通用：函数直接或间接调用自身，需有可到达的终止条件。\nPython / Java / C# / C++ 等：调用过深可能耗尽调用栈；不能假定所有语言或构建都会优化尾递归。", true),
        new("inheritance · 继承", "inheritance|继承", "Java / C#：类通常单继承，可实现多个接口。\nC++ / Python：支持类的多重继承，成员解析规则不同。\nRust / Go：常用 trait / 接口与组合表达复用，不采用同样的类继承体系。", true),
        new("polymorphism · 多态", "polymorphism|多态", "通用：以统一接口使用不同具体类型。\nC++：可通过虚函数进行运行时多态，也可通过模板进行静态多态。\nJava / C#：接口与可覆盖实例成员常用于运行时多态。\nRust：可使用泛型约束的静态分派，或 dyn trait 的动态分派。", true),
        new("coroutine · 协程", "coroutine|coroutines|协程", "Python：async def 创建协程流程，通常由事件循环驱动。\nC++20：使用 co_await / co_yield / co_return，具体调度由配套类型和库决定。\nKotlin：suspend 函数可挂起，常配合协程库；协程不等于独占一个系统线程。", true),
        new("int · 整数", "int|整数", "C / C++：有符号整数类型，实际宽度受实现约束，不应一概假定为 32 位。\nJava / C#：int 为 32 位有符号整数。Go：int 宽度与实现 / 架构相关。\nPython：int 为任意精度整数，实际大小受可用内存限制。", true),
        new("float / double · 浮点数", "float|double|浮点数", "Java / C#：float 通常为 32 位、double 为 64 位二进制浮点数。\nC / C++：浮点表示和精度依实现约束；float 不保证能精确表示所有小数。\nPython：float 通常对应 C 的 double。JavaScript：普通 Number 采用双精度二进制浮点数。"),
        new("bool / boolean · 布尔值", "bool|boolean|布尔", "C++ / C# / Go / Rust：bool 表示真假。Java 使用 boolean。\nPython：bool 的实例为 True / False。JavaScript / TypeScript 使用 boolean 值。\n不同语言对数字、对象、空值的条件转换规则不同。", true),
        new("string / str · 字符串", "string|str|字符串", "C：字符串通常是以零字符结尾的 char 序列，长度不包含结尾零。\nC++：std::string 管理字符序列。Java / C#：String / string 为不可变字符串对象。\nPython：str 表示不可变 Unicode 文本。Go：string 是不可变字节序列，未必是有效 UTF-8。", true),
        new("void · 无返回值", "void", "C / C++ / Java / C#：常用于表示函数或方法没有返回值。\nC / C++：void* 可表示通用对象指针，不能直接当作已知类型对象解引用。\nTypeScript：void 用于不使用返回结果的类型语境。Python 通常以 None 对应无显式返回值。", true),
        new("array · 数组", "array|arrays|数组", "C / C++：内建数组保存固定数量的同类型元素，不自动携带运行时边界检查。\nJava / C#：数组长度固定，元素访问有边界检查。\nJavaScript：Array 可动态增长且可混合元素。Python 的 list 是常用动态序列，和定长数组不同。\nGo：数组长度属于类型的一部分，slice 则是对底层数组片段的描述。"),
        new("dictionary / map · 映射", "dictionary|dict|map|映射|字典", "Python：dict 按键保存值。Java / C#：Map / Dictionary 提供键值映射。\nC++：std::map 通常按键有序，std::unordered_map 使用哈希组织。\nGo：map 是内建映射类型；JavaScript 的 Map 支持任意值作为键。\n不要把数据结构 map 与函数式的逐项映射操作混为一谈。"),
        new("heap / stack · 堆与栈", "heap|stack|堆内存|栈内存", "通用：调用栈常用于函数调用状态；堆常用于动态分配，实际布局可被运行时或编译器优化。\nC++：自动存储期对象通常随作用域销毁，动态对象需正确管理所有权。\nJava / C#：对象通常由垃圾回收管理；不能仅凭变量写在函数内就断言对象在栈上。\n算法中的 heap 也可指堆数据结构，需要结合语境。"),
        new("encapsulation · 封装", "encapsulation|封装", "通用：把状态和操作组织在一起，通过受控接口维护约束。\nJava / C# / C++：常以类和访问控制限制外部直接修改。\nPython：常结合属性与命名约定。Go：通过包与导出标识符控制可见性。", true)
    };

    static readonly Regex ComputerContext = new(@"计算机|电脑|操作系统|内核|服务器|命令行|终端|数据库|网络协议|数据结构|算法|机器学习|深度学习|信息安全|权限|容器|(?<![\p{L}\p{N}_])(?:Linux|Unix|Bash|shell|terminal|computer|computing|software|hardware|server|database|network|algorithm|kernel|Docker|Kubernetes|SQL|TCP|HTTP|HTTPS|DNS|TLS|API|CPU|GPU|Git)(?![\p{L}\p{N}_])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    static readonly Regex CommandContext = new(@"(?m)^[ \t]*(?:[$#][ \t]+)?(?:cat|less|head|tail|find|sort|uniq|wc|touch|file|stat|ps|top|kill|mount|ip|ss|free|man)[ \t]+(?:-[A-Za-z0-9]|~?/|\.\.?(?:/|[ \t]|$)|[^\s]+\.[A-Za-z0-9]|\d+(?:[ \t]|$))", RegexOptions.CultureInvariant);
    // A bare word is not evidence of a computing sense. Unrecognized words use the
    // ordinary translator unless the selection supplies context or a specific hint.
    static readonly Regex TechnicalHint = new(@"(?<![\p{L}\p{N}_])(?:memoization|seqlock|io_uring|RCU|NUMA|SIMD|eBPF|futex|epoll|kqueue|Bloom filter|hazard pointer|false sharing|lock-free|wait-free|zero-copy|type erasure|tail call|JIT|AOT|SFINAE|RAII)(?![\p{L}\p{N}_])|零拷贝|记忆化|无锁算法|尾调用|类型擦除", RegexOptions.CultureInvariant);
    public static int TermCount => Entries.Length + ComputerEntries.Length;
    public sealed record Analysis(string Notes, bool RequestModelNotes, string KnownTerms = "");

    public static string Explain(string text) => Analyze(text).Notes;
    internal static string? TranslateTerm(string text)
    {
        string selected=text.Trim();
        var entry=Entries.Concat(ComputerEntries).FirstOrDefault(item=> {var match=item.Pattern.Match(selected);return match.Success&&match.Index==0&&match.Length==selected.Length;});
        if(entry is null)return null;
        int separator=entry.Name.IndexOf('·');
        return separator>=0 ? entry.Name[(separator+1)..].Trim() : null;
    }
    public static Analysis Analyze(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || !Core.TextLimit.Allowed(text)) return new("", false);
        if (TerminalCommands.Explain(text) is { } command) return new(command.Notes, command.NeedsModel, command.KnownTerms);
        var candidates = Entries.Concat(ComputerEntries).Select(entry => (Entry: entry, Match: entry.Pattern.Match(text))).Where(item => item.Match.Success).OrderBy(item => item.Match.Index).ThenByDescending(item => item.Match.Length).ThenBy(item => item.Entry.IgnoreCase).ToArray();
        // Prefer a full phrase (git rebase / virtual memory) over a nested generic word.
        var matches = candidates.Where(item => !candidates.Any(other => other.Entry != item.Entry && other.Match.Index <= item.Match.Index && other.Match.Index + other.Match.Length >= item.Match.Index + item.Match.Length && (other.Match.Length > item.Match.Length || (other.Match.Length == item.Match.Length && item.Entry.IgnoreCase && !other.Entry.IgnoreCase)))).ToArray();
        string isolated = text.Trim().Trim('`', '\'', '"', '“', '”', '‘', '’').Trim();
        bool wholeTerm = matches.Any(item => string.Equals(item.Match.Value, isolated, StringComparison.OrdinalIgnoreCase));
        bool markedTerm = matches.Any(item => text.Contains("`" + item.Match.Value + "`", StringComparison.Ordinal));
        bool technical = ComputerContext.IsMatch(text) || CommandContext.IsMatch(text) || matches.Any(item => item.Entry.Technical);
        bool show = wholeTerm || markedTerm || technical || Context.IsMatch(text) || matches.Any(item => item.Entry.Distinctive && DistinctiveTokens.Contains(item.Match.Value));
        bool knownCommand = matches.Any(item => item.Entry.Command && item.Match.Value != "sudo" && text.TrimStart().StartsWith(item.Match.Value, StringComparison.Ordinal) && item.Match.Index == text.Length - text.TrimStart().Length);
        bool model = !wholeTerm && !knownCommand && (technical || Context.IsMatch(text) || TechnicalHint.IsMatch(text)) && !text.Contains(TechnicalOutput.Marker, StringComparison.Ordinal);
        if (matches.Length == 0 || !show) return new("", model);
        var output = new StringBuilder("本机词库 · 用途与示例（中文；示例仅作说明，不自动执行）\n");
        foreach (var item in matches.Take(MaximumTerms))
        {
            output.Append('\n').Append(item.Entry.Name).Append('\n').Append(item.Entry.Usage).Append('\n');
            if (Example(item.Entry) is { } example) output.Append("\n示例：").Append(example.Code).Append("\n效果：").Append(example.Result).Append('\n');
        }
        if (matches.Length > MaximumTerms) output.Append("\n另有 ").Append(matches.Length - MaximumTerms).Append(" 项术语；缩小选区可查看其释义。");
        return new(output.ToString().TrimEnd(), model, string.Join("; ", matches.Take(MaximumTerms).Select(item => item.Entry.Name)));
    }
}
