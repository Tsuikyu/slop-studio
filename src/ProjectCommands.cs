using System;
using System.Collections.Generic;

namespace SlopProX
{
    public interface IProjectCommand
    {
        string Description { get; }
        void Apply(ProjectDocument project);
        void Revert(ProjectDocument project);
    }

    public sealed class CompositeCommand : IProjectCommand
    {
        private readonly List<IProjectCommand> _commands;
        public string Description { get; private set; }
        public CompositeCommand(string description, IEnumerable<IProjectCommand> commands) { Description=description;_commands=new List<IProjectCommand>(commands);if(_commands.Count==0)throw new ArgumentException("A grouped command must contain at least one edit.","commands"); }
        public void Apply(ProjectDocument project) { int done=0;try{for(;done<_commands.Count;done++)_commands[done].Apply(project);}catch{for(int i=done-1;i>=0;i--)_commands[i].Revert(project);throw;} }
        public void Revert(ProjectDocument project) { for(int i=_commands.Count-1;i>=0;i--)_commands[i].Revert(project); }
    }

    public abstract class ProjectCommand : IProjectCommand
    {
        public abstract string Description { get; }
        public abstract void Apply(ProjectDocument project);
        public abstract void Revert(ProjectDocument project);

        protected static int FindTrackIndex(ProjectDocument project, Guid trackId)
        {
            for (int i = 0; i < project.Tracks.Count; i++)
                if (project.Tracks[i].Id == trackId)
                    return i;
            throw new InvalidOperationException("The target track is no longer in the project.");
        }

        protected static Track FindTrack(ProjectDocument project, Guid trackId)
        {
            return project.Tracks[FindTrackIndex(project, trackId)];
        }

        protected static int FindRegionIndex(Track track, Guid regionId)
        {
            for (int i = 0; i < track.Regions.Count; i++)
                if (track.Regions[i].Id == regionId)
                    return i;
            throw new InvalidOperationException("The target region is no longer in the project.");
        }

        protected static Region FindRegion(ProjectDocument project, Guid trackId, Guid regionId)
        {
            Track track = FindTrack(project, trackId);
            return track.Regions[FindRegionIndex(track, regionId)];
        }
    }

    public sealed class AddTrackCommand : ProjectCommand
    {
        private readonly Track _track;
        private readonly int _index;

        public AddTrackCommand(Track track, int index)
        {
            _track = track;
            _index = index;
        }

        public override string Description { get { return "Add track"; } }

        public override void Apply(ProjectDocument project)
        {
            if (_index < 0 || _index > project.Tracks.Count)
                throw new InvalidOperationException("The track insertion point is no longer available.");
            foreach (Track existing in project.Tracks)
                if (existing.Id == _track.Id) throw new InvalidOperationException("The track is already in the project.");
            project.Tracks.Insert(_index, _track);
        }

        public override void Revert(ProjectDocument project)
        {
            int index = FindTrackIndex(project, _track.Id);
            project.Tracks.RemoveAt(index);
        }
    }

    public sealed class AddRegionCommand : ProjectCommand
    {
        private readonly Guid _trackId;
        private readonly Region _region;
        private readonly int _index;

        public AddRegionCommand(Guid trackId, Region region, int index)
        {
            _trackId = trackId;
            _region = region;
            _index = index;
        }

        public override string Description { get { return "Add region"; } }

        public override void Apply(ProjectDocument project)
        {
            Track track = FindTrack(project, _trackId);
            if (_index < 0 || _index > track.Regions.Count)
                throw new InvalidOperationException("The region insertion point is no longer available.");
            foreach (Region existing in track.Regions)
                if (existing.Id == _region.Id) throw new InvalidOperationException("The region is already on the track.");
            track.Regions.Insert(_index, _region);
        }

        public override void Revert(ProjectDocument project)
        {
            Track track = FindTrack(project, _trackId);
            track.Regions.RemoveAt(FindRegionIndex(track, _region.Id));
        }
    }

    public sealed class DeleteTrackCommand : ProjectCommand
    {
        private readonly Track _track;
        private readonly int _index;

