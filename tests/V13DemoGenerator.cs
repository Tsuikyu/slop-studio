using System;
using System.IO;
using System.Linq;
using SlopStudio;

internal static class V13DemoGenerator
{
    [STAThread] private static int Main(string[] args)
    {
        if (args.Length != 2) throw new ArgumentException("Usage: V13DemoGenerator <example directory> <verification directory>");
        string examples=Path.GetFullPath(args[0]), verification=Path.GetFullPath(args[1]);
        Directory.CreateDirectory(examples); Directory.CreateDirectory(verification);
        foreach (var project in new[]{DemoProjects.MelodicHouse(),DemoProjects.JPopShibuya()})
        {
            string path=Path.Combine(examples,project.Name+".slopdaw");
            ProjectFile.SaveAtomic(project,path,false); var loaded=ProjectFile.Load(path);
            if (!ProjectFile.Equivalent(project,loaded) || loaded.Tracks.Sum(t=>t.Regions.Sum(r=>r.Notes.Count))==0)
                throw new InvalidDataException("Example did not round trip: "+project.Name);
            using (var engine=new ArrangementEngine(new AppSettings()))
            {
                float[] audio=engine.RenderOffline(loaded,48000*4,96);
                if (audio.Any(x=>Single.IsNaN(x)||Single.IsInfinity(x)) || audio.Max(x=>Math.Abs(x))<0.001)
                    throw new InvalidDataException("Example PCM is silent or invalid: "+project.Name);
                using (var wave=new ExportWaveWriter(Path.Combine(verification,project.Name+"-preview.wav"),48000,2,24,false))
                    wave.Write(audio,0,audio.Length);
            }
            Console.WriteLine("PASS {0}: {1} BPM, {2} tracks",project.Name,project.Tempo,project.Tracks.Count);
        }
        return 0;
    }
}
