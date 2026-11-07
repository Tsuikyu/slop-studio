# Slop Studio 0.10 — Feature and Verification Ledger

Windows 11 x64, 2026-10-29. PASS applies only to the stated test scope. It does not mean every specification item is complete.

| Item | Status | Verification and remaining work |
|---|---|---|
| Windows x64 Release | PASS | Built an independent distribution folder with CMake / Ninja / Clang 23.1.4, opened First Light from the executable, and confirmed normal shutdown. |
| Startup screen | PASS | Loads the supplied image at runtime and updates the bar and text for each startup stage. Verified by capturing the actual screen and observing the updates. Public redistribution rights for the image have not been confirmed. |
| `.slopdaw` | PASS | Tested create, edit, save, reload, schema validation, rejection of unknown versions, atomic replacement, and backups. Complete media collection and path relativization are not implemented. |
| Minimal CyberSquare | PARTIAL | Verified the dark UI, purple accent, square controls, tracks, and mixer on screen. Work remains on all dialogs, DPI levels, and screen sizes. |
| Master FX | PASS | Verified ordering of a real CLAP test plugin and built-in FX, PCM changes from processing order, bypass, save, and reload. Compatibility with every product is unverified. |
| Playback, recording, MIDI | PARTIAL | Confirmed PCM recording through WASAPI virtual loopback, synthetic MIDI events, playback, stop, and save in GUI smoke tests. No performance/recording with physical microphones or keyboards and no listening test were done. |
| ASIO | PARTIAL | FL Studio ASIO passed 16 seconds of continuous playback, pan changes, seeks, 44.1/48 kHz switching, and reconnection after an application-owned process fault. Other drivers, physical MiniFuse connectivity, and listening tests are unverified. |
| VST3 / CLAP | PARTIAL | Verified discovery, creation, parameters, processing, and state saving with fixtures based on the official specifications. All vendors, all features, and commercial instrument products were not tested. |
| OSC | FAIL | The local UDP round-trip receive test failed. A simple UDP loopback also failed in the same environment; the cause is unknown. A real VRChat round trip was not performed. |
| MCP | PARTIAL | Verified standard I/O startup and 11 tools; editing, saving, and playback are denied by default in read-only mode and work after permission is granted, with Undo. Persistent audit, fine-grained permissions, and formal interoperability/safety are incomplete. |
| Menu catalog | PARTIAL | Recorded all 424 specified items with identifiers and mapped 54 to the closest existing commands/handlers. Complete semantic, display, or translation parity is not claimed. |
| Four languages | PARTIAL | GUI smoke tests passed 17 items each for `ja-JP`, `en-US`, `de-DE`, and `ko-KR`. Full translations and language quality across all screens are unverified. |
| Tests | PARTIAL | Six of seven CTest suites passed. `StudioIntegrationTests` passed 19 items and failed one OSC item. Twelve GUI items and MCP real-process tests passed. |
| Publication and licensing | PARTIAL | Includes the application's MIT license, third-party notices, and separate GPL companion source. The rights and public redistribution permission for the supplied startup image are unverified. No GitHub publication or legal assurance was performed. |
| Debian | NOT RUN | The current standalone build is Windows-only. |

## Build Used

```powershell
$root = (Get-Location).Path.Replace('\','/')
$cmake = "$root/work/tools/cmake/cmake-4.4.4-windows-x86_64/bin/cmake.exe"
$source = "$root/outputs/slop-studio-source"
$build = "$root/work/slop-studio/release-build"
$dist = "$root/outputs/dist/slop-studio-windows-x64-release"
$ninja = "$root/work/tools/ninja/ninja.exe"
$llvm = "$root/work/tools/llvm-mingw/llvm-mingw-20261020-ucrt-x86_64"
& $cmake -S $source -B $build -G Ninja -DCMAKE_BUILD_TYPE=Release "-DCMAKE_MAKE_PROGRAM=$ninja" "-DCMAKE_TOOLCHAIN_FILE=$source/cmake/llvm-mingw.cmake" "-DLLVM_MINGW_ROOT=$llvm" "-DSLOPSTUDIO_PACKAGE_DIR=$dist" -DBUILD_TESTING=OFF
& $cmake --build $build --config Release --parallel 3
```

The test build used `work/slop-studio/build` and wrote to `outputs/dist/slop-studio-windows-x64`. `ctest --test-dir work/slop-studio/build -C Release --output-on-failure` was run. GUI and ASIO checks used dedicated test profiles and test projects.

## Next Priorities

Continue isolating the OSC receive failure, verify with physical MIDI/audio/ASIO hardware, fix missing menu commands and translations, improve media collection and format documentation, review plugin/MCP permissions and safety, and refine the whole interface. Full commercial DAW compatibility and production-grade reliability have not been achieved.
