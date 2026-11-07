using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace SlopProX
{
    public enum TrackType
    {
        Audio,
        Midi
    }

    public sealed class MidiNote
    {
        public Guid Id { get; set; }
        public double StartBeat { get; set; }
        public double LengthBeats { get; set; }
        public int Pitch { get; set; }
        public int Velocity { get; set; }
        public MidiNote() { Id = Guid.NewGuid(); LengthBeats = 1.0; Pitch = 60; Velocity = 100; }
    }

    public sealed class Region
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public double StartBeat { get; set; }
        public double LengthBeats { get; set; }
        public string MediaPath { get; set; }
        public double AudioOffsetSeconds { get; set; }
        public bool Muted { get; set; }
        public int Transpose { get; set; }
        public int VelocityOffset { get; set; }
        public List<MidiNote> Notes { get; set; }

        public Region()
        {
            Id = Guid.NewGuid();
            Name = "Region";
            LengthBeats = 4.0;
            MediaPath = null;
            Notes = new List<MidiNote>();
        }
    }

    public sealed class PluginSlot
    {
        public Guid Id { get; set; }
        public string Path { get; set; }
        public string ClassId { get; set; }
        public string Name { get; set; }
        public string Format { get; set; }
        public string State { get; set; }
        public bool UseAra{get;set;}public string AraState{get;set;}public AraDocumentInfo AraDocument{get;set;}
        public bool Bypassed { get; set; }
        public bool IsInstrument { get; set; }
        public PluginSlot() { Id=Guid.NewGuid(); Path=String.Empty; ClassId=String.Empty; Name="Plugin"; Format="VST3"; State=String.Empty;AraState=String.Empty; }
        public PluginSlot Copy() { return new PluginSlot { Id=Id,Path=Path,ClassId=ClassId,Name=Name,Format=Format,State=State,UseAra=UseAra,AraState=AraState,AraDocument=AraDocument,Bypassed=Bypassed,IsInstrument=IsInstrument }; }
    }

    public sealed class Track
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public TrackType Type { get; set; }
        public List<Region> Regions { get; set; }
        public double GainDb { get; set; }
        public double Pan { get; set; }
        public bool Muted { get; set; }
        public bool Solo { get; set; }
        public bool Armed { get; set; }
        public bool ExternalMidi { get; set; }
        public string Instrument { get; set; }
        public int ColorIndex { get; set; }
        public List<PluginSlot> Plugins { get; set; }

        public Track()
        {
            Id = Guid.NewGuid();
            Name = "Track";
            Type = TrackType.Audio;
            Regions = new List<Region>();
            GainDb = 0.0;
            Pan = 0.0;
            Instrument = "Classic Electric Piano";
            Plugins = new List<PluginSlot>();
        }
    }

    public sealed class ProjectDocument
    {
        public StudioState Studio{get;set;}
        public Guid Id { get; set; }
        public string Name { get; set; }
        public double Tempo { get; set; }
        public int TimeSignatureNumerator { get; set; }
        public int TimeSignatureDenominator { get; set; }
        public List<Track> Tracks { get; set; }
        public double MasterGainDb { get; set; }
        public bool CycleEnabled { get; set; }
        public double CycleStartBeat { get; set; }
        public double CycleEndBeat { get; set; }

        public ProjectDocument()
        {
            Id = Guid.NewGuid();
            Name = "Untitled Project";Studio=new StudioState();
            Tempo = 120.0;
            TimeSignatureNumerator = 4;
            TimeSignatureDenominator = 4;
            Tracks = new List<Track>();
            CycleEndBeat = 16.0;
        }
    }

    public static class ProjectFactory
    {
        public static ProjectDocument Create(string projectName, string firstTrackName)
        {
            ProjectDocument project = new ProjectDocument();
            project.Name = projectName;
            Track track = new Track();
            track.Name = firstTrackName;
            track.Type = TrackType.Audio;
            project.Tracks.Add(track);
            ProjectValidator.Validate(project);
            return project;
        }

        public static Track CreateTrack(string name, TrackType type)
        {
            Track track = new Track();
            track.Name = name;
            track.Type = type;
            return track;
        }

        public static Region CreateRegion(string name, double startBeat, double lengthBeats)
        {
            Region region = new Region();
            region.Name = name;
            region.StartBeat = startBeat;
            region.LengthBeats = lengthBeats;
            return region;
        }

        public static List<MidiNote> CloneNotes(IEnumerable<MidiNote> source)
        {
            List<MidiNote> result = new List<MidiNote>();
            if (source == null) return result;
            foreach (MidiNote note in source) { MidiNote copy = new MidiNote(); copy.Id=note.Id; copy.StartBeat=note.StartBeat; copy.LengthBeats=note.LengthBeats; copy.Pitch=note.Pitch; copy.Velocity=note.Velocity; result.Add(copy); }
            return result;
        }

        public static ProjectDocument Clone(ProjectDocument source)
        {
            ProjectDocument clone = new ProjectDocument();
            clone.Id = source.Id;clone.Studio=source.Studio.Copy();
            clone.Name = source.Name;
            clone.Tempo = source.Tempo;
            clone.TimeSignatureNumerator = source.TimeSignatureNumerator;
            clone.TimeSignatureDenominator = source.TimeSignatureDenominator;
            clone.MasterGainDb=source.MasterGainDb;clone.CycleEnabled=source.CycleEnabled;clone.CycleStartBeat=source.CycleStartBeat;clone.CycleEndBeat=source.CycleEndBeat;
            clone.Tracks = new List<Track>();
            foreach (Track sourceTrack in source.Tracks)
            {
                Track track = new Track();
                track.Id = sourceTrack.Id;
                track.Name = sourceTrack.Name;
                track.Type = sourceTrack.Type;
                track.GainDb = sourceTrack.GainDb;
                track.Pan = sourceTrack.Pan;
                track.Muted = sourceTrack.Muted;
                track.Solo = sourceTrack.Solo;
                track.Armed=sourceTrack.Armed;track.ExternalMidi=sourceTrack.ExternalMidi; track.Instrument=sourceTrack.Instrument;track.ColorIndex=sourceTrack.ColorIndex;
                foreach(PluginSlot slot in sourceTrack.Plugins)track.Plugins.Add(slot.Copy());
                track.Regions = new List<Region>();
                foreach (Region sourceRegion in sourceTrack.Regions)
                {
                    Region region = new Region();
                    region.Id = sourceRegion.Id;
                    region.Name = sourceRegion.Name;
                    region.StartBeat = sourceRegion.StartBeat;
                    region.LengthBeats = sourceRegion.LengthBeats;
                    region.MediaPath = sourceRegion.MediaPath;
                    region.AudioOffsetSeconds=sourceRegion.AudioOffsetSeconds;region.Muted=sourceRegion.Muted;region.Transpose=sourceRegion.Transpose;region.VelocityOffset=sourceRegion.VelocityOffset;
                    region.Notes = CloneNotes(sourceRegion.Notes);
                    track.Regions.Add(region);
                }
                clone.Tracks.Add(track);
            }
            return clone;
        }
    }

    public static class ProjectValidator
    {
        public const int MaxNameLength = 120;
        public const int MaxTracks = 128;
        public const int MaxRegions = 32768;
        public const double MaxBeat = 1000000.0;

        public static string NormalizeName(string value, string fieldName)
        {
            if (value == null)
                throw new InvalidDataException(fieldName + " is required.");
            string trimmed = value.Trim();
            if (trimmed.Length == 0)
                throw new InvalidDataException(fieldName + " cannot be empty.");
            if (trimmed.Length > MaxNameLength)
                throw new InvalidDataException(fieldName + " must be " + MaxNameLength.ToString(CultureInfo.InvariantCulture) + " characters or fewer.");
            for (int i = 0; i < trimmed.Length; i++)
            {
                if (char.IsControl(trimmed[i]))
                    throw new InvalidDataException(fieldName + " cannot contain control characters.");
            }
            return trimmed;
        }

        public static void Validate(ProjectDocument project)
        {
            if(project!=null){if(project.Studio==null)throw new InvalidDataException("Studio state missing.");project.Studio.Validate(project);}
            if (project == null)
                throw new InvalidDataException("Project data is missing.");
            if (project.Id == Guid.Empty)
                throw new InvalidDataException("Project identifier is invalid.");
            project.Name = NormalizeName(project.Name, "Project name");
            if (double.IsNaN(project.Tempo) || double.IsInfinity(project.Tempo) || project.Tempo < 20.0 || project.Tempo > 400.0)
                throw new InvalidDataException("Tempo must be between 20 and 400 BPM.");
            if (project.TimeSignatureNumerator < 1 || project.TimeSignatureNumerator > 32)
                throw new InvalidDataException("Time-signature numerator must be between 1 and 32.");
            if (!IsAllowedDenominator(project.TimeSignatureDenominator))
                throw new InvalidDataException("Time-signature denominator must be 1, 2, 4, 8, 16, or 32.");
            if (project.Tracks == null)
                throw new InvalidDataException("Track list is missing.");
            if (project.Tracks.Count > MaxTracks)
                throw new InvalidDataException("A project may contain at most " + MaxTracks.ToString(CultureInfo.InvariantCulture) + " tracks.");

            if(Double.IsNaN(project.MasterGainDb)||Double.IsInfinity(project.MasterGainDb)||project.MasterGainDb < -60||project.MasterGainDb>12)throw new InvalidDataException("Master gain is outside the supported range.");
            if(Double.IsNaN(project.CycleStartBeat)||Double.IsNaN(project.CycleEndBeat)||project.CycleStartBeat<0||project.CycleEndBeat<=project.CycleStartBeat||project.CycleEndBeat>MaxBeat)throw new InvalidDataException("Cycle range is invalid.");
            HashSet<Guid> identifiers = new HashSet<Guid>();
            identifiers.Add(project.Id);
            int regionCount = 0;
            foreach (Track track in project.Tracks)
            {
                if (track == null)
                    throw new InvalidDataException("Track entry is missing.");
                if (track.Id == Guid.Empty || !identifiers.Add(track.Id))
                    throw new InvalidDataException("Track identifiers must be non-empty and unique.");
                track.Name = NormalizeName(track.Name, "Track name");
                if (track.Type != TrackType.Audio && track.Type != TrackType.Midi)
                    throw new InvalidDataException("Track type is unsupported.");
                if (double.IsNaN(track.GainDb) || double.IsInfinity(track.GainDb) || track.GainDb < -60.0 || track.GainDb > 12.0)
                    throw new InvalidDataException("Track gain must be between -60 and +12 dB.");
                if (double.IsNaN(track.Pan) || double.IsInfinity(track.Pan) || track.Pan < -1.0 || track.Pan > 1.0)
                    throw new InvalidDataException("Track pan must be between -1.0 and +1.0.");
                if(track.Instrument==null||track.Instrument.Length>120)throw new InvalidDataException("Instrument preset name is invalid.");
                if(track.Plugins==null||track.Plugins.Count>16)throw new InvalidDataException("Track plugin list is invalid.");
                foreach(PluginSlot slot in track.Plugins){if(slot==null||slot.Id==Guid.Empty||!identifiers.Add(slot.Id)||slot.Path==null||slot.Path.Length>32767||String.IsNullOrWhiteSpace(slot.ClassId)||slot.ClassId.Length>1024||(slot.Format!="VST3"&&slot.Format!="CLAP")||slot.State==null||slot.State.Length>4*1024*1024)throw new InvalidDataException("Plugin slot data is invalid.");slot.Name=NormalizeName(slot.Name,"Plugin name");}
                if (track.Regions == null)
                    throw new InvalidDataException("Region list is missing for track '" + track.Name + "'.");
                regionCount += track.Regions.Count;
                if (regionCount > MaxRegions)
                    throw new InvalidDataException("A project may contain at most " + MaxRegions.ToString(CultureInfo.InvariantCulture) + " regions.");
                foreach (Region region in track.Regions)
                {
                    if (region == null)
                        throw new InvalidDataException("Region entry is missing.");
                    if (region.Id == Guid.Empty || !identifiers.Add(region.Id))
                        throw new InvalidDataException("Region identifiers must be non-empty and unique.");
                    region.Name = NormalizeName(region.Name, "Region name");
                    if(region.Transpose < -48||region.Transpose>48||region.VelocityOffset < -126||region.VelocityOffset>126)throw new InvalidDataException("Region transpose or velocity offset is out of range.");
                    if (double.IsNaN(region.StartBeat) || double.IsInfinity(region.StartBeat) || region.StartBeat < 0.0 || region.StartBeat > MaxBeat)
                        throw new InvalidDataException("Region start must be between 0 and " + MaxBeat.ToString(CultureInfo.InvariantCulture) + " quarter-note beats.");
                    if (double.IsNaN(region.LengthBeats) || double.IsInfinity(region.LengthBeats) || region.LengthBeats < 0.25 || region.LengthBeats > MaxBeat)
                        throw new InvalidDataException("Region length must be between 0.25 and " + MaxBeat.ToString(CultureInfo.InvariantCulture) + " quarter-note beats.");
                    if (region.StartBeat + region.LengthBeats > MaxBeat)
                        throw new InvalidDataException("Region end exceeds the supported timeline range.");
                    if(Double.IsNaN(region.AudioOffsetSeconds)||Double.IsInfinity(region.AudioOffsetSeconds)||region.AudioOffsetSeconds<0)throw new InvalidDataException("Audio source offset is invalid.");
                    if (region.Notes == null) throw new InvalidDataException("MIDI note list is missing for region '" + region.Name + "'.");
                    if (region.Notes.Count > 65536) throw new InvalidDataException("A region may contain at most 65536 MIDI notes.");
                    foreach (MidiNote note in region.Notes)
                    {
                        if (note == null || note.Id == Guid.Empty || !identifiers.Add(note.Id)) throw new InvalidDataException("MIDI note identifiers must be non-empty and unique.");
                        if (track.Type != TrackType.Midi) throw new InvalidDataException("MIDI notes can only be stored on a MIDI track.");
                        if (double.IsNaN(note.StartBeat) || double.IsInfinity(note.StartBeat) || note.StartBeat < 0 || note.StartBeat > region.LengthBeats || double.IsNaN(note.LengthBeats) || double.IsInfinity(note.LengthBeats) || note.LengthBeats < 1.0/9600 || note.StartBeat + note.LengthBeats > region.LengthBeats + 0.000001) throw new InvalidDataException("MIDI note timing is outside its region.");
                        if (note.Pitch < 0 || note.Pitch > 127 || note.Velocity < 1 || note.Velocity > 127) throw new InvalidDataException("MIDI pitch and velocity must be between 0 and 127.");
                    }
                    if (region.MediaPath != null)
                    {
                        if (region.MediaPath.Length > 32767)
                            throw new InvalidDataException("Audio media path exceeds the supported length.");
                        for (int i = 0; i < region.MediaPath.Length; i++)
                            if (char.IsControl(region.MediaPath[i]))
                                throw new InvalidDataException("Audio media path cannot contain control characters.");
                    }
                }
            }
        }

        private static bool IsAllowedDenominator(int value)
        {
            return value == 1 || value == 2 || value == 4 || value == 8 || value == 16 || value == 32;
        }
    }

    public static class ProjectFile
    {
        public const int FormatVersion = 1;
        public const long MaximumFileBytes = 64 * 1024 * 1024;

        private static JavaScriptSerializer CreateSerializer()
        {
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = (int)MaximumFileBytes;
            serializer.RecursionLimit = 32;
            return serializer;
        }

        public static string Serialize(ProjectDocument project)
        {
            ProjectValidator.Validate(project);
            JavaScriptSerializer serializer = CreateSerializer();
            return serializer.Serialize(ToObject(project));
        }

        public static ProjectDocument Deserialize(string json)
        {
            if (json == null)
                throw new InvalidDataException("Project text is missing.");
            if (Encoding.UTF8.GetByteCount(json) > MaximumFileBytes)
                throw new InvalidDataException("Project exceeds the 64 MiB size limit.");
            JavaScriptSerializer serializer = CreateSerializer();
            object parsed;
            try
            {
                parsed = serializer.DeserializeObject(json);
            }
            catch (Exception exception)
            {
                throw new InvalidDataException("Project JSON could not be parsed.", exception);
            }

            IDictionary<string, object> root = RequireObject(parsed, "Project root");
            string schema = RequireString(root, "schema");
            if (schema != "slopdaw-project")
                throw new InvalidDataException("Unsupported project schema: " + schema);
            int version = RequireInteger(root, "version");
            if (version < 1 || version > FormatVersion)
                throw new InvalidDataException("Unsupported .slopdaw version " + version.ToString(CultureInfo.InvariantCulture) + ".");
            ProjectDocument project = new ProjectDocument();
            project.Id = RequireGuid(root, "projectId");
            project.Name = RequireString(root, "name");
            if(root.ContainsKey("studio"))project.Studio=serializer.ConvertToType<StudioState>(root["studio"]);
            project.Tempo = RequireNumber(root, "tempo");
            project.MasterGainDb=OptionalNumber(root,"masterGainDb",0);project.CycleEnabled=OptionalBoolean(root,"cycleEnabled",false);project.CycleStartBeat=OptionalNumber(root,"cycleStartBeat",0);project.CycleEndBeat=OptionalNumber(root,"cycleEndBeat",16);
            IDictionary<string, object> timeSignature = RequireObject(RequireValue(root, "timeSignature"), "Time signature");
            project.TimeSignatureNumerator = RequireInteger(timeSignature, "numerator");
            project.TimeSignatureDenominator = RequireInteger(timeSignature, "denominator");
            IList tracks = RequireArray(RequireValue(root, "tracks"), "Track list");
            project.Tracks = new List<Track>();
            foreach (object trackValue in tracks)
            {
                IDictionary<string, object> trackObject = RequireObject(trackValue, "Track entry");
                Track track = new Track();
                track.Id = RequireGuid(trackObject, "id");
                track.Name = RequireString(trackObject, "name");
                track.GainDb = OptionalNumber(trackObject, "gainDb", 0.0);
                track.Pan = OptionalNumber(trackObject, "pan", 0.0);
                track.Muted = OptionalBoolean(trackObject, "muted", false);
                track.Solo = OptionalBoolean(trackObject, "solo", false);
                track.Armed=OptionalBoolean(trackObject,"armed",false);track.ExternalMidi=OptionalBoolean(trackObject,"externalMidi",false);track.Instrument=OptionalString(trackObject,"instrument")??"Classic Electric Piano";track.ColorIndex=(int)OptionalNumber(trackObject,"colorIndex",0);
                object pluginValues;if(trackObject.TryGetValue("plugins",out pluginValues)&&pluginValues!=null)foreach(object pluginValue in RequireArray(pluginValues,"Plugin slots")){IDictionary<string,object> v=RequireObject(pluginValue,"Plugin slot");PluginSlot slot=new PluginSlot();slot.Id=RequireGuid(v,"id");slot.Path=RequireString(v,"path");slot.ClassId=RequireString(v,"classId");slot.Name=RequireString(v,"name");slot.Format=RequireString(v,"format");slot.State=OptionalString(v,"state")??String.Empty;slot.UseAra=OptionalBoolean(v,"useAra",false);slot.AraState=OptionalString(v,"araState")??String.Empty;slot.Bypassed=OptionalBoolean(v,"bypassed",false);slot.IsInstrument=OptionalBoolean(v,"isInstrument",false);track.Plugins.Add(slot);}
                string type = RequireString(trackObject, "type");
                if (type == "audio")
                    track.Type = TrackType.Audio;
                else if (type == "midi")
                    track.Type = TrackType.Midi;
                else
                    throw new InvalidDataException("Track type '" + type + "' is unsupported.");
                IList regions = RequireArray(RequireValue(trackObject, "regions"), "Region list");
                track.Regions = new List<Region>();
                foreach (object regionValue in regions)
                {
                    IDictionary<string, object> regionObject = RequireObject(regionValue, "Region entry");
                    Region region = new Region();
                    region.Id = RequireGuid(regionObject, "id");
                    region.Name = RequireString(regionObject, "name");
                    region.StartBeat = RequireNumber(regionObject, "startBeat");
                    region.LengthBeats = RequireNumber(regionObject, "lengthBeats");
                    region.MediaPath = OptionalString(regionObject, "mediaPath");
                    region.AudioOffsetSeconds=OptionalNumber(regionObject,"audioOffsetSeconds",0);region.Muted=OptionalBoolean(regionObject,"muted",false);region.Transpose=regionObject.ContainsKey("transpose")?RequireInteger(regionObject,"transpose"):0;region.VelocityOffset=regionObject.ContainsKey("velocityOffset")?RequireInteger(regionObject,"velocityOffset"):0;
                    region.Notes = new List<MidiNote>();
                    object notesValue;
                    if (regionObject.TryGetValue("notes", out notesValue) && notesValue != null)
                    { IList notes = RequireArray(notesValue, "MIDI note list"); foreach (object noteValue in notes) { IDictionary<string, object> noteObject = RequireObject(noteValue, "MIDI note"); MidiNote note = new MidiNote(); note.Id=RequireGuid(noteObject,"id"); note.StartBeat=RequireNumber(noteObject,"startBeat"); note.LengthBeats=RequireNumber(noteObject,"lengthBeats"); note.Pitch=RequireInteger(noteObject,"pitch"); note.Velocity=RequireInteger(noteObject,"velocity"); region.Notes.Add(note); } }
                    track.Regions.Add(region);
                }
                project.Tracks.Add(track);
            }
            ProjectValidator.Validate(project);
            return project;
        }

        public static ProjectDocument Load(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A project path is required.", "path");
            string fullPath = Path.GetFullPath(path);
            FileInfo info = new FileInfo(fullPath);
            if (!info.Exists)
                throw new FileNotFoundException("The selected project file does not exist.", fullPath);
            if (info.Length > MaximumFileBytes)
                throw new InvalidDataException("Project exceeds the 64 MiB size limit.");
            string json;
            using (StreamReader reader = new StreamReader(fullPath, new UTF8Encoding(false, true), true))
                json = reader.ReadToEnd();
            ProjectDocument loaded=Deserialize(json);string projectDirectory=Path.GetDirectoryName(fullPath);foreach(Track track in loaded.Tracks)foreach(Region region in track.Regions)if(!String.IsNullOrEmpty(region.MediaPath)&&!Path.IsPathRooted(region.MediaPath))region.MediaPath=Path.GetFullPath(Path.Combine(projectDirectory,region.MediaPath));return loaded;
        }

        public static void SaveAtomic(ProjectDocument project, string path)
        {
            SaveAtomic(project, path, true);
        }

        public static void SaveAtomic(ProjectDocument project, string path, bool keepBackup)
        {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A project path is required.", "path");
            if (!String.Equals(Path.GetExtension(path), ".slopdaw", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Projects must be saved as .slopdaw.");
            string json = Serialize(project);
            Deserialize(json);
            byte[] bytes = new UTF8Encoding(false).GetBytes(json);
            if (bytes.LongLength > MaximumFileBytes)
                throw new InvalidDataException("Serialized project exceeds the 64 MiB size limit.");
            string fullPath = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(fullPath);
            if (String.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                throw new DirectoryNotFoundException("The project destination directory does not exist.");
            string temporaryPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(fullPath))
                    File.Replace(temporaryPath, fullPath, keepBackup ? fullPath + "." + Guid.NewGuid().ToString("N") + ".bak" : null);
                else
                    File.Move(temporaryPath, fullPath);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    try { File.Delete(temporaryPath); }
                    catch { }
                }
            }
        }

        public static bool Equivalent(ProjectDocument left, ProjectDocument right)
        {
            if (left == null || right == null || left.Id != right.Id ||
                left.Name != right.Name || !NumberEqual(left.Tempo,right.Tempo) ||
                left.TimeSignatureNumerator != right.TimeSignatureNumerator ||
                left.TimeSignatureDenominator != right.TimeSignatureDenominator ||
                new JavaScriptSerializer().Serialize(left.Studio)!=new JavaScriptSerializer().Serialize(right.Studio)||left.Tracks.Count != right.Tracks.Count || !NumberEqual(left.MasterGainDb,right.MasterGainDb)||left.CycleEnabled!=right.CycleEnabled||!NumberEqual(left.CycleStartBeat,right.CycleStartBeat)||!NumberEqual(left.CycleEndBeat,right.CycleEndBeat))
                return false;
            for (int i = 0; i < left.Tracks.Count; i++)
            {
                Track a = left.Tracks[i];
                Track b = right.Tracks[i];
                if (a.Id != b.Id || a.Name != b.Name || a.Type != b.Type || !NumberEqual(a.GainDb,b.GainDb) ||
                    !NumberEqual(a.Pan,b.Pan) || a.Muted != b.Muted || a.Solo != b.Solo || a.Armed!=b.Armed||a.ExternalMidi!=b.ExternalMidi||a.Instrument!=b.Instrument||a.ColorIndex!=b.ColorIndex||!PluginsEquivalent(a.Plugins,b.Plugins)||a.Regions.Count != b.Regions.Count)
                    return false;
                for (int j = 0; j < a.Regions.Count; j++)
                {
                    Region x = a.Regions[j];
                    Region y = b.Regions[j];
                    if (x.Id != y.Id || x.Name != y.Name || !NumberEqual(x.StartBeat,y.StartBeat) ||
                        !NumberEqual(x.LengthBeats,y.LengthBeats) || x.MediaPath != y.MediaPath || !NumberEqual(x.AudioOffsetSeconds,y.AudioOffsetSeconds) || x.Muted!=y.Muted||x.Transpose!=y.Transpose||x.VelocityOffset!=y.VelocityOffset|| !NotesEquivalent(x.Notes, y.Notes))
                        return false;
                }
            }
            return true;
        }

        // Framework JSON parses decimal numbers before converting them to double.
        // Accept sub-nanobeat binary rounding, while retaining exact IDs, MIDI values and plugin state.
        private static bool NumberEqual(double a,double b){return Math.Abs(a-b)<=1e-10;}

        private static bool PluginsEquivalent(IList<PluginSlot> a,IList<PluginSlot> b){if(a.Count!=b.Count)return false;for(int i=0;i<a.Count;i++)if(a[i].Id!=b[i].Id||a[i].Path!=b[i].Path||a[i].ClassId!=b[i].ClassId||a[i].State!=b[i].State||a[i].Name!=b[i].Name||a[i].Format!=b[i].Format||a[i].UseAra!=b[i].UseAra||a[i].AraState!=b[i].AraState||a[i].Bypassed!=b[i].Bypassed||a[i].IsInstrument!=b[i].IsInstrument)return false;return true;}

        private static bool NotesEquivalent(IList<MidiNote> left, IList<MidiNote> right)
        {
            if (left == null || right == null || left.Count != right.Count) return false;
            for (int i=0;i<left.Count;i++) if(left[i].Id!=right[i].Id||!NumberEqual(left[i].StartBeat,right[i].StartBeat)||!NumberEqual(left[i].LengthBeats,right[i].LengthBeats)||left[i].Pitch!=right[i].Pitch||left[i].Velocity!=right[i].Velocity)return false;
            return true;
        }

        private static object ToObject(ProjectDocument project)
        {
            Dictionary<string, object> root = new Dictionary<string, object>();
            root["studio"]=project.Studio;
            root["schema"] = "slopdaw-project";
            root["version"] = FormatVersion;
            root["projectId"] = project.Id.ToString("D");
            root["name"] = project.Name;
            root["tempo"] = project.Tempo;
            root["masterGainDb"]=project.MasterGainDb;root["cycleEnabled"]=project.CycleEnabled;root["cycleStartBeat"]=project.CycleStartBeat;root["cycleEndBeat"]=project.CycleEndBeat;
            Dictionary<string, object> signature = new Dictionary<string, object>();
            signature["numerator"] = project.TimeSignatureNumerator;
            signature["denominator"] = project.TimeSignatureDenominator;
            root["timeSignature"] = signature;
            List<object> tracks = new List<object>();
            foreach (Track track in project.Tracks)
            {
                Dictionary<string, object> trackObject = new Dictionary<string, object>();
                trackObject["id"] = track.Id.ToString("D");
                trackObject["name"] = track.Name;
                trackObject["type"] = track.Type == TrackType.Audio ? "audio" : "midi";
                trackObject["gainDb"] = track.GainDb;
                trackObject["pan"] = track.Pan;
                trackObject["muted"] = track.Muted;
                trackObject["solo"] = track.Solo;
                trackObject["armed"]=track.Armed;trackObject["externalMidi"]=track.ExternalMidi;trackObject["instrument"]=track.Instrument;trackObject["colorIndex"]=track.ColorIndex;
                List<object> plugins=new List<object>();foreach(PluginSlot slot in track.Plugins){Dictionary<string,object> v=new Dictionary<string,object>();v["id"]=slot.Id.ToString("D");v["path"]=slot.Path;v["classId"]=slot.ClassId;v["name"]=slot.Name;v["format"]=slot.Format;v["state"]=slot.State;v["useAra"]=slot.UseAra;v["araState"]=slot.AraState??String.Empty;v["bypassed"]=slot.Bypassed;v["isInstrument"]=slot.IsInstrument;plugins.Add(v);}trackObject["plugins"]=plugins;
                List<object> regions = new List<object>();
                foreach (Region region in track.Regions)
                {
                    Dictionary<string, object> regionObject = new Dictionary<string, object>();
                    regionObject["id"] = region.Id.ToString("D");
                    regionObject["name"] = region.Name;
                    regionObject["startBeat"] = region.StartBeat;
                    regionObject["lengthBeats"] = region.LengthBeats;
                    regionObject["audioOffsetSeconds"]=region.AudioOffsetSeconds;regionObject["muted"]=region.Muted;regionObject["transpose"]=region.Transpose;regionObject["velocityOffset"]=region.VelocityOffset;
                    if (!String.IsNullOrEmpty(region.MediaPath)) regionObject["mediaPath"] = region.MediaPath;
                    List<object> notes = new List<object>();
                    foreach (MidiNote note in region.Notes) { Dictionary<string, object> noteObject = new Dictionary<string, object>(); noteObject["id"]=note.Id.ToString("D"); noteObject["startBeat"]=note.StartBeat; noteObject["lengthBeats"]=note.LengthBeats; noteObject["pitch"]=note.Pitch; noteObject["velocity"]=note.Velocity; notes.Add(noteObject); }
                    regionObject["notes"] = notes;
                    regions.Add(regionObject);
                }
                trackObject["regions"] = regions;
                tracks.Add(trackObject);
            }
            root["tracks"] = tracks;
            return root;
        }

        private static object RequireValue(IDictionary<string, object> values, string key)
        {
            object value;
            if (!values.TryGetValue(key, out value) || value == null)
                throw new InvalidDataException("Required field '" + key + "' is missing.");
            return value;
        }

        private static double OptionalNumber(IDictionary<string, object> values, string key, double fallback)
        {
            object value;
            if (!values.TryGetValue(key, out value) || value == null) return fallback;
            if (value is int) return (int)value;
            if (value is long) return (long)value;
            if (value is decimal) return (double)(decimal)value;
            if (value is double) return (double)value;
            throw new InvalidDataException("Optional field '" + key + "' must be numeric when present.");
        }

        private static bool OptionalBoolean(IDictionary<string, object> values, string key, bool fallback)
        {
            object value;
            if (!values.TryGetValue(key, out value) || value == null) return fallback;
            if (!(value is bool)) throw new InvalidDataException("Optional field '" + key + "' must be boolean when present.");
            return (bool)value;
        }

        private static string OptionalString(IDictionary<string, object> values, string key)
        {
            object value;
            if (!values.TryGetValue(key, out value) || value == null)
                return null;
            string result = value as string;
            if (result == null)
                throw new InvalidDataException("Optional field '" + key + "' must be a string when present.");
            return result;
        }

        private static string RequireString(IDictionary<string, object> values, string key)
        {
            object value = RequireValue(values, key);
            string result = value as string;
            if (result == null)
                throw new InvalidDataException("Field '" + key + "' must be a string.");
            return result;
        }

        private static void RequireEqualString(IDictionary<string, object> values, string key, string expected)
        {
            string actual = RequireString(values, key);
            if (!String.Equals(actual, expected, StringComparison.Ordinal))
                throw new InvalidDataException("Field '" + key + "' has an unsupported value.");
        }

        private static int RequireInteger(IDictionary<string, object> values, string key)
        {
            double number = RequireNumber(values, key);
            if (number < Int32.MinValue || number > Int32.MaxValue || Math.Floor(number) != number)
                throw new InvalidDataException("Field '" + key + "' must be an integer.");
            return (int)number;
        }

        private static double RequireNumber(IDictionary<string, object> values, string key)
        {
            object value = RequireValue(values, key);
            if (value is bool || !(value is IConvertible))
                throw new InvalidDataException("Field '" + key + "' must be a number.");
            double number;
            try { number = Convert.ToDouble(value, CultureInfo.InvariantCulture); }
            catch (Exception exception) { throw new InvalidDataException("Field '" + key + "' must be a number.", exception); }
            if (double.IsNaN(number) || double.IsInfinity(number))
                throw new InvalidDataException("Field '" + key + "' must be finite.");
            return number;
        }

        private static Guid RequireGuid(IDictionary<string, object> values, string key)
        {
            string value = RequireString(values, key);
            Guid result;
            if (!Guid.TryParse(value, out result) || result == Guid.Empty)
                throw new InvalidDataException("Field '" + key + "' must be a non-empty UUID.");
            return result;
        }

        private static IDictionary<string, object> RequireObject(object value, string label)
        {
            IDictionary<string, object> result = value as IDictionary<string, object>;
            if (result == null)
                throw new InvalidDataException(label + " must be a JSON object.");
            return result;
        }

        private static IList RequireArray(object value, string label)
        {
            IList result = value as IList;
            if (result == null)
                throw new InvalidDataException(label + " must be a JSON array.");
            return result;
        }
    }
}