        public DeleteTrackCommand(ProjectDocument project, Guid trackId)
        {
            _index = FindTrackIndex(project, trackId);
            _track = project.Tracks[_index];
        }

        public override string Description { get { return "Delete track"; } }

        public override void Apply(ProjectDocument project)
        {
            project.Tracks.RemoveAt(FindTrackIndex(project, _track.Id));
        }

        public override void Revert(ProjectDocument project)
        {
            project.Tracks.Insert(Math.Min(_index, project.Tracks.Count), _track);
        }
    }

    public sealed class DeleteRegionCommand : ProjectCommand
    {
        private readonly Guid _trackId;
        private readonly Region _region;
        private readonly int _index;

        public DeleteRegionCommand(ProjectDocument project, Guid trackId, Guid regionId)
        {
            _trackId = trackId;
            Track track = FindTrack(project, trackId);
            _index = FindRegionIndex(track, regionId);
            _region = track.Regions[_index];
        }

        public override string Description { get { return "Delete region"; } }

        public override void Apply(ProjectDocument project)
        {
            Track track = FindTrack(project, _trackId);
            track.Regions.RemoveAt(FindRegionIndex(track, _region.Id));
        }

        public override void Revert(ProjectDocument project)
        {
            Track track = FindTrack(project, _trackId);
            track.Regions.Insert(Math.Min(_index, track.Regions.Count), _region);
        }
    }

    public sealed class RenameTrackCommand : ProjectCommand
    {
        private readonly Guid _trackId;
        private readonly string _before;
        private readonly string _after;

        public RenameTrackCommand(Guid trackId, string before, string after)
        {
            _trackId = trackId;
            _before = ProjectValidator.NormalizeName(before, "Track name");
            _after = ProjectValidator.NormalizeName(after, "Track name");
        }

        public override string Description { get { return "Rename track"; } }

        public override void Apply(ProjectDocument project) { FindTrack(project, _trackId).Name = _after; }
        public override void Revert(ProjectDocument project) { FindTrack(project, _trackId).Name = _before; }
    }

    public sealed class RenameRegionCommand : ProjectCommand
    {
        private readonly Guid _trackId;
        private readonly Guid _regionId;
        private readonly string _before;
        private readonly string _after;

        public RenameRegionCommand(Guid trackId, Guid regionId, string before, string after)
        {
            _trackId = trackId;
            _regionId = regionId;
            _before = ProjectValidator.NormalizeName(before, "Region name");
            _after = ProjectValidator.NormalizeName(after, "Region name");
        }

        public override string Description { get { return "Rename region"; } }

        public override void Apply(ProjectDocument project) { FindRegion(project, _trackId, _regionId).Name = _after; }
        public override void Revert(ProjectDocument project) { FindRegion(project, _trackId, _regionId).Name = _before; }
    }

    public sealed class MoveRegionCommand : ProjectCommand
    {
        private readonly Guid _trackId;
        private readonly Guid _regionId;
        private readonly double _before;
        private readonly double _after;

        public MoveRegionCommand(Guid trackId, Guid regionId, double before, double after)
        {
            _trackId = trackId;
            _regionId = regionId;
            _before = before;
            _after = after;
        }

        public override string Description { get { return "Move region"; } }

        public override void Apply(ProjectDocument project) { FindRegion(project, _trackId, _regionId).StartBeat = _after; }
        public override void Revert(ProjectDocument project) { FindRegion(project, _trackId, _regionId).StartBeat = _before; }
    }

    public sealed class ResizeRegionCommand : ProjectCommand
    {
        private readonly Guid _trackId; private readonly Guid _regionId; private readonly double _before; private readonly double _after;
        public ResizeRegionCommand(Guid trackId, Guid regionId, double before, double after) { _trackId=trackId; _regionId=regionId; _before=before; _after=after; }
        public override string Description { get { return "Resize region"; } }
        public override void Apply(ProjectDocument project) { FindRegion(project,_trackId,_regionId).LengthBeats=_after; }
        public override void Revert(ProjectDocument project) { FindRegion(project,_trackId,_regionId).LengthBeats=_before; }
    }

