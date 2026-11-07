using System;
using System.IO;
using System.Text;

namespace SlopProX
{
    public static class WavMetadata
    {
        public static byte[] Build(ProjectMetadata data)
        {
            if(data==null)return new byte[0];data.Validate();
            string[] ids={"INAM","IART","IGNR","ICMT","IURL"};string[] values={data.Title,data.Artist,data.Genre,data.Comment,data.Website};
            using(var body=new MemoryStream())using(var writer=new BinaryWriter(body)){
                writer.Write(Encoding.ASCII.GetBytes("INFO"));
                for(int i=0;i<ids.Length;i++)if(!String.IsNullOrWhiteSpace(values[i])){byte[] text=Encoding.UTF8.GetBytes(values[i]);writer.Write(Encoding.ASCII.GetBytes(ids[i]));writer.Write(text.Length+1);writer.Write(text);writer.Write((byte)0);if(((text.Length+1)&1)!=0)writer.Write((byte)0);}
                writer.Flush();if(body.Length==4)return new byte[0];
                using(var result=new MemoryStream())using(var output=new BinaryWriter(result)){output.Write(Encoding.ASCII.GetBytes("LIST"));output.Write((int)body.Length);body.Position=0;body.CopyTo(result);output.Flush();return result.ToArray();}
            }
        }
    }
}
