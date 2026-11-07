using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace SlopStudio {
 public sealed partial class MainForm {
  private SoftwareKeyboard _softwareKeyboard;private GameController _controller;private Timer _v7Timer;private List<string> _loadedMods=new List<string>();private bool _localizing;private readonly HashSet<Form> _localizedForms=new HashSet<Form>();
  public bool TextEntryFocused{get{return IsTextEntryFocused();}}
  private void InitializeV7Features(){
   _controller=new GameController();var mix=_menu.Items.OfType<ToolStripMenuItem>().FirstOrDefault(i=>i.Text=="Mix");if(mix!=null)AddCommandMenu(mix,AppCommand.ShowStockEffects,"Track Effects…");AddCommandMenu(_fileMenu,AppCommand.ImportMidi,"Import MIDI File…");AddCommandMenu(_viewMenu,AppCommand.ToggleSoftwareKeyboard,"Musical Typing");AddCommandMenu(_helpMenu,AppCommand.UserManual,"User Manual");
   var mods=new ToolStripMenuItem("Mods");AddCommandMenu(mods,AppCommand.LoadLua,"Load Lua Mod…");_menu.Items.Insert(_menu.Items.Count-1,mods);
   _v7Timer=new Timer{Interval=100};_v7Timer.Tick+=delegate{PollAraTransport();if(_settings.Integration.ControllerEnabled)try{_controller.Bindings=_settings.Integration.ControllerBindings;_controller.PreferredKind=_settings.InputMode=="XInput Controller"?1:_settings.InputMode=="DirectInput Controller"?2:0;_controller.Poll(this,ExecuteCommand,delegate(float x,float y,float z){Point screen=_timeline.PointToScreen(new Point(_timeline.Width/2,_timeline.Height/2));if(x!=0)_timeline.NavigateWheel(screen,(int)(-x*120),Keys.Shift);if(y!=0)_timeline.NavigateWheel(screen,(int)(-y*120),Keys.None);if(z!=0)_timeline.NavigateWheel(screen,z<0?120:-120,Keys.Control);});}catch(Exception ex){_settings.Integration.ControllerEnabled=false;ErrorLog.Write("Controller",ex);}if(!_localizing){_localizing=true;try{foreach(Form f in Application.OpenForms)if(_localizedForms.Add(f)){UiText.Apply(f);f.Disposed+=delegate{_localizedForms.Remove(f);};}}finally{_localizing=false;}}};_v7Timer.Start();
   InitializeV8Features();
   Deactivate+=delegate{if(_softwareKeyboard!=null)_softwareKeyboard.ReleaseAll();};
  }
  private void PollAraTransport(){
   if(_engine==null||_project==null||_recording)return;
   foreach(var t in _project.Tracks)foreach(var slot in t.Plugins){if(!slot.UseAra)continue;var instance=_engine.GetPlugin(slot.Id);if(instance==null)continue;var request=instance.TakeAraRequest();if(request==null)continue;
    if((request.Flags&2)!=0)StopAudio();
    if((request.Flags&4)!=0&&request.Position>=0&&!Double.IsInfinity(request.Position)&&!Double.IsNaN(request.Position))SetCursorBeat(request.Position*_project.Tempo/60);
    if((request.Flags&24)!=0){var changed=ProjectFactory.Clone(_project);if((request.Flags&8)!=0&&request.CycleStart>=0&&request.CycleLength>0&&request.CycleLength<86400){changed.CycleStartBeat=request.CycleStart*_project.Tempo/60;changed.CycleEndBeat=(request.CycleStart+request.CycleLength)*_project.Tempo/60;}if((request.Flags&16)!=0)changed.CycleEnabled=request.CycleEnabled;_history.Execute(_project,new SetProjectContentCommand("ARA cycle request",_project,changed));RefreshState();}
    if((request.Flags&1)!=0&&!_engine.Playing)StartTransport();
   }
  }
  internal bool ProcessMusicalTypingShortcut(ref Message message,Keys keys){ShortcutBinding binding;if(!ShortcutRegistry.TryFind(keys,false,out binding))return false;switch(binding.Command){case AppCommand.PlayOrStop:case AppCommand.Stop:case AppCommand.Pause:case AppCommand.Record:case AppCommand.GoToBeginning:case AppCommand.RewindBar:case AppCommand.ForwardBar:case AppCommand.ToggleCycle:bool repeat=(message.LParam.ToInt64()&(1L<<30))!=0;if(repeat&&(binding.Command==AppCommand.PlayOrStop||binding.Command==AppCommand.Record||binding.Command==AppCommand.Pause||binding.Command==AppCommand.ToggleCycle))return true;ExecuteCommand(binding.Command);return true;default:return false;}}
  private bool ExecuteV7(AppCommand command){
   switch(command){case AppCommand.ShowStockEffects:ShowStockRack();return true;case AppCommand.ImportMidi:ImportMediaDialog(true);return true;case AppCommand.ToggleSoftwareKeyboard:ToggleSoftwareKeyboard();return true;case AppCommand.UserManual:OpenUserManual();return true;case AppCommand.LoadLua:using(var file=new OpenFileDialog{Filter="Lua Mod|*.lua"})if(file.ShowDialog(this)==DialogResult.OK)LoadLuaMod(file.FileName);return true;default:return false;}
  }
  private void ToggleSoftwareKeyboard(){
   if(_softwareKeyboard!=null&&!_softwareKeyboard.IsDisposed){_softwareKeyboard.Close();_softwareKeyboard=null;return;}
   Track t=SelectedTrack;if(t==null||t.Type!=TrackType.Midi){AddTrack(TrackType.Midi);t=SelectedTrack;}if(!t.Armed)OnTrackArmRequested(this,new TrackArmEventArgs{TrackId=t.Id,Armed=true});
   _softwareKeyboard=new SoftwareKeyboard(this,delegate(int key,int velocity,bool down){_engine.InjectMidi(down?0x90:0x80,key,down?velocity:0);});_softwareKeyboard.Show(this);_timeline.Focus();
  }
  private void OpenUserManual(){string file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"docs","manual","index-"+_settings.Language+".html");try{if(!File.Exists(file))throw new FileNotFoundException("The User Manual is missing.",file);Process.Start(new ProcessStartInfo(file){UseShellExecute=true});}catch(Exception ex){ShowError("User Manual could not be opened",ex);}}
  private async void LoadLuaMod(string file){
   try{_statusLabel.Text=UiText.Translate("Loading Lua Mod…");var operations=await Task.Factory.StartNew(()=>LuaMods.Load(file));var result=LuaMods.Apply(_project,operations);_history.Execute(_project,new SetProjectContentCommand("Lua Mod: "+Path.GetFileName(file),_project,result));_loadedMods.Add(file);try{LuaModRegistry.Add(file);}catch(Exception ex){ErrorLog.Write("Remember Lua Mod",ex);}RefreshState();foreach(var op in operations)if(op.Command=="transport"){string action=Convert.ToString(op.Arguments["action"]);if(action=="play")StartTransport();else if(action=="stop"||action=="pause")StopAudio();}_statusLabel.Text=UiText.Translate("Lua Mod applied")+" · "+Path.GetFileName(file);}
   catch(Exception ex){ShowError("Lua Mod failed",ex);}
  }
  private void DisposeV7Features(){StopMayhem();foreach(var window in _stockWindows.Values.ToArray())if(!window.IsDisposed)window.Close();_stockWindows.Clear();if(_v7Timer!=null){_v7Timer.Stop();_v7Timer.Dispose();_v7Timer=null;}if(_softwareKeyboard!=null){_softwareKeyboard.Dispose();_softwareKeyboard=null;}if(_controller!=null){_controller.Dispose();_controller=null;}}
 }
}
