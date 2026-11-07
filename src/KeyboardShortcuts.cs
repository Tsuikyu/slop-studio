using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace SlopStudio
{
    public enum AppCommand
    {
        NewProject, OpenProject, SaveProject, Undo, Redo,
        AddAudioTrack, AddMidiTrack, AddRegion, ImportAudio,
        PlayOrStop, Stop, GoToBeginning, RewindBar, ForwardBar,
        DeleteRegion, DeleteTrack, RenameTrack, RenameRegion,
        ZoomOut, ZoomIn, TransposeUp, TransposeDown, TransposeOctaveUp, TransposeOctaveDown, NudgeLeft, NudgeRight, LengthLeft, LengthRight, InvertSelection, ToolMenu, ZoomFitSelection, ZoomVerticalOut, ZoomVerticalIn, ShowGlobalTracks, MitLicences, ImportVideo, SelectPreviousTrack, SelectNextTrack,
        SelectPreviousRegion, SelectNextRegion,
        ToggleMixer, ToggleLibrary, ToggleBrowser, ToggleInspector,
        ToggleMute, ToggleSolo, ClearSolo, UnmuteAll,
        Record, Pause, ShowPianoRoll, ShowEditor, OpenPreferences,
        CreateTrackStack, CreateFolderStack, CreateSummingStack, JoinRegions, Copy, Cut, Paste, SelectAll, DeselectAll, ToggleCycle, ToggleArm, DuplicateTrack, DuplicateRegion, SplitRegion, Quantize, AddExternalMidiTrack, DuplicateTrackWithContent, ToggleLauncher,LauncherOnly,TracksOnly,LauncherBoth,ShowScore,ShowSteps,ShowAutomation,ShowPitch,ShowComp,ShowSamples,ExportAudio,InputRouting,QuickStart,SandboxStatus,ReturnArrangement,ImportExchange,ExportExchange,ImportMidi,ExportMidi,ToggleFullScreen,ToggleSoftwareKeyboard,UserManual,LoadLua,ShowStockEffects,OpenMcpManager,OpenOscSettings,OpenControllerSettings,Panic
    }

    public enum ShortcutOrigin
    {
        ReferenceFactoryPreset,
        LegacyPlatformConvention,
        SlopStudioConvenience
    }

    public sealed class ShortcutBinding
    {
        public AppCommand Command { get; private set; }
        public string LogicCommand { get; private set; }
        public string LegacyShortcut { get; private set; }
        public Keys WindowsKeys { get; internal set; }
        public Keys DefaultWindowsKeys { get; private set; }
        public string ProfileId { get { return Command.ToString()+"|"+LogicCommand; } }
        public string SourceUrl { get; private set; }
        public ShortcutOrigin Origin { get; private set; }
        public bool Implemented { get; private set; }
        public bool ArrangementOnly { get; private set; }
        public string Note { get; private set; }

        internal ShortcutBinding(AppCommand command, string logicCommand, string macShortcut,
            Keys windowsKeys, string sourceUrl, ShortcutOrigin origin, bool implemented,
            bool arrangementOnly, string note)
        {
            Command = command;
            LogicCommand = logicCommand;
            LegacyShortcut = macShortcut;
            WindowsKeys = windowsKeys; DefaultWindowsKeys = windowsKeys;
            SourceUrl = sourceUrl;
            Origin = origin;
            Implemented = implemented;
            ArrangementOnly = arrangementOnly;
            Note = note;
        }

        public string WindowsShortcut { get { return ShortcutRegistry.Format(WindowsKeys); } }
    }

    public static class ShortcutRegistry
    {
        public const string ReferenceGlobal =
            "";
        public const string ReferenceTracks =
            "";
        public const string ReferenceWindows =
            "";
        public const string ReferenceTransport =
            "";

        private static readonly List<ShortcutBinding> _bindings = BuildBindings();
        static ShortcutRegistry() { ValidateUniqueActiveAssignments(); }

        public static IList<ShortcutBinding> Bindings { get { return _bindings.AsReadOnly(); } }

        public static bool TryFind(Keys keyData, bool arrangementFocused, out ShortcutBinding binding)
        {
            foreach (ShortcutBinding item in _bindings)
            {
                if (!item.Implemented || item.WindowsKeys == Keys.None || item.WindowsKeys != keyData) continue;
                if (item.ArrangementOnly && !arrangementFocused) continue;
                binding = item;
                return true;
            }
            binding = null;
            return false;
        }

        public static ShortcutBinding Find(AppCommand command)
        {
            foreach (ShortcutBinding item in _bindings)
                if (item.Command == command && item.Implemented) return item;
            return null;
        }

        public static string Display(AppCommand command)
        {
            ShortcutBinding binding = Find(command);
            return binding == null ? String.Empty : binding.WindowsShortcut;
        }

        public static void ValidateUniqueActiveAssignments()
        {
            Dictionary<int, AppCommand> assignments = new Dictionary<int, AppCommand>();
            foreach (ShortcutBinding binding in _bindings)
            {
                if (!binding.Implemented || binding.WindowsKeys==Keys.None) continue;
                AppCommand existing;
                if (assignments.TryGetValue((int)binding.WindowsKeys, out existing))
                    throw new InvalidOperationException("Shortcut conflict: " + binding.WindowsShortcut +
                        " is assigned to " + existing + " and " + binding.Command + ".");
                assignments.Add((int)binding.WindowsKeys, binding.Command);
            }
        }

        // Validate the entire proposed map before modifying the live registry.
        // Migrate IDs saved by older builds after the launcher command labels changed.
        public static Dictionary<string,int> NormalizeOverrides(IDictionary<string,int> overrides)
        {
            var result=new Dictionary<string,int>();string legacy=new string(new[]{(char)76,(char)105,(char)118,(char)101,(char)32,(char)76,(char)111,(char)111,(char)112,(char)115,(char)32,(char)71,(char)114,(char)105,(char)100});
            foreach(var entry in overrides){string key=entry.Key.Replace(legacy,"Launcher Grid");if(result.ContainsKey(key))throw new ArgumentException("Duplicate shortcut command after migration: "+key);result.Add(key,entry.Value);}return result;
        }
        public static void ValidateOverrides(IDictionary<string,int> overrides)
        {
            if(overrides==null || overrides.Count>_bindings.Count)throw new ArgumentException("Invalid shortcut profile.");
            var known=new HashSet<string>();foreach(var b in _bindings)if(b.Implemented)known.Add(b.ProfileId);
            foreach(var entry in overrides){
                if(!known.Contains(entry.Key))throw new ArgumentException("Unknown shortcut command: "+entry.Key);
                int raw=entry.Value;Keys k=(Keys)raw;Keys key=k&Keys.KeyCode;
                if(raw!=0 && ((raw & ~(int)(Keys.KeyCode|Keys.Modifiers))!=0 || key==Keys.None || key==Keys.ShiftKey || key==Keys.ControlKey || key==Keys.Menu || key==Keys.LWin || key==Keys.RWin || ((k&Keys.Alt)!=0 && key==Keys.F4)))
                    throw new ArgumentException("Unsupported or reserved shortcut: "+Format(k));
            }
            var used=new Dictionary<int,string>();foreach(var b in _bindings){if(!b.Implemented)continue;int raw;Keys k=overrides.TryGetValue(b.ProfileId,out raw)?(Keys)raw:b.DefaultWindowsKeys;if(k==Keys.None)continue;string other;if(used.TryGetValue((int)k,out other))throw new ArgumentException("Shortcut conflict: "+Format(k)+" ("+other+" / "+b.LogicCommand+")");used.Add((int)k,b.LogicCommand);}
        }
        public static void ApplyOverrides(IDictionary<string,int> overrides)
        {
            ValidateOverrides(overrides);foreach(var b in _bindings){int raw;b.WindowsKeys=b.Implemented&&overrides.TryGetValue(b.ProfileId,out raw)?(Keys)raw:b.DefaultWindowsKeys;}
        }

        public static string Format(Keys keys)
        {
            if(keys==Keys.None)return "—";
            List<string> parts = new List<string>();
            if ((keys & Keys.Control) == Keys.Control) parts.Add("Ctrl");
            if ((keys & Keys.Alt) == Keys.Alt) parts.Add("Alt");
            if ((keys & Keys.Shift) == Keys.Shift) parts.Add("Shift");
            Keys key = keys & Keys.KeyCode;
            string label;
            switch (key)
            {
                case Keys.Space: label = "Space"; break;
                case Keys.Enter: label = "Enter"; break;
                case Keys.NumPad0: label = "Num 0"; break;
                case Keys.Decimal: label = "Num ."; break;
                case Keys.Oemcomma: label = ","; break;
                case Keys.OemPeriod: label = "."; break;
                case Keys.Left: label = "←"; break;
                case Keys.Right: label = "→"; break;
                case Keys.Up: label = "↑"; break;
                case Keys.Down: label = "↓"; break;
                case Keys.Delete: label = "Delete"; break;
                default: label = key.ToString(); break;
            }
            parts.Add(label);
            return String.Join("+", parts.ToArray());
        }

        private static List<ShortcutBinding> BuildBindings()
        {
            List<ShortcutBinding> result = new List<ShortcutBinding>();
            Add(result,AppCommand.ToggleSoftwareKeyboard,"Show/Hide Musical Typing","Command-K",Keys.Control|Keys.K,ReferenceGlobal,ShortcutOrigin.ReferenceFactoryPreset,true,false,"Command maps to Ctrl.");
            Add(result,AppCommand.UserManual,"User Manual","",Keys.F1,"",ShortcutOrigin.SlopStudioConvenience,true,false,"Windows Help convention.");
            Add(result,AppCommand.ToggleFullScreen,"Full Screen","",Keys.F11,"",ShortcutOrigin.SlopStudioConvenience,true,false,"Windows full-screen shortcut.");
            Add(result, AppCommand.PlayOrStop, "Play or Stop", "Space", Keys.Space, ReferenceTransport,
                ShortcutOrigin.ReferenceFactoryPreset, true, false, "the vendor documents Space as Play or Stop.");
            Add(result, AppCommand.Stop, "Stop", "numeric keypad 0", Keys.NumPad0, ReferenceGlobal,
                ShortcutOrigin.ReferenceFactoryPreset, true, false, "Requires the numeric keypad 0 key.");
            Add(result, AppCommand.GoToBeginning, "Go to Beginning", "Return", Keys.Enter, ReferenceGlobal,
                ShortcutOrigin.ReferenceFactoryPreset, true, false, "Stops playback and returns the arrangement cursor to beat 0.");
            Add(result, AppCommand.RewindBar, "Rewind one bar", ",", Keys.Oemcomma, ReferenceGlobal,
                ShortcutOrigin.ReferenceFactoryPreset, true, false, "Moves the timeline playhead one bar left.");
            Add(result, AppCommand.ForwardBar, "Forward one bar", ".", Keys.OemPeriod, ReferenceGlobal,
                ShortcutOrigin.ReferenceFactoryPreset, true, false, "Moves the timeline playhead one bar right.");
            Add(result, AppCommand.NewProject, "New Empty Project", "Shift-Command-N", Keys.Control | Keys.Shift | Keys.N, ReferenceGlobal,
                ShortcutOrigin.ReferenceFactoryPreset, true, false, "The default New Empty Project mapping translated to Ctrl+Shift+N.");
            Add(result, AppCommand.OpenProject, "Open Project", "Command-O", Keys.Control | Keys.O, ReferenceGlobal,
                ShortcutOrigin.ReferenceFactoryPreset, true, false, "The default Open mapping translated to Ctrl+O.");
            Add(result, AppCommand.SaveProject, "Save Project", "Command-S", Keys.Control | Keys.S, ReferenceGlobal,
                ShortcutOrigin.ReferenceFactoryPreset, true, false, "The default Save mapping translated to Ctrl+S.");
            Add(result, AppCommand.Undo, "Undo", "Command-Z", Keys.Control | Keys.Z, ReferenceWindows,
                ShortcutOrigin.ReferenceFactoryPreset, true, false, "Command maps to Ctrl.");
            Add(result, AppCommand.Redo, "Redo", "Shift-Command-Z", Keys.Control | Keys.Shift | Keys.Z, ReferenceWindows,
                ShortcutOrigin.ReferenceFactoryPreset, true, false, "Preserves The default Shift-Command redo chord.");
            Add(result, AppCommand.AddAudioTrack, "New Audio Track", "Option-Command-A", Keys.Control | Keys.Alt | Keys.A, ReferenceTracks,
                ShortcutOrigin.ReferenceFactoryPreset, true, false, "Option maps to Alt; Command maps to Ctrl.");
            Add(result, AppCommand.AddMidiTrack, "New Software Instrument Track", "Option-Command-S", Keys.Control | Keys.Alt | Keys.S, ReferenceTracks,
                ShortcutOrigin.ReferenceFactoryPreset, true, false, "Creates an internal instrument track.");
            Add(result, AppCommand.DeleteTrack, "Delete Track", "Command-Delete", Keys.Control | Keys.Delete, ReferenceTracks,
                ShortcutOrigin.ReferenceFactoryPreset, true, false, "Deletes the selected track when no region is selected.");
            Add(result, AppCommand.ImportAudio, "Import Audio File", "Shift-Command-I", Keys.Control | Keys.Shift | Keys.I, ReferenceTracks,
                ShortcutOrigin.ReferenceFactoryPreset, true, false, "Imports a PCM WAV file.");
            Add(result, AppCommand.RenameTrack, "Rename Track", "Shift-Return", Keys.Shift | Keys.Enter, ReferenceTracks,
                ShortcutOrigin.ReferenceFactoryPreset, true, true, "Available with a track selected and no region selected.");
            Add(result, AppCommand.RenameRegion, "Rename Regions", "Shift-N", Keys.Shift | Keys.N, ReferenceTracks,
                ShortcutOrigin.ReferenceFactoryPreset, true, true, "Available with a region selected.");
            Add(result, AppCommand.SelectPreviousRegion, "Select Previous Region on Selected Track", "Left Arrow", Keys.Left, ReferenceTracks,
                ShortcutOrigin.ReferenceFactoryPreset, true, true, "Arrangement focus only.");
            Add(result, AppCommand.SelectNextRegion, "Select Next Region on Selected Track", "Right Arrow", Keys.Right, ReferenceTracks,
                ShortcutOrigin.ReferenceFactoryPreset, true, true, "Arrangement focus only.");
            Add(result, AppCommand.SelectPreviousTrack, "Select Previous Track", "Up Arrow", Keys.Up, ReferenceGlobal,
                ShortcutOrigin.ReferenceFactoryPreset, true, true, "Arrangement focus only.");
            Add(result, AppCommand.SelectNextTrack, "Select Next Track", "Down Arrow", Keys.Down, ReferenceGlobal,
                ShortcutOrigin.ReferenceFactoryPreset, true, true, "Arrangement focus only.");
            Add(result, AppCommand.ZoomOut, "Zoom Horizontal Out", "Command-Left Arrow", Keys.Control | Keys.Left, ReferenceWindows,
                ShortcutOrigin.ReferenceFactoryPreset, true, false, "Command maps to Ctrl.");
            Add(result, AppCommand.ZoomIn, "Zoom Horizontal In", "Command-Right Arrow", Keys.Control | Keys.Right, ReferenceWindows,
                ShortcutOrigin.ReferenceFactoryPreset, true, false, "Command maps to Ctrl.");
            Add(result, AppCommand.ToggleMixer, "Show/Hide Mixer", "X", Keys.X, ReferenceGlobal,
                ShortcutOrigin.ReferenceFactoryPreset, true, false, "Toggles the lower track-control panel.");
            Add(result, AppCommand.ToggleLibrary, "Show/Hide Library", "Y", Keys.Y, ReferenceGlobal,
                ShortcutOrigin.ReferenceFactoryPreset, true, false, "Toggles the sound library.");
            Add(result, AppCommand.ToggleBrowser, "Show/Hide Browsers", "F", Keys.F, ReferenceTracks,
                ShortcutOrigin.ReferenceFactoryPreset, true, false, "Toggles the right-hand project media browser.");
            Add(result, AppCommand.ToggleInspector, "Show/Hide Local Inspector", "Option-Shift-I", Keys.Alt | Keys.Shift | Keys.I, ReferenceWindows,
                ShortcutOrigin.ReferenceFactoryPreset, true, false, "Option maps to Alt.");
            Add(result, AppCommand.ToggleMute, "Toggle Channel Strip Mute", "M", Keys.M, ReferenceGlobal,
                ShortcutOrigin.ReferenceFactoryPreset, true, true, "Toggles mute for the selected track while the arrangement has focus.");
            Add(result, AppCommand.ToggleSolo, "Toggle Channel Strip Solo", "S", Keys.S, ReferenceGlobal,
                ShortcutOrigin.ReferenceFactoryPreset, true, true, "Toggles solo for the selected track while the arrangement has focus.");
            Add(result, AppCommand.ToggleMute, "Toggle Track On", "Option-M", Keys.Alt | Keys.M, ReferenceTracks,
                ShortcutOrigin.ReferenceFactoryPreset, false, true, "Track power-off is distinct from mute and is not implemented.");
            Add(result, AppCommand.ToggleSolo, "Engage Solo Tracks of Selected Regions", "Control-S", Keys.Control | Keys.Alt | Keys.S, ReferenceGlobal,
                ShortcutOrigin.ReferenceFactoryPreset, false, true, "Windows translation conflicts with New Software Instrument Track (Ctrl+Alt+S); use the active S shortcut for solo.");
            Add(result, AppCommand.ClearSolo, "Clear/Recall Solo", "Option-S", Keys.Alt | Keys.S, ReferenceGlobal,
                ShortcutOrigin.ReferenceFactoryPreset, true, false, "Clears solo state for all tracks.");
            Add(result, AppCommand.UnmuteAll, "Mute Off for All", "Control-Shift-M", Keys.Control | Keys.Alt | Keys.Shift | Keys.M, ReferenceGlobal,
                ShortcutOrigin.ReferenceFactoryPreset, true, false, "the vendor Control maps to Ctrl+Alt.");
            Add(result, AppCommand.AddRegion, "Add Region (Slop Studio)", "Ctrl-Shift-R", Keys.Control | Keys.Shift | Keys.R, "",
                ShortcutOrigin.SlopStudioConvenience, true, false, "Slop Studio convenience mapping; not claimed as a reference application default.");
            Add(result, AppCommand.DeleteRegion, "Delete Region (Slop Studio)", "Delete", Keys.Delete, "",
                ShortcutOrigin.SlopStudioConvenience, true, true, "Slop Studio convenience mapping.");
            Add(result, AppCommand.Record, "Record", "R", Keys.R, ReferenceGlobal,
                ShortcutOrigin.ReferenceFactoryPreset, true, false, "Starts a real input recording workflow; dependencies are validated.");
            Add(result, AppCommand.Pause, "Pause", "numeric keypad period", Keys.Decimal, ReferenceGlobal,
                ShortcutOrigin.ReferenceFactoryPreset, true, false, "Pauses transport at the current timeline position.");
            Add(result, AppCommand.ShowPianoRoll, "Show/Hide Piano Roll", "P", Keys.P, ReferenceGlobal,
                ShortcutOrigin.ReferenceFactoryPreset, true, false, "Shows or hides the piano-roll editor.");
            Add(result, AppCommand.ShowEditor, "Show/Hide Editor", "E", Keys.E, ReferenceTracks,
                ShortcutOrigin.ReferenceFactoryPreset, true, false, "Shows or hides the selected region editor.");
            Add(result, AppCommand.OpenPreferences, "Open Settings", "Command-Comma", Keys.Control | Keys.Oemcomma, ReferenceGlobal,
                ShortcutOrigin.ReferenceFactoryPreset, true, false, "Opens persistent audio, MIDI, plugin and display settings.");
            Add(result, AppCommand.Copy, "Copy", "Command-C", Keys.Control | Keys.C, ReferenceWindows,
                ShortcutOrigin.ReferenceFactoryPreset, true, true, "Copies the selected arrangement region.");
            Add(result, AppCommand.Cut, "Cut", "Command-X", Keys.Control | Keys.X, ReferenceWindows,
                ShortcutOrigin.ReferenceFactoryPreset, true, true, "Cuts the selected arrangement region.");
            Add(result, AppCommand.Paste, "Paste", "Command-V", Keys.Control | Keys.V, ReferenceWindows,
                ShortcutOrigin.ReferenceFactoryPreset, true, true, "Pastes the region at the playhead.");
            Add(result, AppCommand.SelectAll, "Select All", "Command-A", Keys.Control | Keys.A, ReferenceWindows,
                ShortcutOrigin.ReferenceFactoryPreset, true, true, "Selects notes or regions in the active editor.");
            Add(result, AppCommand.DeselectAll, "Deselect All", "Shift-D", Keys.Shift | Keys.D, ReferenceWindows,
                ShortcutOrigin.ReferenceFactoryPreset, true, true, "Clears the region selection.");
            const string currentTracks="";
            Add(result,AppCommand.AddExternalMidiTrack,"New External MIDI Track","Option-Command-X",Keys.Control|Keys.Alt|Keys.X,currentTracks,ShortcutOrigin.ReferenceFactoryPreset,true,false,"Routes notes to the external MIDI output configured in Settings.");
            Add(result,AppCommand.ToggleCycle,"Cycle Mode","C",Keys.C,"",ShortcutOrigin.ReferenceFactoryPreset,true,false,"Toggles the actual arrangement loop.");
            Add(result,AppCommand.ToggleArm,"Toggle Track Record Enable","Control-R",Keys.Control|Keys.Alt|Keys.R,"",ShortcutOrigin.ReferenceFactoryPreset,true,true,"the vendor Control translates to Ctrl+Alt.");
            Add(result,AppCommand.ToggleInspector,"Show/Hide Inspector","I",Keys.I,ReferenceGlobal,ShortcutOrigin.ReferenceFactoryPreset,true,false,"Toggles the main inspector.");
            Add(result,AppCommand.DuplicateTrack,"New Track with Duplicate Settings","Command-D",Keys.Control|Keys.D,currentTracks,ShortcutOrigin.ReferenceFactoryPreset,true,true,"Copies track settings without regions.");
            Add(result,AppCommand.DuplicateTrackWithContent,"New Track with Duplicate Settings and Content","Option-Shift-Command-D",Keys.Control|Keys.Alt|Keys.Shift|Keys.D,currentTracks,ShortcutOrigin.ReferenceFactoryPreset,true,true,"Copies track settings and region content.");
            Add(result,AppCommand.DuplicateRegion,"Repeat Regions/Events","Command-R",Keys.Control|Keys.R,currentTracks,ShortcutOrigin.ReferenceFactoryPreset,true,true,"Repeats the selected region once at its end.");
            Add(result,AppCommand.SplitRegion,"Split Regions/Events at Playhead Position","Command-T",Keys.Control|Keys.T,currentTracks,ShortcutOrigin.ReferenceFactoryPreset,true,true,"Splits the selected region at the playhead.");
            Add(result,AppCommand.Quantize,"Quantize Selected Regions/Events","Q",Keys.Q,currentTracks,ShortcutOrigin.ReferenceFactoryPreset,true,true,"Quantizes notes in the selected MIDI region to 1/16.");
            Add(result,AppCommand.ToggleLauncher,"Switch between Launcher Grid and Tracks View","Option-V",Keys.Alt|Keys.V,ReferenceTracks,ShortcutOrigin.ReferenceFactoryPreset,true,false,"Uses the corresponding Slop workspace; advanced reference application behavior remains partial.");
            Add(result,AppCommand.LauncherOnly,"Show Launcher Grid only","Option-L",Keys.Alt|Keys.L,ReferenceTracks,ShortcutOrigin.ReferenceFactoryPreset,true,false,"Uses the corresponding Slop workspace; advanced reference application behavior remains partial.");
            Add(result,AppCommand.TracksOnly,"Show Tracks View only","Option-N",Keys.Alt|Keys.N,ReferenceTracks,ShortcutOrigin.ReferenceFactoryPreset,true,false,"Uses the corresponding Slop workspace; advanced reference application behavior remains partial.");
            Add(result,AppCommand.LauncherBoth,"Show Both, Launcher Grid and Tracks View","Option-B",Keys.Alt|Keys.B,ReferenceTracks,ShortcutOrigin.ReferenceFactoryPreset,true,false,"Uses the corresponding Slop workspace; advanced reference application behavior remains partial.");
            Add(result,AppCommand.ShowScore,"Show/Hide Score Editor","N",Keys.N,"",ShortcutOrigin.ReferenceFactoryPreset,true,false,"Uses the corresponding Slop workspace; advanced reference application behavior remains partial.");
            Add(result,AppCommand.ShowScore,"Open Score Editor","Command-5",Keys.Control|Keys.D5,"",ShortcutOrigin.ReferenceFactoryPreset,true,false,"Uses the corresponding Slop workspace; advanced reference application behavior remains partial.");
            Add(result,AppCommand.ShowSteps,"Open Step Sequencer","Command-3",Keys.Control|Keys.D3,"",ShortcutOrigin.ReferenceFactoryPreset,true,false,"Uses the corresponding Slop workspace; advanced reference application behavior remains partial.");
            Add(result,AppCommand.ShowSamples,"Show/Hide Loop Browser","O",Keys.O,"",ShortcutOrigin.ReferenceFactoryPreset,true,false,"Uses the corresponding Slop workspace; advanced reference application behavior remains partial.");
            Add(result,AppCommand.ShowAutomation,"Hide/Show Track Automation","A",Keys.A,ReferenceTracks,ShortcutOrigin.ReferenceFactoryPreset,true,false,"Uses the corresponding Slop workspace; advanced reference application behavior remains partial.");
            Add(result,AppCommand.ShowPitch,"Show Waveform Editor","Command-F",Keys.Control|Keys.F,ReferenceTracks,ShortcutOrigin.ReferenceFactoryPreset,true,false,"Uses the corresponding Slop workspace; advanced reference application behavior remains partial.");
            Add(result,AppCommand.ExportAudio,"Master Project or Selection","Command-B",Keys.Control|Keys.B,"",ShortcutOrigin.ReferenceFactoryPreset,true,false,"Uses the corresponding Slop workspace; advanced reference application behavior remains partial.");
            Add(result,AppCommand.ShowPianoRoll,"Open Piano Roll","Command-4",Keys.Control|Keys.D4,"",ShortcutOrigin.ReferenceFactoryPreset,true,false,"Uses the corresponding Slop workspace; advanced reference application behavior remains partial.");
            Add(result,AppCommand.ToggleMixer,"Open Mixer","Command-2",Keys.Control|Keys.D2,"",ShortcutOrigin.ReferenceFactoryPreset,true,false,"Uses the corresponding Slop workspace; advanced reference application behavior remains partial.");
            Add(result,AppCommand.CreateTrackStack,"Create Track Stack","Shift-Command-D",Keys.Control|Keys.Shift|Keys.D,ReferenceTracks,ShortcutOrigin.ReferenceFactoryPreset,true,true,"Opens stack type selection.");
            Add(result,AppCommand.CreateFolderStack,"Create Folder Stack","Shift-Command-F",Keys.Control|Keys.Shift|Keys.F,ReferenceTracks,ShortcutOrigin.ReferenceFactoryPreset,true,true,"Creates a folder stack.");
            Add(result,AppCommand.CreateSummingStack,"Create Mix Stack","Shift-Command-G",Keys.Control|Keys.Shift|Keys.G,ReferenceTracks,ShortcutOrigin.ReferenceFactoryPreset,true,true,"Creates a summing stack.");
            Add(result,AppCommand.JoinRegions,"Join Regions/Notes","Command-J",Keys.Control|Keys.J,ReferenceTracks,ShortcutOrigin.ReferenceFactoryPreset,true,true,"Joins supported selected regions on the same track.");
            Add(result,AppCommand.ZoomVerticalOut,"Zoom Vertical Out","Command-Up Arrow",Keys.Control|Keys.Up,ReferenceWindows,ShortcutOrigin.ReferenceFactoryPreset,true,false,"Zooms the editor under the pointer or the focused editor.");
            Add(result,AppCommand.ZoomVerticalIn,"Zoom Vertical In","Command-Down Arrow",Keys.Control|Keys.Down,ReferenceWindows,ShortcutOrigin.ReferenceFactoryPreset,true,false,"Zooms the editor under the pointer or the focused editor.");
            Add(result,AppCommand.ShowGlobalTracks,"Show/Hide Global Tracks","G",Keys.G,ReferenceGlobal,ShortcutOrigin.ReferenceFactoryPreset,true,false,"Shows tempo, meter and chord progression.");
            Add(result,AppCommand.InvertSelection,"Invert Selection","Shift-I",Keys.Shift|Keys.I,ReferenceWindows,ShortcutOrigin.ReferenceFactoryPreset,true,true,"Inverts notes or regions in the active editor.");
            Add(result,AppCommand.ToolMenu,"Show Tool Menu","T",Keys.T,ReferenceWindows,ShortcutOrigin.ReferenceFactoryPreset,true,false,"Selects Pointer, Pencil or the supported context tool.");
            Add(result,AppCommand.ZoomFitSelection,"Toggle Zoom to Fit Selection or All Contents","Z",Keys.Z,ReferenceWindows,ShortcutOrigin.ReferenceFactoryPreset,true,false,"Fits selection or contents; repeat restores the prior view.");
            foreach(var entry in new[]{new[]{AppCommand.TransposeUp.ToString(),"Transpose Region/Event +1 Semitone","Option-Up Arrow",((int)(Keys.Alt|Keys.Up)).ToString()},new[]{AppCommand.TransposeDown.ToString(),"Transpose Region/Event -1 Semitone","Option-Down Arrow",((int)(Keys.Alt|Keys.Down)).ToString()},new[]{AppCommand.TransposeOctaveUp.ToString(),"Transpose Region/Event +12 Semitone","Option-Shift-Up Arrow",((int)(Keys.Alt|Keys.Shift|Keys.Up)).ToString()},new[]{AppCommand.TransposeOctaveDown.ToString(),"Transpose Region/Event -12 Semitone","Option-Shift-Down Arrow",((int)(Keys.Alt|Keys.Shift|Keys.Down)).ToString()},new[]{AppCommand.NudgeLeft.ToString(),"Nudge Region/Event Position Left by Nudge Value","Option-Left Arrow",((int)(Keys.Alt|Keys.Left)).ToString()},new[]{AppCommand.NudgeRight.ToString(),"Nudge Region/Event Position Right by Nudge Value","Option-Right Arrow",((int)(Keys.Alt|Keys.Right)).ToString()},new[]{AppCommand.LengthLeft.ToString(),"Nudge Region/Event Length Left by Nudge Value","Option-Shift-Left Arrow",((int)(Keys.Alt|Keys.Shift|Keys.Left)).ToString()},new[]{AppCommand.LengthRight.ToString(),"Nudge Region/Event Length Right by Nudge Value","Option-Shift-Right Arrow",((int)(Keys.Alt|Keys.Shift|Keys.Right)).ToString()}})Add(result,(AppCommand)Enum.Parse(typeof(AppCommand),entry[0]),entry[1],entry[2],(Keys)Int32.Parse(entry[3]),currentTracks,ShortcutOrigin.ReferenceFactoryPreset,true,true,"Option maps to Alt; the editor snap division supplies the nudge value.");
            return result;
        }

        private static void Add(List<ShortcutBinding> bindings, AppCommand command, string logicCommand,
            string macShortcut, Keys windowsKeys, string source, ShortcutOrigin origin,
            bool implemented, bool arrangementOnly, string note)
        {
            bindings.Add(new ShortcutBinding(command, logicCommand, macShortcut, windowsKeys,
                source, origin, implemented, arrangementOnly, note));
        }
    }
}
