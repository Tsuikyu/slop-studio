using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Linq;

namespace SlopProX
{
    public sealed class PluginDescriptor
    {
        public string Path { get; set; } public string ClassId { get; set; } public string Name { get; set; } public string Vendor { get; set; } public string Format { get; set; } public bool AraSupported{get;set;}public bool Instrument { get; set; }
        public override string ToString(){return Name+"  ·  "+Format;}
        public PluginSlot CreateSlot(){return new PluginSlot{Path=Path,ClassId=ClassId,Name=Name,Format=Format,IsInstrument=Instrument,UseAra=AraSupported};}
    }
    [StructLayout(LayoutKind.Sequential)] public struct MidiEventPacket { public int Offset,Status,Data1,Data2; public MidiEventPacket(int offset,int status,int a,int b){Offset=offset;Status=status;Data1=a;Data2=b;} }
    internal sealed partial class LocalPlugin : IDisposable
    {
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Ansi)] private struct NativeInfo{[MarshalAs(UnmanagedType.ByValArray,SizeConst=256)]public byte[] Id;[MarshalAs(UnmanagedType.ByValArray,SizeConst=256)]public byte[] Name;[MarshalAs(UnmanagedType.ByValArray,SizeConst=128)]public byte[] Vendor;public int Format,Instrument;}
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Ansi)] public struct Parameter{public uint Id;[MarshalAs(UnmanagedType.ByValArray,SizeConst=128)]public byte[] NameBytes;public string Name{get{return Decode(NameBytes);}}public double Value,Minimum,Maximum;public int Steps;}
        private static string Decode(byte[] bytes){if(bytes==null)return String.Empty;int end=Array.IndexOf(bytes,(byte)0);return Encoding.UTF8.GetString(bytes,0,end<0?bytes.Length:end);}
        private const string Library="SlopNative.dll";
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode)]private static extern int sxp_scan(string path,[Out] NativeInfo[] info,int capacity,[Out]byte[] error,int size);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode)]private static extern IntPtr sxp_create(string path,[MarshalAs(UnmanagedType.LPStr)]string id,double rate,int block,[Out]byte[] error,int size);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]private static extern int sxp_process(IntPtr handle,[In,Out]float[] audio,int frames,[In]MidiEventPacket[] events,int count,double tempo,double beat,int playing,int numerator,int denominator);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]private static extern int sxp_param_count(IntPtr handle);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]private static extern int sxp_param_info(IntPtr handle,int index,out Parameter parameter);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]private static extern int sxp_param_set(IntPtr handle,uint id,double value);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]private static extern double sxp_param_get(IntPtr handle,uint id);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]private static extern int sxp_editor_open(IntPtr handle,IntPtr parent,out int width,out int height);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]private static extern void sxp_editor_close(IntPtr handle);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]private static extern void sxp_idle(IntPtr handle);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]private static extern int sxp_state_save(IntPtr handle,[Out]byte[] bytes,int size);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]private static extern int sxp_state_load(IntPtr handle,[In]byte[] bytes,int size);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]private static extern void sxp_destroy(IntPtr handle);
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]private static extern int sxp_latency(IntPtr handle);
        public int LatencySamples{get{lock(_sync)return sxp_latency(_handle);}}
        [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]private static extern int sxp_param_enqueue(IntPtr handle,uint id,double value,int offset);
        public void QueueParameter(uint id,double value,int offset){_processingGate.EnterReadLock();try{if(_handle==IntPtr.Zero||sxp_param_enqueue(_handle,id,value,offset)==0)throw new InvalidOperationException("Parameter event queue rejected the event.");}finally{_processingGate.ExitReadLock();}}
        private readonly object _sync=new object();private readonly System.Threading.ReaderWriterLockSlim _processingGate=new System.Threading.ReaderWriterLockSlim(); private IntPtr _handle; public PluginSlot Slot {get;private set;}
        public static string ResolveBinary(string path)
        {if(Directory.Exists(path)){string directory=System.IO.Path.Combine(path,"Contents","x86_64-win");if(!Directory.Exists(directory))throw new InvalidDataException("The VST3 bundle has no x86_64-win folder.");string[] files=Directory.GetFiles(directory,"*.vst3");if(files.Length==0)throw new InvalidDataException("The VST3 bundle has no 64-bit module.");return files[0];}return System.IO.Path.GetFullPath(path);}
        public static void ValidateArchitecture(string path)
        {using(BinaryReader r=new BinaryReader(File.OpenRead(path))){if(r.BaseStream.Length<256||r.ReadUInt16()!=0x5a4d)throw new InvalidDataException("Plugin is not a Windows PE module.");r.BaseStream.Position=0x3c;int offset=r.ReadInt32();if(offset<0||offset+6>r.BaseStream.Length)throw new InvalidDataException("Plugin PE header is invalid.");r.BaseStream.Position=offset;if(r.ReadUInt32()!=0x4550||r.ReadUInt16()!=0x8664)throw new InvalidDataException("Only Windows x86-64 plugins are supported. A 32-bit or ARM module cannot be loaded.");}}
        public static List<PluginDescriptor> Scan(string path)
        {string original=System.IO.Path.GetFullPath(path);string binary=ResolveBinary(original);ValidateArchitecture(binary);NativeInfo[] info=new NativeInfo[256];byte[] error=new byte[2048];int n=sxp_scan(binary,info,info.Length,error,error.Length);if(n<0)throw new InvalidDataException(Decode(error));List<PluginDescriptor> result=new List<PluginDescriptor>();for(int i=0;i<n;i++)result.Add(new PluginDescriptor{Path=original,ClassId=Decode(info[i].Id),Name=Decode(info[i].Name),Vendor=Decode(info[i].Vendor),Format=info[i].Format==1?"VST3":"CLAP",Instrument=(info[i].Instrument&1)!=0,AraSupported=(info[i].Instrument&2)!=0});return result;}
        public LocalPlugin(PluginSlot slot,int sampleRate,int bufferFrames)
        {Slot=slot;string binary=ResolveBinary(slot.Path);ValidateArchitecture(binary);byte[] error=new byte[2048];_handle=slot.UseAra?sxp_create_ara(binary,slot.ClassId,sampleRate,bufferFrames,error,error.Length):sxp_create(binary,slot.ClassId,sampleRate,bufferFrames,error,error.Length);if(_handle==IntPtr.Zero)throw new InvalidOperationException(Decode(error));try{if(slot.UseAra)UpdateAraDocument(slot.AraDocument??new AraDocumentInfo());RestoreState(slot.State);RestoreAraState(slot.AraState);}catch{Dispose();throw;}}
        public void RestoreState(string state){lock(_sync){if(String.IsNullOrEmpty(state))return;byte[] bytes=Convert.FromBase64String(state);_processingGate.EnterWriteLock();try{if(_handle==IntPtr.Zero||sxp_state_load(_handle,bytes,bytes.Length)==0)throw new InvalidDataException("The plugin rejected its saved state.");}finally{_processingGate.ExitWriteLock();}}}
        public void BindSlot(PluginSlot slot){Slot=slot;}
        public void Process(float[] audio,int frames,MidiEventPacket[] events,int count,double tempo,double beat,bool playing,int numerator,int denominator){_processingGate.EnterReadLock();try{if(_handle==IntPtr.Zero)return;if(sxp_process(_handle,audio,frames,events,count,tempo,beat,playing?1:0,numerator,denominator)!=0)throw new InvalidOperationException("Plugin processing failed: "+Slot.Name);}finally{_processingGate.ExitReadLock();}}
        public List<Parameter> Parameters(){lock(_sync){List<Parameter> result=new List<Parameter>();int count=Math.Min(16384,sxp_param_count(_handle));for(int i=0;i<count;i++){Parameter p;if(sxp_param_info(_handle,i,out p)!=0)result.Add(p);}return result;}}
        public void SetParameter(uint id,double value){lock(_sync){if(sxp_param_set(_handle,id,value)==0)throw new InvalidOperationException("The plugin rejected this parameter change.");}}
        public double GetParameter(uint id){lock(_sync){return sxp_param_get(_handle,id);}}
        public string SaveState(){lock(_sync){int size=sxp_state_save(_handle,null,0);if(size<0||size>3*1024*1024)throw new InvalidDataException("Plugin state exceeds the supported size.");if(size==0)return String.Empty;byte[] bytes=new byte[size];if(sxp_state_save(_handle,bytes,size)!=size)throw new InvalidDataException("Plugin state save failed.");return Convert.ToBase64String(bytes);}}
        public bool OpenEditor(IntPtr parent,out int w,out int h){lock(_sync){return sxp_editor_open(_handle,parent,out w,out h)!=0;}}
        public void CloseEditor(){lock(_sync){if(_handle!=IntPtr.Zero)sxp_editor_close(_handle);}}
        public void Idle(){lock(_sync){if(_handle!=IntPtr.Zero)sxp_idle(_handle);}}
        public void Dispose(){lock(_sync){_processingGate.EnterWriteLock();try{if(_handle!=IntPtr.Zero){sxp_destroy(_handle);_handle=IntPtr.Zero;}}finally{_processingGate.ExitWriteLock();}}}
    }
    public static class PluginCatalog
    {
        public static List<PluginDescriptor> Items=new List<PluginDescriptor>();
        public static string CachePath {get{return System.IO.Path.Combine(System.IO.Path.GetDirectoryName(SettingsStore.DefaultPath),"plugin-cache.json");}}
        public static void Load(){try{if(File.Exists(CachePath))Items=new JavaScriptSerializer().Deserialize<List<PluginDescriptor>>(File.ReadAllText(CachePath))??new List<PluginDescriptor>();}catch{Items=new List<PluginDescriptor>();}}
        public static void Save(){Directory.CreateDirectory(System.IO.Path.GetDirectoryName(CachePath));File.WriteAllText(CachePath,new JavaScriptSerializer().Serialize(Items),new UTF8Encoding(false));}
        public static List<PluginDescriptor> ScanIsolated(string path)
        {string report=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"SlopScan-"+Guid.NewGuid().ToString("N")+".json");try{ProcessStartInfo start=new ProcessStartInfo(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"SlopStudio.exe"),"--scan-plugin \""+path+"\" \""+report+"\"");start.UseShellExecute=false;start.CreateNoWindow=true;start.RedirectStandardOutput=true;start.RedirectStandardError=true;start.WindowStyle=ProcessWindowStyle.Hidden;using(Process process=Process.Start(start)){process.BeginOutputReadLine();process.BeginErrorReadLine();if(!process.WaitForExit(20000)){process.Kill();throw new TimeoutException("Plugin scanner timed out: "+System.IO.Path.GetFileName(path));}if(process.ExitCode!=0)throw new InvalidDataException(File.Exists(report)?File.ReadAllText(report):"Plugin scanner exited unexpectedly ("+process.ExitCode+").");}return new JavaScriptSerializer().Deserialize<List<PluginDescriptor>>(File.ReadAllText(report));}finally{try{if(File.Exists(report))File.Delete(report);}catch{}}}
        public static List<string> Enumerate(IEnumerable<string> paths){
            var found=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending=new Stack<string>();foreach(string path in paths)if(Directory.Exists(path))pending.Push(System.IO.Path.GetFullPath(path));
            while(pending.Count>0){
                string folder=pending.Pop();
                if(folder.EndsWith(".vst3",StringComparison.OrdinalIgnoreCase)){found.Add(folder);continue;}
                try{
                    foreach(string file in Directory.GetFiles(folder)){string ext=System.IO.Path.GetExtension(file);if(ext.Equals(".vst3",StringComparison.OrdinalIgnoreCase)||ext.Equals(".clap",StringComparison.OrdinalIgnoreCase))found.Add(file);}
                    foreach(string child in Directory.GetDirectories(folder)){
                        try{if(child.EndsWith(".vst3",StringComparison.OrdinalIgnoreCase))found.Add(child);else if((File.GetAttributes(child)&FileAttributes.ReparsePoint)==0)pending.Push(child);}
                        catch(UnauthorizedAccessException){}catch(IOException){}
                    }
                }catch(UnauthorizedAccessException){}catch(IOException){}
            }
            return found.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToList();
        }
    }
    public sealed class PluginBrowserDialog:Form {
     private ListView _list;private TextBox _search,_details;private ListBox _category;private Label _status;private Button _scan,_load;private IEnumerable<string> _paths;private List<string> _errors=new List<string>();private bool _cancel,_scanning;
     public PluginDescriptor SelectedPlugin{get;private set;}
     public PluginBrowserDialog(IEnumerable<string> paths){
      _paths=paths.ToArray();Text="Plug-in Manager";ClientSize=new Size(1030,640);MinimumSize=new Size(850,520);StartPosition=FormStartPosition.CenterParent;BackColor=Color.FromArgb(28,31,38);ForeColor=Color.Gainsboro;Font=new Font("Segoe UI",9);
      var head=new Panel{Dock=DockStyle.Top,Height=85,Padding=new Padding(22,13,22,12)};head.Controls.Add(new Label{Text="Plug-in Manager",Font=new Font("Segoe UI",21,FontStyle.Bold),Dock=DockStyle.Top,Height=39});head.Controls.Add(new Label{Text="64-bit  ·  VST3 / CLAP  ·  per-instance process isolation",Dock=DockStyle.Bottom,Height=19,ForeColor=Color.FromArgb(150,167,188)});
      var content=new Panel{Dock=DockStyle.Fill,Padding=new Padding(18,0,18,0)};
      _category=new ListBox{Dock=DockStyle.Left,Width=160,BackColor=Color.FromArgb(34,38,46),ForeColor=Color.Gainsboro,BorderStyle=BorderStyle.None,IntegralHeight=false,ItemHeight=31};_category.Items.AddRange(new object[]{"All Plug-ins","Instruments","Effects","VST3","CLAP","Built-in","Scan Errors"});_category.SelectedIndex=0;_category.SelectedIndexChanged+=delegate{Populate();};
      _search=new TextBox{Dock=DockStyle.Top,Height=29,BackColor=Color.FromArgb(42,47,57),ForeColor=Color.Gainsboro,BorderStyle=BorderStyle.FixedSingle,Margin=new Padding(8)};_search.TextChanged+=delegate{Populate();};
      _list=new ListView{Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,MultiSelect=false,HideSelection=false,BackColor=Color.FromArgb(36,40,48),ForeColor=Color.Gainsboro,BorderStyle=BorderStyle.None};_list.OwnerDraw=true;_list.DrawColumnHeader+=delegate(object sender,DrawListViewColumnHeaderEventArgs e){using(var b=new SolidBrush(Color.FromArgb(48,54,65)))e.Graphics.FillRectangle(b,e.Bounds);TextRenderer.DrawText(e.Graphics,e.Header.Text,Font,new Rectangle(e.Bounds.X+6,e.Bounds.Y,e.Bounds.Width-8,e.Bounds.Height),Color.Gainsboro,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);};_list.DrawItem+=delegate(object sender,DrawListViewItemEventArgs e){e.DrawDefault=true;};_list.DrawSubItem+=delegate(object sender,DrawListViewSubItemEventArgs e){e.DrawDefault=true;};_list.Columns.Add("Plug-in",220);_list.Columns.Add("Format",72);_list.Columns.Add("Type",95);_list.Columns.Add("Vendor",150);_list.SelectedIndexChanged+=delegate{Details();};_list.DoubleClick+=delegate{LoadSelected();};
      _details=new TextBox{Dock=DockStyle.Right,Width=225,Multiline=true,ReadOnly=true,BorderStyle=BorderStyle.None,BackColor=Color.FromArgb(31,35,43),ForeColor=Color.FromArgb(170,187,205),Padding=new Padding(12)};
      var center=new Panel{Dock=DockStyle.Fill,Padding=new Padding(14,0,14,0)};center.Controls.Add(_list);center.Controls.Add(_search);content.Controls.Add(center);content.Controls.Add(_details);content.Controls.Add(_category);
      var footer=new Panel{Dock=DockStyle.Bottom,Height=65,Padding=new Padding(20,12,20,12)};_scan=MakeButton("Rescan Paths");_scan.Dock=DockStyle.Left;_scan.Click+=delegate{if(_scanning){_cancel=true;_scan.Text="Cancelling…";}else Scan();};_load=MakeButton("Load Selected");_load.Dock=DockStyle.Right;_load.BackColor=Color.FromArgb(45,111,170);_load.Click+=delegate{LoadSelected();};_status=new Label{Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,ForeColor=Color.FromArgb(152,171,191)};footer.Controls.Add(_status);footer.Controls.Add(_scan);footer.Controls.Add(_load);Controls.Add(content);Controls.Add(footer);Controls.Add(head);Populate();FormClosing+=delegate{_cancel=true;};
     }
     private Button MakeButton(string text){return new Button{Text=text,Width=135,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(53,60,72),ForeColor=Color.Gainsboro};}
     private IEnumerable<PluginDescriptor> Available(){foreach(var p in PluginCatalog.Items)yield return p;foreach(string name in StockEffects.Names)yield return new PluginDescriptor{Name="Slop "+name,ClassId=name,Format="Built-in",Vendor="Slop Studio",Path="",Instrument=false};}
     private void Populate(){if(_list==null)return;_list.BeginUpdate();_list.Items.Clear();string search=_search.Text.Trim();int category=_category.SelectedIndex;
      if(category==6){foreach(string error in _errors)_list.Items.Add(new ListViewItem(new[]{error,"","",""}){ForeColor=Color.FromArgb(230,158,141)});}
      else foreach(var p in Available()){if(category==1&&!p.Instrument||category==2&&p.Instrument||category==3&&p.Format!="VST3"||category==4&&p.Format!="CLAP"||category==5&&p.Format!="Built-in")continue;if(search.Length>0&&(p.Name+" "+p.Vendor+" "+p.Format).IndexOf(search,StringComparison.OrdinalIgnoreCase)<0)continue;var row=new ListViewItem(new[]{p.Format=="Built-in"?UiText.Translate(p.Name):p.Name,p.Format=="Built-in"?UiText.Translate(p.Format):p.Format,UiText.Translate(p.Instrument?"Instrument":"Effect"),p.Vendor}){Tag=p};_list.Items.Add(row);}
      _list.EndUpdate();_status.Text=_list.Items.Count+" "+UiText.Translate("items")+"  ·  "+_errors.Count+" "+UiText.Translate("scan errors");Details();
     }
     private void Details(){SelectedPlugin=_list.SelectedItems.Count==1?_list.SelectedItems[0].Tag as PluginDescriptor:null;_load.Enabled=SelectedPlugin!=null;_details.Text=SelectedPlugin==null?UiText.Translate("Select a plug-in to inspect its format, vendor and location."):SelectedPlugin.Name+Environment.NewLine+Environment.NewLine+SelectedPlugin.Format+(SelectedPlugin.AraSupported?" / ARA 2":"")+" · "+UiText.Translate(SelectedPlugin.Instrument?"Instrument":"Effect")+Environment.NewLine+SelectedPlugin.Vendor+Environment.NewLine+Environment.NewLine+(SelectedPlugin.Format=="Built-in"?UiText.Translate("Included processor. Parameters and state are saved with the project."):SelectedPlugin.Path);}
     private void LoadSelected(){if(SelectedPlugin==null)return;DialogResult=DialogResult.OK;Close();}
     private void Scan(){_scanning=true;_cancel=false;_scan.Text="Cancel Scan";_status.Text=UiText.Translate("Scanning in isolated worker processes…");Task.Factory.StartNew(delegate{var found=new List<PluginDescriptor>();var errors=new List<string>();try{foreach(string path in _paths)if(!Directory.Exists(path))errors.Add("Search folder unavailable: "+path);foreach(string path in PluginCatalog.Enumerate(_paths)){if(_cancel)break;try{found.AddRange(PluginCatalog.ScanIsolated(path));}catch(Exception ex){errors.Add(Path.GetFileName(path)+": "+ex.Message);}if(IsHandleCreated&&!IsDisposed)try{BeginInvoke((Action)delegate{if(!IsDisposed)_status.Text=UiText.Translate("Scanning…")+" "+Path.GetFileName(path);});}catch{}}if(!_cancel){PluginCatalog.Items=found;PluginCatalog.Save();}}catch(Exception ex){errors.Add(ex.Message);}if(IsDisposed||!IsHandleCreated)return;try{BeginInvoke((Action)delegate{if(IsDisposed)return;_scanning=false;_errors=errors;Populate();_scan.Text="Rescan Paths";if(_cancel)_status.Text=UiText.Translate("Scan cancelled; previous catalog retained.");});}catch{}});}
    }
}
