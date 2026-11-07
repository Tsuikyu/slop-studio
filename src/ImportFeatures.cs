using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace SlopProX {
 public sealed partial class MainForm {
  private bool _importing;
  private void InitializeFileDrop(){RegisterDrop(this);}
  private void RegisterDrop(Control c){
   c.AllowDrop=true;
   c.DragEnter+=delegate(object sender,DragEventArgs e){var files=e.Data.GetData(DataFormats.FileDrop) as string[];e.Effect=files!=null&&files.Any(IsImportable)?DragDropEffects.Copy:DragDropEffects.None;};
   c.DragDrop+=async delegate(object sender,DragEventArgs e){var files=e.Data.GetData(DataFormats.FileDrop) as string[];if(files==null)return;Guid? track=null;double beat=_cursorBeat;Point screen=new Point(e.X,e.Y);if(_timeline.RectangleToScreen(_timeline.ClientRectangle).Contains(screen))_timeline.DropLocation(screen,out track,out beat);else track=_selectedTrackId;await ImportDroppedFiles(files,track,beat);};
   c.ControlAdded+=delegate(object sender,ControlEventArgs e){RegisterDrop(e.Control);};foreach(Control child in c.Controls)RegisterDrop(child);
  }
  private static bool IsImportable(string f){string e=Path.GetExtension(f).ToLowerInvariant();return Array.IndexOf(new[]{".mp4",".mov",".m4v",".avi",".mkv"},e)>=0||MediaImport.IsAudio(f)||e==".mid"||e==".midi"||e==".slopdaw"||e==".flp"||e==".als"||e==".dawproject"||e==".lua";}
  public async Task ImportDroppedFiles(string[] files,Guid? target,double beat){
   await new UiDispatch(this);
   if(_importing||_recording)return;_importing=true;var warnings=new List<string>();try{
    foreach(string file in files){
     if(!File.Exists(file)||!IsImportable(file))continue;
     string ext=Path.GetExtension(file).ToLowerInvariant();_statusLabel.Text=UiText.Translate("Importing…")+" "+Path.GetFileName(file);
     if(Array.IndexOf(new[]{".mp4",".mov",".m4v",".avi",".mkv"},ext)>=0){_importing=false;await ImportMovieAsync(file);_importing=true;continue;}
     if(ext==".slopdaw"){OpenProjectPath(file);continue;}
     if(ext==".lua"){LoadLuaMod(file);continue;}
     if(ext==".als"||ext==".flp"||ext==".dawproject"){if(!ConfirmDiscardChanges())continue;var imported=await Task.Factory.StartNew(()=>ProjectExchange.Import(file,Path.Combine(Path.GetDirectoryName(SettingsStore.DefaultPath),"Imported Media",Guid.NewGuid().ToString("N"))));await new UiDispatch(this);StopAudio();_engine.ResetProject();_project=imported.Project;_project.Studio.ImportNotes=String.Join(Environment.NewLine,imported.Warnings.Distinct().ToArray());_history.Reset(false);_projectPath=null;_selectedTrackId=_project.Tracks.Count>0?(Guid?)_project.Tracks[0].Id:null;_selectedRegionId=null;warnings.AddRange(imported.Warnings);RefreshState();continue;}
     var changes=new List<IProjectCommand>();Track selected=target.HasValue?FindTrack(target.Value):null;Guid? regionId=null,trackId=null;
     if(ext==".mid"||ext==".midi"){
      var imported=await Task.Factory.StartNew(()=>MidiFileImport.Load(file));await new UiDispatch(this);warnings.AddRange(imported.Warnings);
      if(_project.Tracks.All(t=>t.Regions.Count==0)&&Math.Abs(beat)<.001){_history.Execute(_project,new SetTempoCommand(_project.Tempo,imported.Project.Tempo));}
      foreach(var t in imported.Project.Tracks){
       foreach(var r in t.Regions){r.StartBeat+=Math.Max(0,beat);regionId=r.Id;}
       if(selected!=null&&selected.Type==TrackType.Midi&&imported.Project.Tracks.Count==1){foreach(var r in t.Regions)changes.Add(new AddRegionCommand(selected.Id,r,selected.Regions.Count));trackId=selected.Id;}
       else{changes.Add(new AddTrackCommand(t,_project.Tracks.Count));trackId=t.Id;}
      }
     }else{
      string decoded=await Task.Factory.StartNew(()=>MediaImport.Decode(file,Path.Combine(Path.GetDirectoryName(SettingsStore.DefaultPath),"Imported Media")));await new UiDispatch(this);
      var info=WaveAudio.Inspect(decoded);Track track=selected!=null&&selected.Type==TrackType.Audio?selected:ProjectFactory.CreateTrack(Path.GetFileNameWithoutExtension(file),TrackType.Audio);
      if(track!=selected)changes.Add(new AddTrackCommand(track,_project.Tracks.Count));
      var region=ProjectFactory.CreateRegion(Path.GetFileNameWithoutExtension(file),Math.Max(0,beat),Math.Max(.25,info.DurationSeconds*_project.Tempo/60));region.MediaPath=decoded;
      changes.Add(new AddRegionCommand(track.Id,region,track.Regions.Count));trackId=track.Id;regionId=region.Id;
     }
     if(changes.Count>0){_history.Execute(_project,new CompositeCommand("Import "+Path.GetFileName(file),changes));Select(trackId,regionId);}
    }
    if(warnings.Count>0)_project.Studio.ImportNotes=String.Join(Environment.NewLine,warnings.Distinct().ToArray());
    if(warnings.Count>0)_statusLabel.Text=UiText.Translate("Import report")+": "+String.Join(" / ",warnings.Distinct().ToArray());
   }catch(Exception ex){ErrorLog.Write("Import",ex);Action report=delegate{if(!IsDisposed)ShowError("Import failed",ex);};if(InvokeRequired)BeginInvoke(report);else report();}finally{Action complete=delegate{_importing=false;if(!IsDisposed)RefreshState();};if(!IsDisposed){if(InvokeRequired)BeginInvoke(complete);else complete();}}
  }
  private async void ImportMediaDialog(bool midiOnly){
   using(var d=new OpenFileDialog{Multiselect=true,Filter=midiOnly?"Standard MIDI file|*.mid;*.midi":"Audio and MIDI|*.wav;*.wave;*.aif;*.aiff;*.flac;*.mp3;*.ogg;*.m4a;*.mid;*.midi|WAV|*.wav|Standard MIDI file|*.mid;*.midi|All files|*.*"})
    if(d.ShowDialog(this)==DialogResult.OK)await ImportDroppedFiles(d.FileNames,_selectedTrackId,_cursorBeat);
  }
 }
}