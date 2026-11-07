# Slop Studio — Development Handoff

**Project:** Slop Studio  
**Development version:** 0.13  
**Author:** Tsuikyu  
**Primary platform:** Windows 11 x64  
**Inspection date:** 2026-11-05  
**Previous verification records:** 2026-10-30

## 1. Workspace and Source Identification

The latest source tree identified during the previous inspection is:

`outputs/slop-studio-0.13-source/`

This directory is the project root referenced throughout this document.

The workspace also contains older source trees, including `source/`, version 0.11, version 0.12, and previously generated artifacts.

**Use `outputs/slop-studio-0.13-source/` as the initial development target. Verify the directory contents before modifying files.**

Do not confuse older source trees or generated build outputs with the current development source.

### Existing Artifacts

| Artifact | Location |
|---|---|
| Development source | `outputs/slop-studio-0.13-source/` |
| Windows x64 distribution | `outputs/dist/slop-studio-0.13-windows-x64/` |
| Source archive | `outputs/slop-studio-0.13-source.zip` |
| Build verification report | `outputs/slop-studio-0.13-build-verification-ja.md` |

The Git repository state was not verified during the previous inspection.

Inspect the current branch, working tree, and uncommitted changes before development.

Changes to the source directory do not automatically update the source ZIP or distribution directory.

Do not overwrite existing release artifacts unless explicitly requested.

## 2. Development Requirements

Slop Studio is a digital audio workstation targeting Windows 11 x64.

The application uses the Minimal CyberSquare interface design.

### Project Configuration

- Native project format: `.slopdaw`
- Project template format: `.sloptemplate`
- Main application: C# / Windows Forms
- Framework: .NET Framework 4.8
- Native components: C++
- Build system: CMake
- Primary audio backend: WASAPI
- Plugin formats: VST3 / CLAP
- Scripting: Lua
- External control: OSC / MIDI / game controllers
- AI integration: stdio MCP

Do not reintroduce deprecated project formats.

Do not introduce JUCE or replace the existing application framework.

Windows 11 x64 remains the primary development target. Debian Linux is a future porting target.

### Required Documentation

Read the following files before modifying the implementation:

1. `AGENTS.md`
2. `docs/requirements/Slop_Studio_v5_Complete_Spec_JA.txt`
3. `docs/requirements/Slop_Studio_v5_Menu_Catalog.json`
4. `docs/status-v0.13-ja.md`
5. `docs/plan-v0.13-ja.md`
6. `docs/build-and-test.md`
7. `docs/menu-command-map.json`

Follow the latest explicit user instructions when they supersede older project requirements.

Verify implementation details against the actual source code and test results.

Maintain these localization targets:

- `ja-JP`
- `en-US`
- `de-DE`
- `ko-KR`

Maintain existing project saving, backups, Undo / Redo, command registration, and user data protection.

Do not mark untested functionality as verified.

Do not access the user's Downloads directory.

Do not publish, upload, transmit, push, or distribute project files without explicit authorization.

Do not perform physical recording or audio output tests without authorization.

## 3. Repository Structure

The following paths were identified in the previous source inspection.

### Build System

**`CMakeLists.txt` and `cmake/`**

- Windows x64 build configuration
- CMake
- C++17
- Ninja with LLVM-MinGW or MSVC
- .NET Framework 4.8 application build integration

The previous CMake configuration rejected non-Windows builds.

Do not assume cross-platform compilation is currently supported.

### Application Source

**`src/`**

Contains the application UI, project model, command handling, arrangement logic, and audio-related application components.

Relevant files:

| File | Primary responsibility |
|---|---|
| `Program.cs` | Application startup |
| `MainForm.cs` | Main interface and command handling |
| `ArrangementEngine.cs` | Arrangement and audio processing integration |
| `TransportHistory.cs` | Transport and playback position tracking |
| `ProjectModel.cs` | Project data and persistence |
| `ProjectTemplates.cs` | Project templates and related storage behavior |
| `KeyboardShortcuts.cs` | Keyboard command handling |
| `ShortcutProfiles.cs` | Shortcut profiles |
| `PianoRollEditor.cs` | MIDI piano roll |
| `SampleBrowser.cs` | Sample browsing |
| `Launchpad.cs` | Launchpad integration |
| `AudioExport.cs` | Audio exporting |

Inspect current implementations before modifying these files.

### Native Components

**`native/`**

Contains native components and integration layers for:

- WASAPI
- Plugin hosting
- Lua integration
- Native processing bridges

Keep real-time audio processing separate from GUI, file I/O, network I/O, and script execution.

### Companion Processes

**`companions/`**

Contains separate-process components for:

