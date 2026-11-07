using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
namespace SlopStudio {
 public sealed partial class MainForm {
  private Dictionary<Guid,StockEffectEditor> _stockWindows=new Dictionary<Guid,StockEffectEditor>();
  private void AddStockEffect(Track track,string kind){Guid targetId=track==null?Guid.Empty:track.Id;var order=InsertChain.Entries(_project,targetId).Select(e=>e.Id).ToList();var state=_project.Studio.Copy();var slot=StockEffects.Create(targetId,kind);state.Effects.Add(slot);order.Add(slot.Id);InsertChain.Set(state,targetId,order);CommitStudio("Add Slop "+kind,state);OpenStockEffect(slot.Id);}
  private void OpenStockEffect(Guid id){StockEffectSlot slot=_project.Studio.Effects.Find(s=>s.Id==id);if(slot==null)return;StockEffectEditor existing;if(_stockWindows.TryGetValue(id,out existing)&&!existing.IsDisposed){existing.Activate();return;}var editor=new StockEffectEditor(slot,CommitStockEffectEdit,delegate{return _engine.GetStockProcessor(id);},_project.Tracks);_stockWindows[id]=editor;editor.FormClosed+=delegate{_stockWindows.Remove(id);};editor.Show(this);}
  private void CommitStockEffectEdit(StockEffectSlot value){
   if(IsDisposed)return;var state=_project.Studio.Copy();int index=state.Effects.FindIndex(f=>f.Id==value.Id);if(index<0)return;
   state.Effects[index]=value.Copy();_history.Execute(_project,new SetStudioCommand("Edit Slop "+value.Kind,_project.Studio,state));
   if(!_engine.UpdateStockEffect(value)){RefreshState();return;}
   _undoButton.Enabled=_undoMenuItem.Enabled=_history.CanUndo;_redoButton.Enabled=_redoMenuItem.Enabled=_history.CanRedo;
   _statusLabel.Text=BuildStatus();string name=String.IsNullOrEmpty(_projectPath)?_project.Name:Path.GetFileName(_projectPath);
   Text=T("app.title")+" — "+name+(_history.IsDirty?" *":"");
  }
  private void ReorderInsert(Track track,Guid source,Guid target){if(source==target)return;Guid targetId=track==null?Guid.Empty:track.Id;if(track!=null&&FindTrack(track.Id)==null)return;var ids=InsertChain.Entries(_project,targetId).Select(e=>e.Id).ToList();int from=ids.IndexOf(source),to=ids.IndexOf(target);if(from<0||to<0)return;ids.RemoveAt(from);ids.Insert(to,source);var state=_project.Studio.Copy();InsertChain.Set(state,targetId,ids);CommitStudio("Reorder inserts",state);}
  private void ShowStockRack(){
   Track track=SelectedTrack;if(track==null)return;
   using(var dialog=new Form{Text=track.Name+" — Slop Effects",ClientSize=new Size(720,430),StartPosition=FormStartPosition.CenterParent,BackColor=Color.FromArgb(32,36,44),ForeColor=Color.Gainsboro,Font=new Font("Segoe UI",9)}){
    var list=new ListView{Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,MultiSelect=false,HideSelection=false,BackColor=Color.FromArgb(39,44,53),ForeColor=Color.Gainsboro,BorderStyle=BorderStyle.None};list.Columns.Add("#",45);list.Columns.Add("Effect",280);list.Columns.Add("State",120);
    Action refresh=delegate{list.Items.Clear();int index=0;foreach(var slot in _project.Studio.Effects.Where(f=>f.TrackId==track.Id))list.Items.Add(new ListViewItem(new[]{(++index).ToString(),"Slop "+slot.Kind,UiText.Translate(slot.Enabled?"Enabled":"Bypassed")}){Tag=slot.Id});};
    var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=85,Padding=new Padding(10),BackColor=dialog.BackColor,AutoScroll=true};Action<string,Action> add=delegate(string text,Action action){var b=new Button{Text=UiText.Translate(text),AutoSize=true,Height=29,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(55,63,76),ForeColor=Color.Gainsboro};b.Click+=delegate{action();refresh();};footer.Controls.Add(b);};
    foreach(string name in StockEffects.Names){string kind=name;add("+ "+name,delegate{AddStockEffect(track,kind);});}
    Action edit=delegate{if(list.SelectedItems.Count>0){Guid id=(Guid)list.SelectedItems[0].Tag;dialog.Close();BeginInvoke((Action)delegate{OpenStockEffect(id);});}};
    add("Edit",edit);add("Bypass",delegate{if(list.SelectedItems.Count==0)return;Guid id=(Guid)list.SelectedItems[0].Tag;var s=_project.Studio.Copy();var fx=s.Effects.Find(f=>f.Id==id);fx.Enabled=!fx.Enabled;CommitStudio("Bypass Slop effect",s);});add("Remove",delegate{if(list.SelectedItems.Count==0)return;Guid id=(Guid)list.SelectedItems[0].Tag;var s=_project.Studio.Copy();s.Effects.RemoveAll(f=>f.Id==id);CommitStudio("Remove Slop effect",s);});list.DoubleClick+=delegate{edit();};dialog.Controls.Add(list);dialog.Controls.Add(footer);refresh();dialog.ShowDialog(this);
   }
  }
 }
}
