using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SlopStudio
{
    public static class ProjectMediaRelink
    {
        // Replace all regions referring to the same source, preserving timing.
        public static ProjectDocument Replace(ProjectDocument project, string oldPath, string replacement)
        {
            if (project == null || String.IsNullOrWhiteSpace(oldPath)) throw new ArgumentException("No source media is selected.");
            string path = Path.GetFullPath(replacement); WaveAudio.Inspect(path);
            var copy = ProjectFactory.Clone(project); int count = 0;
            foreach (var track in copy.Tracks.Where(t => t.Type == TrackType.Audio))
                foreach (var region in track.Regions.Where(r => String.Equals(r.MediaPath, oldPath, StringComparison.OrdinalIgnoreCase)))
                {
                    region.MediaPath = path;
                    if (region.Name.StartsWith("[Missing] ", StringComparison.Ordinal)) region.Name = region.Name.Substring(10);
                    count++;
                }
            if (count == 0) throw new InvalidOperationException("The selected source is no longer in the project.");
            ProjectValidator.Validate(copy); return copy;
        }
    }

    public sealed partial class MainForm
    {
        internal void RelinkRegionMedia(Guid trackId, Guid regionId)
        {
            var track = FindTrack(trackId); var region = track == null ? null : FindRegion(track, regionId);
            if (track == null || track.Type != TrackType.Audio || region == null || String.IsNullOrEmpty(region.MediaPath)) return;
            using (var dialog = new OpenFileDialog { Title = UiText.Translate("Relink Audio File…"), Filter = "WAV audio|*.wav;*.wave", CheckFileExists = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    _engine.CapturePluginStates();
                    var updated = ProjectMediaRelink.Replace(_project, region.MediaPath, dialog.FileName);
                    _history.Execute(_project, new SetProjectContentCommand("Relink audio file", _project, updated));
                    RefreshState();
                }
                catch (Exception ex) { ShowError("Could not relink audio file", ex); }
            }
        }
    }
}
