"""Calibrate generated face pigments to a reviewed model without changing geometry."""
from statistics import median


def face_material(pixel):
    r, g, b, a = pixel
    if not a:
        return None
    if r > 140 and r > g*1.28 and g > b*1.15 and r-b > 65:
        return 'cheek'
    if r > 100 and g > 65 and min(r, g) > b*1.4 and r-b > 35:
        return 'head'
    return None


def region_pixels(image, head, material):
    left, top, width, height = head
    return [(x, y, image.getpixel((x, y))) for y in range(top, top+height)
            for x in range(left, left+width)
            if face_material(image.getpixel((x, y))) == material]


def normalize_face_palette(image, reference, head, reference_head, materials):
    """Remove per-sheet colour cast, then use only the reference's actual colours.

    A nearest-colour match alone can retain a green/yellow cast by selecting only
    the reference highlights. Align each pigment's median first; preserve shading
    differences, alpha, every pixel position and all non-face materials.
    """
    result = image.copy()
    for material in materials:
        if material not in ('head', 'cheek'):
            raise ValueError(f'Unsupported face material: {material}')
        source = region_pixels(image, head, material)
        target = region_pixels(reference, reference_head, material)
        if not source or not target:
            raise ValueError(f'No {material} pixels to calibrate')
        source_mid = tuple(median(p[c] for _, _, p in source) for c in range(3))
        target_mid = tuple(median(p[c] for _, _, p in target) for c in range(3))
        palette = sorted({p[:3] for _, _, p in target})
        # An even-sized region may have a median between two colour bands.
        # Anchor to one actual pigment so tiny rounding changes cannot switch
        # an entire cheek between the shadow and highlight bands across frames.
        target_mid = min(palette, key=lambda value: sum((value[c]-target_mid[c])**2 for c in range(3)))
        mapped = {}
        for _, _, pixel in source:
            rgb = pixel[:3]
            if rgb not in mapped:
                shifted = tuple(rgb[c]-source_mid[c]+target_mid[c] for c in range(3))
                mapped[rgb] = min(palette, key=lambda value: sum((value[c]-shifted[c])**2 for c in range(3)))
        for x, y, pixel in source:
            result.putpixel((x, y), mapped[pixel[:3]]+(pixel[3],))
    return result

