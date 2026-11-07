# Independent SlopBridge companion — GPL v3

SlopBridge.exe is an optional Windows x86-64 executable, licensed GPL-3.0-only. It uses the official ASIO SDK under its GPLv3 alternative, Ableton Link under GPLv2-or-later and asio-standalone under BSL 1.0. The application, SlopNative.dll and SlopAudio.dll do not link to these libraries. LICENSE and corresponding source are distributed beside the executable.

Build independently with CMake 3.24+, C++17 and a Windows x64 compiler:

    cmake -S Companions/Source -B build/bridge -G "Visual Studio 17 2022" -A x64
    cmake --build build/bridge --config Release

LLVM-MinGW/Ninja is also supported (the top-level toolchain file supplies the compiler). The tested compiler was Clang 23.1.4, llvm-mingw 20261020, static C++ runtime. No ASIO driver is included; the user selects a separately installed x64 driver.

## Public process protocol

Commands:

    SlopBridge.exe --asio <mapping-name> <driver-CLSID> <sample-rate> <buffer-frames>
    SlopBridge.exe --link <mapping-name>

The client creates a named read/write 2 MiB mapping and auto-reset events named <mapping-name>-request and <mapping-name>-response. Data uses little-endian Windows fixed-width fields, interleaved IEEE float audio, and a random mapping name. Offset 80 contains the client's PID; a watchdog exits when the parent dies. This is process fault containment, not an OS security sandbox.

Common fields: int32 status at 0 (0 starting, 1 ready, -1 error), int32 stop at 28, int32 command-pending at 44, UTF-8 error text at 128 (1900-byte capacity).

ASIO: int32 input/output channels at 4/8; block frames at 12; sample rate at 16; input/output latency samples at 20/24; int64 completed frame count at 32; int32 control-panel request at 44; underrun count at 48; driver-reset/rate-change flag at 52. Input float audio starts at 4096 with up to 32 channels, output float stereo starts at 4096 + 8192*32*4. The driver callback signals request after writing input. The client renders a block and signals response. After 80 ms without a response the companion writes silence. Only the first two output channels are used. Unsupported channel sample formats, rates and buffer sizes return explicit errors.

Link: int32 peers at 4; float64 tempo/beat at 8/16; int32 playing at 40. To publish local tempo/start-stop, write float64 tempo at 64, int32 playing at 72 and set command-pending at 44. The Link session's beat is exposed; the current DAW integration follows tempo/start-stop and does not yet implement complete sample-clock phase following.

Third-party clients may implement this documented protocol independently. No C# host types or UI resources are needed to build or use this executable.
