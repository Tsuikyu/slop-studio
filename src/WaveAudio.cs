using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace SlopStudio
{
    public sealed class PcmWaveInfo
    {
        public string Path { get; private set; }
        public int EncodingTag{get;internal set;}
        public int Channels { get; private set; }
        public int SampleRate { get; private set; }
        public int BitsPerSample { get; private set; }
        public long DataBytes { get; private set; }
        public long DataOffset { get; private set; }
        public int BlockAlign { get; private set; }
        public double DurationSeconds { get; private set; }

        internal PcmWaveInfo(string path, int channels, int sampleRate, int bits, long dataBytes, int byteRate, int blockAlign, long dataOffset)
        {
            Path = path;
            Channels = channels;
            SampleRate = sampleRate;
            BitsPerSample = bits;
            DataBytes = dataBytes;
            DurationSeconds = dataBytes / (double)byteRate;
            BlockAlign = blockAlign;
            DataOffset = dataOffset;
        }
    }

    public sealed class PcmWaveData
    {
        public PcmWaveInfo Info { get; private set; }
        public byte[] Samples { get; private set; }
        internal PcmWaveData(PcmWaveInfo info, byte[] samples) { Info = info; Samples = samples; }
    }

    public static class WaveAudio
    {
        private const long MaximumWaveBytes = 256L * 1024L * 1024L;
        private const uint SoundAsync = 0x00000001;
        private const uint SoundNoDefault = 0x00000002;
        private const uint SoundFilename = 0x00020000;
        private static string _playbackCopyPath;

        [DllImport("winmm.dll", EntryPoint = "PlaySoundW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool PlaySoundFile(string fileName, IntPtr module, uint flags);

        [DllImport("winmm.dll", EntryPoint = "PlaySoundW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool StopSound(IntPtr sound, IntPtr module, uint flags);

        public static PcmWaveInfo Inspect(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A WAV file path is required.", "path");
            string fullPath = System.IO.Path.GetFullPath(path);
            FileInfo file = new FileInfo(fullPath);
            if (!file.Exists) throw new FileNotFoundException("The selected audio file could not be found.", fullPath);
            if (file.Length < 44 || file.Length > MaximumWaveBytes)
                throw new InvalidDataException("PCM WAV files must be between 44 bytes and 256 MiB.");

            int channels = 0, sampleRate = 0, bits = 0, byteRate = 0, blockAlign = 0;
            long dataBytes = 0;
            long dataOffset = 0;
            bool formatFound = false;int encodingTag=1;
            using (FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (BinaryReader reader = new BinaryReader(stream, Encoding.ASCII))
            {
                if (ReadFourCc(reader) != "RIFF") throw new InvalidDataException("The file is not a RIFF/WAVE file.");
                uint riffBytes = reader.ReadUInt32();
                if (riffBytes < 4 || riffBytes > stream.Length - 8)
                    throw new InvalidDataException("The WAV RIFF length is invalid.");
                if (ReadFourCc(reader) != "WAVE") throw new InvalidDataException("The file is not a RIFF/WAVE file.");
                long riffEnd = 8L + riffBytes;
                while (stream.Position + 8 <= riffEnd)
                {
                    string chunk = ReadFourCc(reader);
                    uint size = reader.ReadUInt32();
                    long chunkEnd = stream.Position + size;
                    if (chunkEnd > riffEnd || chunkEnd > stream.Length)
                        throw new InvalidDataException("A WAV chunk extends past the end of the file.");
                    if (chunk == "fmt ")
                    {
                        if (size < 16) throw new InvalidDataException("The WAV format chunk is too short.");
                        int encoding = reader.ReadUInt16();encodingTag=encoding;
                        channels = reader.ReadUInt16();
                        sampleRate = checked((int)reader.ReadUInt32());
                        byteRate = checked((int)reader.ReadUInt32());
                        blockAlign = reader.ReadUInt16();
                        bits = reader.ReadUInt16();
                        if(encoding==0xfffe){
                            if(size<40)throw new InvalidDataException("The extensible WAV format chunk is too short.");
                            int extra=reader.ReadUInt16(),validBits=reader.ReadUInt16();uint channelMask=reader.ReadUInt32();var subtype=new Guid(reader.ReadBytes(16));
                            if(extra<22||extra>size-18||validBits<1||validBits>bits)throw new InvalidDataException("The extensible WAV format fields are inconsistent.");
                            if(subtype==new Guid("00000001-0000-0010-8000-00aa00389b71"))encoding=1;
                            else if(subtype==new Guid("00000003-0000-0010-8000-00aa00389b71")&&validBits==32)encoding=3;
                            else throw new InvalidDataException("Only PCM or 32-bit float extensible WAV subformats are supported.");
                            if(channelMask!=0){uint mask=channelMask;int speakers=0;while(mask!=0){speakers+=(int)(mask&1);mask>>=1;}if(speakers!=channels)throw new InvalidDataException("WAV channel mask does not match channel count.");}
                            encodingTag=encoding;
                        }
                        if (encoding != 1 && !(encoding==3&&bits==32)) throw new InvalidDataException("Only integer PCM or 32-bit IEEE float WAV is supported.");
                        formatFound = true;
                    }
                    else if (chunk == "data")
                    {
                        dataBytes = size;
                        dataOffset = stream.Position;
                    }
                    stream.Position = chunkEnd + (size & 1);
                }
            }
            if (!formatFound || dataBytes <= 0) throw new InvalidDataException("The WAV file is missing PCM format or audio data.");
            if (channels < 1 || channels > 2 || sampleRate < 8000 || sampleRate > 192000 ||
                (bits != 8 && bits != 16 && bits != 24 && bits != 32))
                throw new InvalidDataException("Supported audio is mono or stereo PCM at 8–192 kHz and 8, 16, 24, or 32 bits.");
            int expectedAlign = channels * bits / 8;
            if (blockAlign != expectedAlign || byteRate != sampleRate * blockAlign || dataBytes % blockAlign != 0)
                throw new InvalidDataException("The WAV PCM format fields are inconsistent.");
            PcmWaveInfo info = new PcmWaveInfo(fullPath, channels, sampleRate, bits, dataBytes, byteRate, blockAlign, dataOffset);
            if (info.DurationSeconds <= 0.0 || info.DurationSeconds > 36000.0)
                throw new InvalidDataException("The WAV duration is outside the supported range.");
            info.EncodingTag=encodingTag;return info;
        }

        public static PcmWaveData Load(string path)
        {
            PcmWaveInfo info = Inspect(path);
            if (info.DataBytes > Int32.MaxValue) throw new InvalidDataException("WAV sample data exceeds the supported buffer size.");
            byte[] data = new byte[(int)info.DataBytes];
            using (FileStream stream = new FileStream(info.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                stream.Position = info.DataOffset;
                int offset = 0;
                while (offset < data.Length)
                {
                    int count = stream.Read(data, offset, data.Length - offset);
                    if (count <= 0) throw new EndOfStreamException("The WAV audio data ended unexpectedly.");
                    offset += count;
                }
            }
            return new PcmWaveData(info, data);
        }

        public static void Play(string path)
        {
            PcmWaveInfo info = Inspect(path);
            if (!PlaySoundFile(info.Path, IntPtr.Zero, SoundAsync | SoundNoDefault | SoundFilename))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows could not start WAV playback. Check the default audio output device.");
        }

        public static string CreatePlaybackCopy(string path,double offsetSeconds,double durationSeconds,double gainDb,double pan)
        {
            if(offsetSeconds<0||durationSeconds<=0||Double.IsNaN(offsetSeconds)||Double.IsNaN(durationSeconds))throw new ArgumentOutOfRangeException("Playback range is invalid.");
            PcmWaveData source=Load(path);int frameBytes=source.Info.BlockAlign;
            long totalFrames=source.Samples.Length/frameBytes;
            long startFrame=Math.Max(0L,Math.Min(totalFrames,(long)Math.Floor(offsetSeconds*source.Info.SampleRate)));
            long frames=Math.Max(0L,Math.Min(totalFrames-startFrame,(long)Math.Ceiling(durationSeconds*source.Info.SampleRate)));
            if(frames<=0)throw new InvalidDataException("The selected audio region has no remaining source samples.");
            long outputBytes=frames*4L;if(outputBytes>MaximumWaveBytes)throw new InvalidDataException("The selected playback range exceeds the supported temporary audio size.");
            double gain=Math.Pow(10.0,Math.Max(-60.0,Math.Min(12.0,gainDb))/20.0);
            double left=pan>0?1.0-Math.Min(1.0,pan):1.0;double right=pan<0?1.0+Math.Max(-1.0,pan):1.0;
            string output=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"SlopStudio-preview-"+Guid.NewGuid().ToString("N")+".wav");
            try
            {
                using(BinaryWriter writer=new BinaryWriter(File.Create(output)))
                {
                    writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(checked((int)(36+outputBytes)));writer.Write(Encoding.ASCII.GetBytes("WAVE"));
                    writer.Write(Encoding.ASCII.GetBytes("fmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)2);
                    writer.Write(source.Info.SampleRate);writer.Write(source.Info.SampleRate*4);writer.Write((short)4);writer.Write((short)16);
                    writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(checked((int)outputBytes));
                    for(long frame=startFrame;frame<startFrame+frames;frame++)
                    {
                        int offset=checked((int)(frame*frameBytes));
                        double l=DecodeSample(source.Samples,offset,source.Info.BitsPerSample,0)*gain*left;
                        double r=DecodeSample(source.Samples,offset,source.Info.BitsPerSample,source.Info.Channels==1?0:1)*gain*right;
                        writer.Write(ToInt16(l));writer.Write(ToInt16(r));
                    }
                }
                return output;
            }
            catch{try{if(File.Exists(output))File.Delete(output);}catch{}throw;}
        }

        public static void PlayWithMix(string path,double offsetSeconds,double durationSeconds,double gainDb,double pan)
        {
            string output=CreatePlaybackCopy(path,offsetSeconds,durationSeconds,gainDb,pan);
            try
            {
                Stop();
                if(!PlaySoundFile(output,IntPtr.Zero,SoundAsync|SoundNoDefault|SoundFilename))throw new Win32Exception(Marshal.GetLastWin32Error(),"Windows could not start WAV playback. Check the default audio output device.");
                _playbackCopyPath=output;
            }
            catch{try{if(File.Exists(output))File.Delete(output);}catch{}throw;}
        }
        private static double DecodeSample(byte[] data, int frameOffset, int bits, int channel)
        {
            int offset = frameOffset + channel * (bits / 8);
            if (bits == 8) return (data[offset] - 128) / 128.0;
            if (bits == 16) return BitConverter.ToInt16(data, offset) / 32768.0;
            if (bits == 24) { int value = data[offset] | data[offset+1] << 8 | data[offset+2] << 16; if ((value & 0x800000) != 0) value |= unchecked((int)0xff000000); return value / 8388608.0; }
            return BitConverter.ToInt32(data, offset) / 2147483648.0;
        }
        private static short ToInt16(double value) { value=Math.Max(-1.0,Math.Min(0.999969482421875,value)); return (short)Math.Round(value*32768.0); }

        public static void Stop()
        {
            StopSound(IntPtr.Zero, IntPtr.Zero, 0);
            string path=_playbackCopyPath; _playbackCopyPath=null;
            if(!String.IsNullOrEmpty(path)){try{if(File.Exists(path))File.Delete(path);}catch{}}
        }

        private static string ReadFourCc(BinaryReader reader)
        {
            byte[] value = reader.ReadBytes(4);
            if (value.Length != 4) throw new EndOfStreamException("The WAV file ended unexpectedly.");
            return Encoding.ASCII.GetString(value);
        }
    }
}
