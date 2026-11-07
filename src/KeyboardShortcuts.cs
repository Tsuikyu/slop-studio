using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace SlopProX
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
        AppleUSFactoryPreset,
        MacPlatformConvention,
        SlopProXConvenience
    }

    public sealed class ShortcutBinding
    {
        public AppCommand Command { get; private set; }
        public string LogicCommand { get; private set; }
        public string MacShortcut { get; private set; }
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
            MacShortcut = macShortcut;
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
        public const string AppleGlobal =
            "https://support.apple.com/en-asia/guide/logicpro/lgcp32e85cd9/10.7/mac/11.0";
        public const string AppleTracks =
            "https://support.apple.com/en-sg/guide/logicpro/lgcpb99be9c5/10.7/mac/11.0";
        public const string AppleWindows =
            "https://support.apple.com/en-hk/guide/logicpro/lgcp665bbccc/mac";
        public const string AppleTransport =
            "https://support.apple.com/guide/logicpro/use-transport-key-commands-lgcp2814a670/10.7/mac/11.0";

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
            Add(result,AppCommand.ToggleSoftwareKeyboard,"Show/Hide Musical Typing","Command-K",Keys.Control|Keys.K,AppleGlobal,ShortcutOrigin.AppleUSFactoryPreset,true,false,"Command maps to Ctrl.");
            Add(result,AppCommand.UserManual,"User Manual","",Keys.F1,"",ShortcutOrigin.SlopProXConvenience,true,false,"Windows Help convention.");
            Add(result,AppCommand.ToggleFullScreen,"Full Screen","",Keys.F11,"",ShortcutOrigin.SlopProXConvenience,true,false,"Windows full-screen shortcut.");
            Add(result, AppCommand.PlayOrStop, "Play or Stop", "Space", Keys.Space, AppleTransport,
                ShortcutOrigin.AppleUSFactoryPreset, true, false, "Apple documents Space as Play or Stop.");
            Add(result, AppCommand.Stop, "Stop", "numeric keypad 0", Keys.NumPad0, AppleGlobal,
                ShortcutOrigin.AppleUSFactoryPreset, true, false, "Requires the numeric keypad 0 key.");
            Add(result, AppCommand.GoToBeginning, "Go to Beginning", "Return", Keys.Enter, AppleGlobal,
                ShortcutOrigin.AppleUSFactoryPreset, true, false, "Stops playback and returns the arrangement cursor to beat 0.");
            Add(result, AppCommand.RewindBar, "Rewind one bar", ",", Keys.Oemcomma, AppleGlobal,
                ShortcutOrigin.AppleUSFactoryPreset, true, false, "Moves the timeline playhead one bar left.");
            Add(result, AppCommand.ForwardBar, "Forward one bar", ".", Keys.OemPeriod, AppleGlobal,
                ShortcutOrigin.AppleUSFactoryPreset, true, false, "Moves the timeline playhead one bar right.");
            Add(result, AppCommand.NewProject, "New Empty Project", "Shift-Command-N", Keys.Control | Keys.Shift | Keys.N, AppleGlobal,
                ShortcutOrigin.AppleUSFactoryPreset, true, false, "Logic's New Empty Project mapping translated to Ctrl+Shift+N.");
            Add(result, AppCommand.OpenProject, "Open Project", "Command-O", Keys.Control | Keys.O, AppleGlobal,
                ShortcutOrigin.AppleUSFactoryPreset, true, false, "Logic's Open mapping translated to Ctrl+O.");
            Add(result, AppCommand.SaveProject, "Save Project", "Command-S", Keys.Control | Keys.S, AppleGlobal,
                ShortcutOrigin.AppleUSFactoryPreset, true, false, "Logic's Save mapping translated to Ctrl+S.");
            Add(result, AppCommand.Undo, "Undo", "Command-Z", Keys.Control | Keys.Z, AppleWindows,
                ShortcutOrigin.AppleUSFactoryPreset, true, false, "Command maps to Ctrl.");
            Add(result, AppCommand.Redo, "Redo", "Shift-Command-Z", Keys.Control | Keys.Shift | Keys.Z, AppleWindows,
                ShortcutOrigin.AppleUSFactoryPreset, true, false, "Preserves Logic's Shift-Command redo chord.");
            Add(result, AppCommand.AddAudioTrack, "New Audio Track", "Option-Command-A", Keys.Control | Keys.Alt | Keys.A, AppleTracks,
                ShortcutOrigin.AppleUSFactoryPreset, true, false, "Option maps to Alt; Command maps to Ctrl.");
            Add(result, AppCommand.AddMidiTrack, "New Software Instrument Track", "Option-Command-S", Keys.Control | Keys.Alt | Keys.S, AppleTracks,
                ShortcutOrigin.AppleUSFactoryPreset, true, false, "Creates an internal instrument track.");
            Add(result, AppCommand.DeleteTrack, "Delete Track", "Command-Delete", Keys.Control | Keys.Delete, AppleTracks,
                ShortcutOrigin.AppleUSFactoryPreset, true, false, "Deletes the selected track when no region is selected.");
            Add(result, AppCommand.ImportAudio, "Import Audio File", "Shift-Command-I", Keys.Control | Keys.Shift | Keys.I, AppleTracks,
                ShortcutOrigin.AppleUSFactoryPreset, true, false, "Imports a PCM WAV file.");
            Add(result, AppCommand.RenameTrack, "Rename Track", "Shift-Return", Keys.Shift | Keys.Enter, AppleTracks,
                ShortcutOrigin.AppleUSFactoryPreset, true, true, "Available with a track selected and no region selected.");
            Add(result, AppCommand.RenameRegion, "Rename Regions", "Shift-N", Keys.Shift | Keys.N, AppleTracks,
                ShortcutOrigin.AppleUSFactoryPreset, true, true, "Available with a region selected.");
            Add(result, AppCommand.SelectPreviousRegion, "Select Previous Region on Selected Track", "Left Arrow", Keys.Left, AppleTracks,
                ShortcutOrigin.AppleUSFactoryPreset, true, true, "Arrangement focus only.");
            Add(result, AppCommand.SelectNextRegion, "Select Next Region on Selected Track", "Right Arrow", Keys.Right, AppleTracks,
                ShortcutOrigin.AppleUSFactoryPreset, true, true, "Arrangement focus only.");
            Add(result, AppCommand.SelectPreviousTrack, "Select Previous Track", "Up Arrow", Keys.Up, AppleGlobal,
                ShortcutOrigin.AppleUSFactoryPreset, true, true, "Arrangement focus only.");
            Add(result, AppCommand.SelectNextTrack, "Select Next Track", "Down Arrow", Keys.Down, AppleGlobal,
                ShortcutOrigin.AppleUSFactoryPreset, true, true, "Arrangement focus only.");
            Add(result, AppCommand.ZoomOut, "Zoom Horizontal Out", "Command-Left Arrow", Keys.Control | Keys.Left, AppleWindows,
                ShortcutOrigin.AppleUSFactoryPreset, true, false, "Command maps to Ctrl.");
            Add(result, AppCommand.ZoomIn, "Zoom Horizontal In", "Command-Right Arrow", Keys.Control | Keys.Right, AppleWindows,
                ShortcutOrigin.AppleUSFactoryPreset, true, false, "Command maps to Ctrl.");
            Add(result, AppCommand.ToggleMixer, "Show/Hide Mixer", "X", Keys.X, AppleGlobal,
                ShortcutOrigin.AppleUSFactoryPreset, true, false, "Toggles the lower track-control panel.");
            Add(result, AppCommand.ToggleLibrary, "Show/Hide Library", "Y", Keys.Y, AppleGlobal,
                ShortcutOrigin.AppleUSFactoryPreset, true, false, "Toggles the sound library.");
            Add(result, AppCommand.ToggleBrowser, "Show/Hide Browsers", "F", Keys.F, AppleTracks,
                ShortcutOrigin.AppleUSFactoryPreset, true, false, "Toggles the right-hand project media browser.");
            Add(result, AppCommand.ToggleInspector, "Show/Hide Local Inspector", "Option-Shift-I", Keys.Alt | Keys.Shift | Keys.I, AppleWindows,
                ShortcutOrigin.AppleUSFactoryPreset, true, false, "Option maps to Alt.");
            Add(result, AppCommand.ToggleMute, "Toggle Channel Strip Mute", "M", Keys.M, AppleGlobal,
                ShortcutOrigin.AppleUSFactoryPreset, true, true, "Toggles mute for the selected track while the arrangement has focus.");
            Add(result, AppCommand.ToggleSolo, "Toggle Channel Strip Solo", "S", Keys.S, AppleGlobal,
                ShortcutOrigin.AppleUSFactoryPreset, true, true, "Toggles solo for the selected track while the arrangement has focus.");
            Add(result, AppCommand.ToggleMute, "Toggle Track On", "Option-M", Keys.Alt | Keys.M, AppleTracks,
                ShortcutOrigin.AppleUSFactoryPreset, false, true, "Track power-off is distinct from mute and is not implemented.");
            Add(result, AppCommand.ToggleSolo, "Engage Solo Tracks of Selected Regions", "Control-S", Keys.Control | Keys.Alt | Keys.S, AppleGlobal,
                ShortcutOrigin.AppleUSFactoryPreset, false, true, "Windows translation conflicts with New Software Instrument Track (Ctrl+Alt+S); use the active S shortcut for solo.");
            Add(result, AppCommand.ClearSolo, "Clear/Recall Solo", "Option-S", Keys.Alt | Keys.S, AppleGlobal,
                ShortcutOrigin.AppleUSFactoryPreset, true, false, "Clears solo state for all tracks.");
            Add(result, AppCommand.UnmuteAll, "Mute Off for All", "Control-Shift-M", Keys.Control | Keys.Alt | Keys.Shift | Keys.M, AppleGlobal,
                ShortcutOrigin.AppleUSFactoryPreset, true, false, "Apple Control maps to Ctrl+Alt.");
            Add(result, AppCommand.AddRegion, "Add Region (Slop Studio)", "Ctrl-Shift-R", Keys.Control | Keys.Shift | Keys.R, "",
                ShortcutOrigin.SlopProXConvenience, true, false, "Slop Studio convenience mapping; not claimed as a Logic default.");
            Add(result, AppCommand.DeleteRegion, "Delete Region (Slop Studio)", "Delete", Keys.Delete, "",
                ShortcutOrigin.SlopProXConvenience, true, true, "Slop Studio convenience mapping.");
            Add(result, AppCommand.Record, "Record", "R", Keys.R, AppleGlobal,
                ShortcutOrigin.AppleUSFactoryPreset, true, false, "Starts a real input recording workflow; dependencies are validated.");
            Add(result, AppCommand.Pause, "Pause", "numeric keypad period", Keys.Decimal, AppleGlobal,
                ShortcutOrigin.AppleUSFactoryPreset, true, false, "Pauses transport at the current timeline position.");
            Add(result, AppCommand.ShowPianoRoll, "Show/Hide Piano Roll", "P", Keys.P, AppleGlobal,
                ShortcutOrigin.AppleUSFactoryPreset, true, false, "Shows or hides the piano-roll editor.");
            Add(result, AppCommand.ShowEditor, "Show/Hide Editor", "E", Keys.E, AppleTracks,
                ShortcutOrigin.AppleUSFactoryPreset, true, false, "Shows or hides the selected region editor.");
            Add(result, AppCommand.OpenPreferences, "Open Settings", "Command-Comma", Keys.Control | Keys.Oemcomma, AppleGlobal,
                ShortcutOrigin.AppleUSFactoryPreset, true, false, "Opens persistent audio, MIDI, plugin and display settings.");
            Add(result, AppCommand.Copy, "Copy", "Command-C", Keys.Control | Keys.C, AppleWindows,
                ShortcutOrigin.AppleUSFactoryPreset, true, true, "Copies the selected arrangement region.");
            Add(result, AppCommand.Cut, "Cut", "Command-X", Keys.Control | Keys.X, AppleWindows,
                ShortcutOrigin.AppleUSFactoryPreset, true, true, "Cuts the selected arrangement region.");
            Add(result, AppCommand.Paste, "Paste", "Command-V", Keys.Control | Keys.V, AppleWindows,
                ShortcutOrigin.AppleUSFactoryPreset, true, true, "Pastes the region at the playhead.");
            Add(result, AppCommand.SelectAll, "Select All", "Command-A", Keys.Control | Keys.A, AppleWindows,
                ShortcutOrigin.AppleUSFactoryPreset, true, true, "Selects notes or regions in the active editor.");
            Add(result, AppCommand.DeselectAll, "Deselect All", "Shift-D", Keys.Shift | Keys.D, AppleWindows,
                ShortcutOrigin.AppleUSFactoryPreset, true, true, "Clears the region selection.");
            const string currentTracks="https://support.apple.com/guide/logicpro/main-window-tracks-and-various-editors-lgcpe8314fe2/12.3/mac/15.6";
            Add(result,AppCommand.AddExternalMidiTrack,"New External MIDI Track","Option-Command-X",Keys.Control|Keys.Alt|Keys.X,currentTracks,ShortcutOrigin.AppleUSFactoryPreset,true,false,"Routes notes to the external MIDI output configured in Settings.");
            Add(result,AppCommand.ToggleCycle,"Cycle Mode","C",Keys.C,"https://support.apple.com/en-mo/guide/logicpro/lgcp59e41e86/10.7/mac/11.0",ShortcutOrigin.AppleUSFactoryPreset,true,false,"Toggles the actual arrangement loop.");
            Add(result,AppCommand.ToggleArm,"Toggle Track Record Enable","Control-R",Keys.Control|Keys.Alt|Keys.R,"https://support.apple.com/en-mo/guide/logicpro/lgcp0c445ccf/mac",ShortcutOrigin.AppleUSFactoryPreset,true,true,"Apple Control translates to Ctrl+Alt.");
            Add(result,AppCommand.ToggleInspector,"Show/Hide Inspector","I",Keys.I,AppleGlobal,ShortcutOrigin.AppleUSFactoryPreset,true,false,"Toggles the main inspector.");
            Add(result,AppCommand.DuplicateTrack,"New Track with Duplicate Settings","Command-D",Keys.Control|Keys.D,currentTracks,ShortcutOrigin.AppleUSFactoryPreset,true,true,"Copies track settings without regions.");
            Add(result,AppCommand.DuplicateTrackWithContent,"New Track with Duplicate Settings and Content","Option-Shift-Command-D",Keys.Control|Keys.Alt|Keys.Shift|Keys.D,currentTracks,ShortcutOrigin.AppleUSFactoryPreset,true,true,"Copies track settings and region content.");
            Add(result,AppCommand.DuplicateRegion,"Repeat Regions/Events","Command-R",Keys.Control|Keys.R,currentTracks,ShortcutOrigin.AppleUSFactoryPreset,true,true,"Repeats the selected region once at its end.");
            Add(result,AppCommand.SplitRegion,"Split Regions/Events at Playhead Position","Command-T",Keys.Control|Keys.T,currentTracks,ShortcutOrigin.AppleUSFactoryPreset,true,true,"Splits the selected region at the playhead.");
            Add(result,AppCommand.Quantize,"Quantize Selected Regions/Events","Q",Keys.Q,currentTracks,ShortcutOrigin.AppleUSFactoryPreset,true,true,"Quantizes notes in the selected MIDI region to 1/16.");
            Add(result,AppCommand.ToggleLauncher,"Switch between Live Loops Grid and Tracks View","Option-V",Keys.Alt|Keys.V,AppleTracks,ShortcutOrigin.AppleUSFactoryPreset,true,false,"Uses the corresponding Slop workspace; advanced Logic behavior remains partial.");
            Add(result,AppCommand.LauncherOnly,"Show Live Loops Grid only","Option-L",Keys.Alt|Keys.L,AppleTracks,ShortcutOrigin.AppleUSFactoryPreset,true,false,"Uses the corresponding Slop workspace; advanced Logic behavior remains partial.");
            Add(result,AppCommand.TracksOnly,"Show Tracks View only","Option-N",Keys.Alt|Keys.N,AppleTracks,ShortcutOrigin.AppleUSFactoryPreset,true,false,"Uses the corresponding Slop workspace; advanced Logic behavior remains partial.");
            Add(result,AppCommand.LauncherBoth,"Show Both, Live Loops Grid and Tracks View","Option-B",Keys.Alt|Keys.B,AppleTracks,ShortcutOrigin.AppleUSFactoryPreset,true,false,"Uses the corresponding Slop workspace; advanced Logic behavior remains partial.");
            Add(result,AppCommand.ShowScore,"Show/Hide Score Editor","N",Keys.N,"https://support.apple.com/en-ph/guide/logicpro/lgcp02bf31b6/mac",ShortcutOrigin.AppleUSFactoryPreset,true,false,"Uses the corresponding Slop workspace; advanced Logic behavior remains partial.");
            Add(result,AppCommand.ShowScore,"Open Score Editor","Command-5",Keys.Control|Keys.D5,"https://support.apple.com/en-ph/guide/logicpro/lgcp02bf31b6/mac",ShortcutOrigin.AppleUSFactoryPreset,true,false,"Uses the corresponding Slop workspace; advanced Logic behavior remains partial.");
            Add(result,AppCommand.ShowSteps,"Open Step Sequencer","Command-3",Keys.Control|Keys.D3,"https://support.apple.com/en-ph/guide/logicpro/lgcp02bf31b6/mac",ShortcutOrigin.AppleUSFactoryPreset,true,false,"Uses the corresponding Slop workspace; advanced Logic behavior remains partial.");
            Add(result,AppCommand.ShowSamples,"Show/Hide Loop Browser","O",Keys.O,"https://support.apple.com/en-ph/guide/logicpro/lgcp02bf31b6/mac",ShortcutOrigin.AppleUSFactoryPreset,true,false,"Uses the corresponding Slop workspace; advanced Logic behavior remains partial.");
            Add(result,AppCommand.ShowAutomation,"Hide/Show Track Automation","A",Keys.A,AppleTracks,ShortcutOrigin.AppleUSFactoryPreset,true,false,"Uses the corresponding Slop workspace; advanced Logic behavior remains partial.");
            Add(result,AppCommand.ShowPitch,"Show Waveform Editor","Command-F",Keys.Control|Keys.F,AppleTracks,ShortcutOrigin.AppleUSFactoryPreset,true,false,"Uses the corresponding Slop workspace; advanced Logic behavior remains partial.");
            Add(result,AppCommand.ExportAudio,"Master Project or Selection","Command-B",Keys.Control|Keys.B,"https://support.apple.com/en-za/guide/logicpro/lgcpe8314fe2/10.7/mac/11.0",ShortcutOrigin.AppleUSFactoryPreset,true,false,"Uses the corresponding Slop workspace; advanced Logic behavior remains partial.");
            Add(result,AppCommand.ShowPianoRoll,"Open Piano Roll","Command-4",Keys.Control|Keys.D4,"https://support.apple.com/en-ph/guide/logicpro/lgcp02bf31b6/mac",ShortcutOrigin.AppleUSFactoryPreset,true,false,"Uses the corresponding Slop workspace; advanced Logic behavior remains partial.");
            Add(result,AppCommand.ToggleMixer,"Open Mixer","Command-2",Keys.Control|Keys.D2,"https://support.apple.com/en-ph/guide/logicpro/lgcp02bf31b6/mac",ShortcutOrigin.AppleUSFactoryPreset,true,false,"Uses the corresponding Slop workspace; advanced Logic behavior remains partial.");
            Add(result,AppCommand.CreateTrackStack,"Create Track Stack","Shift-Command-D",Keys.Control|Keys.Shift|Keys.D,AppleTracks,ShortcutOrigin.AppleUSFactoryPreset,true,true,"Opens stack type selection.");
            Add(result,AppCommand.CreateFolderStack,"Create Folder Stack","Shift-Command-F",Keys.Control|Keys.Shift|Keys.F,AppleTracks,ShortcutOrigin.AppleUSFactoryPreset,true,true,"Creates a folder stack.");
            Add(result,AppCommand.CreateSummingStack,"Create Mix Stack","Shift-Command-G",Keys.Control|Keys.Shift|Keys.G,AppleTracks,ShortcutOrigin.AppleUSFactoryPreset,true,true,"Creates a summing stack.");
            Add(result,AppCommand.JoinRegions,"Join Regions/Notes","Command-J",Keys.Control|Keys.J,AppleTracks,ShortcutOrigin.AppleUSFactoryPreset,true,true,"Joins supported selected regions on the same track.");
            Add(result,AppCommand.ZoomVerticalOut,"Zoom Vertical Out","Command-Up Arrow",Keys.Control|Keys.Up,AppleWindows,ShortcutOrigin.AppleUSFactoryPreset,true,false,"Zooms the editor under the pointer or the focused editor.");
            Add(result,AppCommand.ZoomVerticalIn,"Zoom Vertical In","Command-Down Arrow",Keys.Control|Keys.Down,AppleWindows,ShortcutOrigin.AppleUSFactoryPreset,true,false,"Zooms the editor under the pointer or the focused editor.");
            Add(result,AppCommand.ShowGlobalTracks,"Show/Hide Global Tracks","G",Keys.G,AppleGlobal,ShortcutOrigin.AppleUSFactoryPreset,true,false,"Shows tempo, meter and chord progression.");
            Add(result,AppCommand.InvertSelection,"Invert Selection","Shift-I",Keys.Shift|Keys.I,AppleWindows,ShortcutOrigin.AppleUSFactoryPreset,true,true,"Inverts notes or regions in the active editor.");
            Add(result,AppCommand.ToolMenu,"Show Tool Menu","T",Keys.T,AppleWindows,ShortcutOrigin.AppleUSFactoryPreset,true,false,"Selects Pointer, Pencil or the supported context tool.");
            Add(result,AppCommand.ZoomFitSelection,"Toggle Zoom to Fit Selection or All Contents","Z",Keys.Z,AppleWindows,ShortcutOrigin.AppleUSFactoryPreset,true,false,"Fits selection or contents; repeat restores the prior view.");
            foreach(var entry in new[]{new[]{AppCommand.TransposeUp.ToString(),"Transpose Region/Event +1 Semitone","Option-Up Arrow",((int)(Keys.Alt|Keys.Up)).ToString()},new[]{AppCommand.TransposeDown.ToString(),"Transpose Region/Event -1 Semitone","Option-Down Arrow",((int)(Keys.Alt|Keys.Down)).ToString()},new[]{AppCommand.TransposeOctaveUp.ToString(),"Transpose Region/Event +12 Semitone","Option-Shift-Up Arrow",((int)(Keys.Alt|Keys.Shift|Keys.Up)).ToString()},new[]{AppCommand.TransposeOctaveDown.ToString(),"Transpose Region/Event -12 Semitone","Option-Shift-Down Arrow",((int)(Keys.Alt|Keys.Shift|Keys.Down)).ToString()},new[]{AppCommand.NudgeLeft.ToString(),"Nudge Region/Event Position Left by Nudge Value","Option-Left Arrow",((int)(Keys.Alt|Keys.Left)).ToString()},new[]{AppCommand.NudgeRight.ToString(),"Nudge Region/Event Position Right by Nudge Value","Option-Right Arrow",((int)(Keys.Alt|Keys.Right)).ToString()},new[]{AppCommand.LengthLeft.ToString(),"Nudge Region/Event Length Left by Nudge Value","Option-Shift-Left Arrow",((int)(Keys.Alt|Keys.Shift|Keys.Left)).ToString()},new[]{AppCommand.LengthRight.ToString(),"Nudge Region/Event Length Right by Nudge Value","Option-Shift-Right Arrow",((int)(Keys.Alt|Keys.Shift|Keys.Right)).ToString()}})Add(result,(AppCommand)Enum.Parse(typeof(AppCommand),entry[0]),entry[1],entry[2],(Keys)Int32.Parse(entry[3]),currentTracks,ShortcutOrigin.AppleUSFactoryPreset,true,true,"Option maps to Alt; the editor snap division supplies the nudge value.");
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