- ASIO
- Ableton Link

These components use GPL-3.0-only licensing.

Maintain separation between the main application and companion process implementations.

### Third-Party Components

**`third_party/`**

Contains external SDKs and dependencies, including components related to:

- VST3
- CLAP
- ARA
- Lua

License information is maintained in:

- `licenses/`
- `THIRD-PARTY-NOTICES.md`

The main application's original source follows the MIT license.

Preserve the applicable license conditions for each dependency.

### Localization and Documentation

**`locales/`**

Contains localization resources for the supported languages.

**`docs/manual/`**

Contains multilingual HTML manuals.

Maintain translation keys and documentation consistency when changing user-facing functionality.

### Resources and Examples

**`resources/`**

Contains application resources, including splash-screen assets.

The redistribution rights for the previously supplied splash-screen image have not been confirmed. Check asset permissions when preparing a public distribution.

**`examples/`**

Contains example projects, including:

- Melodic House — 128 BPM
- J-Pop — 150 BPM

Preserve the example projects and referenced audio assets.

### Tests

**`tests/`**

Contains test implementations and fixtures for:

- Synthetic audio signals
- GUI behavior
- Plugin fixtures
- Version-specific regression tests

Use existing test infrastructure before creating additional test frameworks.

## 4. Development Toolchain

The previous workspace included the following tools under:

`work/tools/`

- CMake
- Ninja
- LLVM-MinGW

These tools may not exist in a different environment.

Check tool availability before attempting a build.

The `work/` directory contains verification artifacts and development tools. It is not the application source tree.

Use the commands in `docs/build-and-test.md` for reproducible builds.

Do not assume paths or tool availability from the previous environment.

## 5. Existing Implementation

The following functionality was identified in the version 0.13 development records:

### Core Production

- Audio playback
- Audio recording
- MIDI processing
- Arrangement editing
- Piano roll
- Sample browser
- Sampler
- Built-in effects
- VST3 / CLAP hosting
- Track inserts
- Mixer
- Track stacks
- Automation
- Slop Launcher

### Project and Application Features

- Project templates
- Configurable Undo history limit
- Standard MIDI File export
- WAV / MP3 metadata tagging
- Keyboard shortcut profiles
- Theme profiles
- F11 fullscreen toggle

Implementation coverage and verification status vary by feature.

Use `docs/status-v0.13-ja.md` for detailed feature status.

The presence of implementation code does not establish successful runtime operation.

### External Dependencies

Some audio and video formats require an external FFmpeg installation.

MP3 encoding requires the appropriate FFmpeg configuration and libmp3lame support.

Audio assets are generally stored as filesystem references.

Project files do not automatically contain every referenced audio asset.

## 6. Known Limitations and Verification Status

The following information comes from previous version 0.13 records.

### Playback Synchronization

Playback position tracking was updated to use:

- Audio output frame history
- A separate Slop Launcher clock

Synchronization with physically audible output has not been verified.

Additional testing must account for:

- WASAPI device position
- ASIO device position
- Audio buffer latency
- Plugin delay compensation
- Transport seeking
- Looping
- Slop Launcher scheduling

Do not mark physical playback synchronization as verified without suitable measurement.

### ASIO

Previous records include:

- FL Studio ASIO callback operation at 48 kHz with silent audio buffers
- WASAPI virtual output testing

These results do not establish physical audio playback or compatibility with every ASIO driver.

The previous ASIO compatibility changes require additional verification.

### Launchpad

Target devices:

- Launchpad X
- Launchpad Mini MK3

Implementation scope:

- Programmer Mode
- 8 tracks × 8 scenes
- Explicit MIDI input and output selection

Synthetic MIDI mapping tests were performed.

Physical Launchpad hardware testing was not performed.

Do not substitute synthetic test results for hardware verification.

### Unimplemented Features

The following items were recorded as not implemented:

- Sample BPM estimation confirmation workflow
- Automatic sample tempo following
- Track freeze
- Track unfreeze
- Official external DAW keyboard shortcut presets

Additional rendering and track replacement functions may remain unavailable. Verify their current state against the status register.

### Stem Export

Existing stem export behavior has these limitations:

- Bus and child-track outputs may overlap.
- Nonlinear master processing may prevent stems from summing to the original mix.

Preserve existing routing behavior when changing stem export.

### Themes

Theme profiles are supported.

Some older custom interface components still contain fixed colors.

### External Project Import

FLP, ALS, and related import functionality has limited compatibility.

Do not assume complete restoration of:

- Instruments
- Plugin states
- Effects
- Automation
- Routing
- Audio trimming
- Time stretching
- Application-specific project data

### Platform and Device Compatibility

