using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SlopProX
{
    public sealed partial class MainForm
    {
        internal void PickItemColor(Guid trackId, Guid? regionId, bool textColor)
        {
            var track = FindTrack(trackId); if (track == null) return;
            bool region = regionId.HasValue; Guid id = regionId ?? trackId;
            var existing = _project.Studio.ItemColor(id, region);
            Color current = textColor ? (existing != null && existing.HasText ? Color.FromArgb(existing.TextArgb) : Color.White)
                : (existing != null && existing.HasBackground ? Color.FromArgb(existing.BackgroundArgb) : StudioDraw.TrackColor(track, track.ColorIndex, _project.Studio));
            using (var dialog = new ColorDialog { Color = current, FullOpen = true, AnyColor = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                SetItemColor(trackId, regionId, textColor, dialog.Color);
            }
        }
        internal void SetItemColor(Guid trackId, Guid? regionId, bool textColor, Color color)
        {
            bool region = regionId.HasValue; Guid id = regionId ?? trackId;
            IList<Guid> selected = region ? _timeline.SelectedRegions : _timeline.SelectedTracks;
            var ids = selected.Contains(id) ? selected : new List<Guid> { id };
            var state = _project.Studio.Copy();
            foreach (Guid item in ids)
            {
                var entry = state.ItemColor(item, region);
                if (entry == null) { entry = new StudioItemColor { Id = item, Region = region }; state.ItemColors.Add(entry); }
                if (textColor) { entry.HasText = true; entry.TextArgb = color.ToArgb(); }
                else { entry.HasBackground = true; entry.BackgroundArgb = color.ToArgb(); }
            }
            CommitStudio(region ? "Change region color" : "Change track color", state);
        }
    }
}
