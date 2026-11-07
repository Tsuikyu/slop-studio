# Third-party notices — Slop Studio 0.13

The MIT license applies to the original application, native VST3/CLAP host and WASAPI bridge, original UI/assets and example music, except for separately licensed third-party and companion files below. No Apple screenshot, logo, icon, font, sample library or application binary is embedded. This is an independent Windows application.

| Distributed component | Upstream / provenance | Applicable notice |
|---|---|---|
| Startup artwork | Provided by the requester; see resources/STARTUP-ART-NOTICE.md | Public redistribution / MIT licensing not independently verified |
| VST3 interface headers | official Steinberg repositories; sdk-revisions.txt | MIT; licenses/vst3-sdk.txt and vst3-pluginterfaces.txt |
| Lua 5.4.8 / SlopControl.dll | Official lua/lua sources, tag v5.4.8; per-file hashes included | MIT, copyright 1994–2025 Lua.org, PUC-Rio; licenses/lua.txt |
| ARA API 2.3 | Official Celemony/ARA_API at pinned commit in sdk-revisions.txt | Apache License 2.0, Celemony Software GmbH; licenses/ara-api.txt |
| CLAP headers | official free-audio/clap; sdk-revisions.txt | MIT; licenses/clap.txt |
| SlopBridge.exe / companions/SlopBridge.cpp | Original independent optional ASIO/Link companion | GPL-3.0-only; Companions/LICENSE and complete corresponding source in Companions/Source |
| ASIO SDK 2.3.4 headers | official https://www.steinberg.net/asiosdk archive; SHA-256 in sdk-revisions.txt | GPL v3 alternative selected, not the proprietary license; licenses/asio-sdk.txt and GPL-3.0.txt |
| Ableton Link | official https://github.com/ableton/link at pinned revision | GPL v2 or later, combined companion distributed under GPL v3; licenses/ableton-link.txt |
| asio-standalone headers used by Link | official https://github.com/chriskohlhoff/asio | Boost Software License 1.0; licenses/asio-standalone.txt |
| DAWproject schema (test fixture) | https://github.com/bitwig/dawproject | MIT, copyright 2020 Bitwig; licenses/dawproject.txt |
| LLVM C++/compiler/unwind runtimes | llvm-mingw 20261020, Clang 23.1.4 | Apache 2.0 with LLVM exceptions / upstream notices; licenses/llvm-runtimes.txt |
| MinGW-w64 startup/runtime portions | llvm-mingw UCRT toolchain | Upstream notice collections in licenses/mingw-w64.txt and mingw-runtime-COPYING.MinGW-w64-runtime.txt |
| Windows/.NET Framework/UCRT | Target operating system | Microsoft system components; not redistributed |

ASIO is a registered trademark of Steinberg Media Technologies GmbH. Ableton Link is referenced as the official synchronization technology. No Steinberg or Ableton branding artwork is used.

SlopNative.dll, SlopAudio.dll and SlopControl.dll do not link to ASIO or Ableton Link code. The MIT application communicates with the independent companion by the documented shared-memory protocol in companions/README.md. The companion can be built and used independently. Its corresponding source, SDK headers, notices and build file are supplied alongside the binary; no network download is necessary to inspect that source.

Third-party plugin binaries are not redistributed. In the preceding 0.11 release the locally installed Ozone 11 Elements, SerumFX and Serum 2 VST3 instances were individually tested for editor attachment, host preset state, application insertion, synthetic PCM/MIDI processing and project state restoration. In earlier testing, Vital was tested from the existing installation; Melodyne ARA and Synthesizer V Studio 2 ARA were tested in limited scenarios, but those tests were not repeated for this release. Their binaries, voices, artwork and licenses are not redistributed. Softube Saturation Knob returned a null VST3 factory in an earlier test; it was not retested. No activation circumvention or replacement plugin was used. Original TestFixtures are ABI/fault fixtures, clearly named as verification tools and not substituted for third-party instances.

ALS and FLP importers are original limited implementations. FLP binary layout facts were checked against the parser author's primary source at https://github.com/demberto/PyFLP ; no PyFLP implementation is copied or linked. Apple keyboard documentation is used as a factual mapping reference. Coverage and adaptations are recorded, not presented as a complete Logic preset.

CMake, Ninja, LLVM toolchain archives, Apple reference images and other applications are not included as runtime assets. The full compiler/runtime notice collections are retained. This inventory records the actual distribution and does not certify universal plugin compatibility.
