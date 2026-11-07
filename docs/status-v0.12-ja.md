# Slop Studio 0.12 — Changes and Verification

Target: Windows 11 x86-64, Release. Built with CMake / Ninja / LLVM-MinGW UCRT and the .NET Framework 4.8 C# compiler. Includes the application and separate-process GPL-3.0-only ASIO / Ableton Link companion. Author credit is Tsuikyu. The disclaimer and Codex attribution remain.

## Changes in This Version

- **PASS (synthetic signal and GUI):** Opened actual WAV export from File → Master with Ctrl+B. Tested 16/24-bit PCM, 32-bit float, and master effects enabled/disabled. Added reference-file waveforms and audition through the Windows default output.
- **PARTIAL:** The three operations to render a region/track at its original position and render/replace all tracks are not implemented and remain disabled. Synchronized A/B comparison with reference audio and audition through the DAW-selected ASIO output are also not implemented.
- **PASS (real VST3 and processing data):** Fixed an issue that sent large numbers of Note Off events to all notes that were not playing during seek and loop operations. With the user-provided project and local Nexus VST3, 3,668 of 3,750 blocks produced audio during approximately 40 seconds of processing at 48 kHz/512 frames; playback continued after looping and the processing worker reported zero missed blocks. Listening through an actual ASIO driver and speakers was not verified.
- **PARTIAL (real FLP):** Recognizes the 88-byte playlist layout. Converted, saved, and reloaded the supplied FLP: 46 tracks, 952 regions, and 20,412 notes. The original files for 370 audio regions (11 media types) were not found, so those regions retain their positions and references as silent `[Missing]` regions. WAV relinking and Undo are available from the right-click menu. Original instruments/effects, automation, routing, trim, stretch, and slide notes are not reconstructed. Review the import report and missing media.
- **PASS (GUI and saving):** Updated Folder Stack / Mix Stack, Waveform Editor, and the Import Video label. Added script registration/selected execution/deletion, controller button assignments, track and region background/text colors, and mouse-wheel/vertical-drag inspector values. Colors and assignments are saved.
- **PASS (GUI) / NOT VERIFIED (hardware):** Added a driver settings button when ASIO is selected and calls to an existing driver panel or a driver panel in a separate process. Verified rejection of an invalid CLSID and invocation from Settings. Actual driver configuration UI, physical MIDI keyboards, microphones, and controller operation were not verified.
- **PASS (GUI):** Updated Slop Launcher with original black and purple components. Added decoded waveforms and auditioning to the sample browser. Fixed Japanese text clipping in the sample browser and About dialog and increased the inspector's initial width. Text clipping has not been ruled out for every language and resolution.

## Verification Results

- `V12CoreTests.exe`: 9/9. Includes 88-byte FLP layout, preserving/relinking/Undo for missing audio, MIDI note stop handling, master signal, colors, controllers, Lua, and invalid ASIO inputs.
- `V12GuiTests.exe`: 11/11. Master menu, labels, Launcher, colors, numeric dragging, waveform, ASIO button, controller screen, Lua/reference audio screen, About, and audio relink menu.
- `V11GuiTests.exe`: 6/6. Dark menus, Compressor, EQ, Sampler, VST3/CLAP preset screen, and Japanese.
- `V9GuiTests.exe`: 13/13. Startup, master FX, MCP settings, piano roll, loop, region merge, insert order, stacks, and others.
- App `--smoke`: 17 items passed for each of `en-US`, `ja-JP`, `de-DE`, and `ko-KR`. Includes display, MIDI editing, synthetic signals, save and reload. Does not prove physical device operation or listening results.
- CTest: 8 of 9 suites passed. The local OSC UDP receive item in `StudioIntegrationTests` failed (the other 19 items passed). A separate raw UDP loopback also timed out after sending. OSC/VRChat communication could not be verified in this environment.
- GUI tests used controlled Windows messages. Real mouse/gamepad operation and DPI on different physical monitors were not verified.

## Specification and Distribution Limitations

The full v5.1 menu specification is not implemented. Compatibility with every FLP and third-party plugin, all ASIO/WASAPI devices, uninterrupted output during every fault, and a Debian build are not guaranteed. Projects use `.slopdaw`.

The existing copyright notices in the MIT license were retained and new author attribution was added. The old name present in the prior notice remains as a rights notice. The startup image was supplied by the requester; rights required for public redistribution and MIT publication have not been verified. Notices for third-party components and the GPL companion are retained. No GitHub publication was performed.
