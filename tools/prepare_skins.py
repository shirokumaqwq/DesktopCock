"""Compile reviewed per-frame material masks and reusable colour palettes.

Recolouring is deterministic: no new poses, resampling or alpha changes. Region
boxes distinguish eyes/beaks from equally dark feather outlines in flattened PNGs.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
import hashlib
import json

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / 'src/DesktopCock/Assets'
SOURCE = ROOT / 'art/skins'
PREVIEW = ROOT / 'art/previews/skins'
REGIONS = {'o': 'outline', 'b': 'body', 'h': 'head', 'c': 'cheek',
           'w': 'wing', 'e': 'eye', 'i': 'highlight', 'k': 'beak', 'f': 'feet'}
MASK_COLOURS = {'o': '#333342', 'b': '#518dc9', 'h': '#ffd75e', 'c': '#ef7850',
                'w': '#e9f4ff', 'e': '#be56c6', 'i': '#ffffff', 'k': '#55c4ae', 'f': '#eea1b5'}


def luminance(pixel):
    r, g, b = pixel[:3]
    return (54*r + 183*g + 19*b + 128) // 256


def inside(boxes, x, y):
    return any(left <= x < right and top <= y < bottom for left, top, right, bottom in boxes)


def material(pixel, x, y, features):
    r, g, b, a = pixel
    if not a:
        return '.'
    level = luminance(pixel)
    # Saturated face colours also occur along the crest. Feet are pink, not yellow.
    yellow = r > 100 and g > 65 and min(r, g) > b*1.4 and r-b > 35
    cheek = r > 140 and r > g*1.28 and g > b*1.15 and r-b > 65
    if inside(features['beak'], x, y) and not yellow:
        return 'k'
    if inside(features['eyes'], x, y) and not yellow and not cheek:
        # Preserve the original small white eye highlights.
        return 'e' if level < 190 else 'i'
    if cheek:
        return 'c'
    if yellow:
        return 'h'
    if r > g+18 and r > b+8 and y >= features.get('feetTop', 48):
        return 'f'
    if level < 65:
        return 'o'
    if level > 205:
        return 'w'
    return 'b'


def map_colour(pixel, ramp):
    level = luminance(pixel)
    for right in range(1, len(ramp)):
        if level <= ramp[right][0]:
            break
    else:
        right = len(ramp)-1
    lo, hi = ramp[right-1], ramp[right]
    amount = max(0, min(hi[0]-lo[0], level-lo[0]))
    span = hi[0]-lo[0]
    a, b = bytes.fromhex(lo[1][1:]), bytes.fromhex(hi[1][1:])
    return tuple((x*(span-amount)+y*amount+span//2)//span for x, y in zip(a, b)) + (pixel[3],)


def recolour(image, mask, palette):
    result = image.copy()
    result.putdata([map_colour(p, palette[REGIONS[r]]) if r != '.' and REGIONS[r] in palette else p
                    for p, r in zip(image.get_flattened_data(), mask)])
    return result


def prepare_skins(manifest=None, images=None):
    if manifest is None:
        manifest = json.loads((ASSETS / 'animations.json').read_text(encoding='utf-8'))
    if images is None:
        images = {name: Image.open(ASSETS / spec['file']).convert('RGBA')
                  for name, spec in manifest['frames'].items()}
    features = json.loads((SOURCE / 'regions.json').read_text(encoding='utf-8'))
    skins = json.loads((SOURCE / 'palettes.json').read_text(encoding='utf-8'))['skins']
    if 'original' not in skins or skins['original']['palette']:
        raise ValueError('Keep an unchanged original skin')
    for skin_id, skin in skins.items():
        if not skin_id or not skin['name']:
            raise ValueError('Every skin needs an id and display name')
        for region, ramp in skin['palette'].items():
            if region not in REGIONS.values() or len(ramp) < 2 or ramp[0][0] != 0 or ramp[-1][0] != 255:
                raise ValueError(f'Invalid colour ramp: {skin_id}/{region}')
            previous = -1
            for level, colour in ramp:
                if not isinstance(level, int) or not previous < level <= 255 or len(colour) != 7 or colour[0] != '#':
                    raise ValueError(f'Invalid colour stop: {skin_id}/{region}')
                bytes.fromhex(colour[1:])
                previous = level
    if set(features) != set(manifest['frames']):
        raise ValueError('Review art/skins/regions.json for every animation frame before exporting skins')
    masks = {}
    for name, spec in manifest['frames'].items():
        reviewed = features[name]
        for kind in ('eyes', 'beak'):
            for left, top, right, bottom in reviewed[kind]:
                if not 0 <= left < right <= images[name].width or not 0 <= top < bottom <= images[name].height:
                    raise ValueError(f'Invalid {kind} region in {name}')
        if reviewed['sha256'] != spec['sha256']:
            raise ValueError(f'Review eye/beak regions for changed frame {name} before refreshing its skin hash')
        actual = hashlib.sha256((ASSETS / spec['file']).read_bytes()).hexdigest()
        if actual != spec['sha256']:
            raise ValueError(f'Regenerate the base animation assets first: {name}')
        im = images[name]
        mask = ''.join(material(im.getpixel((x, y)), x, y, reviewed) for y in range(im.height) for x in range(im.width))
        masks[name] = {'sha256': spec['sha256'], 'rows': [mask[y*im.width:(y+1)*im.width] for y in range(im.height)]}
    paths = [SOURCE / 'regions.json', SOURCE / 'palettes.json', Path(__file__)]
    data = {'schemaVersion': 1, 'regions': REGIONS, 'skins': skins, 'frames': masks,
            'sourceHashes': {p.relative_to(ROOT).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in paths}}
    (ASSETS / 'skins.json').write_text(json.dumps(data, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    manifest['skinManifestSha256'] = hashlib.sha256((ASSETS / 'skins.json').read_bytes()).hexdigest()
    (ASSETS / 'animations.json').write_text(json.dumps(manifest, indent=2)+'\n', encoding='utf-8')
    PREVIEW.mkdir(parents=True, exist_ok=True)
    font_path = Path('C:/Windows/Fonts/msyh.ttc')
    font = ImageFont.truetype(str(font_path), 20) if font_path.exists() else ImageFont.load_default(size=20)
    sample = ['idle', 'idle-front', 'look-up-front', 'beg1', 'pet', 'sleep1', 'walk1', 'stretch2']
    sheet = Image.new('RGB', (len(sample)*192+196, len(skins)*228+48), '#edeadb')
    draw = ImageDraw.Draw(sheet)
    draw.text((18, 12), '玄凤 · 配色对比', font=font, fill='#333a35')
    for index, (skin_id, skin) in enumerate(skins.items()):
        folder = PREVIEW / skin_id
        folder.mkdir(exist_ok=True)
        coloured = {name: recolour(im, ''.join(masks[name]['rows']), skin['palette']) for name, im in images.items()}
        for name, im in coloured.items():
            if im.getchannel('A').tobytes() != images[name].getchannel('A').tobytes():
                raise ValueError(f'Skin changed the silhouette of {name}')
        y = 48+index*228
        draw.text((18, y+78), skin['name'].replace('（', '\n（'), font=font, fill='#333a35')
        for col, name in enumerate(sample):
            im = coloured[name].resize((192, 192), Image.Resampling.NEAREST)
            sheet.paste(im, (196+col*192, y), im)
            draw.text((202+col*192, y+195), name, font=font, fill='#686d62')
        for action, clip in manifest['clips'].items():
            unique = list(dict.fromkeys(f['name'] for f in clip['frames']))
            cell_size = 80 if action == 'Stretch' else max(coloured[name].width for name in unique)
            strip = Image.new('RGBA', (cell_size*len(unique), cell_size), '#edeadb')
            for col, name in enumerate(unique):
                dx = 40-manifest['frames'][name]['foot'][0] if action == 'Stretch' else 0
                strip.alpha_composite(coloured[name], (col*cell_size+dx, 0))
            strip.resize((strip.width*4, cell_size*4), Image.Resampling.NEAREST).save(folder / f'{action}.png')
            sequence = []
            for f in clip['frames']:
                stage = Image.new('RGBA', (cell_size, cell_size), '#edeadb')
                dx = 40-manifest['frames'][f['name']]['foot'][0] if action == 'Stretch' else 0
                stage.alpha_composite(coloured[f['name']], (dx, 0))
                sequence.append(stage.resize((cell_size*4, cell_size*4), Image.Resampling.NEAREST).convert('RGB'))
            sequence[0].save(folder / f'{action}.gif', save_all=True, append_images=sequence[1:],
                             duration=[f['milliseconds'] for f in clip['frames']], loop=0 if clip['loop'] else 1, disposal=2)
    sheet.save(PREVIEW / 'comparison.png')
    for action, clip in manifest['clips'].items():
        folder = PREVIEW / 'regions'
        folder.mkdir(exist_ok=True)
        unique = list(dict.fromkeys(f['name'] for f in clip['frames']))
        cell_size = max(images[name].width for name in unique)
        strip = Image.new('RGB', (cell_size*len(unique), cell_size), '#edeadb')
        for col, name in enumerate(unique):
            mask_image = Image.new('RGB', images[name].size)
            mask_image.putdata([tuple(bytes.fromhex(MASK_COLOURS[r][1:])) if r != '.' else (237,234,219)
                                for r in ''.join(masks[name]['rows'])])
            strip.paste(mask_image, (col*cell_size, 0))
        strip.resize((strip.width*4, cell_size*4), Image.Resampling.NEAREST).save(folder / f'{action}.png')
    print(f'Prepared {len(masks)} material masks, {len(skins)} skins and per-action previews.')


if __name__ == '__main__':
    prepare_skins()
