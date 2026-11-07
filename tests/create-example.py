import json,uuid,math,wave,struct,random,pathlib
root=pathlib.Path(__file__).resolve().parents[1]/'examples'/'First Light'
media=root/'Media';media.mkdir(parents=True,exist_ok=True)
rng=random.Random(71)
def uid():return str(uuid.uuid4())
def note(start,length,pitch,velocity=86):return dict(id=uid(),startBeat=start,lengthBeats=length,pitch=pitch,velocity=velocity)
def region(name,start,length,notes=None,path=None):
 r=dict(id=uid(),name=name,startBeat=start,lengthBeats=length,audioOffsetSeconds=0,notes=notes or [])
 if path:r['mediaPath']=path
 return r
p=dict(schema='slopdaw-project',version=1,projectId=uid(),name='First Light',tempo=108,masterGainDb=-3,cycleEnabled=True,cycleStartBeat=0,cycleEndBeat=64,timeSignature=dict(numerator=4,denominator=4),tracks=[])
chords=[[60,64,67,71],[57,60,64,67],[53,57,60,64],[55,59,62,65]]
patches=[('Electric Piano','Classic Electric Piano',0,-9,0),('Soft Keys','Soft Piano',0,-14,-0.3),('Arpeggio','Pluck',0,-12,0.24),('Sub Bass','Sub Bass',2,-5,0),('Lead','Analog Lead',0,-18,0.12),('Warm Pad','Warm Pad',0,-16,-0.2),('High Pluck','Pluck',2,-18,0.4),('Counter Melody','Soft Piano',0,-18,-0.4)]
for index,(name,preset,color,gain,pan) in enumerate(patches):
 t=dict(id=uid(),name=name,type='midi',gainDb=gain,pan=pan,muted=False,solo=False,armed=index==0,externalMidi=False,instrument=preset,colorIndex=color,plugins=[],regions=[])
 for phrase in range(4):
  if index in (4,6) and phrase==0:continue
  notes=[]
  for bar,chord in enumerate(chords):
   b=bar*4
   if index in (0,1,5):
    for pitch in chord:notes.append(note(b,3.75,pitch+(12 if index==1 else 0),76+index*2))
   elif index==3:
    for off in (0,1.5,2.5,3):notes.append(note(b+off,0.4,chord[0]-24,98))
   elif index in (2,6):
    for step in range(8):notes.append(note(b+step*0.5,0.32,chord[step%4]+(12 if index==6 else 0),72+(step%3)*8))
   else:
    for step in range(4):notes.append(note(b+step,0.7,chord[(step+phrase)%4]+12,80))
  t['regions'].append(region(name,phrase*16,16,notes))
 p['tracks'].append(t)
rate=48000;duration=16*60/108;frames=round(duration*rate)
for name,kind,color,gain in [('Kick','kick',1,-4),('Snare','snare',1,-9),('Hi-Hat','hat',4,-13),('Atmosphere','wash',2,-20)]:
 samples=[]
 for i in range(frames):
  time=i/rate;beat=time*108/60
  if kind=='kick':
   age=(beat%1)*60/108;value=math.sin(2*math.pi*(48*age+7*(1-math.exp(-age*28))))*math.exp(-age*19)*0.65
  elif kind=='snare':
   age=((beat-1)%2)*60/108;value=(rng.uniform(-1,1)*0.8+math.sin(2*math.pi*175*age)*0.2)*math.exp(-age*21)*0.45
  elif kind=='hat':
   age=(beat%0.5)*60/108;value=rng.uniform(-1,1)*math.exp(-age*90)*0.23
  else:value=(math.sin(time*2*math.pi*130.8128)+math.sin(time*2*math.pi*196))*0.06*(0.4+0.6*math.sin(math.pi*time/duration)**2)
  samples.append(round(max(-1,min(1,value))*32767))
 filename=name+'.wav'
 with wave.open(str(media/filename),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(rate);w.writeframes(struct.pack('<'+'h'*len(samples),*samples))
 t=dict(id=uid(),name=name,type='audio',gainDb=gain,pan=0,muted=False,solo=False,armed=False,externalMidi=False,instrument='Classic Electric Piano',colorIndex=color,plugins=[],regions=[])
 for phrase in range(4):t['regions'].append(region(name,phrase*16,16,path='Media/'+filename))
 p['tracks'].append(t)
(root/'First Light.slopdaw').write_text(json.dumps(p,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
(root/'README.txt').write_text('First Light: original generated example composition and synthesized PCM. No the vendor sounds, artwork or third-party samples. Open First Light.slopdaw, press Space. All visible regions contain real MIDI notes or linked audio. Files are relative to the project folder.\n',encoding='utf-8')
print('Created original playable project:',len(p['tracks']),'tracks',sum(len(t['regions']) for t in p['tracks']),'regions')
