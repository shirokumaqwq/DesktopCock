# 动画资源引用清单

> 由 tools/prepare_assets.py 自动生成。片段定义只编辑 art/actions/asset-bindings.json；维护流程见 ASSET_MANAGEMENT.md。

## 动画片段

| 动画 | 首帧 | 末帧 | 循环 | 顺序（帧：毫秒） |
| --- | --- | --- | --- | --- |
| TakingOff | flight-settle | flight-takeoff | 否 | flight-settle:100 → flight-takeoff:120 |
| Flying | flight-up | flight-mid | 是 | flight-up:90 → flight-mid:65 → flight-down:90 → flight-mid:65 |
| Landing | flight-brake | flight-settle | 否 | flight-brake:130 → flight-settle:110 |
| Idle | idle | idle | 是 | idle:2200 → idle-quarter:140 → idle-front:1800 → idle-front-blink:140 → idle-front:900 → idle-quarter:140 → idle:1400 |
| Walk | walk1 | walk12 | 是 | walk1:62.5 → walk2:62.5 → walk3:62.5 → walk4:62.5 → walk5:62.5 → walk6:62.5 → walk7:62.5 → walk8:62.5 → walk9:62.5 → walk10:62.5 → walk11:62.5 → walk12:62.5 |
| Beg | beg1 | beg1 | 是 | beg1:700 → beg2:180 → beg3:220 → beg2:180 → beg1:700 |
| Pet | pet | pet | 是 | pet:450 → pet2:160 → pet3:180 → pet2:160 → pet:450 |
| Sing | sing1 | sing2 | 是 | sing1:220 → sing2:240 |
| Sleep | sleep1 | sleep2 | 是 | sleep1:1400 → sleep2:1400 |
| Stretch | stretch1 | stretch12 | 否 | stretch1:80 → stretch2:90 → stretch3:100 → stretch4:110 → stretch5:120 → stretch6:350 → stretch7:450 → stretch8:150 → stretch9:130 → stretch10:120 → stretch11:110 → stretch12:90 |
| Look | look | look | 是 | look:500 |
| LookFront | look-front | look-front | 是 | look-front:500 |
| LookBack | look-back | look-back | 是 | look-back:500 |
| LookUpDiagonal | look-up-diagonal | look-up-diagonal | 是 | look-up-diagonal:500 |
| LookUpFront | look-up-front | look-up-front | 是 | look-up-front:500 |
| LookUpBack | look-up-back | look-up-back | 是 | look-up-back:500 |
| Turn | turn-quarter-step | turn-quarter-settle | 否 | turn-quarter-step:110 → turn-front-a:110 → turn-front-b:110 → turn-quarter-settle:110 |
| Lift | lift | lift | 是 | lift:500 |
| Bite | bite | bite | 否 | bite:400 |
| TakingOffFront | flight-front-settle | flight-front-takeoff | 否 | flight-front-settle:100 → flight-front-takeoff:120 |
| FlyingFront | flight-front-up | flight-front-mid | 是 | flight-front-up:90 → flight-front-mid:65 → flight-front-down:90 → flight-front-mid:65 |
| LandingFront | flight-front-brake | flight-front-settle | 否 | flight-front-brake:130 → flight-front-settle:110 |

## 帧来源、共享关系与所有引用位置

位置从 1 开始。整帧复用可能保留兼容导出文件；共同来源只改一次。合成层读取原始导入帧，self 层表示该帧的原始素材。

