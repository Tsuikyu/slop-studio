using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using SlopProX;

internal static class V12GuiTests
{
    private static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static int _failures;private static string _root;
    [DllImport("user32.dll")]private static extern IntPtr SendMessage(IntPtr h,int m,IntPtr w,IntPtr l);
    [DllImport("user32.dll")]private static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags);
    private static object Field(object obj,string name){for(Type t=obj.GetType();t!=null;t=t.BaseType){var f=t.GetField(name,Flags|BindingFlags.DeclaredOnly);if(f!=null)return f.GetValue(obj);}throw new Exception(name);}
    private static void Set(object obj,string name,object value){obj.GetType().GetField(name,Flags).SetValue(obj,value);}
    private static object Call(object obj,string name,params object[] args){for(Type t=obj.GetType();t!=null;t=t.BaseType){var m=t.GetMethods(Flags|BindingFlags.DeclaredOnly).FirstOrDefault(x=>x.Name==name&&x.GetParameters().Length==args.Length);if(m!=null)return m.Invoke(obj,args);}throw new Exception(name);}
    private static IEnumerable<Control> Children(Control root){foreach(Control c in root.Controls){yield return c;foreach(var d in Children(c))yield return d;}}
    private static void Check(bool value,string text){if(!value)throw new Exception(text);}
    private static void Pump(int ms){var timer=Stopwatch.StartNew();while(timer.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(3);}}
    private static void Test(string name,Action test){try{test();Pump(40);Console.WriteLine("PASS "+name);}catch(Exception ex){_failures++;Console.WriteLine("FAIL "+name+" :: "+ex);}}
    private static void Screenshot(Form form,string name){Pump(80);using(var bitmap=new Bitmap(form.Width,form.Height)){using(var g=Graphics.FromImage(bitmap)){IntPtr dc=g.GetHdc();try{Check(PrintWindow(form.Handle,dc,2),"Capture failed");}finally{g.ReleaseHdc(dc);}}bitmap.Save(Path.Combine(_root,name+".png"));}}
    private static void Mouse(Control c,int message,int x,int y,int buttons=0){SendMessage(c.Handle,message,(IntPtr)buttons,(IntPtr)((y<<16)|(x&65535)));}
    [STAThread]private static int Main(string[] args)
    {
        _root=Path.Combine(Path.GetFullPath(args[0]),"run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(_root);
        Environment.SetEnvironmentVariable("SLOPX_PROFILE",Path.Combine(_root,"profile"));Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        var settings=new AppSettings{Language="ja-JP",AudioBackend="WASAPI"};settings.Integration.QuickStart=false;
        var locale=LocaleCatalog.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"locales","ja-JP.json"),"ja-JP");
        using(var form=new MainForm(settings,locale,false,null))
        {
            form.Show();Pump(200);
            var project=new ProjectDocument{Name="GUI fixture"};var track=ProjectFactory.CreateTrack("Fixture",TrackType.Midi);var region=ProjectFactory.CreateRegion("Notes",0,4);region.Notes.Add(new MidiNote{Pitch=60,LengthBeats=1});track.Regions.Add(region);project.Tracks.Add(track);
            Set(form,"_project",project);((ProjectHistory)Field(form,"_history")).Reset(true);Call(form,"Select",track.Id,region.Id);Pump(80);
            Test("File Master submenu, Ctrl+B and processing switch",delegate{
                var menu=(MenuStrip)Field(form,"_menu");var file=menu.Items.OfType<ToolStripMenuItem>().First(x=>x.Text=="ファイル");var master=file.DropDownItems.OfType<ToolStripMenuItem>().First(x=>x.Text=="マスター");
                var export=master.DropDownItems.OfType<ToolStripMenuItem>().First();Check(export.ShortcutKeyDisplayString=="Ctrl+B","Master shortcut label missing.");
                var toggle=master.DropDownItems.OfType<ToolStripMenuItem>().First(x=>x.Text.Contains("マスタリング処理"));toggle.PerformClick();Check(!project.Studio.MasterProcessingEnabled,"Master processing toggle failed.");toggle.PerformClick();Check(project.Studio.MasterProcessingEnabled,"Master processing did not re-enable.");
                bool seen=false;var timer=new System.Windows.Forms.Timer{Interval=50};timer.Tick+=delegate{var dialog=Application.OpenForms.OfType<AudioExportDialog>().FirstOrDefault();if(dialog!=null){seen=true;timer.Stop();dialog.Close();}};timer.Start();try{Check((bool)Call(form,"SendTestShortcut",Keys.Control|Keys.B),"Ctrl+B not handled.");}finally{timer.Stop();timer.Dispose();}Check(seen,"Master export dialog did not open.");
                file.ShowDropDown();Pump(60);master.ShowDropDown();Pump(60);using(var bmp=new Bitmap(master.DropDown.Width,master.DropDown.Height)){master.DropDown.DrawToBitmap(bmp,new Rectangle(Point.Empty,bmp.Size));bmp.Save(Path.Combine(_root,"master-menu-ja.png"));}master.HideDropDown();file.HideDropDown();
            });
            Test("Japanese stack and video names",delegate{
                var menu=(MenuStrip)Field(form,"_menu");var trackMenu=menu.Items.OfType<ToolStripMenuItem>().First(x=>x.Text=="トラック");Check(trackMenu.DropDownItems.OfType<ToolStripMenuItem>().Any(x=>x.Text.Contains("ミックス・スタック")),"Mix stack name missing.");Check(trackMenu.DropDownItems.OfType<ToolStripMenuItem>().Any(x=>x.Text.Contains("フォルダー・スタック")),"Folder stack name missing.");
                var file=menu.Items.OfType<ToolStripMenuItem>().First(x=>x.Text=="ファイル");Check(file.DropDownItems.OfType<ToolStripMenuItem>().Any(x=>x.Text.Contains("動画を読み込む")),"Video label missing.");
            });
            Test("Launcher draws its own CyberSquare workspace",delegate{Call(form,"ExecuteCommand",AppCommand.LauncherOnly);Pump(100);var launcher=(Control)Field(form,"_launcher");Check(launcher.Visible&&launcher.BackColor.ToArgb()==CyberTheme.Background.ToArgb(),"Launcher theme missing.");Screenshot(form,"launcher-ja");});
            Test("Track and region colors are saved through the real command",delegate{
                Call(form,"SetItemColor",track.Id,null,false,Color.Crimson);Call(form,"SetItemColor",track.Id,region.Id,false,Color.MediumPurple);Call(form,"SetItemColor",track.Id,region.Id,true,Color.White);
                var p=(ProjectDocument)Field(form,"_project");Check(p.Studio.Background(track.Id,false,Color.Black).ToArgb()==Color.Crimson.ToArgb(),"Track color missing.");var copy=ProjectFile.Deserialize(ProjectFile.Serialize(p));Check(copy.Studio.Background(region.Id,true,Color.Black).ToArgb()==Color.MediumPurple.ToArgb()&&copy.Studio.TextColor(region.Id,true,Color.Black).ToArgb()==Color.White.ToArgb(),"Region colors lost.");
            });
            Test("Inspector value supports mouse-wheel and vertical drag",delegate{
                var inspector=(Control)Field(form,"_inspector");var value=(ScrubNumber)Field(inspector,"_transpose");Check(value.Controls.Cast<Control>().Where(c=>!(c is TextBox)).All(c=>!c.Visible),"Spin arrows visible.");
                var edit=value.Controls.OfType<TextBox>().Single();decimal before=value.Value;Point screen=edit.PointToScreen(new Point(4,4));SendMessage(edit.Handle,0x20a,(IntPtr)(120<<16),(IntPtr)((screen.Y<<16)|(screen.X&65535)));Pump(50);Check(value.Value==before+1,"Wheel did not adjust value.");
                before=value.Value;Mouse(edit,0x201,4,12,1);Mouse(edit,0x200,4,3,1);Mouse(edit,0x202,4,3);Pump(50);Check(value.Value>before,"Drag did not adjust value.");
            });
            Test("Sample selection displays a real decoded waveform",delegate{
                Call(form,"ExecuteCommand",AppCommand.ShowSamples);Pump(100);var browser=(Control)Field(form,"_samples");var files=(ListView)Field(browser,"_files");Check(files.Items.Count>0,"Sample folder unavailable.");var item=files.Items.Cast<ListViewItem>().First(x=>Convert.ToString(x.Tag).EndsWith(".wav",StringComparison.OrdinalIgnoreCase));item.Selected=true;item.Focused=true;
                var wave=(SampleWaveformPreview)Field(browser,"_waveform");for(int i=0;i<150&&String.IsNullOrEmpty(wave.SourcePath);i++)Pump(20);Check(!String.IsNullOrEmpty(wave.SourcePath)&&File.Exists(wave.SourcePath),"Sample preview not loaded.");Screenshot(form,"sample-browser-ja");
            });
            Test("ASIO settings button reaches the selected driver callback",delegate{
                bool invoked=false;using(var prefs=new PreferencesForm(settings,0,delegate(string id){invoked=!String.IsNullOrEmpty(id);return true;})){
                    prefs.Show(form);Pump(70);var driver=(StudioChoice)Field(prefs,"_driver");driver.SelectedItem="ASIO";var outputs=(StudioChoice)Field(prefs,"_out");if(outputs.Items.Count==0){outputs.Items.Add(new AsioDriverChoice{Name="Fixture",Clsid=Guid.NewGuid().ToString("B")});outputs.SelectedIndex=0;}Pump(30);
                    var button=Children(prefs).OfType<Button>().First(x=>x.Text.Contains("ASIO自身"));Check(button.Visible&&button.Enabled,"ASIO panel control is unavailable.");button.PerformClick();Check(invoked,"ASIO callback was not invoked.");prefs.Close();
                }
            });
            Test("Controller command editor is accessible from key settings",delegate{
                using(var prefs=new PreferencesForm(settings,6)){prefs.Show(form);Pump(60);Check(Children(prefs).OfType<Button>().Any(x=>x.Text.Contains("ゲームコントローラー")),"Controller settings button missing.");prefs.Close();}
                using(var editor=new ControllerBindingForm(settings.Integration.ControllerBindings)){editor.Show(form);Pump(30);Check(Children(editor).OfType<ComboBox>().Count()==14,"Controller buttons are missing.");editor.Close();}
            });
            Test("Lua manager and mastering reference open",delegate{Call(form,"ShowLuaManager");Pump(50);Check(Application.OpenForms.OfType<LuaManagerForm>().Any(),"Lua manager missing.");foreach(var w in Application.OpenForms.OfType<LuaManagerForm>().ToArray())w.Close();Call(form,"ShowMasterReference");Pump(50);Check(Application.OpenForms.OfType<MasterReferenceForm>().Any(),"Master reference missing.");foreach(var w in Application.OpenForms.OfType<MasterReferenceForm>().ToArray())w.Close();});
            Test("About shows current version, author and required notices",delegate{
                bool seen=false;var timer=new System.Windows.Forms.Timer{Interval=50};timer.Tick+=delegate{var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(x=>x!=form&&x.Text.Contains("Slop Studio")&&x.Text.Contains("について"));if(dialog==null)return;seen=true;timer.Stop();string text=String.Join(" ",Children(dialog).OfType<Label>().Select(x=>x.Text).ToArray());Check(text.Contains("0.13")&&text.Contains("Tsuikyu")&&text.Contains("OpenAI"),"About content incomplete.");Screenshot(dialog,"about-ja");dialog.Close();};timer.Start();try{Call(form,"ShowAbout");}finally{timer.Stop();timer.Dispose();}Check(seen,"About dialog not shown.");
            });
            Test("Audio region context menu exposes relinking",delegate{
                var current=(ProjectDocument)Field(form,"_project");var audio=ProjectFactory.CreateTrack("Missing audio",TrackType.Audio);var clip=ProjectFactory.CreateRegion("[Missing] Audio",0,4);clip.MediaPath=Path.Combine(_root,"missing.wav");audio.Regions.Add(clip);current.Tracks.Add(audio);
                Call(form,"ExecuteCommand",AppCommand.TracksOnly);Call(form,"Select",audio.Id,clip.Id);var timeline=(TimelineEditor)Field(form,"_timeline");timeline.SetLaneHeight(46);Pump(80);
                var canvas=(Control)Field(timeline,"_canvas");Mouse(canvas,0x204,DisplayScale.Pixels(canvas,212),DisplayScale.Pixels(canvas,91),2);Mouse(canvas,0x205,DisplayScale.Pixels(canvas,212),DisplayScale.Pixels(canvas,91));Pump(40);
                var menu=(ContextMenuStrip)Field(canvas,"_contextMenu");Check(menu.Items.Cast<ToolStripItem>().Any(x=>x.Text.Contains("再リンク")),"Relink menu item missing.");menu.Close();
            });
            Set(form,"_closing",true);form.Close();
        }
        Console.WriteLine("NOT RUN physical controller, MIDI keyboard, microphone, ASIO control panel or speaker audition.");return _failures==0?0:1;
    }
}
