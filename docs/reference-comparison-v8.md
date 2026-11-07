# Slop Studio 0.8 — visual and workflow reference comparison

The supplied modern Logic screenshots, mixer detail, older automation/editor screens and session-grid examples were inspected as references. No reference image was embedded, traced as application artwork, or redistributed.

| Reference region / principle | Implemented component | Result and limit |
|---|---|---|
| Full-width neutral control bar, grouped transport, central position/tempo/meter LCD | StudioControlBar / MainForm | Real play/stop/record/seek; Windows right-side caption controls retained. Original vector icons; exact Apple glyphs/metrics are not reproduced. |
| Library, compact inspector, fixed track headers, wide scrolling arrangement | StudioPanels / TimelineEditor | Shared selection, resizable dividers, actual colored MIDI/audio regions, waveform/note content. Layout fills the desktop; default split favors arranging. Density can change with vertical zoom. |
| Global tracks above regions | GlobalTracksPanel | Pull-down divider or G, tempo/meter controls and editable chord blocks. Variable tempo/key/marker maps and Session Players remain incomplete. |
| Lower mixer with compact channels, meters, pan and instrument slots | MixerPanel / ChannelStripControl | Real mix controls/levels, true plugin instances, left editor/right replacement behavior. Full sends/buses/automation-strip structure is not equivalent. |
| Piano roll linked to selected region and project bars | PianoRollEditor | Marquee/Shift/Alt/Ctrl tools, shared clipboard, note-repeat/split, actual project ruler and playhead. Deeper multi-region editor/view options remain partial. |
| Modern restrained stock effect surfaces | EffectKnob / EffectDisplay / SamplerEditor | Original functional rotary controls, transfer and duck-envelope display, real PCM waveform and mapping. No Apple or third-party plugin artwork was copied. |
| Persistent preferences and clear workflow dialogs | PreferencesForm / StudioDialogs | Dark key-command cells, input modes/animation, original Quick Start and MIT text viewer; native Windows dialog/tab borders differ. |
| Film alignment and session arrangement | VideoPreviewForm / LauncherPanel | Actual FFmpeg frames and audio, existing functional launcher. Full-rate movie editing and full Live Loops behavior are not claimed. |

Actual GUI captures are delivered in outputs/verification/v8 and selected release images. They are captures of the Windows application, not generated mockups. One 1920×1080 monitor at Windows 120 DPI was available. Workspace scaling is distinct from physical monitor-DPI testing.

The result follows the reference hierarchy and improves actual editing behavior. Pixel identity, all screens, the complete Logic 12/13 feature set and commercial-DAW equivalence are NOT VERIFIED / PARTIAL, rather than PASS.

## Researched workflow sources

- [Apple: note selection](https://support.apple.com/en-tm/guide/logicpro/lgcpa906bc30/10.7/mac/11.0): pointer marquee, Shift selection and same-pitch keyboard selection.
- [Apple: assign tools](https://support.apple.com/en-sg/guide/logicpro/lgcp5b365bf4/10.7/mac/11.0): primary and Command-click secondary tools. Command is adapted to Ctrl; the default Slop secondary tool is Marquee.
- [Apple: common window keys](https://support.apple.com/en-sg/guide/logicpro/lgcp665bbccc/mac): vertical/horizontal zoom and shared clipboard.
- [Apple: region/event keys](https://support.apple.com/en-is/guide/logicpro/lgcpe8314fe2/mac): Alt-adapted transpose/nudge/length, repeat and split.
- [Apple: Chord track](https://support.apple.com/nl-nl/guide/logicpro/lgcp2633963f/mac), [Sampler](https://support.apple.com/en-ca/guide/logicpro/lgsifc860d24/mac), [iPad selection](https://support.apple.com/guide/logicpro-ipad/select-notes-lpip83b30b30/ipados).
- [Ableton: audio and MIDI take lanes](https://www.ableton.com/en/manual/comping/): informed the extension of comp playback to MIDI; no Ableton UI was substituted.
- [Image-Line: MIDI import](https://www.image-line.com/fl-studio-learning/fl-studio-online-manual/html/automation_midiimport.htm): informed explicit project vs editor import and instrument audition.
- [Cubase official guide](https://www.steinberg.help/r/cubase-pro/15.0/en): chord/video/input-monitor workflow coverage; Slop's implementation remains limited.
- [REAPER official user guide](https://www.reaper.fm/userguide.php): recording/edit/mix and task-oriented help baseline.
- [Fender Studio Pro manual access](https://support.presonus.com/hc/en-us/articles/42529985007501-Where-To-Find-the-Fender-Studio-Pro-Manual): official manual requires installation. No comprehensive inspection of that manual is claimed.
- [Microsoft: WAVEFORMATEXTENSIBLE](https://learn.microsoft.com/en-us/windows/win32/api/mmreg/ns-mmreg-waveformatextensible), [gesture configuration](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setgestureconfig): PCM validation and native gesture routing.
