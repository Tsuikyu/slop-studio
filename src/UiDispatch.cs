using System;using System.Runtime.CompilerServices;using System.Windows.Forms;
namespace SlopProX {
 // Explicit control-bound continuation, also valid for automation without a message-loop context.
 internal struct UiDispatch:INotifyCompletion {private Control _owner;public UiDispatch(Control owner){_owner=owner;}public UiDispatch GetAwaiter(){return this;}public bool IsCompleted{get{return !_owner.InvokeRequired;}}public void GetResult(){if(_owner.IsDisposed)throw new ObjectDisposedException(_owner.Name);}public void OnCompleted(Action continuation){_owner.BeginInvoke(continuation);}}
}
