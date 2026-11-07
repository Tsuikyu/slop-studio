using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace SlopProX {
 public sealed class StockEffectSlot {
  public Guid Id{get;set;}public Guid TrackId{get;set;}public Guid? SidechainTrackId{get;set;}public string Kind{get;set;}public bool Enabled{get;set;}public Dictionary<string,double> Values{get;set;}
  public StockEffectSlot(){Id=Guid.NewGuid();Enabled=true;Kind="Compressor";Values=new Dictionary<string,double>();}
  public double Value(string name,double fallback){double v;return Values.TryGetValue(name,out v)?v:fallback;}
  public StockEffectSlot Copy(){return new StockEffectSlot{Id=Id,TrackId=TrackId,SidechainTrackId=SidechainTrackId,Kind=Kind,Enabled=Enabled,Values=new Dictionary<string,double>(Values)};}
 }
 public sealed class EffectParameter {public string Key,Name,Unit;public double Min,Max,Default;public EffectParameter(string key,string name,string unit,double min,double max,double initial){Key=key;Name=name;Unit=unit;Min=min;Max=max;Default=initial;}}
 public static class StockEffects {
  public static readonly string[] Names={"Visual Equalizer","Compressor","Saturator","Limiter","EDM Ducker","Stereo Imager"};
  public static List<EffectParameter> Parameters(string kind){
   var p=new List<EffectParameter>();
   if(kind==Names[0]){double[] f={80,300,1000,4000,12000};for(int i=0;i<5;i++){p.Add(new EffectParameter("Frequency"+i,"Band "+(i+1)+" Frequency","Hz",20,20000,f[i]));p.Add(new EffectParameter("Gain"+i,"Band "+(i+1)+" Gain","dB",-24,24,0));p.Add(new EffectParameter("Q"+i,"Band "+(i+1)+" Q","",.2,8,.707));}}
   else if(kind==Names[1]){p.Add(new EffectParameter("Threshold","Threshold","dB",-60,0,-18));p.Add(new EffectParameter("Ratio","Ratio",":1",1,20,4));p.Add(new EffectParameter("Attack","Attack","ms",.1,100,10));p.Add(new EffectParameter("Release","Release","ms",10,1000,120));p.Add(new EffectParameter("Makeup","Makeup","dB",0,24,0));}
   else if(kind==Names[2]){p.Add(new EffectParameter("Drive","Drive","dB",0,30,6));p.Add(new EffectParameter("Mix","Mix","%",0,100,100));p.Add(new EffectParameter("Output","Output","dB",-24,6,-3));}
   else if(kind==Names[3]){p.Add(new EffectParameter("Ceiling","Ceiling","dB",-24,0,-.5));p.Add(new EffectParameter("Release","Release","ms",10,1000,100));}
   else if(kind==Names[4]){p.Add(new EffectParameter("Mode","Mode · 0 Auto / 1 MIDI / 2 Audio","",0,2,0));p.Add(new EffectParameter("Depth","Depth","%",0,100,80));p.Add(new EffectParameter("Period","Period","beats",.25,8,1));p.Add(new EffectParameter("Release","Recovery","ms",10,1500,220));p.Add(new EffectParameter("Input","Sidechain Input Channel","",1,32,1));p.Add(new EffectParameter("Threshold","Sidechain Threshold","dB",-60,0,-24));}
   else if(kind==Names[5]){p.Add(new EffectParameter("Width","Stereo Width","%",0,200,100));p.Add(new EffectParameter("Output","Output","dB",-24,6,0));}
   return p;
  }
  public static StockEffectSlot Create(Guid track,string kind){if(Array.IndexOf(Names,kind)<0)throw new ArgumentException("Unknown stock effect.");var slot=new StockEffectSlot{TrackId=track,Kind=kind};foreach(var p in Parameters(kind))slot.Values[p.Key]=p.Default;return slot;}
  public static void Validate(StockEffectSlot slot){if(slot==null||slot.Values==null||slot.Id==Guid.Empty||Array.IndexOf(Names,slot.Kind)<0)throw new System.IO.InvalidDataException("Invalid stock effect.");var descriptors=Parameters(slot.Kind);foreach(var p in slot.Values){var d=descriptors.Find(x=>x.Key==p.Key);if(d==null||Double.IsNaN(p.Value)||Double.IsInfinity(p.Value)||p.Value<d.Min||p.Value>d.Max)throw new System.IO.InvalidDataException("Invalid stock effect parameter: "+p.Key);}}
 }
 public sealed class EqBand {
  private double b0=1,b1,b2,a1,a2,z1l,z2l,z1r,z2r;
  public void Configure(double rate,double frequency,double db,double q){double w=2*Math.PI*Math.Min(rate*.49,frequency)/rate,alpha=Math.Sin(w)/(2*q),a=Math.Pow(10,db/40),a0=1+alpha/a;b0=(1+alpha*a)/a0;b1=-2*Math.Cos(w)/a0;b2=(1-alpha*a)/a0;a1=-2*Math.Cos(w)/a0;a2=(1-alpha/a)/a0;}
  public float Process(float input,bool right){double z1=right?z1r:z1l,z2=right?z2r:z2l,output=b0*input+z1;z1=b1*input-a1*output+z2;z2=b2*input-a2*output;if(right){z1r=z1;z2r=z2;}else{z1l=z1;z2l=z2;}return (float)output;}
  public double Magnitude(double hz,double rate){double w=2*Math.PI*hz/rate,c=Math.Cos(w),s=Math.Sin(w),c2=Math.Cos(2*w),s2=Math.Sin(2*w);return Math.Sqrt(((b0+b1*c+b2*c2)*(b0+b1*c+b2*c2)+(b1*s+b2*s2)*(b1*s+b2*s2))/((1+a1*c+a2*c2)*(1+a1*c+a2*c2)+(a1*s+a2*s2)*(a1*s+a2*s2)));}
 }
 public sealed class StockEffectProcessor {
  private EqBand[] _bands={new EqBand(),new EqBand(),new EqBand(),new EqBand(),new EqBand()};private double _envelope,_gain=1,_age=1000;private double[] _last=new double[15];public volatile float InputPeak,OutputPeak,Reduction;
  public void Process(StockEffectSlot slot,float[] data,int frames,int rate,double beat,double tempo,MidiEventPacket[] events,int count,float[] input,int inputOffset,IList<MidiInputMessage> live,bool playing,float[] sidechain=null){
   if(!slot.Enabled)return;bool equalizer=slot.Kind==StockEffects.Names[0];if(equalizer)for(int i=0;i<5;i++){double f=slot.Value("Frequency"+i,new[]{80.0,300,1000,4000,12000}[i]),g=slot.Value("Gain"+i,0),q=slot.Value("Q"+i,.707);if(f!=_last[i*3]||g!=_last[i*3+1]||q!=_last[i*3+2]){_bands[i].Configure(rate,f,g,q);_last[i*3]=f;_last[i*3+1]=g;_last[i*3+2]=q;}}
   double threshold=slot.Value("Threshold",-18),ratio=slot.Value("Ratio",4),attack=Math.Exp(-1/(rate*slot.Value("Attack",10)*.001)),release=Math.Exp(-1/(rate*slot.Value("Release",120)*.001)),makeup=Math.Pow(10,slot.Value("Makeup",0)/20);
   int mode=(int)Math.Round(slot.Value("Mode",0)),eventIndex=0;double depth=slot.Value("Depth",80)/100,period=slot.Value("Period",1),outputGain=Math.Pow(10,slot.Value("Output",0)/20),drive=Math.Pow(10,slot.Value("Drive",6)/20),mix=slot.Value("Mix",100)/100,width=slot.Value("Width",100)/100,ceiling=Math.Pow(10,slot.Value("Ceiling",-.5)/20);
   bool liveTrigger=live!=null&&live.Any(e=>(e.Status&240)==0x90&&e.Data2>0);if(liveTrigger)_age=0;
   float inputPeak=0,outputPeak=0;double minimumGain=1;
   for(int i=0;i<frames;i++){double left=data[i*2],right=data[i*2+1],peak=Math.Max(Math.Abs(left),Math.Abs(right));inputPeak=Math.Max(inputPeak,(float)peak);
    if(equalizer){for(int band=0;band<5;band++){left=_bands[band].Process((float)left,false);right=_bands[band].Process((float)right,true);}}
    else if(slot.Kind==StockEffects.Names[1]){double c=peak>_envelope?attack:release;_envelope=c*_envelope+(1-c)*peak;double level=20*Math.Log10(Math.Max(1e-12,_envelope)),over=Math.Max(0,level-threshold),db=-over*(1-1/ratio);_gain=Math.Pow(10,db/20);left*=_gain*makeup;right*=_gain*makeup;minimumGain=Math.Min(minimumGain,_gain);}
    else if(slot.Kind==StockEffects.Names[2]){left=(left*(1-mix)+Math.Tanh(left*drive)/Math.Tanh(drive)*mix)*outputGain;right=(right*(1-mix)+Math.Tanh(right*drive)/Math.Tanh(drive)*mix)*outputGain;}
    else if(slot.Kind==StockEffects.Names[3]){double target=peak>ceiling?ceiling/peak:1;_gain=target<_gain?target:1-(1-_gain)*release;left=Math.Max(-ceiling,Math.Min(ceiling,left*_gain));right=Math.Max(-ceiling,Math.Min(ceiling,right*_gain));minimumGain=Math.Min(minimumGain,_gain);}
    else if(slot.Kind==StockEffects.Names[4]){
     while(eventIndex<count&&events[eventIndex].Offset<=i){var ev=events[eventIndex++];if((ev.Status&240)==0x90&&ev.Data2>0)_age=0;}
     double duck=0;if(mode==0&&playing){double phase=((beat+i*tempo/(60*rate))%period+period)%period/period;duck=Math.Pow(1-phase,2);}
     else if(mode==1){duck=Math.Exp(-_age/(slot.Value("Release",220)*.001));_age+=1.0/rate;}
     else if(mode==2&&(input!=null||sidechain!=null)){int channel=(int)Math.Round(slot.Value("Input",1))-1,index=(inputOffset+i)*32+channel;double sc=sidechain!=null&&i*2+1<sidechain.Length?Math.Max(Math.Abs(sidechain[i*2]),Math.Abs(sidechain[i*2+1])):input!=null&&index<input.Length?Math.Abs(input[index]):0;_envelope=sc>_envelope?sc:_envelope*release;double thresh=Math.Pow(10,slot.Value("Threshold",-24)/20);duck=Math.Max(0,Math.Min(1,(_envelope-thresh)/Math.Max(thresh,.001)));}
     double gain=1-depth*duck;left*=gain;right*=gain;minimumGain=Math.Min(minimumGain,gain);
    }else if(slot.Kind==StockEffects.Names[5]){double mid=(left+right)*.5,side=(left-right)*.5*width;left=(mid+side)*outputGain;right=(mid-side)*outputGain;}
    data[i*2]=(float)left;data[i*2+1]=(float)right;outputPeak=Math.Max(outputPeak,(float)Math.Max(Math.Abs(left),Math.Abs(right)));
   }InputPeak=Math.Max(inputPeak,InputPeak*.85f);OutputPeak=Math.Max(outputPeak,OutputPeak*.85f);Reduction=(float)(-20*Math.Log10(Math.Max(1e-6,minimumGain)));
  }
 }
}
