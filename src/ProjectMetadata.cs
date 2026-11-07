using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SlopProX
{
    public sealed class ProjectMetadata
    {
        public string Title{get;set;} public string Artist{get;set;} public string Genre{get;set;} public string Comment{get;set;} public string Website{get;set;}
        public ProjectMetadata(){Title=Artist=Genre=Comment=Website=String.Empty;}
        public void Validate(){foreach(string value in new[]{Title,Artist,Genre,Comment,Website})if(value==null||value.Length>4096||value.IndexOf('\0')>=0)throw new InvalidDataException("Project information is invalid.");}
    }
    public sealed class ProjectInformationForm:Form
    {
        private readonly TextBox[] _fields=new TextBox[5];public ProjectMetadata Result{get;private set;}
        public ProjectInformationForm(ProjectMetadata current){
            Text=UiText.Translate("Project Information");ClientSize=new Size(620,390);MinimumSize=new Size(500,320);StartPosition=FormStartPosition.CenterParent;BackColor=CyberTheme.Background;ForeColor=CyberTheme.Text;
            var table=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=5,Padding=new Padding(12)};table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,120));table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            string[] labels={"Title","Artist","Genre","Comment","Website"};string[] values={current.Title,current.Artist,current.Genre,current.Comment,current.Website};
            for(int i=0;i<5;i++){table.RowStyles.Add(new RowStyle(SizeType.Percent,20));table.Controls.Add(new Label{Text=UiText.Translate(labels[i]),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,i);var input=new TextBox{Text=values[i],Dock=DockStyle.Fill,BackColor=CyberTheme.Elevated,ForeColor=CyberTheme.Text,MaxLength=4096};if(i==3){input.Multiline=true;input.ScrollBars=ScrollBars.Vertical;}_fields[i]=input;table.Controls.Add(input,1,i);}
            var footer=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=48,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(8)};var apply=new Button{Text=UiText.Translate("Apply"),AutoSize=true};var cancel=new Button{Text=UiText.Translate("Cancel"),AutoSize=true,DialogResult=DialogResult.Cancel};
            apply.Click+=delegate{var data=new ProjectMetadata{Title=_fields[0].Text,Artist=_fields[1].Text,Genre=_fields[2].Text,Comment=_fields[3].Text,Website=_fields[4].Text};data.Validate();Result=data;DialogResult=DialogResult.OK;Close();};footer.Controls.Add(apply);footer.Controls.Add(cancel);Controls.Add(table);Controls.Add(footer);AcceptButton=apply;CancelButton=cancel;
        }
    }
    public sealed partial class MainForm
    {
        private void EditProjectInformation(){using(var dialog=new ProjectInformationForm(_project.Studio.Metadata))if(dialog.ShowDialog(this)==DialogResult.OK){var next=_project.Studio.Copy();next.Metadata=dialog.Result;CommitStudio("Edit project information",next);}}
    }
}
