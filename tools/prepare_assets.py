"""Slice the generated atlas into runtime frames; preserve alpha and foot alignment.
Requires Pillow. The original generated artwork remains in art/.
"""
from pathlib import Path
from PIL import Image
import json

ROOT = Path(__file__).resolve().parents[1]
out = ROOT / 'src/DesktopCock/Assets'
out.mkdir(parents=True, exist_ok=True)
atlas = Image.open(ROOT / 'art/cockatiel-atlas.png').convert('RGBA')
names = ['idle', 'blink', 'walk1', 'walk2', 'walk3', 'walk4', 'beg', 'pet',
         'sing1', 'sing2', 'sleep', 'stretch1', 'stretch2', 'look', 'lift', 'bite']
feet_x = [196, 510, 826, 1144, 181, 508, 820, 1127, 199, 513, 832, 1149, 200, 527, 831, 1129]
feet_y = [278]*4 + [587]*4 + [891]*4 + [1195]*4
frames = {}
sheet = Image.new('RGBA', (256,256))
for i, name in enumerate(names):
    row, col = divmod(i, 4)
    x0, x1 = round(col*1254/4), round((col+1)*1254/4)
    if i == 12: x1 = 350
    if i == 13: x0 = 350
    y0, y1 = round(row*1254/4), round((row+1)*1254/4)
    part = atlas.crop((x0,y0,x1,y1))
    factor = .165
    part = part.resize((round(part.width*factor),round(part.height*factor)), Image.Resampling.NEAREST)
    # Binary alpha makes pixel-art edges and native click boundaries unambiguous.
    part.putalpha(part.getchannel('A').point(lambda a: 255 if a >= 128 else 0))
    dx, dy = 32-round((feet_x[i]-x0)*factor), 56-round((feet_y[i]-y0)*factor)
    frame = Image.new('RGBA',(64,64))
    frame.paste(part,(dx,dy))
    foot_band = frame.getchannel('A').crop((23,45,44,64)).getbbox()
    if foot_band:
        correction = 56-(45+foot_band[3])
        aligned = Image.new('RGBA',(64,64))
        aligned.paste(frame,(0,correction))
        frame = aligned
    frame.save(out / f'{name}.png')
    sheet.paste(frame,(col*64,row*64))
    head = [29,16,19,23]
    if name in ('beg','pet'): head = [29,29,25,19]
    if name == 'sleep': head = [22,28,22,15]
    if name == 'look': head = [24,18,21,23]
    if name == 'bite': head = [32,28,22,21]
    frames[name] = {'file':f'{name}.png','foot':[32,56], 'head':head, 'body':[17,32,28,22], 'feet':[23,48,23,10]}
clips = {
 'Idle': ([['idle',2700],['blink',140]],True),
 'Walk': ([[f'walk{i}',140] for i in range(1,5)],True),
 'Beg': ([['beg',600]],True), 'Pet': ([['pet',450],['beg',120],['pet',450]],True),
 'Sing': ([['sing1',220],['sing2',240]],True), 'Sleep': ([['sleep',1000]],True),
 'Stretch': ([['stretch1',350],['stretch2',400],['stretch1',250]],False),
 'Look': ([['look',500]],True), 'Lift': ([['lift',500]],True), 'Bite': ([['bite',400]],False)}
manifest = {'frames':frames,'clips':{k:{'frames':[{'name':n,'milliseconds':t} for n,t in seq],'loop':loop} for k,(seq,loop) in clips.items()}}
(out/'animations.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
sheet.resize((1024,1024),Image.Resampling.NEAREST).save(ROOT/'art/frames-preview.png')
Image.open(out/'idle.png').resize((64,64),Image.Resampling.NEAREST).save(out/'pet.ico',sizes=[(16,16),(32,32),(64,64)])
print('Prepared 16 frames, animation manifest, icon and preview.')
