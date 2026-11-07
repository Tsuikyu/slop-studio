using System;
using System.Collections.Generic;
using System.Linq;

namespace SlopProX
{
 public static class DemoProjects
 {
  private static Track Instrument(string name,string sound,double gain,double pan=0)
  {var t=ProjectFactory.CreateTrack(name,TrackType.Midi);t.Instrument=sound;t.GainDb=gain;t.Pan=pan;return t;}
  private static void Note(Region r,double start,double length,int pitch,int velocity)
  {r.Notes.Add(new MidiNote{StartBeat=start,LengthBeats=length,Pitch=pitch,Velocity=Math.Max(1,Math.Min(127,velocity))});}
  private static string Section(int block)
  {return block<2?"Intro":block<4?"Verse":block<6?"Build":block<10?"Drop A":block<12?"Break":block<14?"Build B":block<18?"Drop B":"Outro";}

  public static ProjectDocument MelodicHouse()
  {
   var p=new ProjectDocument{Name="EDM Sample - Melodic House",Tempo=128,MasterGainDb=0,CycleEnabled=true,CycleStartBeat=96,CycleEndBeat=160};
   string[] chords={"Am","Fmaj7","C","G"};
   for(int bar=0;bar<80;bar++)p.Studio.Chords.Add(new ChordEvent{Beat=bar*4,Length=4,Name=chords[bar%4]});
   var piano=Instrument("01 · Chord Keys","Soft Piano",-9,-.12);
   var pad=Instrument("02 · Wide Atmosphere","Warm Pad",-19,.12);
   var bass=Instrument("03 · Sub Bass","Sub Bass",-7);
   var arp=Instrument("04 · Pulse Arpeggio","Pluck",-16,.2);
   var lead=Instrument("05 · Main Hook","Analog Lead",-16,-.05);
   var drums=ProjectFactory.CreateTrack("06 · Drum Bus",TrackType.Audio);drums.GainDb=-1;
   var kick=Instrument("07 · Kick","Drum Kit",-1);
   var clap=Instrument("08 · Clap / Snare","Drum Kit",-12,-.04);
   var hats=Instrument("09 · Closed Hats","Drum Kit",-21,.18);
   var open=Instrument("10 · Open Hats","Drum Kit",-22,-.18);
   var perc=Instrument("11 · Percussion","Drum Kit",-24,.28);
   var fill=Instrument("12 · Build Rolls","Drum Kit",-19);
   var mid=Instrument("13 · Mid Bass","Analog Lead",-22,.04);
   var harmony=Instrument("14 · Hook Harmony","Pluck",-22,.24);
   p.Tracks.AddRange(new[]{piano,pad,bass,arp,lead,drums,kick,clap,hats,open,perc,fill,mid,harmony});
   p.Studio.Stacks.Add(new TrackStack{ParentId=drums.Id,Summing=true,Children=new List<Guid>{kick.Id,clap.Id,hats.Id,open.Id,perc.Id,fill.Id}});
   int[][] hooks={
    new[]{76,79,81,84,83,81,79,76},new[]{77,81,84,81,79,77,76,72},
    new[]{76,79,84,86,84,79,76,74},new[]{74,79,83,86,83,81,79,74}};
   for(int block=0;block<20;block++)
   {
    bool drop=(block>=6&&block<10)||(block>=14&&block<18);
    bool build=(block>=4&&block<6)||(block>=12&&block<14);
    bool verse=block>=2&&block<4, intro=block<2, brk=block>=10&&block<12, outro=block>=18;
    foreach(var track in new[]{piano,pad,bass,arp,lead,kick,clap,hats,open,perc,fill,mid,harmony})
    {
     bool active=track==piano || (track==pad&&block>0&&block<19)
      || (track==bass&&(verse||drop||block==18))
      || (track==arp&&(block>=1&&block<19&&!build))
      || (track==lead&&drop)
      || (track==kick&&(verse||drop||block==1||block==18))
      || (track==clap&&(verse||drop||block==18))
      || (track==hats&&(verse||drop||build||block==1||block==18))
      || (track==open&&drop) || (track==perc&&(drop||verse))
      || (track==fill&&build) || (track==mid&&drop) || (track==harmony&&drop&&block%4>=2);
     if(!active)continue;
     var r=ProjectFactory.CreateRegion(Section(block)+" · "+track.Name.Substring(track.Name.IndexOf('·')+2),block*16,16);
     for(int bar=0;bar<4;bar++)
     {
      int absoluteBar=block*4+bar;int[] chord=ChordProgression.Pitches(chords[absoluteBar%4]);double at=bar*4;int variation=(block+bar)%4;
      if(track==piano){foreach(int pitch in chord){Note(r,at,.72,pitch,82);Note(r,at+1.5,.46,pitch,72);Note(r,at+2.5,1.1,pitch,78);}}
      else if(track==pad){foreach(int pitch in chord)Note(r,at,3.8,pitch+12,62+(variation==3?4:0));}
      else if(track==bass){int root=chord[0]-24;foreach(double off in new[]{.0,.75,1.5,2.0,2.75,3.5})Note(r,at+off,off==3.5?.3:.34,root+(off==3.5&&variation==3?7:0),off==0?100:82);}
      else if(track==mid){int root=chord[0]-12;foreach(double off in new[]{.5,1.5,2.5,3.5})Note(r,at+off,.22,root,70+variation*3);}
      else if(track==arp){for(int i=0;i<8;i++)Note(r,at+i*.5,.22,chord[(i+(bar%2))%chord.Length]+12+(i>=6?12:0),68+(i%4)*6);}
      else if(track==lead||track==harmony){int[] melody=hooks[absoluteBar%4];double[] starts={0,.5,1,1.75,2.25,2.75,3.25,3.5};double[] lengths={.42,.42,.65,.4,.4,.4,.2,.42};for(int i=0;i<8;i++){int pitch=melody[i]+(track==harmony?12:0);Note(r,at+starts[i],lengths[i],Math.Min(120,pitch),(track==harmony?57:86)+(i%3)*4);}}
      else if(track==kick){for(int i=0;i<4;i++)if(!intro||i%2==0)Note(r,at+i,.16,36,drop?115:101);}
      else if(track==clap){Note(r,at+1,.14,38,94);Note(r,at+3,.14,38,100);if(drop&&bar==3)Note(r,at+3.75,.12,38,68);}
      else if(track==hats){for(int i=0;i<8;i++)Note(r,at+i*.5,.08,42,(i%2==1?68:49)+(variation==3?5:0));}
      else if(track==open){for(int i=0;i<4;i++)Note(r,at+i+.5,.14,46,65);}
      else if(track==perc){foreach(double off in new[]{.75,1.75,2.625,3.25})Note(r,at+off,.08,51,48+variation*4);}
      else if(track==fill){int division=block%2==1&&bar>=2?4:2;for(int i=0;i<4*division;i++)Note(r,at+i/(double)division,.07,38,Math.Min(105,45+bar*10+i));}
     }
     track.Regions.Add(r);
    }
   }
   foreach(var t in new[]{piano,pad,bass,arp,lead,mid,harmony})
   {
    var duck=StockEffects.Create(t.Id,"EDM Ducker");duck.Values["Mode"]=2;duck.Values["Depth"]=t==bass?68:48;
    duck.Values["Threshold"]=-36;duck.Values["Release"]=180;duck.SidechainTrackId=kick.Id;p.Studio.Effects.Add(duck);
   }
   var compressor=StockEffects.Create(drums.Id,"Compressor");compressor.Values["Threshold"]=-18;compressor.Values["Ratio"]=2.5;p.Studio.Effects.Add(compressor);
   var width=StockEffects.Create(pad.Id,"Stereo Imager");width.Values["Width"]=125;p.Studio.Effects.Add(width);
   var saturator=StockEffects.Create(mid.Id,"Saturator");saturator.Values["Drive"]=9;saturator.Values["Mix"]=42;p.Studio.Effects.Add(saturator);
   var masterCompression=StockEffects.Create(Guid.Empty,"Compressor");masterCompression.Values["Threshold"]=-16;masterCompression.Values["Ratio"]=2;masterCompression.Values["Attack"]=8;masterCompression.Values["Release"]=120;masterCompression.Values["Makeup"]=18;p.Studio.Effects.Add(masterCompression);
   var limiter=StockEffects.Create(Guid.Empty,"Limiter");limiter.Values["Ceiling"]=-1.2;limiter.Values["Release"]=90;p.Studio.Effects.Add(limiter);
   foreach(var t in p.Tracks){int scene=0;foreach(var r in t.Regions.Take(8))p.Studio.Cells.Add(new LauncherCell{TrackId=t.Id,RegionId=r.Id,Scene=scene++});}
   p.Studio.Automation.Add(new AutomationLane{TrackId=pad.Id,Target="Volume",Points=new List<AutomationPoint>{
    new AutomationPoint{Beat=0,Value=-28},new AutomationPoint{Beat=64,Value=-20},new AutomationPoint{Beat=96,Value=-19},
    new AutomationPoint{Beat=160,Value=-23},new AutomationPoint{Beat=224,Value=-19},new AutomationPoint{Beat=320,Value=-30}}});
   ProjectValidator.Validate(p);return p;
  }

