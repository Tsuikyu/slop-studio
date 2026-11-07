using System;
using System.Collections.Generic;
using System.Linq;

namespace SlopProX
{
    public static class TrackStackOperations
    {
        public static ProjectDocument Create(ProjectDocument source, IEnumerable<Guid> selected, bool summing, out Guid parentId)
        {
            var ids = new HashSet<Guid>(selected);
            var before = source.Tracks.Where(t => ids.Contains(t.Id)).ToList();
            if (before.Count < 2 || before.Count != ids.Count) throw new InvalidOperationException("Select at least two tracks.");
            if (source.Studio.Stacks.Any(s => ids.Contains(s.ParentId) || s.Children.Any(ids.Contains)))
                throw new InvalidOperationException("Nested or already grouped tracks are not supported.");
            if (source.Tracks.Count >= ProjectValidator.MaxTracks) throw new InvalidOperationException("The project has reached its track limit.");
            var result = ProjectFactory.Clone(source);
            var children = result.Tracks.Where(t => ids.Contains(t.Id)).ToList();
            int index = result.Tracks.FindIndex(t => ids.Contains(t.Id));
            result.Tracks.RemoveAll(t => ids.Contains(t.Id));
            var parent = ProjectFactory.CreateTrack(summing ? UiText.Translate("Mix Stack") : UiText.Translate("Folder Stack"), TrackType.Audio);
            parentId = parent.Id;
            result.Tracks.Insert(index, parent);
            result.Tracks.InsertRange(index + 1, children);
            result.Studio.Stacks.Add(new TrackStack { ParentId = parent.Id, Children = children.Select(t => t.Id).ToList(), Summing = summing });
            ProjectValidator.Validate(result);
            return result;
        }
    }
}
