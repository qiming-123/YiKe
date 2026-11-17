using System;
using System.Collections.Generic;

namespace YiKe.Translation;

// Reviewed basic-word overrides before the large embedded lexicon. Exact selected
// words/phrases only; no stemming, surrounding text reads or invented sentence context.
internal static class CommonWordTranslations
{
    static readonly Dictionary<string, string> Chinese = new(StringComparer.OrdinalIgnoreCase)
    {
        ["memoization"] = "记忆化（缓存已计算的结果）", ["seqlock"] = "序列锁",
        ["how"] = "如何；怎样；多么", ["what"] = "什么", ["why"] = "为什么", ["when"] = "何时；当……时", ["where"] = "哪里；在……的地方",
        ["who"] = "谁", ["which"] = "哪一个；哪些", ["whose"] = "谁的", ["whether"] = "是否", ["if"] = "如果；是否",
        ["this"] = "这个；这", ["that"] = "那个；那", ["these"] = "这些", ["those"] = "那些", ["each"] = "每个；各自",
        ["every"] = "每个；每一", ["all"] = "全部；所有", ["both"] = "两者都", ["either"] = "两者之一；任一", ["neither"] = "两者都不",
        ["before"] = "在……之前", ["after"] = "在……之后", ["while"] = "当……时；而；一段时间", ["until"] = "直到……为止", ["because"] = "因为",
        ["without"] = "没有；不使用", ["with"] = "和……一起；使用；具有", ["inside"] = "在……里面；内部", ["outside"] = "在……外面；外部", ["including"] = "包括；包含",
        ["between"] = "在两者之间", ["around"] = "在……周围；大约", ["through"] = "穿过；通过", ["above"] = "在……上方；上述", ["below"] = "在……下方；下述",
        ["please"] = "请", ["hello"] = "你好", ["goodbye"] = "再见", ["thanks"] = "谢谢", ["thank you"] = "谢谢你", ["good morning"] = "早上好",
        ["mark"] = "标记；记号；分数", ["marks"] = "标记；记号；分数（复数）；标记（动词第三人称单数）", ["marked"] = "已标记的；标记了；显著的", ["marking"] = "标记；评分",
        ["unmark"] = "取消标记", ["unmarks"] = "取消标记（动词第三人称单数）", ["unmarked"] = "未标记的；取消了标记", ["unmarking"] = "取消标记（进行形式）",
        ["select"] = "选择；选中", ["selected"] = "选中的；已选择的", ["selection"] = "选择；选区；选中的内容", ["unselect"] = "取消选择", ["deselect"] = "取消选择",
        ["check"] = "检查；勾选；核对", ["checked"] = "已检查的；已勾选的", ["uncheck"] = "取消勾选", ["unchecked"] = "未检查的；未勾选的", ["undo"] = "撤销", ["redo"] = "重做",
        ["click"] = "点击", ["press"] = "按；按下；压", ["move"] = "移动；搬动；一步棋", ["drag"] = "拖动；拖拽", ["release"] = "松开；释放；发布",
        ["open"] = "打开；开放的", ["close"] = "关闭；靠近；亲近的", ["save"] = "保存；节省；救助", ["load"] = "加载；装载；负载", ["reload"] = "重新加载",
        ["start"] = "开始；启动", ["stop"] = "停止；停下", ["continue"] = "继续", ["retry"] = "重试", ["cancel"] = "取消", ["confirm"] = "确认",
        ["add"] = "添加；相加", ["remove"] = "移除；删除", ["copy"] = "复制；副本", ["paste"] = "粘贴", ["clear"] = "清除；清晰的", ["reset"] = "重置",
        ["show"] = "显示；展示", ["hide"] = "隐藏；躲藏", ["hidden"] = "隐藏的；被隐藏的", ["visible"] = "可见的", ["invisible"] = "不可见的",
        ["uncover"] = "翻开；揭开；显露；发现", ["uncovered"] = "揭开了；未覆盖的；暴露的", ["cover"] = "覆盖；遮盖；盖子", ["reveal"] = "揭示；显露；显示",
        ["square"] = "正方形；方格；平方；广场", ["squares"] = "正方形；方格（复数）；平方数；求平方（动词第三人称单数）",
        ["rectangle"] = "长方形；矩形", ["rectangles"] = "长方形；矩形（复数）", ["circle"] = "圆；圆圈；环绕", ["circles"] = "圆；圆圈（复数）；环绕（动词第三人称单数）",
        ["grid"] = "网格；格子；栅格", ["grids"] = "网格；格子（复数）", ["cell"] = "单元格；细胞；小室", ["cells"] = "单元格；细胞；小室（复数）",
        ["board"] = "板；棋盘；委员会；登上（交通工具）", ["boards"] = "板；棋盘；委员会（复数）；登上（动词第三人称单数）", ["mine"] = "地雷；矿；我的（东西）", ["mines"] = "地雷；矿（复数）；开采（动词第三人称单数）",
        ["neighbor"] = "邻居；相邻者", ["neighbors"] = "邻居；相邻者（复数）", ["neighbour"] = "邻居；相邻者", ["neighbours"] = "邻居；相邻者（复数）",
        ["neighboring"] = "相邻的；邻近的", ["neighbouring"] = "相邻的；邻近的", ["adjacent"] = "相邻的；邻接的", ["diagonal"] = "对角线；对角的；斜的", ["diagonally"] = "沿对角线地；斜向地",
        ["random"] = "随机的；任意的", ["randomly"] = "随机地；任意地", ["automatically"] = "自动地", ["manually"] = "手动地；用手地",
        ["safe"] = "安全的；保险箱", ["safely"] = "安全地", ["dangerous"] = "危险的", ["lose"] = "输；失去；丢失", ["lost"] = "输了；失去了；丢失的", ["win"] = "赢；获胜",
        ["player"] = "玩家；选手；播放器", ["players"] = "玩家；选手；播放器（复数）", ["game"] = "游戏；比赛", ["games"] = "游戏；比赛（复数）",
        ["flag"] = "旗；标志；标记", ["flags"] = "旗；标志；标记（复数）", ["empty"] = "空的；清空", ["full"] = "满的；完整的",
        ["left"] = "左边；左侧的；离开了；剩下的", ["right"] = "右边；正确的；权利", ["up"] = "向上；在上方", ["down"] = "向下；在下方",
        ["next"] = "下一个；接下来；紧接着", ["previous"] = "上一个；以前的", ["first"] = "第一；首先", ["last"] = "最后；上一个；持续",
        ["simple"] = "简单的；简明的", ["difficult"] = "困难的", ["correct"] = "正确的；纠正", ["wrong"] = "错误的；不对的", ["important"] = "重要的",
        ["required"] = "必需的；要求的", ["optional"] = "可选的；非必需的", ["available"] = "可用的；可获得的", ["unavailable"] = "不可用的；无法获得的",
        ["success"] = "成功", ["failure"] = "失败；故障", ["error"] = "错误；差错", ["warning"] = "警告；提醒", ["help"] = "帮助；求助",
        ["word"] = "单词；词；话语", ["words"] = "单词；词；话语（复数）", ["sentence"] = "句子；判决", ["sentences"] = "句子；判决（复数）",
        ["meaning"] = "含义；意思", ["example"] = "例子；示例", ["examples"] = "例子；示例（复数）", ["detail"] = "细节；详情", ["details"] = "细节；详情（复数）"
    };
    internal static int Count => Chinese.Count;
    internal static string? Find(string source, string target)
    {
        bool simplified = target is "简体中文" or "中文" or "汉语" || target.Equals("zh", StringComparison.OrdinalIgnoreCase) || target.Equals("zh-CN", StringComparison.OrdinalIgnoreCase) || target.Equals("Simplified Chinese", StringComparison.OrdinalIgnoreCase);
        if (!simplified) return null;
        return Chinese.TryGetValue(source.Trim(), out string? value) ? value : BundledEnglishDictionary.Find(source);
    }
}
