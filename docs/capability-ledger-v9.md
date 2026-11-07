# Capability ledger — 0.9

Statuses describe the specific evidence, not unrestricted compatibility.

- PASS — Mixed built-in/external insert display and same-channel drag order: Windows GUI and actual DSP-order tests.
- PASS — MIDI and audio region join on the same track: note/PCM tests and Ctrl+J GUI/undo; Launcher/comp references are refused.
- PASS — Ruler cycle drag/resize/undo, piano Shift+wheel, slash editor button, inspector fold and Windows-only shortcut column: Windows GUI tests.
- PASS — Folder/summing stack creation by Shift-select and context menu, disclosure, parent audio routing, child-state and project persistence: GUI and synthetic PCM tests. Nested stacks, ungroup and full commercial stack semantics are NOT IMPLEMENTED.
- PASS — Post-fader Aux send and built-in Ducker track trigger: synthetic PCM and feedback rejection tests. External VST3/CLAP sidechain input is NOT IMPLEMENTED.
- PASS — Routed audio latency for a tested 256-sample plug-in fixture across stack and Aux paths, including bus inserts and local audio. Dynamic plug-in restart also has a separate tested fixture. Sidechain key latency and all vendor graphs are NOT VERIFIED.
- PARTIAL — Sampler imported PCM, root and ADSR persisted and generated audio; input recording writer tested using synthetic samples. Physical input recording is NOT RUN.
- PARTIAL — Japanese localization and IME handling are improved; Japanese IME physical interactions and every translated string are NOT VERIFIED.
- PARTIAL — FLP, ALS and SMF imports cover subsets. FL instruments/mixer effects and full conversion are not reconstructed.
- PARTIAL — ASIO/WASAPI behavior depends on the device. FL Studio ASIO and WASAPI tests are reported separately; MiniFuse initialization failed in this environment.
- NOT VERIFIED — All plug-in vendors, all Logic key commands, pixel-identical appearance, all features and commercial-grade reliability. A full Logic clone has not been delivered.
- NOT RUN — Physical microphone, physical MIDI note performance, tablet and mixed-monitor DPI.
- FAIL — OSC localhost UDP integration test fails in this environment; VRChat OSC interoperability was not verified.

See the delivery report for exact final build and test results.
