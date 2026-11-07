using System;

namespace SlopStudio
{
    // Maps submitted output frames to the musical position that produced them.
    // A fixed-size ring avoids allocating on the audio callback.
    public sealed class TransportHistory
    {
        private struct Span { public long Start, End; public double Beat, PerFrame; }
        private readonly Span[] _spans = new Span[16384];
        private int _head, _count;
        private double _anchor;

        public void Reset(double beat) { _head=0; _count=0; _anchor=Math.Max(0,beat); }
        public void Record(long deviceStart,int frames,double beat,double beatsPerFrame)
        {
            if(frames<=0)return;
            int index=(_head+_count)%_spans.Length;
            if(_count==_spans.Length){_head=(_head+1)%_spans.Length;index=(_head+_count-1)%_spans.Length;}
            else _count++;
            _spans[index]=new Span{Start=deviceStart,End=deviceStart+frames,Beat=beat,PerFrame=beatsPerFrame};
        }
        public double Position(long deviceFrame,int latencyFrames)
        {
            long at=deviceFrame-Math.Max(0,latencyFrames);
            for(int i=_count-1;i>=0;i--){
                var span=_spans[(_head+i)%_spans.Length];
                if(at>=span.Start)return Math.Max(0,span.Beat+(Math.Min(at,span.End)-span.Start)*span.PerFrame);
            }
            return _count==_spans.Length?_spans[_head].Beat:_anchor;
        }
    }
}
