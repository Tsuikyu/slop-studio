using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SlopProX
{
    public static class RegionMerge
    {
        // Creates new content; source media is never modified.
        public static Region Build(ProjectDocument project, Track track, IList<Region> regions, string audioPath, int sampleRate)
        {
            if (regions == null || regions.Count < 2) throw new InvalidOperationException("Select at least two regions on the same track.");
            var sorted = regions.OrderBy(r => r.StartBeat).ToList();
            double start = sorted[0].StartBeat, end = sorted.Max(r => r.StartBeat + r.LengthBeats);
            var merged = ProjectFactory.CreateRegion(sorted[0].Name, start, end - start);
            if (track.Type == TrackType.Midi)
            {
                foreach (var region in sorted)
                {
                    if (region.Muted) continue;
                    foreach (var note in region.Notes)
                    {
                        double begin = Math.Max(0, note.StartBeat), finish = Math.Min(region.LengthBeats, note.StartBeat + note.LengthBeats);
                        if (finish - begin < 1.0 / 9600) continue;
                        merged.Notes.Add(new MidiNote { StartBeat = region.StartBeat - start + begin, LengthBeats = finish - begin,
                            Pitch = Math.Max(0, Math.Min(127, note.Pitch + region.Transpose)),
                            Velocity = Math.Max(1, Math.Min(127, note.Velocity + region.VelocityOffset)) });
                    }
                }
                return merged;
            }
            if (String.IsNullOrWhiteSpace(audioPath)) throw new ArgumentException("An output WAV is required for audio joins.");
            long total = (long)Math.Ceiling((end - start) * 60 / project.Tempo * sampleRate);
            if (total <= 0 || total > (256L * 1024 * 1024 - 44) / 8) throw new InvalidOperationException("Joined audio exceeds WAV size limits.");
            var sources = new Dictionary<string, CachedAudio>(StringComparer.OrdinalIgnoreCase);
            foreach (var region in sorted)
                if (!region.Muted && !String.IsNullOrWhiteSpace(region.MediaPath) && !sources.ContainsKey(region.MediaPath))
                    sources.Add(region.MediaPath, CachedAudio.Load(region.MediaPath));
            using (var writer = new ExportWaveWriter(audioPath, sampleRate, 2, 32, true))
            {
                var block = new float[8192];
                for (long first = 0; first < total; first += 4096)
                {
                    int n = (int)Math.Min(4096, total - first);
                    Array.Clear(block, 0, block.Length);
                    foreach (var region in sorted)
                    {
                        CachedAudio media;
                        if (region.Muted || String.IsNullOrWhiteSpace(region.MediaPath) || !sources.TryGetValue(region.MediaPath, out media)) continue;
                        double regionStart = (region.StartBeat - start) * 60 / project.Tempo * sampleRate;
                        double regionEnd = (region.StartBeat + region.LengthBeats - start) * 60 / project.Tempo * sampleRate;
                        int lo = Math.Max(0, (int)Math.Ceiling(regionStart - first)), hi = Math.Min(n, (int)Math.Ceiling(regionEnd - first));
                        for (int i = lo; i < hi; i++)
                        {
                            double pos = (first + i - regionStart) * media.Rate / sampleRate + region.AudioOffsetSeconds * media.Rate;
                            long index = (long)pos;
                            if (index < 0 || index >= media.Frames) continue;
                            long next = Math.Min(media.Frames - 1, index + 1);
                            double fraction = pos - index;
                            for (int c = 0; c < 2; c++)
                            {
                                int channel = media.Channels == 1 ? 0 : c;
                                float a = media.Samples[index * media.Channels + channel], b = media.Samples[next * media.Channels + channel];
                                block[i * 2 + c] += (float)(a + (b - a) * fraction);
                            }
                        }
                    }
                    writer.Write(block, 0, n * 2);
                }
            }
            merged.MediaPath = audioPath;
            return merged;
        }
    }
}
