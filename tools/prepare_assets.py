"""Import independent action sources into transparent, foot-aligned runtime frames.

Requires Pillow. Action sheets share the canonical model where applicable.
Historical individual sprites and the combined atlas are never read.
"""
from pathlib import Path
from PIL import Image, ImageDraw
import json
import math
import hashlib

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'art/actions'
OUT = ROOT / 'src/DesktopCock/Assets'
PREVIEW = ROOT / 'art/previews'
frames = {}
images = {}
source_hashes = {}
action_scales = {}
current_source = None


def source_bytes(path):
    path = Path(path).resolve()
    data = path.read_bytes()
    source_hashes[path.relative_to(ROOT).as_posix()] = hashlib.sha256(data).hexdigest()
    return data


def open_source(relative):
    global current_source
    current_source = "art/actions/" + relative
    from io import BytesIO
    return Image.open(BytesIO(source_bytes(SOURCE / relative))).convert('RGBA')


bindings = json.loads(source_bytes(SOURCE / 'asset-bindings.json'))
for variant in bindings['reviewedVariants']:
    digest = hashlib.sha256(source_bytes(SOURCE / variant['reference'])).hexdigest()
    if digest != variant['referenceSha256']:
        affected = [name for name, clip in bindings['clips'].items()
                    if any(f['name'] in variant['frames'] for f in clip['frames'])]
        raise ValueError(f"Review {variant['source']} against changed {variant['reference']} before regenerating assets; "
                         f"affected clips/previews: {', '.join(affected)}; skin regions: {', '.join(variant['frames'])}")


def register(action, name, frame, head=None, foot=None, origin=None):
    folder = OUT / action
    folder.mkdir(parents=True, exist_ok=True)
    frame.save(folder / f'{name}.png')
    images[name] = frame
    anchor = foot or [32, 56]
    offset_x, offset_y = anchor[0]-32, anchor[1]-56
    frames[name] = {
        'file': f'{action}/{name}.png', 'foot': anchor,
        'origin': origin or {'source': current_source},
        'width': frame.width, 'height': frame.height,
        'sha256': hashlib.sha256((folder / f'{name}.png').read_bytes()).hexdigest(),
        'head': head or [29, 14, 23, 27],
        'body': [16+offset_x, 32+offset_y, 32, 22],
        'feet': [22+offset_x, 48+offset_y, 27, 10],
    }


