"""Import one character model at one physical scale for every standing angle."""
from io import BytesIO
from pathlib import Path
import json
from PIL import Image, ImageDraw, ImageFont


def prepare_model(root, source_bytes, connected_cells, register):
    spec = json.loads(source_bytes(root / 'art/model/model.json'))
    source = Image.open(BytesIO(source_bytes(root / spec['source']))).convert('RGBA')
    source.putdata([(0, 0, 0, 0) if min(r, b) > g+18 and min(r, b) > g*1.35
                    else (r, g, b, a) for r, g, b, a in source.get_flattened_data()])
    cells = dict(zip(spec['views'], connected_cells(source, spec['columns'], spec['rows'])))
    # One scale for the whole model: a narrow front view must never be fitted to
    # the side view's width, nor a lowered/rotated crest fitted to its height.
    scale = spec['standingHeight'] / cells[spec['scaleReference']].height
    views, heads, feet_by_view = {}, {}, {}
    folder = root / 'art/model/frames'
    folder.mkdir(parents=True, exist_ok=True)
    for name, cell in cells.items():
        cell = cell.resize((round(cell.width*scale), round(cell.height*scale)), Image.Resampling.NEAREST)
        cell.putalpha(cell.getchannel('A').point(lambda a: 255 if a >= 128 else 0))
        feet = [(x, y) for y in range(round(cell.height*.65), cell.height) for x in range(cell.width)
                if (p := cell.getpixel((x, y)))[3] and p[0] > 140 and p[1] > 75
                and p[0] > p[1]*1.2 and p[0] > p[2]*1.2]
        if not feet:
            raise ValueError(f'Model has no visible feet: {name}')
        xs, ys = zip(*feet)
        dx = spec['foot'][0]-round((min(xs)+max(xs))/2)
        # Preserve the approved long tail without shrinking the animal. The host
        # places each frame by its declared foot anchor, so shifting both is neutral.
        shift = max(0, 1-dx)
        dx += shift
        feet_by_view[name] = [spec['foot'][0]+shift, spec['foot'][1]]
        # One outline pixel below the pink sole. Back-view tail may extend below
        # the feet; it must not change the animal's scale or its foot registration.
        dy = spec['foot'][1]-2-max(ys)
        if dx < 1 or dy < 1 or dx+cell.width > 63 or dy+cell.height > 63:
            raise ValueError(f'Model view exceeds its canvas: {name}')
        frame = Image.new('RGBA', (64, 64))
        frame.alpha_composite(cell, (dx, dy))
        yellow = [(x, y) for y in range(48) for x in range(64)
                  if (p := frame.getpixel((x, y)))[3] and p[0] > 160 and p[1] > 120
                  and p[0] > p[2]*1.3 and p[1] > p[2]*1.2]
        hx, hy = zip(*yellow)
        heads[name] = [min(hx)-2, min(hy)-2, max(hx)-min(hx)+5, max(hy)-min(hy)+5]
        views[name] = frame
        frame.save(folder / f'{name}.png')
    # Reuse the open pose exactly; the generated closed-eye marks are the only
    # parts that may change during a blink, never the head/body silhouette.
    blink = views['front'].copy()
    for box in spec['blinkPatches']:
        blink.paste(views['front-blink'].crop(box), tuple(box[:2]))
    views['front-blink'] = blink
    heads['front-blink'] = list(heads['front'])
    blink.save(folder / 'front-blink.png')
    for name, binding in spec['bindings'].items():
        view = binding['view']
        register(binding['action'], name, views[view], heads[view],
                 foot=feet_by_view[view],
                 origin={'source': spec['source'], 'selection': f"canonical {view}; shared {spec['standingHeight']}px side scale"})

    font = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 22)
    small = ImageFont.truetype('C:/Windows/Fonts/msyh.ttc', 16)
    for filename, order, labels in [
        ('ThreeViews.png', ['front', 'side', 'back'], ['正面', '侧面', '背面']),
        ('TurnAngles.png', ['side', 'rear', 'quarter', 'front'], ['侧视', '回头', '斜面转身', '正面转身'])
    ]:
        sheet = Image.new('RGB', (len(order)*288, 360), '#edeadb')
        draw = ImageDraw.Draw(sheet)
        for index, (name, label) in enumerate(zip(order, labels)):
            left, top = index*288+16, 32
            draw.line((left, top+56*4, left+256, top+56*4), fill='#c3bda9')
            frame = views[name].resize((256, 256), Image.Resampling.NEAREST)
            sheet.paste(frame, (left, top), frame)
            draw.text((left+100, 294), label, font=font, fill='#333a35')
        draw.text((20, 334), '同一比例导出 · 脚底基准 y=56 · 背面尾羽不作为缩放基准', font=small, fill='#686d62')
        sheet.save(root / 'art/model' / filename)
    source_bytes(Path(__file__))
    return views
