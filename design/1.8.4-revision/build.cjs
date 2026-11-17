const fs=require('node:fs'),path=require('node:path');
const original=fs.readFileSync(path.join(__dirname,'../1.8.1-concepts/C.html'),'utf8');
const css=`
.sheet{min-height:850px;background:radial-gradient(ellipse at 90% 20%,#35215388,transparent 60%),#080c19}
.prism .shell{padding:2px;clip-path:polygon(28px 0,calc(100% - 58px) 0,calc(100% - 30px) 28px,100% 28px,100% calc(100% - 35px),calc(100% - 35px) 100%,35px 100%,0 calc(100% - 35px),0 28px);background:linear-gradient(130deg,#88ffff,#8664cf 40%,#f38bde 75%,#68f0fa);filter:drop-shadow(0 0 8px #8a58d640)}
.prism .chrome{height:38px;padding-left:36px;background:linear-gradient(100deg,#111c32,#211830);border-bottom:1px solid #7872aa88}
.prism .chrome{padding-right:55px}
.prism .layout{height:542px}.prism .sidebar{padding-top:24px;background:linear-gradient(165deg,#211932,#0c1527);border-right:1px solid #7773aa88}
.prism .main{background:linear-gradient(140deg,#1a1930aa,#11192baa);padding-top:20px}
.heading{border-bottom:0;padding-bottom:11px;position:relative}.heading:after{content:'';position:absolute;left:0;right:95px;bottom:0;height:1px;background:linear-gradient(90deg,#66efff,#9f74d9,transparent)}
.heading:before{content:'';position:absolute;right:4px;bottom:0;width:72px;height:1px;background:#ad75db;box-shadow:-12px -5px 0 #655587}
.prism .select,.prism .card,.prism .nav,.prism .save{position:relative;background:transparent;clip-path:none;border:0;box-shadow:none;isolation:isolate}
.prism .select{padding:12px 22px;min-height:44px}.prism .card{padding:18px 23px 16px;margin-top:14px;min-height:99px}
.prism .card:after{display:none}.prism .nav{padding-left:20px;min-height:45px}.prism .nav.active{box-shadow:none}
.prism .save{padding:13px 33px;filter:drop-shadow(0 0 6px #8865bd66)}
.frame-outline{position:absolute;inset:0;width:100%;height:100%;z-index:-1;overflow:visible;pointer-events:none}
.section-title{margin-top:17px}.section-title:before{width:7px;height:7px;transform:rotate(45deg);background:transparent;border:1px solid #83f9f6;box-shadow:0 0 6px #42dfc877}
.footer{border-top:0;margin-top:22px;padding-top:16px;position:relative}.footer:before{content:'';position:absolute;left:0;right:0;top:0;height:1px;background:linear-gradient(90deg,#685283,#6b99a9,transparent)}
.prism .corner-sigil{height:43px;width:57px;background:linear-gradient(145deg,#66ffff88,#b968f088);opacity:.8}
.glyph text{font-family:'Microsoft YaHei UI';font-size:25px;font-weight:600;font-style:normal;letter-spacing:0}
.icon-card{border:1px solid #8174b5;box-shadow:inset 0 0 22px #7140a51c}.icon-card .large{width:76px;height:76px}
.caption p{color:#b5b8d1}.caption h1{font-size:23px}.glyph{filter:drop-shadow(1px 2px 1px #050816) drop-shadow(0 0 4px #72b2e144)}
`;
const script=`<script>
function outline(el,n){
 const w=el.clientWidth,h=el.clientHeight,card=el.classList.contains('card'),nav=el.classList.contains('nav'),save=el.classList.contains('save'),active=el.classList.contains('active');
 const c=card?21:save?12:nav?12:13;
 const poly=(i,k)=>'M '+(i+k)+' '+i+' H '+(w-i-k)+' L '+(w-i)+' '+(i+k)+' V '+(h-i-k)+' L '+(w-i-k)+' '+(h-i)+' H '+(i+k)+' L '+i+' '+(h-i-k)+' V '+(i+k)+' Z';
 const fill=save?'url(#b'+n+')':card?'url(#f'+n+')':active?'#29233e':'#0c1425';
 const edge=nav&&!active?'#4b5575':'url(#b'+n+')';
 const top=card?'<path fill="none" d="M 23 2 H '+(w*.40)+'" stroke="#91fff1" stroke-width="2.4"/><path fill="none" d="M '+(w*.40+7)+' 2 H '+(w*.65)+'" stroke="#d88cef" stroke-width="1.5"/><path fill="none" d="M '+(w-2)+' '+(h*.45)+' V '+(h-22)+' L '+(w-22)+' '+(h-2)+'" stroke="#db8bf2" stroke-width="2.3"/>':'';
 const svg='<svg class="frame-outline" viewBox="0 0 '+w+' '+h+'" aria-hidden="true"><defs><linearGradient id="b'+n+'"><stop stop-color="#82fff0"/><stop offset=".52" stop-color="#9c80e6"/><stop offset="1" stop-color="#f291de"/></linearGradient><linearGradient id="f'+n+'"><stop stop-color="#1b2b41"/><stop offset="1" stop-color="#2b203b"/></linearGradient></defs><path d="'+poly(1,c)+'" fill="'+fill+'" stroke="'+edge+'" stroke-width="1.5"/><path d="'+poly(5,Math.max(c-2,5))+'" fill="none" stroke="'+(save?'#f3ecff88':'#8491c648')+'" stroke-width=".7"/>'+top+'</svg>';
 el.insertAdjacentHTML('afterbegin',svg);
}
document.querySelectorAll('.card,.select,.nav,.save').forEach(outline);
</script>`;
let html=original.replace('C · 全息棱镜','修正稿 · 切角与描边强化').replace('青紫渐变 · 光轨叠层 · 棱形徽标','端正字形 · 双层斜切轮廓 · 分段青紫描边').replace('异形划词图标 · C','端正字形 · 棱镜徽标').replace('保留“译”字，增加双层边框与霓虹细节','使用正规“译”字，不倾斜、不拉伸笔画').replace('光感更明显，强调边缘与层叠空间','立体感放在底座、倒角与边缘高光').replace('外观初稿，仅用于比较方向；最终只保留你选定的一套。正式图标约 48–56 px，图中放大展示细节。','修正预览，尚未安装。强化外框、导航、内容卡片、输入框和按钮的切角描边，翻译浮窗不变。').replace('</style>',css+'</style>').replace('</html>',script+'</html>');
fs.mkdirSync(__dirname,{recursive:true});fs.writeFileSync(path.join(__dirname,'revision.html'),html);console.log('Created revision.html');
