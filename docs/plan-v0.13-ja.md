# Slop Studio 0.13 — Priorities and Difficulty Before Implementation

This is a provisional assessment based on the existing 0.12 code and verification ledger. Verify each feature's behavior and persistence; do not present incomplete work as finished.

| Priority | Item | Difficulty | Dependencies and acceptance criteria to verify first |
|---|---|---|---|
| P0 | Playback position and audio drift (Tracks / Launcher) | High | Track processed frames, device playback position, buffers, loops, and seeks separately. Verify with synthetic signals and physical hardware where available. |
| P0 | Master volume in the upper-right | Medium | Measure hit testing, round-trip master values, DSP output, and mixer synchronization; display it in the original angular UI. |
| P1 | Protect sample/template originals | High | Verify that Save As behavior, atomic saves, Undo, and media references prevent overwriting originals. |
| P1 | MIDI export and stem export | Medium–High | Re-import standard MIDI and verify note positions, lengths, velocities, and tempo. Audit existing stem routing and overwrite behavior on failure. |
| P1 | Browser bookmarks, favorites, and search | Medium | Save path references only; do not move source files. Verify missing-media handling and restoration after restart. |
| P1 | Undo history limit and fullscreen | Medium | Verify history persistence, configurable limits, and taskbar visibility/restoration. |
| P2 | Piano-roll editing such as velocity | High | Verify edits to selected notes, Undo, saving, and values delivered to instruments. Clearly identify unsupported edits. |
| P2 | Project information and WAV/MP3 metadata | High | Check text encoding and compatibility. Do not present MP3 export as available if MP3 encoding is absent. |
| P2 | Save and load shortcut profiles | High | Inspect the existing central registry, conflicts, text entry, and settings restoration. Do not fabricate other DAWs' shortcut profiles. |
| P2 | Improve sample projects and create a J-pop project | High | Create original MIDI and instruments; verify 128/150 BPM, playback, and saving. Separate measured loudness from listening conditions. |
| P3 | Infer sample BPM from filenames and follow tempo | Very High | Requires confirmation of inferred values, per-sample persistence, later editing, and real pitch-preserving audio processing. Do not infer tempo from digits alone. |
| P3 | Launchpad control | Very High | Check each device's MIDI mapping, port contention, and LED updates. Without hardware, report only synthetic MIDI coverage. |
| P3 | Freeze and unfreeze tracks | Very High | Safely preserve plugin state, routing, source media, Undo, recovery, and changes during playback. |
| P3 | Custom themes and presets | High | Verify the shared UI, common colors, saving, contrast, and behavior after restart across the application. |

Do not consider everything complete at once. Prioritize audio consistency and data protection, and report only what was actually verified.