The following areas require additional verification:

- Physical MIDI devices
- Microphones
- ASIO hardware
- VST3 / CLAP plugin combinations
- Multiple-monitor DPI configurations
- Debian builds
- External project compatibility

## 7. Previous Test Results

The previous verification report was recorded on **2026-10-30**.

### CTest

| Result | Recorded value |
|---|---|
| Total test suites | 10 |
| Passed | 9 |
| Failed | 1 |
| Failed area | OSC UDP reception |

### OSC Failure

OSC UDP reception failed in the previous test environment.

A basic local UDP test also timed out.

The underlying cause was not established.

Potential investigation areas include:

- UDP socket initialization
- Address and port binding
- Local networking restrictions
- Firewall rules
- Test process lifecycle
- Message transmission and reception
- OSC message parsing

These are investigation targets, not confirmed causes.

Reproduce the failure before changing the implementation.

Record the environment and exact results.

### Audio Verification

Previous records report successful testing of:

- FL Studio ASIO 48 kHz silent callback
- WASAPI virtual output

These results do not establish verified physical audio output in the current environment.

### Example Project Testing

Previous example-project checks used four-second measurement intervals.

These measurements do not establish complete audio quality verification for entire projects.

### Current Verification State

**No builds or tests were rerun while preparing the original handoff on 2026-11-05.**

All results above refer to the previous verification records.

Run fresh tests before reporting current results.

Do not overwrite historical test records.

Append new results with their dates, commands, environments, and outcomes.

## 8. Development Priorities

Use `docs/plan-v0.13-ja.md` as the current development plan.

The following priorities were established in the previous handoff.

### P0 — Environment Inspection and Baseline Verification

1. Read `AGENTS.md` and the current development documentation.
2. Inspect the actual source tree.
3. Check installed build tools and dependencies.
4. Inspect Git status.
5. Identify existing user data and local settings.
6. Configure an isolated test profile.
7. Reproduce the previous baseline build.
8. Run available regression tests.
9. Record new results without overwriting previous reports.

Do not modify existing user projects or preferences during baseline testing.

### P0 — Playback Synchronization

Investigate and verify synchronization between transport position and audible output.

Testing areas:

- WASAPI output timing
- ASIO output timing
- Device position
- Buffer scheduling
- Plugin delay compensation
- Slop Launcher timing
- Loop transitions
- Seeking

Use measured timing information where available.

If the environment cannot safely support physical synchronization testing, record the status as unverified.

### P1 — Automatic Sample Tempo Following

Design, implement, and verify:

1. Sample BPM estimation
2. BPM confirmation dialog
3. Per-sample persistent tempo settings
4. Manual correction and re-editing
5. Tempo-following behavior
6. Pitch-preserving time stretching
7. Project tempo change handling
8. Original audio preservation
9. Save and reload persistence
10. Regression tests

Do not destructively alter original sample files.

### P1 — Track Freeze and Unfreeze

Define data preservation requirements before implementation.

Preserve:

- Original MIDI
- Plugin states
- Plugin parameters
- Track routing
- Automation
- Undo / Redo
- Original track configuration
- Project serialization
- Restoration after saving and reloading

Verify rendered audio against expected track output.

Test freeze and unfreeze using actual audio processing.

Do not delete the original editable track state.

### P2 — OSC and Additional Improvements

Investigate the recorded UDP failure.

Reproduce and isolate the issue before applying changes.

Continue improvements to:

- Stem exporting
- Theme consistency
- Piano roll
- Launchpad integration
- Keyboard shortcuts

Use physical hardware tests when supported and authorized.

Record unavailable hardware tests as unverified.

## 9. Build and Test Procedures

The primary build instructions are located in:

`docs/build-and-test.md`

The previous build verification report is:

`outputs/slop-studio-0.13-build-verification-ja.md`

This report contains environment information, build commands, and archive hashes.

### Release Build

The previous Release build configuration used:

```text
-DBUILD_TESTING=OFF
```

### Verification Build

Use a separate build directory and output location with:

```text
-DBUILD_TESTING=ON
```

Keep verification artifacts separate from distribution outputs.

### CTest

Run the applicable tests with:

```powershell
ctest --test-dir <test-build> -C Release --output-on-failure
```

Replace `<test-build>` with the actual verification build directory.

### Available Test Entry Points

The previous development records identify these test executables and commands:

```text
V13CoreTests.exe <isolated-dir>
V13GuiTests.exe <isolated-dir>
V12GuiTests.exe
V9GuiTests.exe
SlopStudio.exe --smoke <report> ja-JP
```

Verify executable locations and arguments against the current source and build documentation.

Use dedicated temporary directories for test data.

