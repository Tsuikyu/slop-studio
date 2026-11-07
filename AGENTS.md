# AGENTS.md — Slop Studio Development Instructions

## 1. Project Information

- **Project:** Slop Studio
- **Current development version:** 0.13
- **Author:** Tsuikyu
- **Primary platform:** Windows 11 x64
- **Future platform target:** Debian Linux
- **Application framework:** C# / Windows Forms / .NET Framework 4.8
- **Audio and plugin components:** C++
- **Build system:** CMake
- **Original source code license:** MIT
- **UI design:** Minimal CyberSquare
- **Project file format:** `.slopdaw`

These instructions apply to all coding agents working in this repository.

Use English for technical documentation, code comments, development plans, and agent-to-agent communication unless the user explicitly requests another language.

Respond to the user in their requested language.

Do not rename the application, change its project file format, or replace its core architecture without explicit authorization.

## 2. Primary Objective

Develop and maintain Slop Studio as a digital audio workstation supporting:

- Audio recording and playback
- MIDI recording and editing
- Multitrack arrangement
- Region editing
- Piano roll editing
- Audio processing
- Software instruments
- Mixing and routing
- Automation
- VST3 and CLAP plugins
- Audio and MIDI import/export
- Project saving and recovery
- External controller integration
- Lua scripting
- OSC communication
- MCP-based AI integration

Preserve existing functionality while implementing additional features and fixing defects.

Prioritize functional correctness, audio stability, data integrity, performance, and maintainability.

## 3. Source of Requirements

The primary product requirements document is:

`docs/requirements/Slop_Studio_v5_Complete_Spec_JA.txt`

Additional project references:

- `docs/HANDOFF.md`
- `docs/status-v0.13-ja.md`
- `docs/plan-v0.13-ja.md`
- `docs/build-and-test.md`
- `docs/implementation-plan.md`
- `docs/menu-command-map.json`
- `docs/requirements/Slop_Studio_v5_Menu_Catalog.json`
- `docs/slopdaw-format.md`

### Priority Rules

Apply the following priority order:

1. Latest explicit user instructions
2. Repository-level agent instructions and safety constraints
3. Current version implementation plan and verified status
4. Primary product requirements
5. Older planning documents

Higher-priority instructions override conflicting lower-priority project requirements.

Do not assume that documentation accurately reflects the current implementation.

Verify implementation details against source code and test results.

When documentation conflicts with actual behavior, identify the discrepancy and update the relevant documentation after verification.

Do not overwrite newer implementation decisions with older requirements.

## 4. Repository Inspection

Before modifying code:

1. Read this file.
2. Review the current development status and handoff documents.
3. Inspect the repository structure.
4. Check the current Git branch and working tree.
5. Identify existing uncommitted changes.
6. Locate the relevant source files.
7. Inspect existing implementations and dependencies.
8. Review available build and test procedures.
9. Identify affected components and potential regressions.
10. Define the implementation and verification steps.

Do not assume the repository is empty.

Do not recreate existing components without checking their implementation.

Do not overwrite unrelated changes.

Do not initialize a new repository inside the existing project.

### Git Operations

Allowed without additional approval:

- `git status`
- `git diff`
- `git log`
- `git show`
- Read-only repository inspection

Do not perform destructive Git operations without explicit user authorization.

Do not execute:

- `git reset --hard`
- `git clean -fd`
- `git clean -fdx`
- Destructive rebases
- Force pushes
- Branch deletion
- History rewriting

Do not automatically commit, push, publish, or create releases unless requested.

Preserve existing Git history.

## 5. Data Protection

Protect all existing project assets, including:

- Source files
- `.slopdaw` projects
- Audio files
- MIDI files
- Presets
- Templates
- Plugin configurations
- Theme files
- Keyboard shortcut configurations
- User preferences
- Build configurations
- Test fixtures
- Documentation
- License files
- Git metadata

Do not delete, move, overwrite, or replace user-owned data without explicit permission.

Existing source files may be edited when required for the authorized development task.

Use targeted changes rather than replacing entire files unnecessarily.

When modifying a data format, provide compatibility handling or an explicit migration procedure.

Do not perform irreversible migrations without authorization.

Do not change files outside the repository unless required and explicitly authorized.

