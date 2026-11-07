# Slop Studio — Windows x64 Build and Verification

Requirements: Windows 11 x64, the .NET Framework 4.8 `Framework64` C# compiler, CMake 3.24 or later, Ninja, and LLVM-MinGW UCRT x64 (or the Visual Studio x64 C++ toolchain). JUCE is not used. See `sdk-revisions.txt` for the pinned SDK revisions.

## Release Build

Open PowerShell in the source root and adjust the tool paths for your environment.

```powershell
$cmake = 'C:/Tools/cmake/bin/cmake.exe'
& $cmake -S . -B build/release -G Ninja `
  -DCMAKE_BUILD_TYPE=Release `
  -DCMAKE_MAKE_PROGRAM=C:/Tools/ninja/ninja.exe `
  -DCMAKE_TOOLCHAIN_FILE=cmake/llvm-mingw.cmake `
  -DLLVM_MINGW_ROOT=C:/Tools/llvm-mingw `
  -DSLOPSTUDIO_PACKAGE_DIR=dist/windows-x64 `
  -DBUILD_TESTING=OFF
& $cmake --build build/release --config Release --parallel 3
```

Distribute `SlopStudio.exe`, the DLLs, `Resources/Startup.png`, `locales`, `docs`, `Examples`, `licenses`, `Companions`, and the accompanying notices together. The .NET and UCRT runtimes supplied by the operating system are not bundled. FFmpeg is an optional external dependency.

## Test Build

Use separate build and output directories, set `-DBUILD_TESTING=ON`, and run the matching `ctest.exe --test-dir build/test -C Release --output-on-failure`. Use an isolated test profile for GUI and fixture tests; do not use a user's in-progress production projects.

```powershell
dist/test/V9GuiTests.exe verification/gui
dist/test/V11GuiTests.exe verification/gui-v11
dist/test/V11CoreTests.exe verification/core-v11
dist/test/V12CoreTests.exe verification/core-v12
dist/test/V12GuiTests.exe verification/gui-v12
dist/test/V13CoreTests.exe verification/core-v13
dist/test/V13GuiTests.exe verification/gui-v13
dist/test/SlopStudio.exe --smoke verification/app-ja.txt ja-JP
python tests/McpProcessTests.py dist/test/SlopStudio.exe verification/mcp
```

GUI smoke tests accept `ja-JP`, `en-US`, `de-DE`, and `ko-KR`. Virtual loopback and synthetic MIDI do not prove performance or recording with physical devices. Record automated results separately from manual checks that were not performed.

The latest results are recorded in `status-v0.13-ja.md`. A `build.ps1` script is also provided, but `-SkipTests` only skips test execution; it does not mean that tests passed.