Do not point tests at user project directories.

### Isolated Application Profile

Set the existing environment variable:

`SLOPX_PROFILE`

Use a dedicated directory inside the authorized workspace.

Example in PowerShell:

```powershell
$env:SLOPX_PROFILE = "D:\SlopStudioTestProfile"
```

The example path is illustrative. Select an appropriate existing or explicitly created test directory for the current workspace.

Do not point the profile to production user data.

Keep test-generated preferences and project files separate from normal application state.

### External Test Dependencies

Depending on the test, additional components may be required:

- FFmpeg
- Physical MIDI hardware
- Physical audio devices
- ASIO drivers
- Commercial or third-party plugins

Diagnostic plugin fixtures may exist in the test environment but are not necessarily included in normal distributions.

Confirm dependency availability before running tests.

## 10. Test Environment Safety

GUI and audio tests may interact with system devices and other applications.

Before testing:

- Inspect the current audio device configuration.
- Use isolated application profiles.
- Use synthetic or virtual devices where appropriate.
- Avoid modifying production audio settings.
- Preserve existing user projects.
- Keep temporary files in designated test locations.
- Do not overwrite existing recordings.
- Do not run unauthorized physical recording tests.
- Do not initiate unauthorized physical audio output.

Do not interfere with other music production applications or active audio sessions.

Use explicit test classifications:

- **PASS:** Executed successfully.
- **FAIL:** Executed and failed.
- **BLOCKED:** Execution prevented by a specific dependency or environment limitation.
- **NOT TESTED:** Not executed.

Include commands, environments, and relevant logs for failed tests.

## 11. Required Agent Workflow

For each development task:

1. Identify the requested functionality.
2. Read the relevant requirements.
3. Inspect the current implementation.
4. Check existing tests.
5. Determine affected files and dependencies.
6. Implement the requested change.
7. Build the affected components.
8. Run relevant tests.
9. Check for regressions.
10. Update the implementation status.
11. Report the changes and test results.

Make focused modifications.

Do not rewrite unrelated components.

Do not replace functional implementations without technical justification.

Do not invent successful test results.

Do not modify archived release packages during normal source development.

Do not remove or reset existing project data.

Do not push, upload, publish, or transmit project material without authorization.

## 12. Documentation Updates

After implementing or verifying a feature, update the relevant documentation:

- `docs/status-v0.13-ja.md`
- `docs/plan-v0.13-ja.md`
- `docs/HANDOFF.md`
- `docs/build-and-test.md`, if build or test procedures change
- `docs/menu-command-map.json`, if command mappings change

Maintain existing documentation languages and file naming conventions unless instructed otherwise.

Do not replace historical verification results with newly obtained results.

Preserve the date and environment of each test record.

Distinguish:

- Implemented and tested
- Implemented but unverified
- Partially implemented
- Not implemented
- Blocked

## 13. Required Final Report

At the end of each development task, provide a concise technical report containing:

### Changes

- Implemented functionality
- Fixed defects
- Modified files

### Build

- Build commands executed
- Build status
- Relevant errors

### Tests

- Tests executed
- PASS / FAIL / BLOCKED / NOT TESTED
- Reproduction steps for failures

### Remaining Work

- Known defects
- Unimplemented requirements
- Unverified functionality
- Recommended next development task

Report only work actually performed.

If the environment prevents a build or test, identify the specific blocker.

Do not claim physical hardware compatibility based on synthetic tests.

Do not report previous verification results as current results.

---

## 14. Handoff Summary

**Current development target:** Slop Studio 0.13

**Initial source directory:** `outputs/slop-studio-0.13-source/`

**Primary operating system:** Windows 11 x64

**Required first actions:**

1. Verify the source tree and Git state.
2. Read repository instructions and current development documentation.
3. Confirm the build environment.
4. Reproduce available baseline tests using isolated data.
5. Investigate playback synchronization.
6. Continue the approved version 0.13 implementation plan.

**Important existing limitations:**

- Automatic sample BPM following is not implemented.
- Track freeze / unfreeze is not implemented.
- Physical playback synchronization requires verification.
- Launchpad hardware testing is incomplete.
- OSC local UDP reception previously failed.
- ASIO and plugin compatibility require further testing.

**Data protection requirements:**

- Preserve source files and Git history.
- Preserve user projects, audio assets, and preferences.
- Use isolated test directories.
- Do not overwrite existing artifacts without authorization.
- Do not perform unauthorized external operations.

**Verification baseline:** 2026-10-30 records, including 9 passed CTest suites out of 10.

**Current build and test status:** Not established by this handoff document. Fresh verification is required.

**End of development handoff.**