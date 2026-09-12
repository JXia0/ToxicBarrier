from pathlib import Path
import math, random, struct, wave, shutil
root = Path(__file__).resolve().parents[1]
# Original synthesized game audio: no sampled songs or external recordings.
audio=root/'Assets/Resources/Audio'; audio.mkdir(parents=True,exist_ok=True)
rate=22050
def save(name,data):
 peak=max(abs(v) for v in data) or 1
 gain=min(1,.85/peak)
 with wave.open(str(audio/(name+'.wav')),'wb') as w:
  w.setnchannels(1); w.setsampwidth(2); w.setframerate(rate)
  w.writeframes(b''.join(struct.pack('<h',int(max(-1,min(1,v*gain))*32767)) for v in data))
def chime(name,notes,step=.12):
 data=[0.] * int((len(notes)*step+.65)*rate)
 for j,n in enumerate(notes):
  f=440*2**((n-69)/12)
  for i in range(int(.6*rate)):
   t=i/rate; v=(math.sin(2*math.pi*f*t)+.2*math.sin(4*math.pi*f*t))*.23*min(1,t/.008)*math.exp(-t*7)
   index=int(j*step*rate)+i
   if index<len(data): data[index]+=v
 save(name,data)
for name,notes in {'Pickup':[79,86],'Supply':[67,74,79,83],'Upgrade':[60,67,72,76,79], 'Victory':[60,64,67,72,76,79,84], 'Defeat':[64,60,55,48]}.items(): chime(name,notes,.16 if name in ['Victory','Defeat'] else .09)
rng=random.Random(60912)
for name,duration in [('Attack',.16),('Hurt',.22)]:
 data=[]
 for i in range(int(duration*rate)):
  t=i/rate; envelope=math.sin(math.pi*t/duration)**2
  v=(rng.uniform(-1,1)*.16+math.sin(2*math.pi*(170 if name=='Hurt' else 450)*t)*.09)*envelope
  data.append(v)
 save(name,data)
# 24-second looping ambient arpeggio; notes wrap at the loop boundary.
length=24; data=[0.]*(length*rate)
chords=[[48,55,60,64],[45,52,57,60],[41,48,53,57],[43,50,55,59]]
for beat in range(48):
 chord=chords[(beat//12)%4]; note=chord[beat%4]+12; f=440*2**((note-69)/12)
 for i in range(int(2.5*rate)):
  t=i/rate; envelope=min(1,t/.04)*math.exp(-t*2.2)
  v=(math.sin(2*math.pi*f*t)+.18*math.sin(2*math.pi*f*2*t))*.09*envelope
  data[(int(beat*.5*rate)+i)%len(data)]+=v
save('Refuge',data)
source=root/'Assets/PeterRadiant/2D Pixel Art Black Knight/Alebardium/Bloodlines UI/Audio'
for name,src in [('Click','Click Button SFX.wav'),('Hover','Hover Button SFX.wav')]: shutil.copyfile(source/src,audio/(name+'.wav'))
(audio/'README.txt').write_text('Refuge, Attack, Hurt, Pickup, Supply, Upgrade, Victory, Defeat: original synthesized audio for Toxic Barrier.\nClick and Hover: existing project Bloodlines UI audio assets.\n',encoding='utf-8')
print('Created 10 game audio clips.')