Build output cleanup must be limited to verified generated files.

Use temporary directories for potentially destructive tests.

## 6. Architecture

Maintain the existing architecture.

| Component | Technology |
|---|---|
| Application UI | C# / Windows Forms |
| Application framework | .NET Framework 4.8 |
| Audio engine | C++ |
| Plugin processing | C++ |
| Build configuration | CMake |
| Primary audio backend | WASAPI |
| ASIO integration | Separate companion process |
| Ableton Link integration | Separate companion process |
| Plugin formats | VST3 / CLAP |
| Scripting | Lua |
| External communication | OSC |
| AI integration | stdio MCP |
| Primary platform | Windows 11 x64 |

Do not introduce JUCE.

Do not migrate the application to another UI framework or runtime without explicit approval.

Do not replace the audio engine without a defined technical requirement and authorization.

Maintain separation between:

- UI state
- Project state
- Audio processing
- MIDI processing
- Plugin hosting
- File operations
- External communication
- Scripting
- AI integration

Avoid unnecessary dependencies.

Use existing project conventions whenever possible.

## 7. Real-Time Audio Requirements

Real-time audio processing must remain deterministic and non-blocking.

Do not execute the following operations directly inside real-time audio callbacks:

- File I/O
- Network I/O
- GUI operations
- Lua script execution
- MCP request handling
- Blocking synchronization
- Unbounded memory allocation
- Potentially blocking logging
- Long-running computations
- Operations with unpredictable execution time

Use appropriate queues, state snapshots, or other real-time-safe communication mechanisms.

Verify thread safety for shared audio and UI state.

Handle sample rates, buffer sizes, channel layouts, and audio device changes correctly.

Prevent:

- Buffer underruns
- Unintended audio discontinuities
- Deadlocks
- Data races
- Invalid buffer access
- Memory leaks
- Plugin lifecycle errors

Changes to playback, scheduling, or audio processing require regression testing.

## 8. User Interface

The interface uses the Minimal CyberSquare design system.

### Design Requirements

- Dark background
- Blue-purple accents
- Square-oriented controls
- Consistent spacing
- Readable text
- Consistent interaction behavior
- High-DPI awareness
- Efficient rendering
- Keyboard accessibility where applicable

Do not introduce a new visual design system without authorization.

### Localization

Supported UI languages:

- `ja-JP`
- `en-US`
- `de-DE`
- `ko-KR`

Maintain localization resources for all supported languages.

Avoid hardcoded user-facing strings when localization resources are available.

Preserve existing translation keys.

Keyboard layout and interaction preferences must remain independent of UI language.

Do not assume a keyboard shortcut is valid without checking conflicts.

### Window Menu Order

Maintain this command order:

1. Lua Scripts
2. OSC / VRChat Integration
3. MCP AI Integration Manager
4. Game Controller Manager
5. Panic!

Use the appropriate localized display strings.

Preserve command identifiers and existing bindings.

## 9. Command Registry

Use the Command Registry as the common execution interface for supported application operations.

The menu catalog is located at:

`docs/requirements/Slop_Studio_v5_Menu_Catalog.json`

Maintain the mapping between menu items and `command_id` values.

The existing mapping is documented in:

`docs/menu-command-map.json`

### Requirements

- Reuse existing command identifiers.
- Avoid duplicate command implementations.
- Route supported menu actions through the Command Registry.
- Route Lua, OSC, and MCP operations through the appropriate command interfaces.
- Apply permission checks before execution.
- Update the UI when command state changes.
- Preserve Undo / Redo integration where applicable.
- Disable menu actions that have no functional implementation.

Do not classify a command as implemented based solely on its menu entry, button, event handler, or placeholder.

Command execution must produce the intended application state change.

## 10. Project File Format

The standard project format is:

`.slopdaw`

Maintain compatibility with the documented format.

The project system must preserve supported:

- Track data
- Region data
- MIDI events
- Plugin states
- Insert ordering
- Automation
- Routing
- Project metadata
- Relevant application settings

### Saving

Implement and maintain:

- Atomic save operations where supported
- Backup creation before overwriting
- Error handling
- Save failure recovery
- Data integrity checks

### Loading

Verify:

