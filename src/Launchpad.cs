using System;
using System.Linq;

namespace SlopProX
{
    // Launchpad X / Mini MK3 Programmer Mode: top-left is note 81, bottom-left 11.
    // https://userguides.novationmusic.com/hc/en-gb/articles/24001475492498-Controlling-the-Launchpad-X-surface
    public static class LaunchpadMapping
    {
        public static int PadNote(int row,int column){if(row<0||row>7||column<0||column>7)throw new ArgumentOutOfRangeException();return (8-row)*10+column+1;}
        public static LauncherCell Cell(ProjectDocument project,int status,int note,int velocity)
        {
            if(status!=0x90||velocity<=0)return null;int row=8-note/10,column=note%10-1;if(row<0||row>7||column<0||column>7||PadNote(row,column)!=note)return null;
            var tracks=TrackRouting.Visible(project);if(row>=tracks.Count)return null;var track=tracks[row];var cell=project.Studio.Cells.FirstOrDefault(c=>c.TrackId==track.Id&&c.Scene==column);
            return cell!=null&&track.Regions.Any(r=>r.Id==cell.RegionId)?cell:null;
        }
    }
    public sealed class LaunchpadController:IDisposable
    {
        private MidiInputDevice _input;private WinMmMidiOutput _output;private readonly int[] _colors=new int[64];private volatile bool _disposed;
        public event Action<int,int,int> Pad;
        public LaunchpadController(int input,int output)
        {
            for(int i=0;i<_colors.Length;i++)_colors[i]=-1;
            try{_output=WinMmMidiOutput.OpenDevice(output);_input=new MidiInputDevice(input,Receive);}catch{Dispose();throw;}
        }
        private void Receive(object sender,MidiInputMessage e){if(_disposed)return;var handler=Pad;if(handler!=null)handler(e.Status,e.Data1,e.Data2);}
        public void Update(ProjectDocument project,ArrangementEngine engine)
        {
            if(_disposed||_output==null)return;var tracks=TrackRouting.Visible(project);
            for(int row=0;row<8;row++)for(int column=0;column<8;column++){
                int color=0;if(row<tracks.Count){var t=tracks[row];var cell=project.Studio.Cells.FirstOrDefault(c=>c.TrackId==t.Id&&c.Scene==column);if(cell!=null&&t.Regions.Any(r=>r.Id==cell.RegionId)){color=45;if(engine.IsLaunched(t.Id,cell.RegionId))color=19;else if(engine.IsLaunchPending(t.Id,cell.RegionId))color=5;}}
                int index=row*8+column;if(color!=_colors[index]){_output.SendPacket(0x90,LaunchpadMapping.PadNote(row,column),color);_colors[index]=color;}
            }
        }
        public void Dispose(){if(_disposed)return;_disposed=true;if(_input!=null){_input.Dispose();_input=null;}if(_output!=null){try{for(int row=0;row<8;row++)for(int col=0;col<8;col++)_output.SendPacket(0x90,LaunchpadMapping.PadNote(row,col),0);}catch{} _output.Dispose();_output=null;}}
    }
}