| 帧 | 原始导入来源 | 依赖关系 | 动画引用位置 |
| --- | --- | --- | --- |
| idle | art/model/model-sheet.png；canonical side; shared 41px side scale | 独立帧 | Idle[1]首帧；Idle[7]末帧 |
| idle-quarter | art/model/model-sheet.png；canonical quarter; shared 41px side scale | 独立帧 | Idle[2]；Idle[6] |
| idle-front | art/model/model-sheet.png；canonical front; shared 41px side scale | 独立帧 | Idle[3]；Idle[5] |
| idle-front-blink | art/model/model-sheet.png；canonical front-blink; shared 41px side scale | 独立帧 | Idle[4] |
| look-back | art/model/model-sheet.png；canonical rear; shared 41px side scale | 独立帧 | LookBack[1]首帧末帧 |
| look | art/actions/look.png；3x2 cell 1 | 整帧复用 ← idle；交互区域 ← idle | Look[1]首帧末帧 |
| look-front | art/actions/look.png；3x2 cell 2 | 统一色板 ← idle；人工复核参考 ← art/model/model-sheet.png | LookFront[1]首帧末帧 |
| look-up-diagonal | art/actions/look.png；3x2 cell 4 | 人工复核参考 ← art/model/model-sheet.png | LookUpDiagonal[1]首帧末帧 |
| look-up-front | art/actions/look.png；3x2 cell 5 | 人工复核参考 ← art/model/model-sheet.png | LookUpFront[1]首帧末帧 |
| look-up-back | art/actions/look.png；3x2 cell 6 | 人工复核参考 ← art/model/model-sheet.png | LookUpBack[1]首帧末帧 |
| turn-quarter-step | art/actions/turn.png；2x2 cell 1 | 局部合成 ← idle-quarter；局部合成 ← idle-quarter；局部合成 ← idle-quarter；局部合成 ← turn-quarter-step；交互区域 ← idle-quarter | Turn[1]首帧 |
| turn-front-a | art/actions/turn.png；2x2 cell 2 | 局部合成 ← idle-front；局部合成 ← idle-front；局部合成 ← idle-front；局部合成 ← turn-front-a；交互区域 ← idle-front | Turn[2] |
| turn-front-b | art/actions/turn.png；2x2 cell 3 | 局部合成 ← idle-front；局部合成 ← idle-front；局部合成 ← idle-front；局部合成 ← turn-front-b；交互区域 ← idle-front | Turn[3] |
| turn-quarter-settle | art/actions/turn.png；2x2 cell 4 | 局部合成 ← idle-quarter；局部合成 ← idle-quarter；局部合成 ← idle-quarter；局部合成 ← turn-quarter-settle；交互区域 ← idle-quarter | Turn[4]末帧 |
| beg1 | art/actions/beg.png；2x2 cell 1 | 独立帧 | Beg[1]首帧；Beg[5]末帧 |
| beg2 | art/actions/beg.png；2x2 cell 2 | 独立帧 | Beg[2]；Beg[4] |
| beg3 | art/actions/beg.png；2x2 cell 3 | 独立帧 | Beg[3] |
| pet | art/actions/pet.png；2x2 cell 1 | 缩放基准 ← art/actions/beg.png；人工复核参考 ← art/actions/beg.png | Pet[1]首帧；Pet[5]末帧 |
| pet2 | art/actions/pet.png；2x2 cell 2 | 缩放基准 ← art/actions/beg.png；人工复核参考 ← art/actions/beg.png | Pet[2]；Pet[4] |
| pet3 | art/actions/pet.png；2x2 cell 3 | 缩放基准 ← art/actions/beg.png；人工复核参考 ← art/actions/beg.png | Pet[3] |
| sleep1 | art/actions/sleep.png；2x2 cell 1 | 独立帧 | Sleep[1]首帧 |
| sleep2 | art/actions/sleep.png；2x2 cell 2 | 独立帧 | Sleep[2]末帧 |
| walk1 | art/model/model-sheet.png；canonical side torso + rig far/near feet; stride step 1 | 独立图层源 ← art/actions/walk-rig.png | Walk[1]首帧 |
| walk2 | art/model/model-sheet.png；canonical side torso + rig far/near feet; stride step 2 | 独立图层源 ← art/actions/walk-rig.png | Walk[2] |
| walk3 | art/model/model-sheet.png；canonical side torso + rig far/near feet; stride step 3 | 独立图层源 ← art/actions/walk-rig.png | Walk[3] |
| walk4 | art/model/model-sheet.png；canonical side torso + rig far/near feet; stride step 4 | 独立图层源 ← art/actions/walk-rig.png | Walk[4] |
| walk5 | art/model/model-sheet.png；canonical side torso + rig far/near feet; stride step 5 | 独立图层源 ← art/actions/walk-rig.png | Walk[5] |
| walk6 | art/model/model-sheet.png；canonical side torso + rig far/near feet; stride step 6 | 独立图层源 ← art/actions/walk-rig.png | Walk[6] |
| walk7 | art/model/model-sheet.png；canonical side torso + rig far/near feet; stride step 7 | 独立图层源 ← art/actions/walk-rig.png | Walk[7] |
| walk8 | art/model/model-sheet.png；canonical side torso + rig far/near feet; stride step 8 | 独立图层源 ← art/actions/walk-rig.png | Walk[8] |
| walk9 | art/model/model-sheet.png；canonical side torso + rig far/near feet; stride step 9 | 独立图层源 ← art/actions/walk-rig.png | Walk[9] |
| walk10 | art/model/model-sheet.png；canonical side torso + rig far/near feet; stride step 10 | 独立图层源 ← art/actions/walk-rig.png | Walk[10] |
| walk11 | art/model/model-sheet.png；canonical side torso + rig far/near feet; stride step 11 | 独立图层源 ← art/actions/walk-rig.png | Walk[11] |
| walk12 | art/model/model-sheet.png；canonical side torso + rig far/near feet; stride step 12 | 独立图层源 ← art/actions/walk-rig.png | Walk[12]末帧 |
| flight-takeoff | art/actions/flight.png；3x2 cell 1; canonical side head registration | 独立图层源 ← art/model/model-sheet.png；统一色板 ← idle；人工复核参考 ← art/model/model-sheet.png | TakingOff[2]末帧 |
| flight-up | art/actions/flight.png；3x2 cell 2; canonical side head registration | 独立图层源 ← art/model/model-sheet.png；统一色板 ← idle；人工复核参考 ← art/model/model-sheet.png | Flying[1]首帧 |
| flight-mid | art/actions/flight.png；3x2 cell 3; canonical side head registration | 独立图层源 ← art/model/model-sheet.png；统一色板 ← idle；人工复核参考 ← art/model/model-sheet.png | Flying[2]；Flying[4]末帧 |
| flight-down | art/actions/flight.png；3x2 cell 4; canonical side head registration | 独立图层源 ← art/model/model-sheet.png；统一色板 ← idle；人工复核参考 ← art/model/model-sheet.png | Flying[3] |
| flight-brake | art/actions/flight.png；3x2 cell 5; canonical side head registration | 独立图层源 ← art/model/model-sheet.png；统一色板 ← idle；人工复核参考 ← art/model/model-sheet.png | Landing[1]首帧 |
| flight-settle | art/model/model-sheet.png；canonical side at 16px flight inset | 独立帧 | TakingOff[1]首帧；Landing[2]末帧 |
| flight-front-takeoff | art/actions/flight-front.png；3x2 cell 1; canonical front head registration | 独立图层源 ← art/model/model-sheet.png；统一色板 ← idle；人工复核参考 ← art/model/model-sheet.png | TakingOffFront[2]末帧 |
| flight-front-up | art/actions/flight-front.png；3x2 cell 2; canonical front head registration | 独立图层源 ← art/model/model-sheet.png；统一色板 ← idle；人工复核参考 ← art/model/model-sheet.png | FlyingFront[1]首帧 |
| flight-front-mid | art/actions/flight-front.png；3x2 cell 3; canonical front head registration | 独立图层源 ← art/model/model-sheet.png；统一色板 ← idle；人工复核参考 ← art/model/model-sheet.png | FlyingFront[2]；FlyingFront[4]末帧 |
| flight-front-down | art/actions/flight-front.png；3x2 cell 4; canonical front head registration | 独立图层源 ← art/model/model-sheet.png；统一色板 ← idle；人工复核参考 ← art/model/model-sheet.png | FlyingFront[3] |
| flight-front-brake | art/actions/flight-front.png；3x2 cell 5; canonical front head registration | 独立图层源 ← art/model/model-sheet.png；统一色板 ← idle；人工复核参考 ← art/model/model-sheet.png | LandingFront[1]首帧 |
| flight-front-settle | art/model/model-sheet.png；canonical front at 16px flight inset | 独立帧 | TakingOffFront[1]首帧；LandingFront[2]末帧 |
| sing1 | art/actions/sing.png；2x1 cell 1 | 独立帧 | Sing[1]首帧 |
| sing2 | art/actions/sing.png；2x1 cell 2 | 独立帧 | Sing[2]末帧 |
| stretch1 | art/actions/stretch.png；4x3 cell 1 | 独立图层源 ← art/model/model-sheet.png；整帧复用 ← idle；交互区域 ← idle | Stretch[1]首帧 |
| stretch2 | art/actions/stretch.png；4x3 cell 2 | 独立图层源 ← art/model/model-sheet.png；统一色板 ← idle | Stretch[2] |
| stretch3 | art/actions/stretch.png；4x3 cell 3 | 独立图层源 ← art/model/model-sheet.png；统一色板 ← idle | Stretch[3] |
| stretch4 | art/actions/stretch.png；4x3 cell 4 | 独立图层源 ← art/model/model-sheet.png；统一色板 ← idle | Stretch[4] |
| stretch5 | art/actions/stretch.png；4x3 cell 5 | 独立图层源 ← art/model/model-sheet.png；统一色板 ← idle | Stretch[5] |
| stretch6 | art/actions/stretch.png；4x3 cell 6 | 独立图层源 ← art/model/model-sheet.png；统一色板 ← idle | Stretch[6] |
| stretch7 | art/actions/stretch.png；4x3 cell 7 | 独立图层源 ← art/model/model-sheet.png；统一色板 ← idle | Stretch[7] |
| stretch8 | art/actions/stretch.png；4x3 cell 8 | 独立图层源 ← art/model/model-sheet.png；统一色板 ← idle | Stretch[8] |
| stretch9 | art/actions/stretch.png；4x3 cell 9 | 独立图层源 ← art/model/model-sheet.png；统一色板 ← idle | Stretch[9] |
| stretch10 | art/actions/stretch.png；4x3 cell 10 | 独立图层源 ← art/model/model-sheet.png；统一色板 ← idle | Stretch[10] |
| stretch11 | art/actions/stretch.png；4x3 cell 11 | 独立图层源 ← art/model/model-sheet.png；统一色板 ← idle | Stretch[11] |
| stretch12 | art/actions/stretch.png；4x3 cell 12 | 独立图层源 ← art/model/model-sheet.png；整帧复用 ← idle；交互区域 ← idle | Stretch[12]末帧 |
| lift | art/actions/lift.png；1x1 cell 1 | 独立帧 | Lift[1]首帧末帧 |
| bite | art/actions/bite.png；1x1 cell 1 | 独立帧 | Bite[1]首帧末帧 |

