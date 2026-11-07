using System;
using System.IO;
using System.Windows.Forms;

namespace SlopStudio
{
    public static class ProjectProtection
    {
        public static bool IsProtected(string path,string applicationDirectory)
        {
            if(String.IsNullOrWhiteSpace(path))return false;
            string full=Path.GetFullPath(path);
            if(String.Equals(Path.GetExtension(full),".sloptemplate",StringComparison.OrdinalIgnoreCase))return true;
            return IsBundledExample(full,applicationDirectory);
        }
        private static bool IsBundledExample(string full,string applicationDirectory){string examples=Path.GetFullPath(Path.Combine(applicationDirectory,"Examples")).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;return full.StartsWith(examples,StringComparison.OrdinalIgnoreCase);}
        public static void ValidateProjectDestination(string path,string applicationDirectory)
        {
            if(!String.Equals(Path.GetExtension(path),".slopdaw",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Save projects with the .slopdaw extension.");
            if(IsProtected(path,applicationDirectory))throw new InvalidOperationException("Examples and templates are read-only. Save the project in another location.");
        }
        public static void SaveNewTemplate(ProjectDocument project,string path,string applicationDirectory)
        {
            string full=Path.GetFullPath(path);
            if(!String.Equals(Path.GetExtension(full),".sloptemplate",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Templates use the .sloptemplate extension.");
            if(IsBundledExample(full,applicationDirectory))throw new InvalidOperationException("The bundled examples folder is read-only.");
            string temp=full+"."+Guid.NewGuid().ToString("N")+".tmp.slopdaw";
            try{ProjectFile.SaveAtomic(project,temp,false);File.Move(temp,full);}
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }

    public sealed partial class MainForm
    {
        private void SaveProjectAsTemplate()
        {
            string folder=Path.Combine(Path.GetDirectoryName(SettingsStore.DefaultPath),"Templates");Directory.CreateDirectory(folder);
            using(var dialog=new SaveFileDialog{Title=UiText.Translate("Save Project as Template…"),Filter="Slop Studio template|*.sloptemplate",DefaultExt="sloptemplate",AddExtension=true,OverwritePrompt=false,InitialDirectory=folder,FileName=SafeFileName(_project.Name)+".sloptemplate"}){
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                try{
                    string path=Path.GetFullPath(dialog.FileName);_engine.CapturePluginStates();ProjectProtection.SaveNewTemplate(_project,path,AppDomain.CurrentDomain.BaseDirectory);_statusLabel.Text=UiText.Translate("Template saved; referenced audio remains in its original location.");
                }catch(Exception ex){ShowError("Template could not be saved",ex);}
            }
        }
    }
}
