# Integrations — Slop Studio 0.6

## MCP

Launch the actual Windows executable with --mcp using a stdio-capable MCP client. It opens its own visible DAW process; it does not attach to an already-running separate Slop window. Protocol 2025-11-25, UTF-8 newline-delimited JSON-RPC 2.0. initialize, notifications/initialized, ping, tools/list and tools/call are supported. Limits: 1 MiB/request line; no HTTP transport or built-in model/API key.

Tools: get_project, create_track, add_midi_note, transport, set_tempo, set_track_mix, save_project, open_project, export_audio, launch_cell, set_automation.

The server invokes the main UI command/history path. Saving captures actual plugin state. Open refuses to discard unsaved changes. A tool error is returned as isError rather than a fake success. Plugin/scanner/companion stdout is redirected so vendor output cannot corrupt the MCP stream.

Generic client configuration (replace the command with your extracted absolute path):

~~~json
{"mcpServers":{"Slop-Studio":{"command":"C:/Apps/SlopStudio/SlopStudio.exe","args":["--mcp"]}}}
~~~

McpProcessTests.py is a real subprocess client: it initializes, lists 11 tools, creates MIDI/automation, plays/stops, saves, exports audio and reopens the project. RecoveryCrashTests.py also terminates the actual DAW and restores its automatic snapshot in a new process.

Primary protocol: https://modelcontextprotocol.io/specification/2025-11-25/basic/transports

## OSC / VRChat

Enable in Settings → Integration. Receive is bound to loopback at 9001 by default; send destination is 127.0.0.1:9000, matching VRChat's documented default ports. Float/int/string/bool types and immediate bundles are decoded. Future timetag scheduling is not implemented.

Incoming:
- /slop/play [bool] and /avatar/parameters/SlopPlay [bool]
- /slop/stop
- /slop/tempo float BPM
- /slop/scene int zero-based scene index

Outgoing:
- /avatar/parameters/SlopPlaying bool
- /avatar/parameters/SlopBeat float, phase within four beats normalized 0–1
- /avatar/parameters/SlopLevel float, actual master peak

The avatar must expose matching parameters and have OSC enabled. Actual VRChat client behavior was NOT RUN. Verified evidence is typed codec tests and real bidirectional localhost UDP, not an avatar session.

Primary references:
https://docs.vrchat.com/docs/osc-overview
https://docs.vrchat.com/docs/osc-avatar-parameters

## Ableton Link

Enable in Settings → Integration. The separate GPL companion uses the official Link library. Two real local companion processes discover peers and exchange tempo/start-stop state. The application follows tempo/start-stop changes. Complete DAW phase/sample-clock following, long-term drift and external LAN interoperability are incomplete or unverified. Link integration is PARTIAL, not full synchronization parity.

Official source: https://github.com/ableton/link
Independent companion protocol/license: ../companions/README.md in the source archive.
