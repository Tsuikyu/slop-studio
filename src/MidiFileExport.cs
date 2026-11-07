using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace SlopProX
{
    public static class MidiFileExport
    {
        private const int Division=960;
        private sealed class Event { public long Tick; public int Order; public byte[] Data; }
        private static void U16(BinaryWriter w,int n){w.Write((byte)(n>>8));w.Write((byte)n);}
        private static void U32(BinaryWriter w,long n){w.Write((byte)(n>>24));w.Write((byte)(n>>16));w.Write((byte)(n>>8));w.Write((byte)n);}
        private static void Var(BinaryWriter w,long value){if(value<0||value>0x0fffffff)throw new InvalidDataException("MIDI delta time exceeds the supported limit.");int n=(int)value;int buffer=n&127;while((n>>=7)>0)buffer=(buffer<<8)|((n&127)|128);for(;;){w.Write((byte)buffer);if((buffer&128)==0)break;buffer>>=8;}}
        private static byte[] Meta(int type,byte[] payload){using(var m=new MemoryStream())using(var w=new BinaryWriter(m)){w.Write((byte)0xff);w.Write((byte)type);Var(w,payload.Length);w.Write(payload);return m.ToArray();}}
        public static void Save(ProjectDocument project,string path)
        {
            ProjectValidator.Validate(project);var tracks=project.Tracks.Where(t=>t.Type==TrackType.Midi).ToList();
            if(tracks.Count==0)throw new InvalidOperationException("The project has no MIDI tracks to export.");
            string target=Path.GetFullPath(path);string temporary=target+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{
                using(var output=new BinaryWriter(new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))){
                    output.Write(Encoding.ASCII.GetBytes("MThd"));U32(output,6);U16(output,1);U16(output,tracks.Count);U16(output,Division);
                    for(int index=0;index<tracks.Count;index++){
                        var t=tracks[index];var events=new List<Event>();
                        events.Add(new Event{Tick=0,Order=-3,Data=Meta(3,Encoding.UTF8.GetBytes(t.Name))});
                        if(index==0){int micro=(int)Math.Round(60000000.0/project.Tempo);events.Add(new Event{Tick=0,Order=-2,Data=Meta(0x51,new[]{(byte)(micro>>16),(byte)(micro>>8),(byte)micro})});int power=0;for(int d=project.TimeSignatureDenominator;d>1;d>>=1)power++;events.Add(new Event{Tick=0,Order=-1,Data=Meta(0x58,new[]{(byte)project.TimeSignatureNumerator,(byte)power,(byte)24,(byte)8})});}
                        int channel=String.Equals(t.Instrument,"Drum Kit",StringComparison.OrdinalIgnoreCase)?9:0;
                        foreach(var r in t.Regions)foreach(var n in r.Notes){
                            long on=checked((long)Math.Round((r.StartBeat+n.StartBeat)*Division));long off=Math.Max(on+1,checked((long)Math.Round((r.StartBeat+n.StartBeat+n.LengthBeats)*Division)));
                            byte pitch=(byte)Math.Max(0,Math.Min(127,n.Pitch+r.Transpose));byte velocity=(byte)Math.Max(1,Math.Min(127,n.Velocity+r.VelocityOffset));
                            events.Add(new Event{Tick=on,Order=1,Data=new[]{(byte)(0x90|channel),pitch,velocity}});events.Add(new Event{Tick=off,Order=0,Data=new[]{(byte)(0x80|channel),pitch,(byte)0}});
                        }
                        events=events.OrderBy(x=>x.Tick).ThenBy(x=>x.Order).ToList();
                        using(var track=new MemoryStream())using(var writer=new BinaryWriter(track)){
                            long previous=0;foreach(var e in events){long delta=e.Tick-previous;while(delta>0x0fffffff){Var(writer,0x0fffffff);writer.Write(Meta(0x7f,new byte[0]));delta-=0x0fffffff;}Var(writer,delta);writer.Write(e.Data);previous=e.Tick;}
                            Var(writer,0);writer.Write(Meta(0x2f,new byte[0]));writer.Flush();if(track.Length>Int32.MaxValue)throw new InvalidDataException("MIDI track is too large.");
                            output.Write(Encoding.ASCII.GetBytes("MTrk"));U32(output,track.Length);track.Position=0;track.CopyTo(output.BaseStream);
                        }
                    }
                    output.Flush();output.BaseStream.Flush();
                }
                if(File.Exists(target))File.Replace(temporary,target,null);else File.Move(temporary,target);
            }finally{if(File.Exists(temporary))File.Delete(temporary);}
        }
    }
}
