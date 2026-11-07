using System.Drawing;
using System.Windows.Forms;

namespace SlopProX
{
    public sealed partial class MainForm
    {
        private bool _fullScreen,_previousTopMost;private FormWindowState _previousWindowState;private FormBorderStyle _previousBorder;private Rectangle _previousBounds;
        private void InitializeFullScreen(){Activated+=delegate{if(_fullScreen)TopMost=true;};Deactivate+=delegate{if(_fullScreen)TopMost=false;};}
        private void ToggleFullScreen()
        {
            if(!_fullScreen){
                var bounds=Screen.FromControl(this).Bounds;_previousWindowState=WindowState;_previousBounds=WindowState==FormWindowState.Normal?Bounds:RestoreBounds;_previousBorder=FormBorderStyle;_previousTopMost=TopMost;
                _fullScreen=true;WindowState=FormWindowState.Normal;FormBorderStyle=FormBorderStyle.None;Bounds=bounds;TopMost=true;BringToFront();
            }else{
                TopMost=_previousTopMost;FormBorderStyle=_previousBorder;WindowState=FormWindowState.Normal;Bounds=_previousBounds;_fullScreen=false;WindowState=_previousWindowState;
            }
        }
    }
}
