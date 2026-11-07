using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
namespace SlopProX {
 public static class MidiFileImport {
  private sealed class Note {public long Start,End;public int Pitch,Velocity,Channel;}
  private sealed class Tempo {public long Tick;public int Micro=500000;}
  private sealed class TrackData {public string Name;public List<Note> Notes=new List<Note>();}
  private static int U16(BinaryReader r){return (r.ReadByte()<<8)|r.ReadByte();}
  private static uint U32(BinaryReader r){return ((uint)U16(r)<<16)|(uint)U16(r);}
  private static long Var(BinaryReader r){long n=0;for(int i=0;i<4;i++){int b=r.ReadByte();n=(n<<7)|(uint)(b&127);if((b&128)==0)return n;}throw new InvalidDataException("MIDI delta time is longer than four bytes.");}
  public static ImportResult Load(string path) {
   if(new FileInfo(path).Length>64*1024*1024)throw new InvalidDataException("MIDI file exceeds 64 MiB.");
   var result=new ImportResult{Project=new ProjectDocument{Name=Path.GetFileNameWithoutExtension(path)}};
   var tracks=new List<TrackData>();var tempos=new List<Tempo>{new Tempo{Tick=0}};
   int division;
   using(var r=new BinaryReader(File.OpenRead(path))){
    if(Encoding.ASCII.GetString(r.ReadBytes(4))!="MThd")throw new InvalidDataException("Standard MIDI file header MThd is missing.");
    uint header=U32(r);if(header<6||header>1024)throw new InvalidDataException("Invalid MIDI header size.");
    int format=U16(r),count=U16(r);division=U16(r);if(format>1)throw new InvalidDataException("MIDI format 2 contains independent songs; export it as MIDI format 0 or 1.");
    if(count>128||division==0)throw new InvalidDataException("MIDI track count or timing division is unsupported.");r.BaseStream.Position+=header-6;
    for(int tr=0;tr<count;tr++){
     if(Encoding.ASCII.GetString(r.ReadBytes(4))!="MTrk")throw new InvalidDataException("MIDI track chunk MTrk is missing.");
     long length=U32(r),end=r.BaseStream.Position+length;if(end>r.BaseStream.Length)throw new InvalidDataException("MIDI track is truncated.");
     var td=new TrackData{Name="MIDI "+(tr+1)};tracks.Add(td);long tick=0;int running=0;var held=new Dictionary<int,Queue<Note>>();var released=new List<Note>();bool[] sustain=new bool[16];
     Action<int,int,long> release=delegate(int ch,int key,long at){Queue<Note> q;if(held.TryGetValue(ch*128+key,out q)&&q.Count>0){var n=q.Dequeue();if(sustain[ch])released.Add(n);else n.End=Math.Max(n.Start+1,at);}};
     int events=0;
     while(r.BaseStream.Position<end){
      if(++events>2000000)throw new InvalidDataException("MIDI event count exceeds supported limit.");
      tick+=Var(r);int status=r.ReadByte(),first=-1;
      if(status<128){if(running<0x80||running>=0xf0)throw new InvalidDataException("MIDI running status has no preceding channel status.");first=status;status=running;}
      if(status==0xff){int meta=r.ReadByte();int size=checked((int)Var(r));if(size>end-r.BaseStream.Position)throw new InvalidDataException("MIDI meta event is truncated.");byte[] bytes=r.ReadBytes(size);
       if(meta==3&&size>0)td.Name=new UTF8Encoding(false,false).GetString(bytes).TrimEnd('\0');
       else if(meta==0x51&&size==3){int micro=(bytes[0]<<16)|(bytes[1]<<8)|bytes[2];if(micro>0)tempos.Add(new Tempo{Tick=tick,Micro=micro});}
       else if(meta==0x58&&size>=2&&tick==0){result.Project.TimeSignatureNumerator=Math.Max(1,Math.Min(32,(int)bytes[0]));result.Project.TimeSignatureDenominator=1<<Math.Min(5,(int)bytes[1]);}
       if(meta==0x2f)break;continue;
      }
      if(status==0xf0||status==0xf7){running=0;long n=Var(r);if(n>end-r.BaseStream.Position)throw new InvalidDataException("MIDI SysEx is truncated.");r.BaseStream.Position+=n;continue;}
      if(status>=0xf8)continue;
      if(status>=0xf0)throw new InvalidDataException("Unsupported MIDI system event.");
      running=status;int type=status&240,ch=status&15;int a=first>=0?first:r.ReadByte(),b=(type==0xc0||type==0xd0)?0:r.ReadByte();if(a>127||b>127)throw new InvalidDataException("Invalid MIDI data byte.");
      if(type==0x90&&b>0){var n=new Note{Start=tick,End=tick+1,Pitch=a,Velocity=b,Channel=ch};td.Notes.Add(n);Queue<Note> q;if(!held.TryGetValue(ch*128+a,out q))held[ch*128+a]=q=new Queue<Note>();q.Enqueue(n);}
      else if(type==0x80||type==0x90)release(ch,a,tick);
      else if(type==0xb0&&a==64){sustain[ch]=b>=64;if(!sustain[ch]){foreach(var n in released.Where(n=>n.Channel==ch))n.End=Math.Max(n.Start+1,tick);released.RemoveAll(n=>n.Channel==ch);}}
     }
     foreach(var q in held.Values)foreach(var n in q)n.End=Math.Max(n.Start+1,tick);
     foreach(var n in released)n.End=Math.Max(n.Start+1,tick);
     r.BaseStream.Position=end;
    }
   }
   tempos=tempos.OrderBy(t=>t.Tick).ToList();int initial=tempos.Last(t=>t.Tick==0).Micro;
   result.Project.Tempo=Math.Max(20,Math.Min(400,60000000.0/initial));
   Func<long,double> beat;
   if((division&0x8000)!=0){int fps=-(sbyte)(division>>8),sub=division&255;double rate=(fps==29?29.97:fps)*sub;if(rate<=0)throw new InvalidDataException("Invalid SMPTE MIDI timing.");beat=t=>t/rate*result.Project.Tempo/60;}
   else beat=delegate(long tick){double seconds=0;long previous=0;int micro=500000;foreach(var tempo in tempos){if(tempo.Tick>tick)break;seconds+=(tempo.Tick-previous)*micro/(division*1000000.0);previous=tempo.Tick;micro=tempo.Micro;}seconds+=(tick-previous)*micro/(division*1000000.0);return seconds*result.Project.Tempo/60;};
   foreach(var td in tracks)foreach(var group in td.Notes.GroupBy(n=>n.Channel)){
    var track=ProjectFactory.CreateTrack((td.Name+" · "+(group.Key+1)).Substring(0,Math.Min(120,td.Name.Length+4)),TrackType.Midi);
    if(group.Key==9)track.Instrument="Drum Kit";
    var region=ProjectFactory.CreateRegion(td.Name.Substring(0,Math.Min(120,td.Name.Length)),0,Math.Max(4,group.Max(n=>beat(n.End))));
    foreach(var n in group){double at=beat(n.Start);region.Notes.Add(new MidiNote{StartBeat=at,LengthBeats=Math.Max(1.0/9600,beat(n.End)-at),Pitch=n.Pitch,Velocity=n.Velocity});}
    region.LengthBeats=Math.Max(region.LengthBeats,region.Notes.Max(n=>n.StartBeat+n.LengthBeats));track.Regions.Add(region);result.Project.Tracks.Add(track);
   }
   if(tempos.Select(t=>t.Micro).Distinct().Count()>1)result.Warnings.Add("Tempo changes were converted to note positions at the initial project tempo, preserving elapsed timing.");
   if(result.Project.Tracks.Count==0)result.Warnings.Add("This MIDI file contains no note events.");
   if(result.Project.Tracks.Count>128)throw new InvalidDataException("MIDI import produces more than 128 instrument tracks.");
   ProjectValidator.Validate(result.Project);return result;
  }
 }
}