    public sealed class SetTrackMixCommand : ProjectCommand
    {
        private readonly Guid _trackId;
        private readonly double _beforeGain;
        private readonly double _afterGain;
        private readonly double _beforePan;
        private readonly double _afterPan;
        private readonly bool _beforeMute;
        private readonly bool _afterMute;
        private readonly bool _beforeSolo;
        private readonly bool _afterSolo;

        public SetTrackMixCommand(Guid trackId, Track before, Track after)
        {
            if (before == null || after == null) throw new ArgumentNullException(before == null ? "before" : "after");
            _trackId = trackId;
            _beforeGain = before.GainDb; _afterGain = after.GainDb;
            _beforePan = before.Pan; _afterPan = after.Pan;
            _beforeMute = before.Muted; _afterMute = after.Muted;
            _beforeSolo = before.Solo; _afterSolo = after.Solo;
        }

        public override string Description { get { return "Change track mix"; } }
        public override void Apply(ProjectDocument project) { Set(FindTrack(project, _trackId), _afterGain, _afterPan, _afterMute, _afterSolo); }
        public override void Revert(ProjectDocument project) { Set(FindTrack(project, _trackId), _beforeGain, _beforePan, _beforeMute, _beforeSolo); }

        private static void Set(Track track, double gain, double pan, bool muted, bool solo)
        {
            track.GainDb = gain; track.Pan = pan; track.Muted = muted; track.Solo = solo;
        }
    }

    public sealed class SetTempoCommand : ProjectCommand
    {
        private readonly double _before;
        private readonly double _after;

        public SetTempoCommand(double before, double after)
        {
            _before = before;
            _after = after;
        }

        public override string Description { get { return "Change tempo"; } }
        public override void Apply(ProjectDocument project) { project.Tempo = _after; }
        public override void Revert(ProjectDocument project) { project.Tempo = _before; }
    }

    public sealed class SetMidiNotesCommand : ProjectCommand
    {
        private readonly Guid _trackId, _regionId;
        private readonly List<MidiNote> _before, _after;
        public SetMidiNotesCommand(ProjectDocument project, Guid trackId, Guid regionId, IList<MidiNote> notes)
        { _trackId=trackId; _regionId=regionId; _before=ProjectFactory.CloneNotes(FindRegion(project,trackId,regionId).Notes); _after=ProjectFactory.CloneNotes(notes); }
        public override string Description { get { return "Edit MIDI notes"; } }
        public override void Apply(ProjectDocument project) { FindRegion(project,_trackId,_regionId).Notes=ProjectFactory.CloneNotes(_after); }
        public override void Revert(ProjectDocument project) { FindRegion(project,_trackId,_regionId).Notes=ProjectFactory.CloneNotes(_before); }
    }

