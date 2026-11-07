using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using SlopStudio;

internal static class V13GuiTests
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr h,int m,IntPtr w,IntPtr l);
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr h,IntPtr dc,uint flags);
    private static int failures; private static string root;
    private static void Check(bool b,string why){if(!b)throw new Exception(why);}
    private static object Field(object o,string n){for(Type t=o.GetType();t!=null;t=t.BaseType){var f=t.GetField(n,Flags|BindingFlags.DeclaredOnly);if(f!=null)return f.GetValue(o);}throw new Exception(n);}
    private static void Set(object o,string n,object v){o.GetType().GetField(n,Flags).SetValue(o,v);}
    private static object Call(object o,string n,params object[] a){for(Type t=o.GetType();t!=null;t=t.BaseType){var m=t.GetMethods(Flags|BindingFlags.DeclaredOnly).FirstOrDefault(x=>x.Name==n&&x.GetParameters().Length==a.Length);if(m!=null)return m.Invoke(o,a);}throw new Exception(n);}
    private static void Pump(int ms){var sw=Stopwatch.StartNew();while(sw.ElapsedMilliseconds<ms){Application.DoEvents();Thread.Sleep(3);}}
    private static void Test(string n,Action a){try{a();Pump(30);Console.WriteLine("PASS "+n);}catch(Exception e){failures++;Console.WriteLine("FAIL "+n+" :: "+e);}}
    private static void Mouse(Control c,int message,int x,int y,int buttons=0){SendMessage(c.Handle,message,(IntPtr)buttons,(IntPtr)((y<<16)|(x&65535)));}
    private static void Screenshot(Form f,string name){Pump(80);using(var b=new Bitmap(f.Width,f.Height)){using(var g=Graphics.FromImage(b)){IntPtr dc=g.GetHdc();try{Check(PrintWindow(f.Handle,dc,2),"Screenshot capture failed");}finally{g.ReleaseHdc(dc);}}b.Save(Path.Combine(root,name+".png"));}}
    private static IEnumerable<ToolStripMenuItem> Menus(ToolStripItemCollection items){foreach(ToolStripItem raw in items){var item=raw as ToolStripMenuItem;if(item==null)continue;yield return item;foreach(var child in Menus(item.DropDownItems))yield return child;}}
    [STAThread] private static int Main(string[] args)
    {
        root=Path.Combine(Path.GetFullPath(args[0]),"run-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("SLOPSTUDIO_PROFILE",Path.Combine(root,"profile"));Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        var settings=new AppSettings{Language="ja-JP",AudioBackend="WASAPI"};settings.Integration.QuickStart=false;
        var locale=LocaleCatalog.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"locales","ja-JP.json"),"ja-JP");
        using(var form=new MainForm(settings,locale,false,null))
        {
            form.Size=new Size(1440,900);form.Show();Pump(200);
            var project=new ProjectDocument{Name="V13 GUI fixture"};var track=ProjectFactory.CreateTrack("Piano",TrackType.Midi);var region=ProjectFactory.CreateRegion("Notes",0,4);
            region.Notes.Add(new MidiNote{Pitch=60,StartBeat=0,LengthBeats=1,Velocity=70});track.Regions.Add(region);project.Tracks.Add(track);
            Set(form,"_project",project);((ProjectHistory)Field(form,"_history")).Reset(true);Call(form,"Select",track.Id,region.Id);Pump(80);
            Test("Instrument artwork uses the CyberSquare accent",delegate{
                var library=(Control)Field(form,"_browser");var artwork=(Control)Field(library,"_art");using(var bitmap=new Bitmap(artwork.Width,artwork.Height)){artwork.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));int purple=0,green=0;for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++){Color c=bitmap.GetPixel(x,y);if(c.B>c.G+20&&c.B>c.R+20&&c.R>50)purple++;if(c.G>c.R+60&&c.G>c.B+60)green++;}Check(purple>10&&green==0,"Instrument artwork did not use the blue-purple accent.");}
            });
            Test("Master slider mouse operation updates the audio snapshot",delegate{
                var engine=(ArrangementEngine)Field(form,"_engine");engine.PrepareOffline(project,0);var bar=(Control)Field(form,"_controlBar");bar.Invalidate();Pump(80);
                var hit=(Rectangle)Field(bar,"_volume");Check(hit.Width>0,"Master slider was not painted.");int x=DisplayScale.Pixels(bar,hit.X+55),y=DisplayScale.Pixels(bar,hit.Y+15);
                Mouse(bar,0x201,x,y,1);Mouse(bar,0x202,x,y);Pump(50);double gain=((ProjectDocument)Field(form,"_project")).MasterGainDb;
                var actual=(ProjectDocument)Field(engine,"_project");Check(gain < -20 && gain > -30 && Math.Abs(actual.MasterGainDb-gain)<.001,"Slider and engine master gains differ.");Screenshot(form,"main-master");
            });
            Test("Velocity bar changes a note at beat zero and Undo restores it",delegate{
                Call(form,"OpenEditor");var split=(SplitContainer)Field(form,"_workspaceSplit");split.Panel2Collapsed=false;split.SplitterDistance=Math.Max(split.Panel1MinSize,split.Height-370);Pump(100);
                var roll=(PianoRollEditor)Field(form,"_pianoRoll");var canvas=(Control)Field(roll,"_canvas");int logicalHeight=((ScaledCanvas)canvas).Height;Check(logicalHeight>=140,"Velocity lane needs more height: "+logicalHeight);
                int x=DisplayScale.Pixels(canvas,55),y=DisplayScale.Pixels(canvas,logicalHeight-24),dy=DisplayScale.Pixels(canvas,12);
                Mouse(canvas,0x201,x,y,1);Mouse(canvas,0x200,x,y-dy,1);Mouse(canvas,0x202,x,y-dy);Pump(50);
                int velocity=((ProjectDocument)Field(form,"_project")).Tracks[0].Regions[0].Notes[0].Velocity;Check(velocity>70,"Velocity drag did not change note: "+velocity);
                Screenshot(form,"piano-velocity");Call(form,"Undo");Check(((ProjectDocument)Field(form,"_project")).Tracks[0].Regions[0].Notes[0].Velocity==70,"Undo did not restore velocity.");
            });
            Test("Sample bookmark and favorites survive reopening without moving the original",delegate{
                string folder=Path.Combine(root,"samples");Directory.CreateDirectory(folder);string file=Path.Combine(folder,"silent.wav");using(var wave=new ExportWaveWriter(file,48000,1,16,false))wave.Write(new float[480],0,480);byte[] original=File.ReadAllBytes(file);
                using(var browser=new SampleBrowser()){form.Controls.Add(browser);browser.BringToFront();browser.LoadFolder(folder);Pump(50);Call(browser,"ToggleBookmark");var list=(ListView)Field(browser,"_files");list.Items[0].Selected=true;list.Items[0].Focused=true;Pump(40);Call(browser,"ToggleFavorite");Call(browser,"ShowFavorites");Pump(30);Check(list.Items.Count==1,"Favorite not displayed.");((TextBox)Field(browser,"_search")).Text="not-a-match";Check(list.Items.Count==0,"Favorites search failed.");form.Controls.Remove(browser);}
                using(var browser=new SampleBrowser()){Call(browser,"ShowFavorites");var list=(ListView)Field(browser,"_files");Check(list.Items.Count==1,"Favorite did not persist.");var places=(ComboBox)Field(browser,"_places");Check(places.Items.Cast<object>().Any(x=>Convert.ToString(x)==folder),"Bookmark did not persist.");}
                Check(File.Exists(file)&&original.SequenceEqual(File.ReadAllBytes(file)),"Original sample was changed.");
            });
            Test("Fullscreen covers the screen and restores window geometry",delegate{
                Rectangle before=form.Bounds;var border=form.FormBorderStyle;bool top=form.TopMost;try{Call(form,"ToggleFullScreen");Pump(40);Check(form.Bounds==Screen.FromControl(form).Bounds&&form.FormBorderStyle==FormBorderStyle.None,"Fullscreen bounds are wrong.");}
                finally{if((bool)Field(form,"_fullScreen"))Call(form,"ToggleFullScreen");}Pump(40);Check(form.Bounds==before&&form.FormBorderStyle==border&&form.TopMost==top,"Window state was not restored.");
            });
            Test("Opening a template creates an unsaved copy and preserves the original",delegate{
                ((ProjectHistory)Field(form,"_history")).Reset(true);string template=Path.Combine(root,"Template.sloptemplate");ProjectProtection.SaveNewTemplate(project,template,root);byte[] original=File.ReadAllBytes(template);
                Call(form,"OpenProjectPath",template);Check(Field(form,"_projectPath")==null,"Template remained a direct save target.");string copy=Path.Combine(root,"Saved copy.slopdaw");Check((bool)Call(form,"SaveToPath",copy),"Could not save the copy.");Check(File.Exists(copy)&&original.SequenceEqual(File.ReadAllBytes(template)),"Template was modified.");
            });
            Test("Custom shortcut executes and the menu displays the active assignment",delegate{
                var binding=ShortcutRegistry.Find(AppCommand.ToggleMixer);var key=Keys.Control|Keys.Shift|Keys.F12;var overrides=new Dictionary<string,int>{{binding.ProfileId,(int)key}};
                try{ShortcutRegistry.ApplyOverrides(overrides);var menu=(MenuStrip)Field(form,"_menu");Call(form,"RefreshShortcutLabels",menu.Items);var item=Menus(menu.Items).First(x=>x.Tag is AppCommand&&(AppCommand)x.Tag==AppCommand.ToggleMixer);Check(item.ShortcutKeyDisplayString==ShortcutRegistry.Format(key),"Menu shortcut label is stale.");
                    var split=(SplitContainer)Field(form,"_workspaceSplit");bool before=split.Panel2Collapsed;Check((bool)Call(form,"SendTestShortcut",key),"Custom shortcut was not handled.");Check(split.Panel2Collapsed!=before,"Custom shortcut did not execute the mixer command.");
                }finally{ShortcutRegistry.ApplyOverrides(new Dictionary<string,int>());Call(form,"RefreshShortcutLabels",((MenuStrip)Field(form,"_menu")).Items);}
            });
            Test("Launchpad settings expose opt-in ports without opening a device",delegate{
                using(var dialog=new PreferencesForm(settings,1)){dialog.Show(form);Pump(60);var enabled=(CheckBox)Field(dialog,"_lpEnabled");var input=(StudioChoice)Field(dialog,"_lpIn");var output=(StudioChoice)Field(dialog,"_lpOut");Check(!enabled.Checked&&input.Visible&&output.Visible,"Launchpad controls are missing or enabled by default.");dialog.Close();}
            });
            Set(form,"_closing",true);form.Close();
        }
        Test("Theme settings alter the rendered master slider after restart",delegate{
            int custom=Color.FromArgb(255,75,180).ToArgb();var themed=new AppSettings{Language="ja-JP"};themed.Integration.QuickStart=false;themed.ThemeOverrides["Accent"]=custom;
            try{using(var form=new MainForm(themed,locale,false,null)){form.Size=new Size(1440,900);form.Show();Pump(100);Check(CyberTheme.Accent.ToArgb()==custom,"Theme setting did not reach the UI palette.");Screenshot(form,"custom-theme");Set(form,"_closing",true);form.Close();}}
            finally{CyberTheme.ApplyPalette(new Dictionary<string,int>());}
        });
        Console.WriteLine("NOT RUN physical display DPI changes, audio output or hearing audition.");return failures==0?0:1;
    }
}
