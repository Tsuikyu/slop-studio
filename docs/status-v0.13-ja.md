# Slop Studio 0.13 — Implementation and Verification Ledger

Target: Windows 11 x86-64, Release, CMake / Ninja / LLVM-MinGW UCRT, .NET Framework 4.8. The ledger distinguishes device-specific and physical-audio verification and does not mark unimplemented features complete. The priorities and difficulty assessment from before work are in `plan-v0.13-ja.md`.

| Requirement | Status | Implementation, verification, and limits |
|---|---|---|
| Instrument icons | PASS | Changed instrument icons in the library and track headers and the instrument slot to a shared blue-purple accent. Verified rendered colors and existing operations with Windows GUI tests. |
| Master control in upper-right | PASS | Connected an angular purple slider to a master DSP snapshot. Verified with Windows mouse messages, mixer display, and PCM amplitude ratio at -12 dB. |
| Playback/audio synchronization | PARTIAL | Separated submitted and completed audio frames; loop, seek, tempo change, and PDC use frame history. Launcher uses its own clock independent of the arrangement loop. Verified with synthetic PCM and simulated latency. No loopback/listening test measured actual acoustic output against the displayed position. ASIO and WASAPI report latency differently across drivers; a complete fix across all devices is unverified. |
| MIDI export | PASS | Re-imported and verified SMF format 1, multiple tracks, tempo, meter, positions, note lengths, pitches, and velocities. Complete exchange of MIDI CC and tempo maps is out of scope. |
| Piano roll | PARTIAL | Added a velocity lane for selected notes. Bars at beat starts can also be dragged; verified real GUI interaction and Undo. Editing across many MIDI controllers and a fully equivalent editing workflow are not implemented. |
| Sample search, bookmarks, and favorites | PASS | Saves path references without moving source files. Tested adding, removing, restart-like restoration, and search. Search is limited to the current folder or favorites and is not recursive. |
| Launchpad | PARTIAL / NOT VERIFIED | Implemented the first 8 tracks × 8 scenes, launch, LED colors, and separate MIDI port selection for Launchpad X / Mini MK3 Programmer mode. Synthetic tests use MIDI mappings based on official Novation documentation. No hardware was available. Page navigation and other models are unsupported/unverified. The device must be switched to Programmer mode. |
| Automatic sample BPM detection and sync | NOT IMPLEMENTED | Filename-based detection, per-sample settings, later editing, and following project tempo are not implemented. The existing waveform editor has manual timing tools, but they are not a substitute. |
| Project metadata and audio tags | PASS | Saves title, artist, genre, comments, and website. Verified WAV RIFF INFO and MP3 tags via external FFmpeg/libmp3lame using `ffprobe`. FFmpeg is not bundled. |
| Protect templates and included examples | PASS | Opening `.sloptemplate` or an included item from `Examples` creates a new-copy workflow. Normal saving requires a different name, and application save-path validation rejects overwriting the original. Audio uses path references; this does not prevent external applications from changing source files. |
| Undo history limit | PASS | Tested Settings range 1–10,000, persistence/restoration, pruning older history, and saved-state tracking. |
| Themes | PARTIAL | Implemented 12 shared color settings, color selection, `.sloptheme` save/load, and applying after restart. Verified GUI and settings reload. Some existing custom drawing and plugin windows still use fixed colors. Contrast depends on user choices. |
| Sample projects | PARTIAL | Saved/reloaded original MIDI and bundled instruments for 128 BPM Melodic House and 150 BPM J-pop projects; verified finite PCM values and audio generation. The EDM drop's four-second measurement changed from -32.3 LUFS to -12.4 LUFS, with -1.2 dBFS true peak. Full-project listening and quality on professional/club systems were not evaluated. |
| Shortcut profiles | PARTIAL | Verified key assignments for existing commands, duplicate detection, `.slopkeys` save/load, settings persistence, GUI display, and execution. No authoritative preset from another DAW is bundled. Unimplemented commands and some editor-specific keys are not covered. |
| Track freeze | NOT IMPLEMENTED | Safe freeze/unfreeze that restores original MIDI and plugin state is not implemented. |
| Stems | PARTIAL | Individual outputs are saved to a new folder. Fixed silence caused by muting the summing destination; verified bus routing and sibling-track separation with synthetic audio. Each stem passes through master processing individually, so their sum is not guaranteed to match with nonlinear FX. Summing buses and their child tracks together duplicates content. |
| Fullscreen | PASS | F11 uses the full screen rectangle and topmost state; pressing F11 again restores the previous size and window state. Verified with Windows GUI tests. Multiple physical monitors and mixed DPI are unverified. |
| OSC | FAIL / NOT VERIFIED | Local UDP receive timed out in CTest. A separate plain UDP loopback also failed; communication could not be confirmed in this environment. The cause is not conclusively attributed to the environment, and VRChat communication is unverified. |
| VST3 / CLAP | PARTIAL | Verified DSP, sample-accurate automation, and isolation of a faulty worker using existing test plugins. Compatibility with every vendor and uninterrupted audio during actual faults are not guaranteed. |

## Tests Run

- `V13CoreTests.exe`: 13 items covering synthetic audio/MIDI, stems, saving, profiles, Launchpad mapping, and related behavior. With `SLOPSTUDIO_TEST_MP3=1`, one additional item using external FFmpeg also passed.
- `V13GuiTests.exe`: Nine Windows-window tests covering master control, piano roll, sample browser, fullscreen, templates, custom keys, Launchpad settings, themes, and instrument icons.
- `V12GuiTests.exe`: 11 items; `V9GuiTests.exe`: 12 items passed. These used controlled Windows messages; they do not cover all physical mouse and tablet gestures.
- CTest: 9 of 10 suites passed; one OSC item in `StudioIntegrationTests` failed (the other 19 items in that suite passed).
- Normal launch of final distribution EXE: window appeared and responded. Self-diagnostics passed 15 items each in Japanese, English, German, and Korean. Two items each were NOT RUN because the distribution excludes diagnostic VST3/CLAP plugins; those plugins were tested in a separate test build.
- Extracted distribution ZIP to another folder and confirmed normal startup and responsiveness. Extracted the source ZIP to another folder, rebuilt Release with CMake / Ninja, and passed 15 Japanese self-diagnostic items; two diagnostic plugin items were NOT RUN.
- FL Studio ASIO: about eight seconds of callbacks at 48 kHz with silent output; zero underruns. WASAPI: confirmed silent frame progression on the selected virtual output. Other devices, listening to real music, recording quality, and the gap between displayed position and acoustic output are unverified.
- Sample loudness was measured over four seconds using FFmpeg EBU R128; this is not a full-song, sound-quality, or club playback assessment.

## Reference Material

The Launchpad interface is based on [Novation MIDI documentation](https://userguides.novationmusic.com/hc/en-gb/articles/24001489325330-MIDI-on-Launchpad-X), and pad/LED MIDI numbers are based on [Novation surface-control documentation](https://userguides.novationmusic.com/hc/en-gb/articles/24001475492498-Controlling-the-Launchpad-X-surface).

## Usage Notice

This development build is not complete at the level of commercial DAWs. Compatibility with every plugin or ASIO device, mixed-DPI multi-monitor behavior, Debian, and uninterrupted audio during all failures is not guaranteed. Third-party instruments, external FFmpeg, and user-created media are not included. Rights required for public redistribution and MIT publication of the startup image have not been verified. Notices and source for SDKs outside the MIT-licensed application and the separate-process GPL companion are included in the distribution.