    public sealed class SetRegionOptionsCommand:ProjectCommand
    {private Guid _track,_region;private bool _bm,_am;private int _bt,_at,_bv,_av;public SetRegionOptionsCommand(Track t,Region r,bool mute,int transpose,int velocity){_track=t.Id;_region=r.Id;_bm=r.Muted;_bt=r.Transpose;_bv=r.VelocityOffset;_am=mute;_at=transpose;_av=velocity;}public override string Description{get{return "Change region playback parameters";}}public override void Apply(ProjectDocument p){Region r=FindRegion(p,_track,_region);r.Muted=_am;r.Transpose=_at;r.VelocityOffset=_av;}public override void Revert(ProjectDocument p){Region r=FindRegion(p,_track,_region);r.Muted=_bm;r.Transpose=_bt;r.VelocityOffset=_bv;}}
    public sealed class SetTrackArmCommand:ProjectCommand
    {private Guid _id;private bool _before,_after;public SetTrackArmCommand(Track t,bool after){_id=t.Id;_before=t.Armed;_after=after;}public override string Description{get{return "Record enable track";}}public override void Apply(ProjectDocument p){FindTrack(p,_id).Armed=_after;}public override void Revert(ProjectDocument p){FindTrack(p,_id).Armed=_before;}}
    public sealed class SetInstrumentCommand:ProjectCommand
    {private Guid _id;private string _before,_after;private bool _external;public SetInstrumentCommand(Track t,string after){_id=t.Id;_before=t.Instrument;_external=t.ExternalMidi;_after=after;}public override string Description{get{return "Select instrument patch";}}public override void Apply(ProjectDocument p){FindTrack(p,_id).Instrument=_after;FindTrack(p,_id).ExternalMidi=false;}public override void Revert(ProjectDocument p){FindTrack(p,_id).Instrument=_before;FindTrack(p,_id).ExternalMidi=_external;}}
    public sealed class SetPluginSlotsCommand:ProjectCommand
    {private Guid _id;private List<PluginSlot> _before,_after;private static List<PluginSlot> Copy(IEnumerable<PluginSlot> slots){List<PluginSlot> result=new List<PluginSlot>();foreach(PluginSlot slot in slots)result.Add(slot.Copy());return result;}public SetPluginSlotsCommand(Track t,IEnumerable<PluginSlot> after){_id=t.Id;_before=Copy(t.Plugins);_after=Copy(after);}public override string Description{get{return "Edit track plugins";}}public override void Apply(ProjectDocument p){FindTrack(p,_id).Plugins=Copy(_after);}public override void Revert(ProjectDocument p){FindTrack(p,_id).Plugins=Copy(_before);}}
    public sealed class SetMasterGainCommand:ProjectCommand
    {private double _before,_after;public SetMasterGainCommand(double before,double after){_before=before;_after=after;}public override string Description{get{return "Change output gain";}}public override void Apply(ProjectDocument p){p.MasterGainDb=_after;}public override void Revert(ProjectDocument p){p.MasterGainDb=_before;}}
    public sealed class SetCycleCommand:ProjectCommand
    {private bool _before,_after;public SetCycleCommand(bool before,bool after){_before=before;_after=after;}public override string Description{get{return "Toggle cycle";}}public override void Apply(ProjectDocument p){p.CycleEnabled=_after;}public override void Revert(ProjectDocument p){p.CycleEnabled=_before;}}
    public sealed class SetCycleRangeCommand:ProjectCommand
    {private readonly bool _beforeEnabled;private readonly double _beforeStart,_beforeEnd,_afterStart,_afterEnd;public SetCycleRangeCommand(ProjectDocument p,double start,double end){_beforeEnabled=p.CycleEnabled;_beforeStart=p.CycleStartBeat;_beforeEnd=p.CycleEndBeat;_afterStart=start;_afterEnd=end;}public override string Description{get{return "Set cycle range";}}public override void Apply(ProjectDocument p){p.CycleEnabled=true;p.CycleStartBeat=_afterStart;p.CycleEndBeat=_afterEnd;}public override void Revert(ProjectDocument p){p.CycleEnabled=_beforeEnabled;p.CycleStartBeat=_beforeStart;p.CycleEndBeat=_beforeEnd;}}
    public sealed class SetMeterCommand:ProjectCommand
    {private int _bn,_bd,_an,_ad;public SetMeterCommand(ProjectDocument p,int n,int d){_bn=p.TimeSignatureNumerator;_bd=p.TimeSignatureDenominator;_an=n;_ad=d;}public override string Description{get{return "Change time signature";}}public override void Apply(ProjectDocument p){p.TimeSignatureNumerator=_an;p.TimeSignatureDenominator=_ad;}public override void Revert(ProjectDocument p){p.TimeSignatureNumerator=_bn;p.TimeSignatureDenominator=_bd;}}
    public sealed class ReorderTrackCommand:ProjectCommand
    {private Guid _id;private int _before,_after;public ReorderTrackCommand(ProjectDocument p,Guid id,int after){_id=id;_before=FindTrackIndex(p,id);_after=after;}public override string Description{get{return "Reorder track";}}private void Move(ProjectDocument p,int i){Track t=FindTrack(p,_id);p.Tracks.Remove(t);p.Tracks.Insert(Math.Max(0,Math.Min(p.Tracks.Count,i)),t);}public override void Apply(ProjectDocument p){Move(p,_after);}public override void Revert(ProjectDocument p){Move(p,_before);}}
    public sealed class TrimRegionCommand:ProjectCommand
    {private Guid _track,_region;private double _bs,_bl,_bo,_as,_al,_ao;private List<MidiNote> _before,_after;public TrimRegionCommand(Track t,Region r,double start,double length,double offset){_track=t.Id;_region=r.Id;_bs=r.StartBeat;_bl=r.LengthBeats;_bo=r.AudioOffsetSeconds;_as=start;_al=length;_ao=offset;_before=ProjectFactory.CloneNotes(r.Notes);_after=new List<MidiNote>();foreach(MidiNote original in r.Notes){MidiNote n=ProjectFactory.CloneNotes(new[]{original})[0];double a=n.StartBeat-(start-r.StartBeat),b=a+n.LengthBeats;a=Math.Max(0,a);b=Math.Min(length,b);if(b-a<0.0625)continue;n.StartBeat=a;n.LengthBeats=b-a;_after.Add(n);}}public override string Description{get{return "Trim region";}}public override void Apply(ProjectDocument p){Region r=FindRegion(p,_track,_region);r.StartBeat=_as;r.LengthBeats=_al;r.AudioOffsetSeconds=_ao;r.Notes=ProjectFactory.CloneNotes(_after);}public override void Revert(ProjectDocument p){Region r=FindRegion(p,_track,_region);r.StartBeat=_bs;r.LengthBeats=_bl;r.AudioOffsetSeconds=_bo;r.Notes=ProjectFactory.CloneNotes(_before);}}

