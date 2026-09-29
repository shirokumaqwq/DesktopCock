"""Resource provenance and generated documentation; no image processing here."""
from pathlib import Path
import hashlib
import math
import posixpath


def build_catalog(frames, bindings):
    clips = bindings['clips']
    for name, clip in clips.items():
        if not clip['frames'] or not isinstance(clip['loop'], bool):
            raise ValueError(f'Invalid clip: {name}')
        for item in clip['frames']:
            if item['name'] not in frames:
                raise ValueError(f"{name} references missing frame {item['name']}")
            duration = item['milliseconds']
            if not math.isfinite(duration) or duration <= 0:
                raise ValueError(f'Invalid frame duration: {name}')
    for name, frame in frames.items():
        composition = bindings['compositions'].get(name)
        origin = frame['origin']
        dependencies = []
        for source in origin.get('additionalSources', []):
            dependencies.append({'kind': 'layer-source', 'source': source})
        if origin.get('paletteReference'):
            dependencies.append({'kind': 'palette', 'frame': origin['paletteReference']})
        if composition:
            full = (len(composition['layers']) == 1 and
                    composition['layers'][0]['crop'] == [0, 0, 64, 64] and
                    composition['layers'][0]['at'] == [0, 0])
            for layer in composition['layers']:
                dependencies.append({'kind': 'reuse' if full else 'composition',
                                     'frame': layer['frame'], 'crop': layer['crop'], 'at': layer['at']})
            if composition['headFrom'] != name:
                dependencies.append({'kind': 'hit-region', 'frame': composition['headFrom']})
        if origin.get('scaleReference'):
            dependencies.append({'kind': 'scale', 'source': f"art/actions/{origin['scaleReference'].lower()}.png"})
        for variant in bindings['reviewedVariants']:
            if name in variant['frames']:
                dependencies.append({'kind': 'review', 'source': posixpath.normpath('art/actions/' + variant['reference'])})
        frame['dependencies'] = dependencies

    # Composition layers always read the original imported frames, including self
    # layers; they never read earlier composition outputs. Match the importer.
    source_frames = {}
    for name, frame in frames.items():
        dependencies = frame['dependencies']
        image_layers = [d for d in dependencies if d['kind'] in ('reuse', 'composition')]
        sources = {frames[d['frame']]['origin']['source'] for d in image_layers}
        if not image_layers:
            sources.add(frame['origin']['source'])
        sources.update(d['source'] for d in dependencies if 'source' in d)
        sources.update(frames[d['frame']]['origin']['source'] for d in dependencies if d['kind'] in ('hit-region', 'palette'))
        for source in sources:
            source_frames.setdefault(source, set()).add(name)
    impact = {}
    for source, names in sorted(source_frames.items()):
        consumers = [name for name, clip in clips.items() if any(f['name'] in names for f in clip['frames'])]
        impact[source] = {'frames': sorted(names), 'clips': consumers,
                          'skinRegions': sorted(names), 'previews': [f'art/previews/{c}.gif' for c in consumers]}
    return clips, impact


def write_reference_document(root, frames, clips, impact):
    lines = ['# 动画资源引用清单', '',
             '> 由 tools/prepare_assets.py 自动生成。片段定义只编辑 art/actions/asset-bindings.json；维护流程见 ASSET_MANAGEMENT.md。', '',
             '## 动画片段', '', '| 动画 | 首帧 | 末帧 | 循环 | 顺序（帧：毫秒） |', '| --- | --- | --- | --- | --- |']
    for name, clip in clips.items():
        sequence = ' → '.join(f"{f['name']}:{f['milliseconds']}" for f in clip['frames'])
        lines.append(f"| {name} | {clip['frames'][0]['name']} | {clip['frames'][-1]['name']} | {'是' if clip['loop'] else '否'} | {sequence} |")
    lines += ['', '## 帧来源、共享关系与所有引用位置', '',
              '位置从 1 开始。整帧复用可能保留兼容导出文件；共同来源只改一次。合成层读取原始导入帧，self 层表示该帧的原始素材。', '',
              '| 帧 | 原始导入来源 | 依赖关系 | 动画引用位置 |', '| --- | --- | --- | --- |']
    labels = {'reuse': '整帧复用', 'composition': '局部合成', 'scale': '缩放基准', 'review': '人工复核参考', 'hit-region': '交互区域', 'layer-source': '独立图层源', 'palette': '统一色板'}
    for name, frame in frames.items():
        origin = frame['origin']
        source = origin['source'] + ('；' + origin['selection'] if origin.get('selection') else '')
        deps = '；'.join(f"{labels[d['kind']]} ← {d.get('frame', d.get('source'))}" for d in frame['dependencies']) or '独立帧'
        users = []
        for clip_name, clip in clips.items():
            for index, f in enumerate(clip['frames'], 1):
                if f['name'] == name:
                    role = ('首帧' if index == 1 else '') + ('末帧' if index == len(clip['frames']) else '')
                    users.append(f'{clip_name}[{index}]{role}')
        lines.append(f"| {name} | {source} | {deps} | {'；'.join(users)} |")
    lines += ['', '## 修改源文件的影响范围', '',
              '下列帧的换色区域也需要复核；逐动画 GIF 位于 art/previews，换色预览位于其 skins 子目录。', '',
              '| 源文件 | 受影响帧／换色区域 | 受影响动画／预览 |', '| --- | --- | --- |']
    for source, item in impact.items():
        lines.append(f"| {source} | {', '.join(item['frames'])} | {', '.join(item['clips'])} |")
    path = Path(root) / 'art/actions/ANIMATION_REFERENCES.md'
    path.write_text('\n'.join(lines) + '\n', encoding='utf-8')
    return {path.relative_to(root).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()}
