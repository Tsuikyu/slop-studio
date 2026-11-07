using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
namespace SlopStudio {
 public sealed class AraClipInfo {public string Id{get;set;}public string Name{get;set;}public string Path{get;set;}public double StartSeconds{get;set;}public double OffsetSeconds{get;set;}public double DurationSeconds{get;set;}}
 public sealed class AraDocumentInfo {
  public List<AraClipInfo> Clips{get;set;}public double Tempo{get;set;}public int Numerator{get;set;}public int Denominator{get;set;}
  public AraDocumentInfo(){Clips=new List<AraClipInfo>();Tempo=120;Numerator=4;Denominator=4;}
  public static AraDocumentInfo FromTrack(ProjectDocument p,Track track){var info=new AraDocumentInfo{Tempo=p.Tempo,Numerator=p.TimeSignatureNumerator,Denominator=p.TimeSignatureDenominator};foreach(var r in track.Regions)if(!String.IsNullOrEmpty(r.MediaPath)&&!r.Muted)info.Clips.Add(new AraClipInfo{Id=r.Id.ToString(),Name=r.Name,Path=r.MediaPath,StartSeconds=r.StartBeat*60/p.Tempo,OffsetSeconds=r.AudioOffsetSeconds,DurationSeconds=r.LengthBeats*60/p.Tempo});return info;}
 }
 public sealed class AraTransportRequest {public int Flags;public double Position,CycleStart,CycleLength;public bool CycleEnabled;}
 internal sealed partial class LocalPlugin {
  [DllImport(Library,CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode)]private static extern IntPtr sxp_create_ara(string path,[MarshalAs(UnmanagedType.LPStr)]string id,double rate,int block,[Out]byte[] error,int size);
  [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]private static extern int sxp_ara_begin(IntPtr p,double tempo,int n,int d,[Out]byte[] error,int capacity);
  [DllImport(Library,CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode)]private static extern int sxp_ara_region(IntPtr p,string path,[In]byte[] id,[In]byte[] name,double start,double offset,double duration,[Out]byte[] error,int capacity);
  [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]private static extern int sxp_ara_end(IntPtr p,[Out]byte[] error,int capacity);
  [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]private static extern int sxp_ara_save(IntPtr p,[Out]byte[] data,int capacity);
  [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]private static extern int sxp_ara_restore(IntPtr p,[In]byte[] data,int size);
  [DllImport(Library,CallingConvention=CallingConvention.Cdecl)]private static extern int sxp_ara_requests(IntPtr p,out double position,out double start,out double length,out int enabled);
  public void UpdateAraDocument(AraDocumentInfo document){if(!Slot.UseAra)return;lock(_sync){_processingGate.EnterWriteLock();try{byte[] error=new byte[2048];if(sxp_ara_begin(_handle,document.Tempo,document.Numerator,document.Denominator,error,error.Length)==0)throw new InvalidOperationException(Decode(error));try{foreach(var clip in document.Clips)if(sxp_ara_region(_handle,clip.Path,Encoding.UTF8.GetBytes(clip.Id+"\0"),Encoding.UTF8.GetBytes(clip.Name+"\0"),clip.StartSeconds,clip.OffsetSeconds,clip.DurationSeconds,error,error.Length)==0)throw new InvalidOperationException(Decode(error));}finally{if(sxp_ara_end(_handle,error,error.Length)==0)throw new InvalidOperationException(Decode(error));}Slot.AraDocument=document;}finally{_processingGate.ExitWriteLock();}}}
  public string SaveAraState(){if(!Slot.UseAra)return "";lock(_sync){int size=sxp_ara_save(_handle,null,0);if(size<0||size>6*1024*1024)throw new InvalidOperationException("ARA archive is unavailable or exceeds 6 MiB.");var bytes=new byte[size];if(sxp_ara_save(_handle,bytes,size)!=size)throw new InvalidOperationException("ARA archive save failed.");return Convert.ToBase64String(bytes);}}
  public void RestoreAraState(string state){if(!Slot.UseAra||String.IsNullOrEmpty(state))return;lock(_sync){byte[] bytes=Convert.FromBase64String(state);_processingGate.EnterWriteLock();try{if(sxp_ara_restore(_handle,bytes,bytes.Length)==0)throw new InvalidOperationException("The ARA plug-in rejected its document archive.");}finally{_processingGate.ExitWriteLock();}}}
  public AraTransportRequest TakeAraRequest(){if(!Slot.UseAra)return null;lock(_sync){var r=new AraTransportRequest();int enabled;r.Flags=sxp_ara_requests(_handle,out r.Position,out r.CycleStart,out r.CycleLength,out enabled);r.CycleEnabled=enabled!=0;return r.Flags==0?null:r;}}
 }
 public sealed partial class NativePlugin {
  private string _araFingerprint;private int _araSequence;
  public void UpdateAraDocument(AraDocumentInfo doc){if(!Slot.UseAra)return;string signature=new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(doc);if(signature==_araFingerprint)return;Rpc("ara-update",doc,15000);_araFingerprint=signature;}
  public AraTransportRequest TakeAraRequest(){if(!Slot.UseAra||_view==null)return null;int sequence=_view.ReadInt32(3944);if(sequence==_araSequence)return null;_araSequence=sequence;return new AraTransportRequest{Flags=_view.ReadInt32(3908),Position=_view.ReadDouble(3912),CycleStart=_view.ReadDouble(3920),CycleLength=_view.ReadDouble(3928),CycleEnabled=_view.ReadInt32(3936)!=0};}
 }
}
