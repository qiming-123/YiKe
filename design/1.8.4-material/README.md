# YIKE 材质概念稿 v1

使用内置 image_gen 生成，2026-11-15。

文件：material-study-v1.png

概念稿已根据用户确认接入开发版 WPF，尚未打包或安装。此前 1.8.4-revision 稿保留。程序集仍使用现有 1.8.3 版本号，本目录名是设计迭代编号。

方向：定制 YK 金属折面字标、暗色玻璃棱晶图标、青紫边缘反光、具备厚度的倒角玻璃面板。

## WPF 实现

- MaterialMark.cs：分面填充的独立棱晶与 YK 矢量字标，高对比度系统使用单色轮廓；无位图背景、动画或新计时器。
- CyberPrism.cs：窗口、卡片、按钮的多层金属倒角和暗色反光；透明标题栏按钮保留简洁外观。
- SettingsWindow.cs：主界面 YK / YIKE 品牌与深色侧栏。
- SelectionIcon 通过原有 Badge 入口使用棱晶；点击翻译逻辑保留。翻译浮窗内容及样式未改动。
- 实现为概念稿的原生矢量化版本；不等同于概念渲染中的拉丝纹理、真实折射和棚拍反射。任务栏与安装包的旧 ICO 尚未替换。

实机 WPF 排版截图：settings-wpf-v1.png；划词图标：selection-prism-wpf-v1.png。

验证：Release 构建成功，0 警告、0 错误；现有 --visual 检查通过，覆盖设置、在线设置、模型/语言下拉和翻译浮窗排版。此验证不代表实时翻译或高对比度桌面验收。

## 完整生成提示词

Use case: logo-brand / stylized-concept.
Create a premium art-directed material and identity concept board for a desktop translation app called YIKE. This is a NEW visual concept, not a screenshot reproduction. Landscape 1536x1024 composition on near-black midnight navy. Typography exact: "YIKE", "01 / MONOGRAM", "02 / PRISM", "MATERIAL STUDY".
Top left small elegant tracked YIKE label and MATERIAL STUDY. Two spacious columns: left a large bespoke YK monogram, right an abstract crystalline symbol for translation. Bottom narrow strip: a restrained application material sample with a smoked glass card and a small custom YIKE wordmark.
The YK must be custom sculptural lettering, angular, expertly optically balanced, recognizable Y and K, interlocking but legible; machined titanium with broad chamfered bevels, satin silver faces, darker side faces and subtle cyan left / violet right reflected light. It must look designed rather than a stock font. The abstract mark should be an original precise four-direction faceted prism, with internal negative space and a dark glass crystal core, crisp polished metal facets, translucent violet and cyan optical depth. Jewel-like dimensionality, physically believable material thickness and reflection.
Art direction: premium dark smoked glass, polished titanium bevels, subtle iridescent spectral edges, luminous cyan/violet accents confined to edges, elegant editorial layout, large negative space, realistic studio product render, front three-quarter shallow perspective. Each symbol is displayed prominently about 300px tall. Small matching 48px versions beneath each main symbol for practical icon evaluation. Card sample has a substantial bevel frame and layered smoked glass; typography clean and restrained. Avoid generic hexagon badges, Chinese glyphs, wireframe-only surfaces, excessive neon bloom, gaming HUD clutter, ornamental text, rainbow flood, plastic surfaces, illegible arbitrary lettering.