- Valid project parsing
- State reconstruction
- Track restoration
- Region restoration
- MIDI restoration
- Supported plugin state restoration
- Automation restoration
- Referenced audio handling

### Audio References

Audio assets are generally referenced rather than embedded.

Preserve media paths and existing relinking behavior.

Do not silently delete regions with missing audio.

Maintain missing-media placeholders where supported.

### Compatibility

Test serialization changes against existing project fixtures.

Do not modify the project schema without evaluating migration requirements.

Document any format changes.

## 11. Audio and MIDI Editing

Maintain and extend the existing audio and MIDI editing functions.

### Audio

- Audio tracks
- Regions
- Playback
- Recording
- Waveform display
- Waveform editing
- Gain and pan
- Effects processing
- Track routing
- Master output
- Audio export

Use the feature name **Waveform Editor** for time and pitch editing.

### MIDI

- MIDI tracks
- Piano roll
- Note creation and deletion
- Note position editing
- Note duration editing
- Velocity editing
- MIDI playback
- MIDI recording
- MIDI import/export
- Undo / Redo

Editing operations must update the project state consistently.

Verify that edited data survives saving and reloading.

### Playback Synchronization

Verify synchronization between:

- Playback transport
- Timeline position
- GUI playhead
- MIDI scheduling
- Audible audio output

Account for audio buffering and latency.

Do not mark synchronization tests as passed without measured or reproducible results.

## 12. Mixer and Plugin Hosting

Maintain the existing mixer and plugin architecture.

Required processing areas include:

- Track inserts
- Master inserts
- Aux routing
- Track stacks
- Effect ordering
- Plugin parameters
- Plugin state persistence
- Automation
- Master processing

### VST3 / CLAP

Verify the following operations:

1. Plugin discovery
2. Plugin loading
3. Plugin initialization
4. Audio and MIDI processing
5. Parameter updates
6. State serialization
7. State restoration
8. Plugin unloading
9. Error handling

Test specific plugin configurations individually.

Do not report universal plugin compatibility based on a limited test set.

### Presets

Maintain the `.sloppreset` format.

Preserve the existing preset save, load, copy, paste, and comparison behavior.

External sample dependencies must remain correctly referenced.

## 13. Import and Export

Maintain supported import and export functionality.

### Import

Supported import areas include:

- MIDI
- Audio
- Limited FLP import
- Limited ALS import
- Limited DAWproject import

Preserve available MIDI events, arrangement data, and referenced audio where supported.

Maintain `[Missing]` regions and audio relinking functionality.

Do not report complete project compatibility when instruments, effects, routing, automation, or other data cannot be fully restored.

### Export

Maintain:

- MIDI export
- WAV export
- MP3 export with required external dependencies
- Master processing
- Metadata tagging
- Stem export

Verify actual exported data.

Check output duration, sample format, channel count, and audio integrity.

Stem export tests must account for routing duplication and nonlinear master processing.

## 14. External Integrations

### Lua

Maintain the Lua Mod Manager.

Requirements:

- Register multiple `.lua` files.
- Execute selected scripts explicitly.
- Validate requested operations.
- Use the Command Registry for application commands.
- Preserve application stability when scripts fail.

Do not execute untrusted scripts automatically.

### OSC

Maintain OSC communication separately from the audio callback.

Validate incoming messages.

Dispatch supported operations through the Command Registry.

Investigate the previously recorded local UDP reception failure.

Do not change the failure status without a successful verification test.

### Game Controllers

Maintain controller mapping through the existing key command system.

Preserve user-defined mappings.

Test input handling and command dispatch.

### Launchpad

Target devices:

- Launchpad X
- Launchpad Mini MK3

Target mode:

- Programmer Mode

Supported control scope:

- First 8 tracks
- First 8 scenes

Maintain explicit MIDI input and output selection.

Physical hardware testing has not been completed.

Record actual device test results separately from software-level tests.

## 15. MCP Integration

MCP uses stdio transport.

MCP must remain disabled during normal application startup.

MCP mode is activated with:

```powershell
SlopStudio.exe --mcp
```

### Default Permissions

- Local operation only
- Read-only initial access
- Editing disabled until explicitly authorized
- Session-specific editing permission
- Additional confirmation for file operations
- Additional confirmation for record-arm operations

