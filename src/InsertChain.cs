using System;
using System.Collections.Generic;
using System.Linq;

namespace SlopProX
{
    // One persisted ordering is shared by the mixer and the audio graph.
    public sealed class TrackInsertOrder
    {
        public Guid TrackId { get; set; }
        public List<Guid> Items { get; set; }
        public TrackInsertOrder() { Items = new List<Guid>(); }
    }

    public sealed class InsertEntry
    {
        public PluginSlot Plugin;
        public StockEffectSlot Stock;
        public Guid Id { get { return Plugin != null ? Plugin.Id : Stock.Id; } }
        public string Name { get { return Plugin != null ? Plugin.Name : "Slop " + Stock.Kind; } }
        public bool Bypassed { get { return Plugin != null ? Plugin.Bypassed : !Stock.Enabled; } }
    }

    public static class InsertChain
    {
        public static List<InsertEntry> Entries(ProjectDocument project, Track track)
        {
            return Entries(project, track == null ? Guid.Empty : track.Id);
        }

        public static List<InsertEntry> Entries(ProjectDocument project, Guid targetId)
        {
            var available = new Dictionary<Guid, InsertEntry>();
            var fallback = new List<InsertEntry>();
            Track owner = targetId == Guid.Empty ? null : project.Tracks.Find(t => t.Id == targetId);
            IEnumerable<PluginSlot> plugins = targetId == Guid.Empty
                ? (project.Studio.MasterPlugins ?? new List<PluginSlot>())
                : owner == null ? new List<PluginSlot>() : owner.Plugins;
            foreach (var plugin in plugins)
                if (!plugin.IsInstrument) fallback.Add(new InsertEntry { Plugin = plugin });
            foreach (var effect in project.Studio.Effects)
                if (effect.TrackId == targetId) fallback.Add(new InsertEntry { Stock = effect });
            foreach (var entry in fallback) available[entry.Id] = entry;
            var result = new List<InsertEntry>();
            var order = project.Studio.InsertOrders.Find(o => o.TrackId == targetId);
            if (order != null && order.Items != null)
                foreach (Guid id in order.Items)
                {
                    InsertEntry entry;
                    if (available.TryGetValue(id, out entry)) { result.Add(entry); available.Remove(id); }
                }
            foreach (var entry in fallback)
                if (available.Remove(entry.Id)) result.Add(entry);
            return result;
        }

        public static void Set(StudioState studio, Guid trackId, IEnumerable<Guid> ids)
        {
            studio.InsertOrders.RemoveAll(o => o.TrackId == trackId);
            studio.InsertOrders.Add(new TrackInsertOrder { TrackId = trackId, Items = ids.Distinct().ToList() });
        }
    }
}
