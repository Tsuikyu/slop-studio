using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace SlopProX
{
    public sealed class SampleLibraryData
    {
        public List<string> Bookmarks{get;set;} public List<string> Favorites{get;set;}
        public SampleLibraryData(){Bookmarks=new List<string>();Favorites=new List<string>();}
        public void Validate(){if(Bookmarks==null||Favorites==null||Bookmarks.Count>512||Favorites.Count>8192||Bookmarks.Concat(Favorites).Any(x=>String.IsNullOrWhiteSpace(x)||x.Length>32767))throw new InvalidDataException("Sample library list is invalid.");}
    }
    public static class SampleLibraryStore
    {
        public static string DefaultPath{get{return Path.Combine(Path.GetDirectoryName(SettingsStore.DefaultPath),"sample-library.json");}}
        public static SampleLibraryData Load(string path){if(!File.Exists(path))return new SampleLibraryData();if(new FileInfo(path).Length>4*1024*1024)throw new InvalidDataException("Sample library list is too large.");var j=new JavaScriptSerializer{MaxJsonLength=4*1024*1024};var data=j.Deserialize<SampleLibraryData>(File.ReadAllText(path,Encoding.UTF8));if(data==null)throw new InvalidDataException("Sample library list is invalid.");data.Validate();return data;}
        public static void Save(SampleLibraryData data,string path){data.Validate();string full=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(full));string temp=full+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))using(var writer=new StreamWriter(stream,new UTF8Encoding(false))){var j=new JavaScriptSerializer{MaxJsonLength=4*1024*1024};writer.Write(j.Serialize(data));writer.Flush();stream.Flush(true);}if(File.Exists(full))File.Replace(temp,full,full+".bak");else File.Move(temp,full);}
            finally{if(File.Exists(temp))File.Delete(temp);}}
        public static bool Toggle(List<string> items,string path){string full=Path.GetFullPath(path);int index=items.FindIndex(x=>String.Equals(x,full,StringComparison.OrdinalIgnoreCase));if(index>=0){items.RemoveAt(index);return false;}items.Add(full);return true;}
    }
}
