using System;
using System.Runtime.InteropServices;

namespace SlopProX
{
    public sealed class WinMmMidiOutput : IDisposable
    {
        private IntPtr _handle;
        private const uint Mapper=0xFFFFFFFF;
        [DllImport("winmm.dll",EntryPoint="midiOutGetNumDevs")] private static extern uint GetDeviceCount();
        [DllImport("winmm.dll",EntryPoint="midiOutOpen")] private static extern uint Open(out IntPtr handle,uint device,IntPtr callback,IntPtr instance,uint flags);
        [DllImport("winmm.dll",EntryPoint="midiOutShortMsg")] private static extern uint ShortMessage(IntPtr handle,uint message);
        [DllImport("winmm.dll",EntryPoint="midiOutReset")] private static extern uint Reset(IntPtr handle);
        [DllImport("winmm.dll",EntryPoint="midiOutClose")] private static extern uint Close(IntPtr handle);
        public static uint DeviceCount { get { return GetDeviceCount(); } }
        public static WinMmMidiOutput OpenDevice(int device){IntPtr handle;uint result=Open(out handle,unchecked((uint)device),IntPtr.Zero,IntPtr.Zero,0);if(result!=0)throw new InvalidOperationException("MIDI output could not be opened (MMRESULT "+result+").");return new WinMmMidiOutput(handle);}
        public void SendPacket(int status,int data1,int data2){Send(status,data1,data2);}
        public static WinMmMidiOutput OpenDefault()
        {
            uint count=GetDeviceCount(); if(count==0)throw new InvalidOperationException("Windows reports no MIDI output device. Connect or enable a MIDI output, then try again.");
            IntPtr handle; uint result=Open(out handle,Mapper,IntPtr.Zero,IntPtr.Zero,0);
            if(result!=0) throw new InvalidOperationException("Windows MIDI output could not be opened (MMRESULT "+result+").");
            return new WinMmMidiOutput(handle);
        }
        private WinMmMidiOutput(IntPtr handle){_handle=handle;}
        public void NoteOn(int channel,int pitch,int velocity){Send(0x90|(channel&15),pitch,velocity);}
        public void NoteOff(int channel,int pitch){Send(0x80|(channel&15),pitch,0);}
        private void Send(int status,int data1,int data2){if(_handle==IntPtr.Zero)throw new ObjectDisposedException("WinMmMidiOutput");uint message=(uint)(status|(data1<<8)|(data2<<16));uint result=ShortMessage(_handle,message);if(result!=0)throw new InvalidOperationException("Windows MIDI output rejected a message (MMRESULT "+result+").");}
        public void AllNotesOff(){if(_handle==IntPtr.Zero)return;try{for(int c=0;c<16;c++){Send(0xB0|c,120,0);Send(0xB0|c,123,0);}Reset(_handle);}catch{}}
        public void Dispose(){if(_handle==IntPtr.Zero)return;AllNotesOff();Close(_handle);_handle=IntPtr.Zero;GC.SuppressFinalize(this);}
        ~WinMmMidiOutput(){Dispose();}
    }
}
