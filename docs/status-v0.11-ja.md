# Slop Studio 0.11 — Changes and Verification

Windows 11 x86-64, 2026-10-29. PASS below applies only to the stated test conditions. It does not guarantee behavior with every installed product version or device.

| Item | Status | Changes and results |
|---|---|---|
| Text in dark menus | PASS | Active items are drawn in bright text and disabled items in distinguishable gray. The actual Japanese menu image and pixels were checked in a GUI test. |
| Number of menu items | PARTIAL | Added seven settings categories and existing edit, track, mix, and view commands to menus. The full requested catalog and all features from other DAWs are not implemented. |
| Ozone 11 Elements / SerumFX | PASS | On this PC, verified discovery, insertion, native editor display, preset save/restore, synthetic PCM processing, and project restore with actual VST3 plugins. No speaker listening test was performed. |
| Serum 2 | PASS | Verified insertion, editor display, presets, playback from synthetic MIDI, and project restore with the actual VST3 instrument on this PC. No physical keyboard was used. |
| Third-party plugins in general | PARTIAL | Fixed and fixture-tested VST3 initialization return handling, controller connection order, changing state sizes, and editor display during processing. Not every product or version was tested. |
| Compressor | PASS | Verified knob changes reach the active DSP instance and change synthetic PCM output, along with Undo/Redo and UI refresh. No subjective listening assessment was performed. |
| Preset bar | PARTIAL | Added `.sloppreset` save/load for built-in FX, Sampler, VST3, and CLAP; in-session copy/paste between the same plugin type; and comparison with the state when the editor opened. Native preset formats of external plugins are unsupported. Sampler media are references and are not embedded. |
| Sampler ADSR | PASS | GUI tests passed for changing and saving/restoring all four parameters using knobs and numeric entry. Recording from physical inputs was not verified. |
| Visual Equalizer | PARTIAL | GUI tests passed for displaying real five-band filter responses on an original dark graph and changing frequency/gain by dragging points. A real-time spectrum display and the reference's exact band configuration are not implemented. |
| Regression of earlier features | PARTIAL | Twelve existing V9 GUI tests and 17 self-diagnostic items per language passed across all four languages. This does not cover every existing feature or physical device. |
| Automated tests | PARTIAL | Seven of eight CTest suites passed. `StudioIntegrationTests` passed 19 of 20 items; one local OSC UDP receive test failed. Eight V11 core and six GUI items passed. |
| Physical devices / ASIO | NOT RUN | This release was not tested using physical speakers, microphones, MIDI keyboards, or ASIO hardware. WASAPI virtual-loopback recording passed in four-language self-diagnostics. |
| Debian / all DPI settings | NOT RUN | Not tested outside Windows or when switching per-monitor DPI. |
| Publication and licensing | PARTIAL | Includes the application's MIT license, third-party notices, and the separate GPL companion and its source. Public redistribution rights for the supplied startup image have not been verified. |

## Causes and Fixes

- The system menu renderer drew dark text on dark backgrounds. Menu items, disabled states, and arrows now share an original renderer.
- Some VST3s returned a valid unimplemented response for `setProcessing`, which had been treated as failure. Explicit rejection remains an error. The MIDI support query was also moved until after the controller connects. Official specification: <https://steinbergmedia.github.io/vst3_dev_portal/pages/Technical%2BDocumentation/API%2BDocumentation/Index.html>.
- Audio processing and plugin editor creation could hold the same lock and wait on each other. The editor can now be opened during processing, while state restoration and destruction remain mutually exclusive. This does not guarantee fault isolation or compatibility with every vendor.
- Editing a built-in FX previously rebuilt the processing graph. Parameter changes now update the active processor and preserve its state.
- Interaction was designed after analyzing the structure of reference images. No vendor images or icons were included. The plugin settings workflow reference link was removed from this historical report.

## Local Release Build

```powershell
$root = (Get-Location).Path.Replace('\','/')
$cmake = "$root/work/tools/cmake/cmake-4.4.4-windows-x86_64/bin/cmake.exe"
$source = "$root/outputs/slop-studio-0.11-source"
$build = "$root/work/slop-studio-011/release-build"
$dist = "$root/outputs/dist/slop-studio-0.11-windows-x64"
$ninja = "$root/work/tools/ninja/ninja.exe"
$llvm = "$root/work/tools/llvm-mingw/llvm-mingw-20261020-ucrt-x86_64"
& $cmake -S $source -B $build -G Ninja -DCMAKE_BUILD_TYPE=Release "-DCMAKE_MAKE_PROGRAM=$ninja" "-DCMAKE_TOOLCHAIN_FILE=$source/cmake/llvm-mingw.cmake" "-DLLVM_MINGW_ROOT=$llvm" "-DSLOPSTUDIO_PACKAGE_DIR=$dist" -DBUILD_TESTING=OFF
& $cmake --build $build --config Release --parallel 3
```

For a test build, use a separate folder with the same settings and `-DBUILD_TESTING=ON`. See `build-and-test.md` for general dependencies and instructions.
