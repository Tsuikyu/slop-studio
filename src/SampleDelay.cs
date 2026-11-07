using System;
namespace SlopProX {
 internal sealed class SampleDelay {
  private float[] _history=new float[4096];private long _written;private int _delay,_previous,_fade;
  public void SetDelay(int samples){samples=Math.Max(0,samples);if(samples==_delay)return;int capacity=_history.Length/2;if(samples+2>capacity){int size=capacity;while(size<samples+2)size*=2;var larger=new float[size*2];for(long i=Math.Max(0,_written-capacity);i<_written;i++){larger[(i%size)*2]=_history[(i%capacity)*2];larger[(i%size)*2+1]=_history[(i%capacity)*2+1];}_history=larger;}_previous=_delay;_delay=samples;_fade=_written==0?0:128;}
  public void Process(ref float left,ref float right){int capacity=_history.Length/2,index=(int)(_written%capacity)*2;_history[index]=left;_history[index+1]=right;long next=_written-_delay;float l=next<0?0:_history[(next%capacity)*2],r=next<0?0:_history[(next%capacity)*2+1];if(_fade>0){long old=_written-_previous;float oldL=old<0?0:_history[(old%capacity)*2],oldR=old<0?0:_history[(old%capacity)*2+1];float blend=_fade/128f;l=l*(1-blend)+oldL*blend;r=r*(1-blend)+oldR*blend;_fade--;}_written++;left=l;right=r;}
 }
}