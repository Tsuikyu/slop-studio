using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SlopProX
{
    public sealed class TrackStack
    {
        public Guid ParentId { get; set; }
        public List<Guid> Children { get; set; }
        public bool Summing { get; set; }
        public bool Collapsed { get; set; }
        public TrackStack() { Children = new List<Guid>(); }
    }

    public sealed class AuxSend
    {
        public Guid SourceId { get; set; }
        public Guid TargetId { get; set; }
        public double GainDb { get; set; }
        public bool Enabled { get; set; }
        public AuxSend() { GainDb = -12; Enabled = true; }
    }

    public static class TrackRouting
    {
        public static List<Track> Visible(ProjectDocument project)
        {
            var alive = new HashSet<Guid>(project.Tracks.Select(t => t.Id));
            var hidden = new HashSet<Guid>();
            foreach (var stack in project.Studio.Stacks)
                if (stack.Collapsed && alive.Contains(stack.ParentId))
                    foreach (Guid child in stack.Children) hidden.Add(child);
            return project.Tracks.Where(t => !hidden.Contains(t.Id)).ToList();
        }

        public static List<Tuple<Guid, Guid>> Edges(ProjectDocument project, bool includeSidechain = true)
        {
            var alive = new HashSet<Guid>(project.Tracks.Select(t => t.Id));
            var edges = new List<Tuple<Guid, Guid>>();
            foreach (var stack in project.Studio.Stacks)
                if (stack.Summing && alive.Contains(stack.ParentId))
                    foreach (Guid child in stack.Children)
                        if (alive.Contains(child)) edges.Add(Tuple.Create(child, stack.ParentId));
            foreach (var send in project.Studio.Sends)
                if (send.Enabled && alive.Contains(send.SourceId) && alive.Contains(send.TargetId))
                    edges.Add(Tuple.Create(send.SourceId, send.TargetId));
            if (includeSidechain) foreach (var effect in project.Studio.Effects)
                if (effect.Enabled && effect.SidechainTrackId.HasValue && alive.Contains(effect.SidechainTrackId.Value) && alive.Contains(effect.TrackId))
                    edges.Add(Tuple.Create(effect.SidechainTrackId.Value, effect.TrackId));
            return edges;
        }

        public static List<Track> ProcessingOrder(ProjectDocument project)
        {
            var edges = Edges(project);
            var left = new List<Track>(project.Tracks);
            var ordered = new List<Track>();
            var complete = new HashSet<Guid>();
            while (left.Count > 0)
            {
                Track next = left.FirstOrDefault(t => edges.All(e => e.Item2 != t.Id || complete.Contains(e.Item1)));
                if (next == null) throw new InvalidDataException("Routing would create a feedback loop.");
                ordered.Add(next); complete.Add(next.Id); left.Remove(next);
            }
            return ordered;
        }

        public static HashSet<Guid> SoloAudible(ProjectDocument project)
        {
            var solo = project.Tracks.Where(t => t.Solo).Select(t => t.Id).ToList();
            if (solo.Count == 0) return null;
            var edges = Edges(project, false);
            var audible = new HashSet<Guid>(solo);
            foreach (bool forward in new[] { true, false })
            {
                var seen = new HashSet<Guid>(solo);
                var pending = new Queue<Guid>(solo);
                while (pending.Count > 0)
                {
                    Guid current = pending.Dequeue();
                    foreach (var edge in edges)
                    {
                        if ((forward ? edge.Item1 : edge.Item2) != current) continue;
                        Guid other = forward ? edge.Item2 : edge.Item1;
                        if (seen.Add(other)) { audible.Add(other); pending.Enqueue(other); }
                    }
                }
            }
            return audible;
        }
    }
}
