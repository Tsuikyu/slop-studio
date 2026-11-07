using System;
using System.IO;
using System.Text;
using System.Threading;
namespace SlopProX
{
 public sealed class AudioExportOptions{public int SampleRate=48000;public int Bits=24;public bool Float;public double StartBeat,EndBeat=16,TailSeconds;}
 public sealed class ExportWaveWriter:IDisposable
 {
  private FileStream _stream;private BinaryWriter _writer;private int _rate,_channels,_bits;private bool _float;private long _bytes;private byte[] _metadata=new byte[0];
  public ExportWaveWriter(string file,int rate,int channels,int bits,bool floating){if(bits!=16&&bits!=24&&bits!=32||floating&&bits!=32)throw new ArgumentException("Invalid WAV encoding.");_rate=rate;_channels=channels;_bits=bits;_float=floating;_stream=new FileStream(file,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.Read);_writer=new BinaryWriter(_stream);_writer.Write(new byte[44]);Header();}
  private void Header(){long position=_stream.Position;_stream.Position=0;_writer.Write(Encoding.ASCII.GetBytes("RIFF"));_writer.Write((uint)(36+_bytes+(_bytes&1)+_metadata.Length));_writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));_writer.Write(16);_writer.Write((ushort)(_float?3:1));_writer.Write((ushort)_channels);_writer.Write(_rate);_writer.Write(_rate*_channels*_bits/8);_writer.Write((ushort)(_channels*_bits/8));_writer.Write((ushort)_bits);_writer.Write(Encoding.ASCII.GetBytes("data"));_writer.Write((uint)_bytes);_stream.Position=position;}
  public void SetMetadata(ProjectMetadata data){_metadata=WavMetadata.Build(data);}
  public void Write(float[] data,int offset,int count){if(_bytes+(long)count*_bits/8>UInt32.MaxValue-36)throw new IOException("RIFF WAV size limit reached.");for(int i=offset;i<offset+count;i++){double v=data[i];if(Double.IsNaN(v)||Double.IsInfinity(v))throw new IOException("DSP emitted non-finite audio.");if(_float)_writer.Write((float)v);else{v=Math.Max(-1,Math.Min(1,v));long n=(long)Math.Round(v*(_bits==16?32767:_bits==24?8388607:2147483647));for(int b=0;b<_bits/8;b++)_writer.Write((byte)(n>>(8*b)));}}_bytes+=(long)count*_bits/8;}
  public void Dispose(){if(_writer==null)return;try{if(36+_bytes+(_bytes&1)+_metadata.Length>UInt32.MaxValue)throw new IOException("RIFF WAV size limit reached.");if((_bytes&1)!=0)_writer.Write((byte)0);_writer.Write(_metadata);Header();_writer.Flush();_stream.Flush(true);}finally{_writer.Dispose();_writer=null;}}
 }
 public static class AudioExport
 {
  public static ProjectDocument StemProject(ProjectDocument project,Guid trackId){var copy=ProjectFactory.Clone(project);if(!copy.Tracks.Exists(t=>t.Id==trackId))throw new InvalidOperationException("Stem track no longer exists.");foreach(var track in copy.Tracks)track.Solo=track.Id==trackId;return copy;}
  public static void Render(ProjectDocument project,string file,AudioExportOptions options,CancellationToken cancel,Action<double> progress){
   if(options.EndBeat<=options.StartBeat||options.StartBeat<0||options.TailSeconds<0||options.TailSeconds>60)throw new InvalidDataException("Invalid export range.");
   ProjectDocument snapshot=ProjectFactory.Clone(project);snapshot.CycleEnabled=false;foreach(var track in snapshot.Tracks){track.Regions.RemoveAll(r=>r.StartBeat>=options.EndBeat);foreach(var region in track.Regions){region.LengthBeats=Math.Min(region.LengthBeats,options.EndBeat-region.StartBeat);region.Notes.RemoveAll(n=>n.StartBeat>=region.LengthBeats);foreach(var note in region.Notes)note.LengthBeats=Math.Min(note.LengthBeats,region.LengthBeats-note.StartBeat);}}foreach(var lane in snapshot.Studio.Automation){double value=lane.Evaluate(options.EndBeat,0);lane.Points.RemoveAll(p=>p.Beat>=options.EndBeat);lane.Points.Add(new AutomationPoint{Beat=options.EndBeat,Value=value});}foreach(var comp in snapshot.Studio.Comps){comp.Slices.RemoveAll(s=>s.StartBeat>=options.EndBeat);foreach(var slice in comp.Slices)slice.EndBeat=Math.Min(slice.EndBeat,options.EndBeat);}
   var settings=new AppSettings{SampleRate=options.SampleRate,BufferFrames=512};
   long frames=(long)Math.Ceiling(((options.EndBeat-options.StartBeat)*60/project.Tempo+options.TailSeconds)*options.SampleRate);
   string temporary=file+"."+Guid.NewGuid().ToString("N")+".rendering.wav";
   try{using(var engine=new ArrangementEngine(settings))using(var writer=new ExportWaveWriter(temporary,options.SampleRate,2,options.Bits,options.Float)){
    writer.SetMetadata(project.Studio.Metadata);
    engine.PrepareOffline(snapshot,options.StartBeat);if(engine.Warnings.Count>0)throw new InvalidOperationException(String.Join(Environment.NewLine,engine.Warnings.ToArray()));
    float[] block=new float[1024];int skip=engine.CompensationSamples;while(skip>0){cancel.ThrowIfCancellationRequested();int n=Math.Min(512,skip);engine.Render(block,n,0);skip-=n;}
    for(long position=0;position<frames;position+=512){cancel.ThrowIfCancellationRequested();int n=(int)Math.Min(512,frames-position);engine.Render(block,n,0);writer.Write(block,0,n*2);if(progress!=null)progress((position+n)/(double)frames);}
   }if(File.Exists(file))File.Replace(temporary,file,null);else File.Move(temporary,file);}finally{if(File.Exists(temporary))File.Delete(temporary);}
  }
 }
}
