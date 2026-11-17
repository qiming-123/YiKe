using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using YiKe.Core;
using YiKe.UI;
using YiKe.Data;
using System.IO;

// Real WPF window layout fixtures. Synthetic text is labelled; these do not certify live API translation.
public static class VisualChecks
{
    public static int Run()
    {
        int result=0;
        var thread=new Thread(()=>
        {
            try
            {
                var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
                Theme.Apply("dark"); Theme.InstallStyles();
                var directory=Path.Combine(Environment.CurrentDirectory,"artifacts","visual-qa");Directory.CreateDirectory(directory);
                foreach(string theme in new[]{"dark"})
                {
                    Theme.Apply(theme);var settings=new AppSettings{Theme=theme,LocalEngine="nmt",Model="QA fixture — no request"};
                    var selectionIcon=new SelectionIcon();selectionIcon.Present(new(1,0,Environment.ProcessId,"layout fixture",500,300,DateTimeOffset.UtcNow));
                    Capture(selectionIcon,Path.Combine(directory,theme+"-selection-icon.png"));selectionIcon.Clear();selectionIcon.Close();
                    var window=new PopupWindow();
                    window.Begin("[UI layout fixture] First paragraph.\n\nSecond paragraph: 42.",settings,500,300,0);
                    window.BeginAnimation(UIElement.OpacityProperty,null);window.Opacity=1;
                    Capture(window,Path.Combine(directory,theme+"-loading.png"));
                    window.Update(new(1,"[界面排版测试 · 非真实 API 结果]\n\n最好的工具，会自然融入你的工作流程。\n\n版本 42 已就绪。\n\n"+string.Join("\n",Enumerable.Repeat("长文本滚动区域：保留段落、数字和必要格式。",24)),true));
                    Capture(window,Path.Combine(directory,theme+"-long.png"));
                    window.Update(new(1,"部分内容",Error:"连接中断，译文未完成。请主动重试。"));
                    Capture(window,Path.Combine(directory,theme+"-error.png")); window.Close();
                    var programming=new PopupWindow();programming.Begin("static",settings,500,300,0);
                    programming.BeginAnimation(UIElement.OpacityProperty,null);programming.Opacity=1;
                    programming.Update(new(1,"[界面排版测试] 静态",true,ProgrammingNotes:YiKe.Translation.ProgrammingGlossary.Explain("static")));
                    Capture(programming,Path.Combine(directory,theme+"-programming.png"));
                    ScrollExamples(programming);Capture(programming,Path.Combine(directory,theme+"-programming-example.png"));programming.Close();
                    var linux=new PopupWindow();linux.Begin("chmod 755 script.sh",settings,500,300,0);
                    linux.BeginAnimation(UIElement.OpacityProperty,null);linux.Opacity=1;
                    linux.Update(new(1,"[界面排版测试] 修改 script.sh 的权限。",true,ProgrammingNotes:YiKe.Translation.ProgrammingGlossary.Explain("chmod 755 script.sh")));
                    Capture(linux,Path.Combine(directory,theme+"-linux.png"));
                    ScrollExamples(linux);Capture(linux,Path.Combine(directory,theme+"-linux-example.png"));linux.Close();
                    var terminal=new PopupWindow();terminal.Begin("grep -n -i 'error' ./demo/app.log",settings,500,300,0);
                    terminal.BeginAnimation(UIElement.OpacityProperty,null);terminal.Opacity=1;
                    terminal.Update(new(1,"[界面排版测试] 在日志中查找 error，并显示行号。",true,ProgrammingNotes:YiKe.Translation.ProgrammingGlossary.Explain("grep -n -i 'error' ./demo/app.log")));
                    Capture(terminal,Path.Combine(directory,theme+"-terminal-command.png"));ScrollExamples(terminal);
                    Capture(terminal,Path.Combine(directory,theme+"-terminal-example.png"));terminal.Close();
                    var word=new PopupWindow();word.Begin("how",settings,500,300,0);word.BeginAnimation(UIElement.OpacityProperty,null);word.Opacity=1;
                    word.Update(new(1,YiKe.Translation.CommonWordTranslations.Find("how","简体中文")!,true,ProgrammingNotes:YiKe.Translation.ComputingWordNotes.Explain("how")));
                    Capture(word,Path.Combine(directory,theme+"-ordinary-how.png"));word.Close();
                    var pendingWord=new PopupWindow();pendingWord.Begin("how",settings,500,300,0);pendingWord.BeginAnimation(UIElement.OpacityProperty,null);pendingWord.Opacity=1;
                    pendingWord.Update(new(1,"如何；怎样；多么",true,ProgrammingNotes:YiKe.Translation.ComputingWordNotes.Explain("how"),ExplanationPending:true));
                    Capture(pendingWord,Path.Combine(directory,theme+"-word-notes-pending.png"));pendingWord.Close();
                    var store=new SettingsStore(userDirectory:Path.Combine(directory,"isolated-data"));
                    var w=new SettingsWindow(()=>settings,(_,_)=>{},store,new CredentialStore(store.UserDirectory),()=>"UI 布局测试，不代表 API 验收。",modelCatalog:new RecordingModelCatalog());w.Show();Capture(w,Path.Combine(directory,theme+"-settings.png"));
                    var mode=Walk(w).OfType<ComboBox>().Single(c=>AutomationProperties.GetName(c)=="翻译方式");mode.SelectedIndex=1;Capture(w,Path.Combine(directory,theme+"-online-settings.png"));
                    w.AddOnlineProfile();
                    var profileName=Walk(w).OfType<TextBox>().Single(c=>AutomationProperties.GetName(c)=="配置名称");profileName.Text="备用服务 · 排版示例";
                    var profiles=Walk(w).OfType<ComboBox>().Single(c=>AutomationProperties.GetName(c)=="在线 API 配置");profiles.BringIntoView();
                    Capture(w,Path.Combine(directory,theme+"-multiple-api.png"));
                    var model=Walk(w).OfType<ComboBox>().Single(c=>AutomationProperties.GetName(c)=="模型名称");
                    model.ItemsSource=new[]{"fixture-chat-a","fixture-chat-b"};model.Text="fixture-chat-a";model.BringIntoView();model.IsDropDownOpen=true;
                    CapturePopup(model,Path.Combine(directory,theme+"-models.png"));model.IsDropDownOpen=false;
                    var language=Walk(w).OfType<ComboBox>().Single(c=>AutomationProperties.GetName(c)=="目标语言");
                    language.BringIntoView();language.IsDropDownOpen=true;CapturePopup(language,Path.Combine(directory,theme+"-languages.png"));language.IsDropDownOpen=false;
                    Capture(w,Path.Combine(directory,theme+"-language-settings.png"));w.Close();
                }
                app.Shutdown();Console.WriteLine("PASS WPF cyberpunk loading, long content, error, settings, model and language dropdown layout fixtures");
            }
            catch(Exception ex){Console.WriteLine(ex);result=1;}
        });thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();return result;
    }
    static void Capture(Window window,string path)
    {
        window.UpdateLayout();var frame=new DispatcherFrame();window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);window.UpdateLayout();
        if(window.ActualWidth<1 || window.ActualHeight<1)throw new Exception("Window failed to layout");
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(window.ActualWidth*1.5),(int)Math.Ceiling(window.ActualHeight*1.5),144,144,PixelFormats.Pbgra32);bitmap.Render(window);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);png.Save(file);
    }
    static void ScrollExamples(Window window) => Walk(window).OfType<ScrollViewer>().Single(scroll => scroll.Content is StackPanel body && body.Children.OfType<Expander>().Any(expander => Equals(expander.Header, "计算机说明 · 用法与示例"))).ScrollToEnd();
    static IEnumerable<DependencyObject> Walk(DependencyObject root)
    { yield return root;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Walk(VisualTreeHelper.GetChild(root,i)))yield return child; }
    static void CapturePopup(ComboBox combo,string path)
    {
        var frame=new DispatcherFrame();combo.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);
        var child=(FrameworkElement)((Popup)combo.Template.FindName("PART_Popup",combo)).Child;child.UpdateLayout();
        if(child.ActualWidth<100||child.ActualHeight<30)throw new Exception("Dropdown failed to layout");
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(child.ActualWidth*1.5),(int)Math.Ceiling(child.ActualHeight*1.5),144,144,PixelFormats.Pbgra32);bitmap.Render(child);
        var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);png.Save(file);
    }
}
