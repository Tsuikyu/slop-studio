# Slop Studio 0.9 defect audit

| Finding | Cause and correction | Verification / limitations |
|---|---|---|
| Built-in effects absent from mixer; no drag order | Separate stock/native lists and separate DSP passes. A saved ID order now drives both slot display and the processing chain; drag within a channel commits the order. | Automated Windows GUI drag and real CLAP + stock DSP order tests; scrolling to drag beyond visible slots is not covered. |
| ASIO playback stops | Driver/companion startup, watchdog and callbacks are device dependent. v8 corrected resync notifications; v9 preserves the fix. | FL Studio ASIO continuous/reconnect test is device-specific. MiniFuse ASIO initialization returned “No device is connected to the PC”; root cause unknown and not fixed. |
| Slash editor button inactive | Duplicate toolbar command key overwrote its hit target. | Windows GUI mouse test verifies it opens the editor without adding a region. |
| IME and untranslated dynamic text | Canvas input handling and labels did not cover every route. Canvas IME and virtual key handling were adjusted; new labels translated. | Focus/IME property and labels checked in GUI; physical Japanese IME interaction remains unverified. |
| Sampler lacks import, recording and ADSR | Preset-only engine/UI. Added saved sample path, selected-input capture and envelope. | Synthetic channel capture, PCM playback, ADSR and save/reopen tested; physical microphone not tested. |
| Loop range and piano horizontal scrolling | No locator drag; wheel bounds followed only the region. | Actual GUI drag/resize/undo and Shift+wheel tests. |
| Region joining | No join command. MIDI events are merged; audio is rendered to a new 32-bit float WAV. | PCM/note tests plus GUI Ctrl+J and undo; same track only; Launcher/comp references are rejected. |
| Vertical compact view | 18-pixel minimum and overlapping labels. Lowered minimum to 12 and adapted compact header. | Current screenshot and shortcut should be reviewed at actual DPI; full visual parity not claimed. |
| Track inspector does not collapse | Header lacked toggle action. | Windows GUI toggle test. |
| Stack and Aux missing | No audio route graph. Added folder/summing stack, Aux, post-fader send, Ducker track key and cycle rejection. | Shift-selection/context menu/disclosure GUI and PCM routing/round-trip tests; nested stack, ungroup and external plug-in sidechain not supported. |
| Mixer latency after new routing | Previous flat maximum applied independently to every track. | Real 256-sample CLAP fixture impulse aligned across direct, summing and Aux paths including inserts and local bus audio. Sidechain key compensation and arbitrary graphs/plugins remain unverified. |
| Shortcut preference uses macOS notation | Preferences displayed source-reference column. | Windows GUI verifies default Windows key column; source records remain for audit only. |
| Example lacks developed arrangement | Replaced with original 14-track/80-bar example and built-in Ducker/key routing. | Save/reopen and finite PCM tests; musical quality has not been evaluated by listening. |

The user-supplied references show dense tracks, a persistent side inspector, compact transport and channel strips with blue insert slots. The redesigned mixer follows the relevant structure using original drawing code. The application remains visually and functionally different; this is not verified full parity.
