# reference application shortcut compatibility — Slop Studio 0.8

Exported from the executable registry: 82 bindings, 80 active. This is a partial implementation, not all reference application keys.

Command→Ctrl; Option→Alt; Control→Ctrl+Alt; Shift→Shift. Modifiers are unioned, not duplicated. Conflicting translated assignments stay disabled. GUI/menu/shortcut paths share command handlers. Plain-arrow piano note movement is a Slop convenience; documented Alt-arrow mappings are also implemented. Text entry retains ordinary typing/editing. Hovered arrangement/piano context is checked before focused editor fallback.

PASS below only marks keys actually exercised by V8GuiTests. NOT VERIFIED rows have registry uniqueness/menu integration evidence, not individual execution evidence. Inactive bindings are PARTIAL. Vendor documentation/source confidence is retained verbatim as registry metadata.

| reference application command | legacy platform | Windows | Slop handler | Origin / source | Status | Scope / limitations |
|---|---|---|---|---|---|---|
| Show/Hide Musical Typing | Command-K | Ctrl+K | ToggleSoftwareKeyboard | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Command maps to Ctrl. |
| User Manual | — | F1 | UserManual | SlopStudioConvenience · Slop convenience | NOT VERIFIED | Windows Help convention. |
| Play or Stop | Space | Space | PlayOrStop | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | the vendor documents Space as Play or Stop. |
| Stop | numeric keypad 0 | Num 0 | Stop | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Requires the numeric keypad 0 key. |
| Go to Beginning | Return | Enter | GoToBeginning | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Stops playback and returns the arrangement cursor to beat 0. |
| Rewind one bar | , | , | RewindBar | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Moves the timeline playhead one bar left. |
| Forward one bar | . | . | ForwardBar | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Moves the timeline playhead one bar right. |
| New Empty Project | Shift-Command-N | Ctrl+Shift+N | NewProject | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | The default New Empty Project mapping translated to Ctrl+Shift+N. |
| Open Project | Command-O | Ctrl+O | OpenProject | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | The default Open mapping translated to Ctrl+O. |
| Save Project | Command-S | Ctrl+S | SaveProject | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | The default Save mapping translated to Ctrl+S. |
| Undo | Command-Z | Ctrl+Z | Undo | the vendorUSFactoryPreset · [source]() | PASS | Command maps to Ctrl. |
| Redo | Shift-Command-Z | Ctrl+Shift+Z | Redo | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Preserves The default Shift-Command redo chord. |
| New Audio Track | Option-Command-A | Ctrl+Alt+A | AddAudioTrack | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Option maps to Alt; Command maps to Ctrl. |
| New Software Instrument Track | Option-Command-S | Ctrl+Alt+S | AddMidiTrack | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Creates an internal instrument track. |
| Delete Track | Command-Delete | Ctrl+Delete | DeleteTrack | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Deletes the selected track when no region is selected. |
| Import Audio File | Shift-Command-I | Ctrl+Shift+I | ImportAudio | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Imports a PCM WAV file. |
| Rename Track | Shift-Return | Shift+Enter | RenameTrack | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Available with a track selected and no region selected. |
| Rename Regions | Shift-N | Shift+N | RenameRegion | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Available with a region selected. |
| Select Previous Region on Selected Track | Left Arrow | ← | SelectPreviousRegion | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Arrangement focus only. |
| Select Next Region on Selected Track | Right Arrow | → | SelectNextRegion | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Arrangement focus only. |
| Select Previous Track | Up Arrow | ↑ | SelectPreviousTrack | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Arrangement focus only. |
| Select Next Track | Down Arrow | ↓ | SelectNextTrack | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Arrangement focus only. |
| Zoom Horizontal Out | Command-Left Arrow | Ctrl+← | ZoomOut | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Command maps to Ctrl. |
| Zoom Horizontal In | Command-Right Arrow | Ctrl+→ | ZoomIn | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Command maps to Ctrl. |
| Show/Hide Mixer | X | X | ToggleMixer | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Toggles the lower track-control panel. |
| Show/Hide Library | Y | Y | ToggleLibrary | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Toggles the sound library. |
| Show/Hide Browsers | F | F | ToggleBrowser | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Toggles the right-hand project media browser. |
| Show/Hide Local Inspector | Option-Shift-I | Alt+Shift+I | ToggleInspector | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Option maps to Alt. |
| Toggle Channel Strip Mute | M | M | ToggleMute | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Toggles mute for the selected track while the arrangement has focus. |
| Toggle Channel Strip Solo | S | S | ToggleSolo | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Toggles solo for the selected track while the arrangement has focus. |
| Toggle Track On | Option-M | Alt+M | ToggleMute | the vendorUSFactoryPreset · [source]() | PARTIAL | Track power-off is distinct from mute and is not implemented. |
| Engage Solo Tracks of Selected Regions | Control-S | Ctrl+Alt+S | ToggleSolo | the vendorUSFactoryPreset · [source]() | PARTIAL | Windows translation conflicts with New Software Instrument Track (Ctrl+Alt+S); use the active S shortcut for solo. |
| Clear/Recall Solo | Option-S | Alt+S | ClearSolo | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Clears solo state for all tracks. |
| Mute Off for All | Control-Shift-M | Ctrl+Alt+Shift+M | UnmuteAll | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | the vendor Control maps to Ctrl+Alt. |
| Add Region (Slop Studio) | Ctrl-Shift-R | Ctrl+Shift+R | AddRegion | SlopStudioConvenience · Slop convenience | NOT VERIFIED | Slop Studio convenience mapping; not claimed as a reference application default. |
| Delete Region (Slop Studio) | Delete | Delete | DeleteRegion | SlopStudioConvenience · Slop convenience | NOT VERIFIED | Slop Studio convenience mapping. |
| Record | R | R | Record | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Starts a real input recording workflow; dependencies are validated. |
| Pause | numeric keypad period | Num . | Pause | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Pauses transport at the current timeline position. |
| Show/Hide Piano Roll | P | P | ShowPianoRoll | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Shows or hides the piano-roll editor. |
| Show/Hide Editor | E | E | ShowEditor | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Shows or hides the selected region editor. |
| Open Settings | Command-Comma | Ctrl+, | OpenPreferences | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Opens persistent audio, MIDI, plugin and display settings. |
| Copy | Command-C | Ctrl+C | Copy | the vendorUSFactoryPreset · [source]() | PASS | Copies the selected arrangement region. |
| Cut | Command-X | Ctrl+X | Cut | the vendorUSFactoryPreset · [source]() | PASS | Cuts the selected arrangement region. |
| Paste | Command-V | Ctrl+V | Paste | the vendorUSFactoryPreset · [source]() | PASS | Pastes the region at the playhead. |
| Select All | Command-A | Ctrl+A | SelectAll | the vendorUSFactoryPreset · [source]() | PASS | Selects notes or regions in the active editor. |
| Deselect All | Shift-D | Shift+D | DeselectAll | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Clears the region selection. |
| New External MIDI Track | Option-Command-X | Ctrl+Alt+X | AddExternalMidiTrack | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Routes notes to the external MIDI output configured in Settings. |
| Cycle Mode | C | C | ToggleCycle | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Toggles the actual arrangement loop. |
| Toggle Track Record Enable | Control-R | Ctrl+Alt+R | ToggleArm | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | the vendor Control translates to Ctrl+Alt. |
| Show/Hide Inspector | I | I | ToggleInspector | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Toggles the main inspector. |
| New Track with Duplicate Settings | Command-D | Ctrl+D | DuplicateTrack | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Copies track settings without regions. |
| New Track with Duplicate Settings and Content | Option-Shift-Command-D | Ctrl+Alt+Shift+D | DuplicateTrackWithContent | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Copies track settings and region content. |
| Repeat Regions/Events | Command-R | Ctrl+R | DuplicateRegion | the vendorUSFactoryPreset · [source]() | PASS | Repeats the selected region once at its end. |
| Split Regions/Events at Playhead Position | Command-T | Ctrl+T | SplitRegion | the vendorUSFactoryPreset · [source]() | PASS | Splits the selected region at the playhead. |
| Quantize Selected Regions/Events | Q | Q | Quantize | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Quantizes notes in the selected MIDI region to 1/16. |
| Switch between Launcher Grid and Tracks View | Option-V | Alt+V | ToggleLauncher | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Uses the corresponding Slop workspace; advanced reference application behavior remains partial. |
| Show Launcher Grid only | Option-L | Alt+L | LauncherOnly | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Uses the corresponding Slop workspace; advanced reference application behavior remains partial. |
| Show Tracks View only | Option-N | Alt+N | TracksOnly | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Uses the corresponding Slop workspace; advanced reference application behavior remains partial. |
| Show Both, Launcher Grid and Tracks View | Option-B | Alt+B | LauncherBoth | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Uses the corresponding Slop workspace; advanced reference application behavior remains partial. |
| Show/Hide Score Editor | N | N | ShowScore | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Uses the corresponding Slop workspace; advanced reference application behavior remains partial. |
| Open Score Editor | Command-5 | Ctrl+D5 | ShowScore | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Uses the corresponding Slop workspace; advanced reference application behavior remains partial. |
| Open Step Sequencer | Command-3 | Ctrl+D3 | ShowSteps | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Uses the corresponding Slop workspace; advanced reference application behavior remains partial. |
| Show/Hide Loop Browser | O | O | ShowSamples | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Uses the corresponding Slop workspace; advanced reference application behavior remains partial. |
| Hide/Show Track Automation | A | A | ShowAutomation | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Uses the corresponding Slop workspace; advanced reference application behavior remains partial. |
| Hide/Show Flex Pitch/Time | Command-F | Ctrl+F | ShowPitch | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Uses the corresponding Slop workspace; advanced reference application behavior remains partial. |
| Bounce | Command-B | Ctrl+B | ExportAudio | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Uses the corresponding Slop workspace; advanced reference application behavior remains partial. |
| Open Piano Roll | Command-4 | Ctrl+D4 | ShowPianoRoll | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Uses the corresponding Slop workspace; advanced reference application behavior remains partial. |
| Open Mixer | Command-2 | Ctrl+D2 | ToggleMixer | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Uses the corresponding Slop workspace; advanced reference application behavior remains partial. |
| Zoom Vertical Out | Command-Up Arrow | Ctrl+↑ | ZoomVerticalOut | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Zooms the editor under the pointer or the focused editor. |
| Zoom Vertical In | Command-Down Arrow | Ctrl+↓ | ZoomVerticalIn | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Zooms the editor under the pointer or the focused editor. |
| Show/Hide Global Tracks | G | G | ShowGlobalTracks | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Shows tempo, meter and chord progression. |
| Invert Selection | Shift-I | Shift+I | InvertSelection | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Inverts notes or regions in the active editor. |
| Show Tool Menu | T | T | ToolMenu | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Selects Pointer, Pencil or the supported context tool. |
| Toggle Zoom to Fit Selection or All Contents | Z | Z | ZoomFitSelection | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Fits selection or contents; repeat restores the prior view. |
| Transpose Region/Event +1 Semitone | Option-Up Arrow | Alt+↑ | TransposeUp | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Option maps to Alt; the editor snap division supplies the nudge value. |
| Transpose Region/Event -1 Semitone | Option-Down Arrow | Alt+↓ | TransposeDown | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Option maps to Alt; the editor snap division supplies the nudge value. |
| Transpose Region/Event +12 Semitone | Option-Shift-Up Arrow | Alt+Shift+↑ | TransposeOctaveUp | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Option maps to Alt; the editor snap division supplies the nudge value. |
| Transpose Region/Event -12 Semitone | Option-Shift-Down Arrow | Alt+Shift+↓ | TransposeOctaveDown | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Option maps to Alt; the editor snap division supplies the nudge value. |
| Nudge Region/Event Position Left by Nudge Value | Option-Left Arrow | Alt+← | NudgeLeft | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Option maps to Alt; the editor snap division supplies the nudge value. |
| Nudge Region/Event Position Right by Nudge Value | Option-Right Arrow | Alt+→ | NudgeRight | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Option maps to Alt; the editor snap division supplies the nudge value. |
| Nudge Region/Event Length Left by Nudge Value | Option-Shift-Left Arrow | Alt+Shift+← | LengthLeft | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Option maps to Alt; the editor snap division supplies the nudge value. |
| Nudge Region/Event Length Right by Nudge Value | Option-Shift-Right Arrow | Alt+Shift+→ | LengthRight | the vendorUSFactoryPreset · [source]() | NOT VERIFIED | Option maps to Alt; the editor snap division supplies the nudge value. |
