using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace SlopStudio
{
    public static class Mp3Export
    {
        // Windows CreateProcess argument quoting; no shell is involved.
        private static string Quote(string text){var b=new StringBuilder("\"");int slash=0;foreach(char c in text){if(c=='\\'){slash++;continue;}if(c=='\"'){b.Append('\\',slash*2+1);b.Append(c);slash=0;continue;}b.Append('\\',slash);slash=0;b.Append(c);}b.Append('\\',slash*2);b.Append('"');return b.ToString();}
        public static void Render(ProjectDocument project,string target,AudioExportOptions options,CancellationToken cancel,Action<double> progress)
        {
            if(options.SampleRate>48000)throw new InvalidDataException("MP3 export supports sample rates up to 48000 Hz.");
            string full=Path.GetFullPath(target),basePath=full+"."+Guid.NewGuid().ToString("N"),wave=basePath+".wav",encoded=basePath+".mp3";
            try{
                AudioExport.Render(project,wave,new AudioExportOptions{SampleRate=options.SampleRate,Bits=24,Float=false,StartBeat=options.StartBeat,EndBeat=options.EndBeat,TailSeconds=options.TailSeconds},cancel,p=>{if(progress!=null)progress(p*.9);});
                var args=new StringBuilder("-nostdin -hide_banner -loglevel error -y -i ").Append(Quote(wave)).Append(" -c:a libmp3lame -b:a 320k -id3v2_version 3");
                var metadata=project.Studio.Metadata??new ProjectMetadata();metadata.Validate();string[] keys={"title","artist","genre","comment","website"};string[] values={metadata.Title,metadata.Artist,metadata.Genre,metadata.Comment,metadata.Website};
                for(int i=0;i<keys.Length;i++)if(!String.IsNullOrWhiteSpace(values[i]))args.Append(" -metadata ").Append(Quote(keys[i]+"="+values[i]));
                args.Append(" -f mp3 ").Append(Quote(encoded));var start=new ProcessStartInfo(VideoDecoder.FfmpegPath(),args.ToString()){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardError=true};
                try{using(var process=Process.Start(start)){var errors=process.StandardError.ReadToEndAsync();while(!process.WaitForExit(100)){if(cancel.IsCancellationRequested){process.Kill();process.WaitForExit();cancel.ThrowIfCancellationRequested();}}if(process.ExitCode!=0)throw new IOException("FFmpeg MP3 export failed: "+errors.Result.Trim());}}
                catch(System.ComponentModel.Win32Exception){throw new InvalidOperationException("MP3 export requires FFmpeg with libmp3lame. Install FFmpeg or set SLOPSTUDIO_FFMPEG.");}
                cancel.ThrowIfCancellationRequested();if(!File.Exists(encoded)||new FileInfo(encoded).Length==0)throw new IOException("FFmpeg produced no MP3 file.");
                if(File.Exists(full))File.Replace(encoded,full,null);else File.Move(encoded,full);if(progress!=null)progress(1);
            }finally{if(File.Exists(wave))File.Delete(wave);if(File.Exists(encoded))File.Delete(encoded);}
        }
    }
}
