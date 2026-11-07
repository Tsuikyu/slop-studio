using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Threading;
using SlopStudio;

internal static class V12UserProjectTests
{
    private static int Main(string[] args)
    {
        if (args.Length != 3) { Console.Error.WriteLine("Usage: V12UserProjectTests flp|verify-conversion|nexus input output-directory"); return 2; }
        string root = Path.GetFullPath(args[2]); Directory.CreateDirectory(root);
        Environment.SetEnvironmentVariable("SLOPSTUDIO_PROFILE", Path.Combine(root, "profile"));
        try
        {
            if (args[0] == "flp")
            {
                var result = ProjectExchange.ImportFlp(args[1]);
                result.Project.Studio.ImportNotes = String.Join(Environment.NewLine, result.Warnings.Distinct().ToArray());
                int tracks = result.Project.Tracks.Count, regions = result.Project.Tracks.Sum(t => t.Regions.Count);
                int notes = result.Project.Tracks.Sum(t => t.Regions.Sum(r => r.Notes.Count));
                if (tracks == 0 || regions == 0 || notes == 0) throw new Exception("No supported pattern notes imported.");
                string copy = Path.Combine(root, "imported.slopdaw"); ProjectFile.SaveAtomic(result.Project, copy, false);
                var reopen = ProjectFile.Load(copy);
                if (reopen.Tracks.Count != tracks || reopen.Tracks.Sum(t => t.Regions.Sum(r => r.Notes.Count)) != notes)
                    throw new Exception("Imported project contents changed on reopen.");
                Console.WriteLine("PASS actual FLP import and save/reopen: " + tracks + " tracks, " + regions + " regions, " + notes + " notes; " + result.Warnings.Count + " warnings.");
            }
            else if (args[0] == "verify-conversion")
            {
                var p = ProjectFile.Load(args[1]); var regions = p.Tracks.SelectMany(t => t.Regions).ToArray();
                int audio = regions.Count(r => !String.IsNullOrEmpty(r.MediaPath)), missing = regions.Count(r => !String.IsNullOrEmpty(r.MediaPath) && !File.Exists(r.MediaPath));
                if (p.Tracks.Count == 0 || regions.Length == 0 || String.IsNullOrEmpty(p.Studio.ImportNotes)) throw new Exception("Converted project or its warnings are missing.");
                Console.WriteLine("PASS converted project opens: " + p.Tracks.Count + " tracks, " + regions.Length + " regions, " + audio + " audio references, " + missing + " missing; import warnings retained.");
            }
            else if (args[0] == "nexus" || args[0] == "nexus-sync" || args[0] == "nexus-noreset" || args[0] == "nexus-default")
            {
                var project = ProjectFile.Load(args[1]);
                var track = project.Tracks.First(t => t.Plugins.Any(p => p.Name.IndexOf("nexus", StringComparison.OrdinalIgnoreCase) >= 0));
                foreach (var t in project.Tracks) { t.Muted = t.Id != track.Id; t.Solo = false; }
                var slot = track.Plugins.First(p => p.Name.IndexOf("nexus", StringComparison.OrdinalIgnoreCase) >= 0);
                if(args[0]=="nexus-default")slot.State=null;
                using (var engine = new ArrangementEngine(new AppSettings { SampleRate = 48000, BufferFrames = 512 }))
                {
                    engine.PrepareOffline(project, 0);
                    var plugin = engine.GetPlugin(slot.Id);
                    if (plugin == null) throw new Exception("Nexus could not be instantiated: " + String.Join("; ", engine.Warnings.ToArray()));
                    object targetScene = ((IEnumerable)typeof(ArrangementEngine).GetField("_scenes", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(engine)).Cast<object>().First(s => ((Track)s.GetType().GetField("Track").GetValue(s)).Id == track.Id);
                    if(args[0]=="nexus-noreset")targetScene.GetType().GetField("ResetNotes").SetValue(targetScene,false);
                    bool realtime = args[0] == "nexus"; if (realtime) plugin.EnableRealtime(512);
                    int blocks = 3750; var block = new float[1024]; var watch = Stopwatch.StartNew();
                    int nonzero = 0, afterLoop = 0; float maximum = 0, secondPeak = 0; int secondNonzero = 0, healthySeconds = 0;
                    for (int i = 0; i < blocks; i++)
                    {
                        engine.Render(block, 512, 0);
                        float peak = block.Max(x => Math.Abs(x)); maximum = Math.Max(maximum, peak); secondPeak = Math.Max(secondPeak, peak); if (peak > 0.00001f) { nonzero++; secondNonzero++; if(i>=3400)afterLoop++; }
                        if (i % 94 == 93) { if (plugin.Healthy) healthySeconds++; Console.WriteLine("second=" + i / 94 + " healthy=" + plugin.Healthy + " peak=" + secondPeak.ToString("0.0000") + " blocks=" + secondNonzero + " missed=" + plugin.RealtimeMissedBlocks + " warnings=" + engine.Warnings.Count); secondPeak=0; secondNonzero=0; }
                        long target = (long)((i + 1) * 512.0 * 1000 / 48000);
                        int sleep = (int)(target - watch.ElapsedMilliseconds); if (realtime && sleep > 0) Thread.Sleep(sleep);
                    }
                    Console.WriteLine("RESULT healthy=" + plugin.Healthy + " nonzeroBlocks=" + nonzero + "/" + blocks + " afterLoop=" + afterLoop + " peak=" + maximum + " missed=" + plugin.RealtimeMissedBlocks + " warnings=" + engine.Warnings.Count + " checked=" + healthySeconds);
                    if (!plugin.Healthy || nonzero < blocks * .70 || afterLoop < 150) throw new Exception("Nexus lost sustained audio or failed after the project loop: " + plugin.Failure);
                }
            }
            else throw new ArgumentException("Unknown mode");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine("FAIL " + ex); return 1; }
    }
}