def connected_cells(source, columns, rows):
    """Extract complete sprites even when a generated grid has uneven gutters."""
    from collections import deque
    mask = bytearray(source.getchannel('A').tobytes())
    w, h = source.size
    components = []
    for start in range(w*h):
        if not mask[start]:
            continue
        mask[start] = 0
        todo = deque([start])
        x0 = x1 = start % w
        y0 = y1 = start // w
        count = 0
        members = []
        while todo:
            index = todo.popleft()
            members.append(index)
            x, y = index % w, index // w
            x0, y0, x1, y1 = min(x0, x), min(y0, y), max(x1, x), max(y1, y)
            count += 1
            for other in (index-1 if x else -1, index+1 if x+1 < w else -1,
                          index-w if y else -1, index+w if y+1 < h else -1):
                if other >= 0 and mask[other]:
                    mask[other] = 0
                    todo.append(other)
        if count > 200:
            box = (x0, y0, x1+1, y1+1)
            cell = source.crop(box)
            # Neighbouring birds may have overlapping bounding rectangles even
            # though their actual silhouettes are separate. Keep this component only.
            alpha = bytearray(cell.width*cell.height)
            for index in members:
                alpha[(index//w-y0)*cell.width + index%w-x0] = 255
            cell.putalpha(Image.frombytes('L', cell.size, bytes(alpha)))
            components.append((box, cell))
    if len(components) != columns*rows:
        raise ValueError(f'Expected {columns*rows} connected sprites, got {len(components)}')
    components.sort(key=lambda item: (int((item[0][1]+item[0][3])/2/h*rows), item[0][0]))
    return [cell for _, cell in components]


def import_action(action, names, columns, rows, width, head=None, *, connected=False, height=None, scale_like=None):
    """Chroma-key import, uniform per-action scale, fixed tail/sole registration.

    Magenta is solely an import background. It is removed before nearest-neighbor
    downsampling; runtime files always have binary alpha, never a painted backdrop.
    """
    source = open_source(f'{action.lower()}.png')
    pixels = source.get_flattened_data() if hasattr(source, 'get_flattened_data') else source.getdata()
    source.putdata([(0, 0, 0, 0) if min(r, b) > g + 18 and min(r, b) > g*1.35
                    else (r, g, b, a) for r, g, b, a in pixels])
    cells = connected_cells(source, columns, rows) if connected else []
    for i in range(0 if connected else columns * rows):
        row, col = divmod(i, columns)
        cell = source.crop((round(col*source.width/columns), round(row*source.height/rows),
                            round((col+1)*source.width/columns), round((row+1)*source.height/rows)))
        bounds = cell.getbbox()
        if bounds is None:
            raise ValueError(f'Empty source cell: {action} {i}')
        cells.append(cell.crop(bounds))
    factor = height / max(cell.height for cell in cells) if height else width / max(cell.width for cell in cells)
    if scale_like:
        reference_factor, reference_width = action_scales[scale_like]
        factor = reference_factor * reference_width / source.width
    action_scales[action] = (factor, source.width)
    action_dx = None
    anchor_shift = 0
    for cell_index, (name, cell) in enumerate(zip(names, cells)):
        if name is None:
            continue
        cell = cell.resize((round(cell.width*factor), round(cell.height*factor)), Image.Resampling.NEAREST)
        cell.putalpha(cell.getchannel('A').point(lambda a: 255 if a >= 128 else 0))
        if cell.height > 53 or cell.width > 58:
            raise ValueError(f'Frame exceeds canvas: {name}')
        frame = Image.new('RGBA', (64, 64))
        # Anchor each action at the feet. Walking keeps one X offset throughout
        # the cycle so swinging a leg cannot shift the entire body sideways.
        if action_dx is None or action in ('Idle', 'Look', 'Turn', 'Stretch'):
            sole_depth = 2 if action == 'Stretch' else 5
            feet = [x for y in range(max(0, cell.height-sole_depth), cell.height) for x in range(cell.width)
                    if (p := cell.getpixel((x, y)))[3] and p[0] > 140 and p[1] > 75
                    and p[0] > p[1]*1.2 and p[0] > p[2]*1.2]
            if not feet:
                raise ValueError(f'Cannot locate feet: {name}')
            action_dx = 32-round((min(feet)+max(feet))/2)
            anchor_shift = max(0, 1-action_dx)
            action_dx += anchor_shift
        if action_dx < 1 or action_dx+cell.width > 63:
            raise ValueError(f'Anchored frame exceeds canvas: {name}')
        frame.paste(cell, (action_dx, 56-cell.height))
        yellow = [(x, y) for y in range(48) for x in range(64)
                  if (p := frame.getpixel((x, y)))[3] and p[0] > 160 and p[1] > 120
                  and p[0] > p[2]*1.3 and p[1] > p[2]*1.2]
        frame_head = head
        if yellow and head is None:
            xs, ys = zip(*yellow)
            frame_head = [max(0, min(xs)-2), max(0, min(ys)-2), max(xs)-min(xs)+5, max(ys)-min(ys)+5]
        register(action, name, frame, frame_head, foot=[32+anchor_shift, 56], origin={'source': current_source,
                 'selection': f'{columns}x{rows} cell {cell_index+1}', 'scaleReference': scale_like})


def import_walk_rig():
    """Assemble generated art layers on a repeatable, distance-matched foot path.

    The art is generated in walk-rig.png; this only places those bitmap layers.
    During each six-pixel stance, the ankle moves back one pixel per frame, exactly
    cancelling the host's one-pixel forward travel. The other foot lifts and returns.
    """
    source = open_source('walk-rig.png')
    source.putdata([(0, 0, 0, 0) if min(r, b) > g+18 and min(r, b) > g*1.35 else (r, g, b, a)
                    for r, g, b, a in source.get_flattened_data()])
    parts = connected_cells(source, 3, 1)
    body = max(parts, key=lambda cell: cell.width*cell.height)
    feet = [cell for cell in parts if cell is not body]
    def brightness(cell):
        pixels = [p for p in cell.get_flattened_data() if p[3]]
        return sum(p[0]+p[1]+p[2] for p in pixels)/len(pixels)
    far, near = sorted(feet, key=brightness)
    # Share the exact standing torso and head, so entering a walk never changes
    # the character design. The generated rig contributes the two moving feet.
    body = model_views['side'].crop((0, 0, 64, 52))
    far, near = [foot.resize((7, 5), Image.Resampling.NEAREST) for foot in (far, near)]
    foot_anchor = frames['idle']['foot']
    for index in range(12):
        frame = Image.new('RGBA', (64, 64))
        for foot, offset in ((far, 6), (near, 0)):
            phase = (index+offset) % 12
            ankle = foot_anchor[0]+3-phase if phase <= 6 else foot_anchor[0]-3+(phase-6)
            lift = 0 if phase <= 6 else round(2*math.sin((phase-6)*math.pi/6))
            frame.alpha_composite(foot, (ankle-2, 51-lift))
        # Body rises one pixel at mid-stance, while the planted sole stays level.
        bob = -1 if index % 6 in (2, 3) else 0
        frame.alpha_composite(body, (0, bob))
        head = list(frames['idle']['head'])
        head[1] += bob
        register('Walk', f'walk{index+1}', frame, head, foot=foot_anchor,
                 origin={'source': 'art/model/model-sheet.png', 'additionalSources': [current_source],
                         'selection': f'canonical side torso + rig far/near feet; stride step {index+1}'})


# Establish the shared standing scale before importing any animated variants.
from prepare_model import prepare_model
model_views = prepare_model(ROOT, source_bytes, connected_cells, register)

# The rear view was already registered from the model; skip the sheet's third
# cell so importing gaze variants cannot replace that canonical pose.
import_action('Look', ['look', 'look-front', None, 'look-up-diagonal', 'look-up-front', 'look-up-back'],
              3, 2, 44, connected=True, height=41)
import_action('Turn', ['turn-quarter-step', 'turn-front-a', 'turn-front-b', 'turn-quarter-settle'],
              2, 2, 44, height=41)
# The fourth candidate's crest movement is too large for a gentle invitation.
bowed_scale = bindings['scaleGroups']['bowed']
import_action('Beg', ['beg1', 'beg2', 'beg3', None], 2, 2, bowed_scale['width'])
import_action('Pet', ['pet', 'pet2', 'pet3', None], 2, 2, bowed_scale['width'],
              scale_like=bowed_scale['referenceAction'])
import_action('Sleep', ['sleep1', 'sleep2', None, None], 2, 2, 43, [29, 24, 23, 19])
import_walk_rig()


def import_flight(front=False):
    """Register generated flight poses by the head, never by spread-wing bounds."""
    source = open_source('flight-front.png' if front else 'flight.png')
    source.putdata([(0, 0, 0, 0) if min(r, b) > g+18 and min(r, b) > g*1.35 else (r, g, b, a)
                    for r, g, b, a in source.get_flattened_data()])
    source.putalpha(source.getchannel('A').point(lambda a: 255 if a >= 160 else 0))
    cells = connected_cells(source, 3, 2)
    prefix = 'flight-front' if front else 'flight'
    names = [f'{prefix}-{pose}' for pose in ('takeoff', 'up', 'mid', 'down', 'brake', 'settle')]
    view = 'front' if front else 'side'
    idle = 'idle-front' if front else 'idle'
    def yellow_bounds(cell, minimum_count=4):
        points = [(x, y) for y in range(cell.height) for x in range(cell.width)
                  if (p := cell.getpixel((x, y)))[3] and p[0] > 150 and p[1] > 120
                  and p[0] > p[2]*1.4 and p[1] > p[2]*1.3]
        xs, ys = zip(*points)
        # Ignore isolated edge-colour specks; these aren't face pixels.
        from collections import Counter
        rows, columns = Counter(ys), Counter(xs)
        xs = [x for x, count in columns.items() if count >= minimum_count]
        ys = [y for y, count in rows.items() if count >= minimum_count]
        return min(xs), min(ys), max(xs)+1, max(ys)+1
    bounds = [yellow_bounds(cell) for cell in cells]
    standing_head = yellow_bounds(model_views[view], minimum_count=1)
    def cheek_area(cell):
        return sum(1 for r, g, b, a in cell.get_flattened_data()
                   if a and r > 170 and r > g*1.45 and g > b*1.25 and r-b > 100)
    # Crest tilt changes the yellow bounding height. Cheek pigment area keeps
    # skull scale comparable even when the wings and crest move.
    factor = math.sqrt(cheek_area(model_views[view]) / sorted(cheek_area(c) for c in cells)[len(cells)//2])
    for cell_index, (name, cell, box) in enumerate(zip(names, cells, bounds)):
        if name == f'{prefix}-settle':
            frame = Image.new('RGBA', (96, 96))
            frame.alpha_composite(model_views[view], (16, 16))
            head = list(frames[idle]['head'])
            head[0] += 16
            head[1] += 16
            register('Flight', name, frame, head, [frames[idle]['foot'][0]+16, 72],
                     origin={'source': 'art/model/model-sheet.png', 'selection': f'canonical {view} at 16px flight inset'})
            continue
        resized = cell.resize((round(cell.width*factor), round(cell.height*factor)), Image.Resampling.NEAREST)
        # Canvas grows by 16px on every edge; the character's head does not grow.
        dx = round(16+(standing_head[0]+standing_head[2])/2-(box[0]+box[2])/2*factor)
        dy = round(16+(standing_head[1]+standing_head[3])/2-(box[1]+box[3])/2*factor)
        if dx < 0 or dy < 0 or dx+resized.width > 96 or dy+resized.height > 96:
            raise ValueError(f'Flight wings exceed 96px canvas: {name}')
        frame = Image.new('RGBA', (96, 96))
        frame.alpha_composite(resized, (dx, dy))
        register('Flight', name, frame,
                 [round(dx+box[0]*factor)-2, round(dy+box[1]*factor)-2,
                  round((box[2]-box[0])*factor)+4, round((box[3]-box[1])*factor)+4],
                 [frames[idle]['foot'][0]+16, 72],
                 origin={'source': current_source, 'additionalSources': ['art/model/model-sheet.png'],
                         'selection': f'3x2 cell {cell_index+1}; canonical {view} head registration'})


import_flight()
import_flight(front=True)

import_action('Sing', ['sing1', 'sing2'], 2, 1, 44, connected=True, height=41)
import_action('Stretch', [f'stretch{i+1}' for i in range(12)], 4, 3, 48, connected=True, height=41)
# Follow the planted far foot, not the midpoint of the moving leg. Preserve each
# authored head/neck pose: replacing the upper body with idle freezes the stretch.
def planted_right(frame):
    return max(x for y in range(53, 56) for x in range(64)
               if (p := frame.getpixel((x, y)))[3] and p[0] > 140 and p[1] > 75
               and p[0] > p[1]*1.2 and p[0] > p[2]*1.2)

sole_offset = planted_right(images['idle']) - frames['idle']['foot'][0]
for i in range(12):
    name = f'stretch{i+1}'
    anchor = [planted_right(images[name])-sole_offset, 56]
    origin = dict(frames[name]['origin'], additionalSources=['art/model/model-sheet.png'])
    register('Stretch', name, images[name], frames[name]['head'], foot=anchor, origin=origin)
import_action('Lift', ['lift'], 1, 1, 44, height=41)
import_action('Bite', ['bite'], 1, 1, 49)

# A pose binds to the approved appearance, rather than keeping a separate head copy.
# Read all inputs from the original imported frames so self-referencing foot/body
# layers cannot accidentally pick up a previously assembled output.
imported = dict(images)
for name, composition in bindings['compositions'].items():
    frame = Image.new('RGBA', (64, 64))
    for layer in composition['layers']:
        frame.alpha_composite(imported[layer['frame']].crop(layer['crop']), tuple(layer['at']))
    head = list(frames[composition['headFrom']]['head'])
    offset = composition.get('headOffset', [0, 0])
    head[0] += offset[0]
    head[1] += offset[1]
    action = frames[name]['file'].split('/')[0]
    foot = frames[composition['headFrom']]['foot'] if len(composition['layers']) == 1 else frames[name]['foot']
    register(action, name, frame, head, foot=foot, origin=frames[name]['origin'])
    frames[name]['composition'] = composition

# Final pigment selection uses the approved standing model, after pose assembly.
# Keep original skin a passthrough; source casts must be corrected in the assets.
from normalize_palette import normalize_face_palette
source_bytes(ROOT / 'tools/normalize_palette.py')
for group in bindings.get('paletteGroups', {}).values():
    reference = group['referenceFrame']
    for name in group['frames']:
        spec = frames[name]
        frame = normalize_face_palette(images[name], images[reference], spec['head'],
                                       frames[reference]['head'], group['regions'])
        origin = dict(spec['origin'], paletteReference=reference, paletteRegions=group['regions'])
        register(spec['file'].split('/')[0], name, frame, spec['head'], spec['foot'], origin)

from asset_catalog import build_catalog, write_reference_document
clip_specs, impact = build_catalog(frames, bindings)
clips = {name: ([(f['name'], f['milliseconds']) for f in clip['frames']], clip['loop'])
         for name, clip in clip_specs.items()}
manifest = {'frames': frames, 'clips': clip_specs, 'sourceImpact': impact,
            'generatedDocuments': write_reference_document(ROOT, frames, clip_specs, impact)}
source_bytes(ROOT / 'tools/asset_catalog.py')
source_bytes(Path(__file__))
manifest['sourceHashes'] = source_hashes
OUT.mkdir(parents=True, exist_ok=True)
(OUT / 'animations.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
images['idle'].save(OUT / 'pet.ico', sizes=[(16, 16), (32, 32), (64, 64)])

# Review each action separately; never combine unrelated actions into an atlas.
PREVIEW.mkdir(parents=True, exist_ok=True)
for action, (sequence, loop) in clips.items():
    unique = list(dict.fromkeys(name for name, _ in sequence))
    cell_size = 80 if action == 'Stretch' else max(images[name].width for name in unique)
    sheet = Image.new('RGBA', (cell_size*len(unique), cell_size), (237, 234, 219, 255))
    for index, name in enumerate(unique):
        dx = 40-frames[name]['foot'][0] if action == 'Stretch' else 0
        sheet.alpha_composite(images[name], (index*cell_size+dx, 0))
    sheet.resize((sheet.width*4, cell_size*4), Image.Resampling.NEAREST).save(PREVIEW / f'{action}.png')
    animation = []
    for name, _ in sequence:
        background = Image.new('RGBA', (cell_size, cell_size), (237, 234, 219, 255))
        dx = 40-frames[name]['foot'][0] if action == 'Stretch' else 0
        background.alpha_composite(images[name], (dx, 0))
        animation.append(background.resize((cell_size*4, cell_size*4), Image.Resampling.NEAREST).convert('RGB'))
    animation[0].save(PREVIEW / f'{action}.gif', save_all=True, append_images=animation[1:],
                      duration=[t for _, t in sequence], loop=0 if loop else 1, disposal=2)

# Ground markers make stride/translation mismatch visible during art review.
travel = []
for index in range(72):
    stage = Image.new('RGBA', (160, 72), (237, 234, 219, 255))
    draw = ImageDraw.Draw(stage)
    draw.line((0, 56, 160, 56), fill=(183, 174, 149, 255))
    for tick in range(0, 160, 12):
        draw.line((tick, 57, tick, 60), fill=(183, 174, 149, 255))
    stage.alpha_composite(images[f'walk{index%12+1}'], (index+8, 0))
    travel.append(stage.resize((640, 288), Image.Resampling.NEAREST).convert('RGB'))
travel[0].save(PREVIEW / 'WalkGround.gif', save_all=True, append_images=travel[1:],
               duration=62.5, loop=0, disposal=2)

# Show the real turn sequence, including the facing change at the frontal midpoint.
turn_sequence = [('look', 350, False), ('look-front', 110, False), ('look-back', 550, False),
                 ('turn-quarter-step', 110, False), ('turn-front-a', 110, False),
                 ('turn-front-b', 110, True), ('turn-quarter-settle', 110, True), ('look', 700, True)]
turn_preview = []
for name, _, mirrored in turn_sequence:
    frame = images[name].transpose(Image.Transpose.FLIP_LEFT_RIGHT) if mirrored else images[name]
    stage = Image.new('RGBA', (64, 64), (237, 234, 219, 255))
    stage.alpha_composite(frame)
    turn_preview.append(stage.resize((256, 256), Image.Resampling.NEAREST).convert('RGB'))
turn_preview[0].save(PREVIEW / 'Turn.gif', save_all=True, append_images=turn_preview[1:],
                     duration=[duration for _, duration, _ in turn_sequence], loop=0, disposal=2)
print(f'Prepared {len(frames)} independent frames and {len(clips)} clips.')

# Skin masks use the final composed frames, including shared heads and walk layers.
from prepare_skins import prepare_skins
prepare_skins(manifest, images)