Reject unauthorized:

- Editing
- File deletion
- External data transmission
- Recording operations
- Destructive project changes

### Implementation Requirements

- Route supported operations through the Command Registry.
- Apply permission checks before command execution.
- Record executed operations in logs.
- Validate client requests.
- Prevent uncontrolled access to local files.
- Maintain separation from the real-time audio thread.
- Handle malformed requests safely.
- Preserve existing project state on command failure.

Do not bypass user confirmation through alternative code paths.

MCP interoperability and security require additional verification.

## 16. Development Phases

Maintain the implementation plan in:

`docs/implementation-plan.md`

Use P0–P4 to organize implementation tasks.

These phases classify functionality and do not imply that every listed feature is awaiting implementation.

### P0 — Application Foundation

- Command Registry
- Menu command mapping
- Main GUI
- Project creation
- Project saving and loading
- Localization infrastructure
- Basic Undo / Redo

### P1 — Audio and MIDI Foundation

- WASAPI output
- Audio playback
- MIDI processing
- Region editing
- Piano roll
- Sampler
- Basic audio export

### P2 — Production and Mixing

- Recording
- Mixer
- Aux and group routing
- Track stacks
- VST3 / CLAP
- Automation
- Effect chains
- Presets
- Master processing

### P3 — External Integration

- Lua
- OSC
- Game controllers
- MCP
- MIDI controller integration
- Permission management

### P4 — Advanced Processing

- Advanced waveform editing
- Pitch editing
- Time editing
- Automatic performance features
- Source separation
- Advanced sample processing
- Additional production features

Update task status using verified implementation and test results.

Do not reset completed phases.

Do not replace existing implementation plans without reviewing them.

## 17. Version 0.13 Development Status

Use the following files for current priorities:

- `docs/status-v0.13-ja.md`
- `docs/plan-v0.13-ja.md`
- `docs/HANDOFF.md`

### Not Implemented

The following features are currently not implemented:

- Automatic sample BPM following
- Track freeze / unfreeze
- Rendering regions or tracks to audio at their original positions
- Replacing all tracks with rendered audio

### Requires Additional Verification

- Playhead and audible output synchronization
- Launchpad X / Mini MK3 hardware operation
- Physical MIDI keyboard compatibility
- Microphone input configurations
- ASIO driver compatibility
- VST3 / CLAP plugin compatibility
- Multiple-monitor DPI behavior
- OSC local UDP reception
- MCP interoperability and security
- Project saving and recovery integrity

### Recorded Failure

OSC local UDP reception failed in the recorded test environment.

Preserve this result until a subsequent test establishes a new outcome.

Do not infer implementation completeness from source code presence alone.

Update the status documents when functionality is implemented or successfully verified.

## 18. Build Requirements

Primary environment:

- Windows 11 x64
- CMake 3.24 or newer
- Ninja or MSVC
- x64 C/C++ toolchain
- C# compiler compatible with .NET Framework 4.8
- Required SDKs and license files

Use the build commands documented in:

`docs/build-and-test.md`

Do not invent build commands when verified repository commands are available.

Preserve SDK configuration and licensing requirements.

Do not change compiler settings or dependencies without evaluating compatibility.

### Build Validation

Check:

- Configuration success
- Compilation success
- Linking success
- Required output files
- Runtime dependencies
- Application startup
- Relevant warnings and errors

A successful build does not establish runtime correctness.

## 19. Testing

Perform tests relevant to every code change.

### Core End-to-End Test

Verify the following workflow when changes affect core application behavior:

1. Launch application.
2. Display main GUI.
3. Create a new project.
4. Add an audio or MIDI track.
5. Create or modify a region.
6. Save the project.
7. Close or unload the project.
8. Reload the saved project.
9. Verify restored data.
10. Start playback.
11. Verify audible output where audio hardware is available.
12. Export audio.
13. Verify the exported file.

For foundational implementation work, prioritize application startup, project creation, saving, and reloading before advancing to audio playback.

For existing functionality, use this workflow as a regression test.

### Test Status

Use these classifications:

- **PASS:** Executed and passed.
- **FAIL:** Executed and failed.
- **BLOCKED:** Could not execute because of an identified dependency or environment limitation.
- **NOT TESTED:** Not executed.

