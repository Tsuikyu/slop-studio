using System;
using System.Diagnostics;
using System.IO;
namespace SlopProX {
 public static class MediaImport {
  public static bool IsAudio(string path){return Array.IndexOf(new[]{".wav",".wave",".aif",".aiff",".flac",".mp3",".ogg",".m4a",".wma"},Path.GetExtension(path).ToLowerInvariant())>=0;}
  public static string Decode(string input,string folder) {
   if(Path.GetExtension(input).Equals(".wav",StringComparison.OrdinalIgnoreCase)){WaveAudio.Inspect(input);return Path.GetFullPath(input);}
   Directory.CreateDirectory(folder);string output=Path.Combine(folder,Path.GetFileNameWithoutExtension(input)+"-"+Guid.NewGuid().ToString("N").Substring(0,8)+".wav");
   var info=new ProcessStartInfo(VideoDecoder.FfmpegPath(),"-nostdin -hide_banner -loglevel error -i \""+input+"\" -vn -ac 2 -ar 48000 -c:a pcm_s24le \""+output+"\""){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardError=true};
   try{using(var p=Process.Start(info)){var read=p.StandardError.ReadToEndAsync();if(!p.WaitForExit(60000)){p.Kill();throw new TimeoutException("Audio decoding exceeded 60 seconds.");}string error=read.Result;if(p.ExitCode!=0)throw new InvalidDataException(error.Trim());}WaveAudio.Inspect(output);return output;}
   catch(System.ComponentModel.Win32Exception){throw new InvalidOperationException("This audio format needs FFmpeg on PATH. WAV PCM/float import works without it.");}
   catch{try{File.Delete(output);}catch{}throw;}
  }
 }
}