## 修改源文件的影响范围

下列帧的换色区域也需要复核；逐动画 GIF 位于 art/previews，换色预览位于其 skins 子目录。

| 源文件 | 受影响帧／换色区域 | 受影响动画／预览 |
| --- | --- | --- |
| art/actions/beg.png | beg1, beg2, beg3, pet, pet2, pet3 | Beg, Pet |
| art/actions/bite.png | bite | Bite |
| art/actions/flight-front.png | flight-front-brake, flight-front-down, flight-front-mid, flight-front-takeoff, flight-front-up | TakingOffFront, FlyingFront, LandingFront |
| art/actions/flight.png | flight-brake, flight-down, flight-mid, flight-takeoff, flight-up | TakingOff, Flying, Landing |
| art/actions/lift.png | lift | Lift |
| art/actions/look.png | look-front, look-up-back, look-up-diagonal, look-up-front | LookFront, LookUpDiagonal, LookUpFront, LookUpBack |
| art/actions/pet.png | pet, pet2, pet3 | Pet |
| art/actions/sing.png | sing1, sing2 | Sing |
| art/actions/sleep.png | sleep1, sleep2 | Sleep |
| art/actions/stretch.png | stretch10, stretch11, stretch2, stretch3, stretch4, stretch5, stretch6, stretch7, stretch8, stretch9 | Stretch |
| art/actions/turn.png | turn-front-a, turn-front-b, turn-quarter-settle, turn-quarter-step | Turn |
| art/actions/walk-rig.png | walk1, walk10, walk11, walk12, walk2, walk3, walk4, walk5, walk6, walk7, walk8, walk9 | Walk |
| art/model/model-sheet.png | flight-brake, flight-down, flight-front-brake, flight-front-down, flight-front-mid, flight-front-settle, flight-front-takeoff, flight-front-up, flight-mid, flight-settle, flight-takeoff, flight-up, idle, idle-front, idle-front-blink, idle-quarter, look, look-back, look-front, look-up-back, look-up-diagonal, look-up-front, stretch1, stretch10, stretch11, stretch12, stretch2, stretch3, stretch4, stretch5, stretch6, stretch7, stretch8, stretch9, turn-front-a, turn-front-b, turn-quarter-settle, turn-quarter-step, walk1, walk10, walk11, walk12, walk2, walk3, walk4, walk5, walk6, walk7, walk8, walk9 | TakingOff, Flying, Landing, Idle, Walk, Stretch, Look, LookFront, LookBack, LookUpDiagonal, LookUpFront, LookUpBack, Turn, TakingOffFront, FlyingFront, LandingFront |