Include the actual test command or procedure when reporting results.

Do not report unexecuted tests as passed.

Do not substitute mock-based results for physical hardware verification.

Do not declare a feature functional solely because its source code compiles.

### Regression Testing

Test affected:

- Existing projects
- Audio playback
- MIDI editing
- Plugin state
- Serialization
- Undo / Redo
- User preferences
- Localization
- External integration
- Audio export

Investigate regressions introduced by the current change.

## 20. Code Quality Requirements

Follow existing project conventions.

### Implementation

- Prefer focused changes.
- Reuse existing components.
- Avoid duplicated logic.
- Avoid unnecessary abstractions.
- Keep interfaces consistent.
- Maintain backward compatibility where required.
- Keep unrelated code unchanged.

### Error Handling

- Handle expected failures explicitly.
- Preserve useful diagnostic information.
- Avoid silently swallowing exceptions.
- Report failed operations accurately.
- Preserve user data when operations fail.

### Performance

- Avoid blocking real-time audio execution.
- Minimize unnecessary allocations in critical paths.
- Avoid excessive UI redraws.
- Manage plugin and audio resource lifetimes.
- Prevent resource leaks.
- Consider large projects and long sessions.

### State Management

Maintain consistency between:

- UI state
- Project state
- Audio engine state
- Plugin state
- Automation state
- Undo / Redo history
- Serialized project data

Verify state restoration after saving and reloading.

## 21. Licensing and Distribution

The application's original source code follows the MIT license.

Third-party components retain their respective licenses.

ASIO and Ableton Link companion programs operate as separate processes and use GPL-3.0-only.

Preserve:

- `LICENSE`
- `THIRD-PARTY-NOTICES.md`
- `licenses`
- `Companions/Source`

Check licensing before adding external SDKs, libraries, plugins, audio assets, or images.

Preserve required license notices and corresponding source code.

Assets without confirmed redistribution rights must not be added to public release packages.

Do not publish or distribute the repository without user authorization.

## 22. Task Execution Rules

For each assigned development task:

1. Identify the requested behavior.
2. Inspect the existing implementation.
3. Determine the smallest appropriate change.
4. Identify affected dependencies.
5. Implement the change.
6. Build the affected components.
7. Run relevant tests.
8. Review for regressions.
9. Update relevant documentation.
10. Report the result.

Do not modify unrelated functionality.

Do not introduce placeholder implementations and mark them complete.

Do not invent test results or runtime behavior.

Do not perform unauthorized destructive operations.

When a task cannot be completed, report the specific blocker and preserve the current working state.

Do not replace an implementation task with a lengthy explanation.

## 23. Completion Criteria

A feature may be marked **IMPLEMENTED** when:

- The required code exists.
- The specified operation performs its intended behavior.
- Required components build successfully.
- Relevant tests have passed.
- State changes are handled correctly.
- No known regression introduced by the change remains unresolved.
- Relevant development documentation has been updated.

Use **PARTIALLY IMPLEMENTED** when only part of the required behavior is functional.

Use **NOT IMPLEMENTED** when the required functionality is absent.

Use **UNVERIFIED** when implementation exists but sufficient testing has not been completed.

Use **BLOCKED** when progress depends on an unavailable requirement, dependency, environment, or authorization.

Do not equate the presence of UI controls, menu entries, source files, or successful compilation with functional completion.

## 24. Required Final Report

After completing a development task, report:

### Changes
- Implemented functionality
- Fixed defects
- Changed files

### Build
- Commands executed
- PASS / FAIL / BLOCKED / NOT TESTED
- Relevant errors

### Tests
- Tests executed
- Results
- Reproduction steps for failures

### Remaining Work
- Known defects
- Unimplemented requirements
- Unverified functionality
- Recommended next development step

Keep reports concise and technical.

Do not include promotional language, fictional dialogue, philosophical commentary, unrelated disclaimers, or self-justification.

Do not claim work was performed unless the corresponding changes or tool results exist.

---

**Project:** Slop Studio  
**Version:** 0.13  
**Author:** Tsuikyu  
**Primary target:** Windows 11 x64

**End of AGENTS.md**
