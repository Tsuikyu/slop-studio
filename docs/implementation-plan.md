# Slop Studio Implementation Plan

## Baseline and Existing Assets

This working copy is based on Slop Studio 0.9 with the master-FX revision. It retains Windows x64, CMake, C# WinForms, the C++ audio/VST3/CLAP host, and the separate GPL ASIO/Link companion process. The source and existing distributions are stored separately. Preserve the existing MIDI, Sampler, Launcher, Slop Pitch, ARA, Lua, OSC, gamepad, master FX, and other features; do not treat unverified behavior as complete.

## P0: Product Name, Interface, and Saving

- No dependencies: use the Slop Studio name and the original Minimal CyberSquare colors and square components while retaining the existing layout, interaction, and accessibility.
- No dependencies: make `.slopdaw` the new standard format and support only `.slopdaw` in Slop Studio. Verify the new schema, rejection of future unsupported versions, atomic saving, and backups.
- Depends on the save format: migrate examples, drag-and-drop, launch arguments, recovery, and native DAWproject extensions.
- No dependencies: retain the four language resources and existing key commands; change the editor name to Sloppy.
- P0 verification: Windows startup, all four languages, edit → save as `.slopdaw` → reload, user-data integrity, and master-FX regression checks.

## P1: Existing Production Features

- Depends on P0: run regression checks for audio/MIDI playback, recording, editing, bounce, Sampler, and master FX using the existing implementation. Mark hardware-dependent behavior unverified when the required device is unavailable.
- Auditing locks, allocations, and exception paths in the audio thread remains ongoing. Real-time safety is not currently guaranteed.

## P2: Mixer and Editing

- Depends on P1: preserve existing operations for stacks, buses, sends, plugins, automation, Sloppy, score editing, and takes.
- Depends on P0: document the mapping between the supplied menu catalog and existing AppCommand handlers. Do not present an unimplemented command as an available operation. This does not mean the entire specification has been implemented.

## P3: External Integrations

- Depends on P0/P1: retain existing Lua, OSC, gamepad, and stdio MCP integrations. MCP should start disabled and read-only; editing and file operations require explicit permission. Treat auditing, fine-grained approvals, manager UI, and formal MCP conformance as incomplete unless verified.

## P4: Extensions and Publication Readiness

- Depends on P1–P3: implement and verify Sloppy's non-destructive advanced editing, MPE, score editing, takes, and other requirements individually.
- The original application follows an MIT policy. Preserve the respective terms for the separate-process ASIO/Link components and third-party code such as the ARA API. Verify included materials and copyright holders; do not claim legal clearance.
- Prioritize the Windows CMake build. Keep Debian support and public release/publishing outside this verification scope.

The supplied specification's `.slopstudio` extension is provisional; follow the direct user requirement for `.slopdaw`.
