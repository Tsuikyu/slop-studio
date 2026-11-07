local track = slop.call("create_track", {name="Lua Arpeggio", type="midi"})
slop.call("set_tempo", {bpm=128})
for step=0,15 do
  slop.call("add_midi_note", {
    trackId=track.trackId, start=step*0.25, duration=0.2,
    pitch=60+({0,4,7,12})[(step % 4)+1], velocity=96
  })
end
