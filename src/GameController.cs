using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace SlopProX {
 [StructLayout(LayoutKind.Sequential)]public struct GameControllerState {public int Kind,Index;public uint Buttons;public float X,Y,RightX,RightY,LeftTrigger,RightTrigger;}
 public sealed class GameController:IDisposable {
  [DllImport("SlopControl.dll",CallingConvention=CallingConvention.Cdecl)]private static extern int slc_controller(IntPtr window,out GameControllerState state);
  [DllImport("SlopControl.dll",CallingConvention=CallingConvention.Cdecl)]private static extern int slc_controller_mode(IntPtr window,int preferred,out GameControllerState state);public int PreferredKind;
  [DllImport("SlopControl.dll",CallingConvention=CallingConvention.Cdecl)]private static extern void slc_controller_close();
  private uint _last;private DateTime _next;public string[] Bindings=GameControllerBindings.Default();
  public string Connected{get;private set;}
  public void Poll(Form owner,Action<AppCommand> command,Action<float,float,float> camera){
   if(Form.ActiveForm!=owner){_last=0;return;}GameControllerState state;if(slc_controller_mode(owner.Handle,PreferredKind,out state)==0){Connected=null;_last=0;return;}Connected=state.Kind==1?"XInput":"DirectInput";Apply(state,command,camera);
  }
  public void Apply(GameControllerState state,Action<AppCommand> command,Action<float,float,float> camera){
   uint down=state.Buttons&~_last;_last=state.Buttons;
   bool repeat=DateTime.UtcNow>=_next;if(repeat)_next=DateTime.UtcNow.AddMilliseconds(120);
   for(int i=0;i<Math.Min(14,Bindings.Length);i++){AppCommand mapped;if(!GameControllerBindings.TryCommand(Bindings[i],out mapped))continue;bool navigates=mapped==AppCommand.RewindBar||mapped==AppCommand.ForwardBar||mapped==AppCommand.SelectPreviousTrack||mapped==AppCommand.SelectNextTrack;if((down&(1u<<i))!=0||i>=8&&i<=11&&navigates&&repeat&&(state.Buttons&(1u<<i))!=0)command(mapped);}
   if(repeat){float x=Math.Abs(state.X)>.3?state.X:0,y=Math.Abs(state.Y)>.3?state.Y:0,z=Math.Abs(state.RightY)>.35?state.RightY:0;if(x!=0||y!=0||z!=0)camera(x,y,z);}
  }
  public void Dispose(){slc_controller_close();}
 }
}