    public sealed class ProjectHistory
    {
        private readonly List<IProjectCommand> _commands = new List<IProjectCommand>();
        private readonly List<Guid> _states = new List<Guid>();
        private int _cursor;
        private Guid _savedState;
        private int _limit=200;

        public ProjectHistory()
        {
            Reset(true);
        }

        public bool CanUndo { get { return _cursor > 0; } }
        public int Limit { get{return _limit;} set{if(value<1||value>10000)throw new ArgumentOutOfRangeException("value");_limit=value;Trim();} }
        public bool CanRedo { get { return _cursor < _commands.Count; } }
        public bool IsDirty { get { return CurrentState != _savedState; } }
        public int UndoCount { get { return _cursor; } }
        public int RedoCount { get { return _commands.Count - _cursor; } }
        private Guid CurrentState { get { return _states[_cursor]; } }

        public void Execute(ProjectDocument project, IProjectCommand command)
        {
            if (project == null || command == null)
                throw new ArgumentNullException(project == null ? "project" : "command");
            ProjectValidator.Validate(project);
            command.Apply(project);
            try
            {
                ProjectValidator.Validate(project);
            }
            catch
            {
                command.Revert(project);
                throw;
            }

            if (_cursor < _commands.Count)
            {
                _commands.RemoveRange(_cursor, _commands.Count - _cursor);
                _states.RemoveRange(_cursor + 1, _states.Count - (_cursor + 1));
            }
            _commands.Add(command);
            _states.Add(Guid.NewGuid());
            _cursor++;
            Trim();
        }

        private void Trim()
        {
            while(_commands.Count>_limit&&_cursor>0){_commands.RemoveAt(0);_states.RemoveAt(0);_cursor--;}
            while(_commands.Count>_limit){_commands.RemoveAt(_commands.Count-1);_states.RemoveAt(_states.Count-1);}
        }

        public void Undo(ProjectDocument project)
        {
            if (!CanUndo)
                return;
            IProjectCommand command = _commands[_cursor - 1];
            command.Revert(project);
            ProjectValidator.Validate(project);
            _cursor--;
        }

        public void Redo(ProjectDocument project)
        {
            if (!CanRedo)
                return;
            IProjectCommand command = _commands[_cursor];
            command.Apply(project);
            ProjectValidator.Validate(project);
            _cursor++;
        }

        public void MarkSaved()
        {
            _savedState = CurrentState;
        }

        public void Reset(bool markSaved)
        {
            _commands.Clear();
            _states.Clear();
            _states.Add(Guid.NewGuid());
            _cursor = 0;
            _savedState = markSaved ? CurrentState : Guid.Empty;
        }
    }
}