  // Original instrumental arrangement; no third-party melodies or recordings.
  public static ProjectDocument JPopShibuya()
  {
   var p=new ProjectDocument{Name="J-Pop - Shibuya",Tempo=150,MasterGainDb=-1,CycleEnabled=false};
   var keys=Instrument("01 · Piano","Soft Piano",-8,-.12);var pluck=Instrument("02 · Motion","Pluck",-14,.16);
   var pad=Instrument("03 · Atmosphere","Warm Pad",-19,.1);var lead=Instrument("04 · Melody","Analog Lead",-13,-.04);
   var bass=Instrument("05 · Bass","Sub Bass",-10);var drums=Instrument("06 · Drums","Drum Kit",-7);
   var accent=Instrument("07 · Counterline","Pluck",-17,.24);p.Tracks.AddRange(new[]{keys,pluck,pad,lead,bass,drums,accent});
   string[] chords={"Em","C","G","D"};int[][] motif={new[]{76,79,81,79,74,76,71,74},new[]{76,79,83,81,79,76,74,72},new[]{79,83,86,83,81,79,76,74},new[]{78,81,83,81,78,76,74,71}};
   for(int bar=0;bar<64;bar++){
    double baseBeat=bar*4;int[] chord=ChordProgression.Pitches(chords[bar%4]);p.Studio.Chords.Add(new ChordEvent{Beat=baseBeat,Length=4,Name=chords[bar%4]});
    bool intro=bar<8,verse=bar>=8&&bar<24,chorus=(bar>=24&&bar<40)||(bar>=48&&bar<60),bridge=bar>=40&&bar<48,outro=bar>=60;
    foreach(var track in p.Tracks){bool active=track==keys||track==pad||track==pluck&&!outro||track==lead&&(verse||chorus||bridge)||track==bass&&!intro&&!outro||track==drums&&!intro||track==accent&&chorus;if(!active)continue;
     var r=ProjectFactory.CreateRegion((intro?"Intro":verse?"Verse":chorus?"Chorus":bridge?"Bridge":"Outro")+" · "+track.Name.Substring(track.Name.IndexOf('·')+2),baseBeat,4);
     if(track==keys){foreach(int pitch in chord){Note(r,0,.65,pitch,78);Note(r,1.5,.45,pitch,72);Note(r,2.5,1.15,pitch,82);}}
     else if(track==pad){foreach(int pitch in chord)Note(r,0,3.8,pitch+12,54);}
     else if(track==pluck){for(int i=0;i<8;i++)Note(r,i*.5,.24,chord[(i+bar%3)%chord.Length]+12,54+(i%4)*6);}
     else if(track==bass){int root=chord[0]-24;foreach(double at in new[]{0.0,.75,1.5,2.5,3.25})Note(r,at,.31,root+(at==3.25&&bar%4==3?7:0),at==0?100:80);}
     else if(track==lead||track==accent){int[] notes=motif[bar%4];double[] starts={0,.5,1,1.5,2.0,2.5,3.0,3.5};for(int i=0;i<notes.Length;i++)if(!verse||i%4!=3)Note(r,starts[i],i==7?.4:.34,notes[i]+(track==accent?12:0),track==accent?58:chorus?95:78);}
     else if(track==drums){for(int i=0;i<4;i++){Note(r,i,.14,36,chorus?110:95);if(i%2==1)Note(r,i,.12,38,90);}for(int i=0;i<8;i++)Note(r,i*.5,.08,42,i%2==0?48:64);if(bar%8==7)for(int i=0;i<4;i++)Note(r,3+i*.25,.07,38,48+i*10);}
     track.Regions.Add(r);
    }
   }
   var compressor=StockEffects.Create(Guid.Empty,"Compressor");compressor.Values["Threshold"]=-16;compressor.Values["Ratio"]=2;compressor.Values["Makeup"]=13;p.Studio.Effects.Add(compressor);
   var limiter=StockEffects.Create(Guid.Empty,"Limiter");limiter.Values["Ceiling"]=-1.5;p.Studio.Effects.Add(limiter);ProjectValidator.Validate(p);return p;
  }
 }
}
