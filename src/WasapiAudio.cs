using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
namespace SlopProX
{
 // FramesPlayed estimates frames that reached the endpoint, rather than frames queued for it.
 public interface IAudioOutput:IDisposable {long FramesPlayed{get;} event Action<Exception> Failed;}
 public sealed class AudioEndpoint{public string Id,Name;public override string ToString(){return Name;}}
 public sealed class WasapiStream:IAudioOutput
 {
  [UnmanagedFunctionPointer(CallingConvention.Cdecl)]private delegate void Callback(IntPtr audio,int frames,int channels,long position);
  [DllImport("SlopAudio.dll",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode)]private static extern int sxa_device(int capture,int index,StringBuilder id,int cap,StringBuilder name,int namecap);
  [DllImport("SlopAudio.dll",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode)]private static extern IntPtr sxa_open(string id,int capture,int exclusive,int rate,int block,Callback callback,[Out]byte[] error,int cap);
  [DllImport("SlopAudio.dll",CallingConvention=CallingConvention.Cdecl)]private static extern void sxa_close(IntPtr handle);
  [DllImport("SlopAudio.dll",CallingConvention=CallingConvention.Cdecl)]private static extern long sxa_frames(IntPtr handle);
  [DllImport("SlopAudio.dll",CallingConvention=CallingConvention.Cdecl)]private static extern int sxa_channels(IntPtr handle);
  [DllImport("SlopAudio.dll",CallingConvention=CallingConvention.Cdecl)]private static extern int sxa_status(IntPtr handle,[Out]byte[] error,int capacity);
  [DllImport("SlopAudio.dll",CallingConvention=CallingConvention.Cdecl)]private static extern int sxa_format(IntPtr handle);
  [DllImport("SlopAudio.dll",CallingConvention=CallingConvention.Cdecl)]private static extern int sxa_bufferframes(IntPtr handle);
  public string SampleFormat{get{int value=sxa_format(_handle);return (value&65535)+((value&65536)!=0?"-bit float":"-bit PCM");}}public int DeviceBufferFrames{get{return sxa_bufferframes(_handle);}}
  private bool _reported;private IntPtr _handle;private Callback _callback;private float[] _samples;public int Channels{get;private set;}
  public event Action<Exception> Failed;public long FramesPlayed{get{if(_handle==IntPtr.Zero)return 0;if(!_reported){byte[] error=new byte[2048];if(sxa_status(_handle,error,error.Length)<0){_reported=true;var h=Failed;if(h!=null)h(new InvalidOperationException(Encoding.UTF8.GetString(error).TrimEnd('\0')));}}return sxa_frames(_handle);}}
  public static List<AudioEndpoint> Devices(bool input){var result=new List<AudioEndpoint>{new AudioEndpoint{Id="",Name="System default"}};for(int i=0;i<256;i++){var id=new StringBuilder(1024);var name=new StringBuilder(512);int n=sxa_device(input?1:0,i,id,id.Capacity,name,name.Capacity);if(n<=0)break;result.Add(new AudioEndpoint{Id=id.ToString(),Name=name.ToString()});}return result;}
  public WasapiStream(string id,bool input,bool exclusive,int rate,int block,Action<float[],int,int,long> action){
   _samples=new float[65536];_callback=delegate(IntPtr data,int frames,int channels,long position){try{int count=frames*channels;if(count>_samples.Length)throw new InvalidOperationException("Audio packet exceeds capacity.");if(input)Marshal.Copy(data,_samples,0,count);else Array.Clear(_samples,0,count);action(_samples,frames,channels,position);if(!input)Marshal.Copy(_samples,0,data,count);}catch(Exception ex){Action<Exception> h=Failed;if(h!=null)h(ex);}};
   byte[] error=new byte[2048];_handle=sxa_open(id,input?1:0,exclusive?1:0,rate,block,_callback,error,error.Length);if(_handle==IntPtr.Zero)throw new InvalidOperationException(Encoding.UTF8.GetString(error).TrimEnd('\0'));Channels=sxa_channels(_handle);
  }
  public void Dispose(){if(_handle!=IntPtr.Zero){IntPtr h=_handle;_handle=IntPtr.Zero;sxa_close(h);}GC.KeepAlive(_callback);}
 }
}
