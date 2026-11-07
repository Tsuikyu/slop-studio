using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace SlopProX {
 public static class ChordProgression {
  public static int[] Pitches(string name){var match=Regex.Match(name??"",@"^([A-Ga-g])([#b]?)(maj7|m7|m|7|dim|sus2|sus4|add9)?$");if(!match.Success)throw new ArgumentException("Use chord names such as C, Am, Fmaj7, G7, Dsus4 or Bb.");int root=new[]{9,11,0,2,4,5,7}[Char.ToUpperInvariant(match.Groups[1].Value[0])-'A'];if(match.Groups[2].Value=="#")root++;if(match.Groups[2].Value=="b")root--;root=(root+12)%12;string quality=match.Groups[3].Value;int[] intervals=quality=="dim"?new[]{0,3,6}:quality=="sus2"?new[]{0,2,7}:quality=="sus4"?new[]{0,5,7}:quality=="m"?new[]{0,3,7}:quality=="m7"?new[]{0,3,7,10}:quality=="7"?new[]{0,4,7,10}:quality=="maj7"?new[]{0,4,7,11}:quality=="add9"?new[]{0,4,7,14}:new[]{0,4,7};for(int i=0;i<intervals.Length;i++)intervals[i]+=60+root;return intervals;}
  public static List<ChordEvent> Parse(string text,double beats){var result=new List<ChordEvent>();foreach(string part in (text??"").Split(new[]{'|',','},StringSplitOptions.RemoveEmptyEntries)){string name=part.Trim();Pitches(name);result.Add(new ChordEvent{Beat=result.Count*beats,Length=beats,Name=name});}if(result.Count==0||result.Count>256)throw new ArgumentException("Enter 1 to 256 chords.");return result;}
  public static Track CreateTrack(IList<ChordEvent> chords){var track=ProjectFactory.CreateTrack("Chord Progression",TrackType.Midi);track.Instrument="Classic Electric Piano";foreach(var chord in chords){var region=ProjectFactory.CreateRegion(chord.Name,chord.Beat,chord.Length);foreach(int pitch in Pitches(chord.Name))region.Notes.Add(new MidiNote{StartBeat=0,LengthBeats=Math.Max(.0625,chord.Length-.0625),Pitch=pitch,Velocity=84});track.Regions.Add(region);}return track;}
 }
}
