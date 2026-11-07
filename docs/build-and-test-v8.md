# Reproducible Windows build — 0.8

Requirements: Windows 11 x86-64, .NET Framework 4.8+, CMake 3.24+, Ninja, LLVM-MinGW x86_64. Official VST3/CLAP/ARA/ASIO/Link and Lua sources with notices are included in this source distribution. JUCE is not used.

```powershell
./build.ps1 -BuildDirectory build/windows-x64 -OutputDirectory dist/windows-x64 -CmakePath <cmake.exe> -NinjaPath <ninja.exe> -LlvmMingwRoot <llvm-mingw-x86_64> -SkipTests
cmake --build build/windows-x64 --parallel 3
ctest --test-dir build/windows-x64 --output-on-failure
./dist/windows-x64/SlopStudio.exe
```

CMake builds all test executables. V8GuiTests.exe <new-report-folder> [movie-fixture-folder] runs the application on Windows using own-window messages/captures. V8CoreTests.exe <new-report-folder> [movie-fixture-folder] tests pure/model/PCM/import paths and available installed fixtures. Optional movie fixtures are fixture.mp4, fixture.mov, fixture-float.wav. FFmpeg can generate synthetic testsrc2 plus sine input; no copyrighted assets are needed. V8BackendTests.exe <report.txt> tests actual installed FL Studio ASIO and WASAPI. Its optional filter and third CLSID argument select a separate driver test. Never report missing hardware as passed.

Actual delivery commands, clean ZIP hashes and runtime status are in the separately delivered Engineering/Delivery reports. Source ZIP is rebuilt from a clean extraction. Test fixtures/tests are excluded from the production runtime. Current OSC UDP receive test fails in the Codex environment; CTest is not universally green.

The Windows smoke test also validates that the main menu and painted transport have sufficient physical height at 150% UI scaling. The production window was visually inspected at the test machine’s 120 DPI; this does not verify multi-monitor DPI changes.
