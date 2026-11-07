# `.slopdaw` Project Format

The standard format is UTF-8 JSON. The root object contains `schema: "slopdaw-project"`, integer `version: 1`, project ID, name, tempo, meter, tracks, and `studio` state. Tracks may contain regions, MIDI notes, instrument/plugin states, and related data. Invalid data, other schemas, and unsupported future versions are rejected. The file-size limit is 64 MiB.

Saving writes to a temporary file in the same directory, validates and flushes it, then replaces the existing file while retaining a uniquely named `.bak` backup. Backups accumulate, so users should manage them as needed. Automatic recovery snapshots are stored elsewhere.

Audio media are referenced as external files. Relative paths are resolved from the project directory on load, but not all references are made relative, and media collection is not yet available. Moving or deleting a referenced file may make the audio unrecoverable. Native DAWproject extensions also use this format.
