# 伸翅补帧与唱歌口型生成记录

模式：内置 imagegen。2026-09-29，用户确认基础版现有造型后，要求伸翅扩充到 8–16 帧，并修复唱歌不张嘴。

最终选择 12 帧，总计 1000ms：60、60、70、70、80、130、130、80、80、80、80、80ms。中段保持完整伸展，首尾直接复用 idle，支撑脚配准。当前 v3 完整使用每帧头颈运动，已移除早期固定头部覆盖。当前共 56 独立帧、19 片段。

## 伸翅初稿（历史记录，当前头颈使用下方 v3 修订）

参考图依次：修改前 `art/actions/stretch.png`、`art/model/model-sheet.png`。
最终源图：`art/actions/stretch.png`。
原件：`C:/Users/panxingyue/.codex/generated_images/01a0eae4-b461-7351-92af-44158cff5ca5/exec-55d12fe6-35e3-4153-853d-d4a024a76630.png`。

```text
Use case: identity-preserve.
Production animation sprite sheet for an existing beloved pixel cockatiel. Image1 is the existing two-pose wing stretch to EXTEND; Image2 is the approved canonical model, top-left side bird gives exact character and scale. Preserve the grey slender body, compact yellow skull, solid tiny dark eye, orange cheek, small grey closed beak, long slender left-pointing tail, white wing panel and pink feet. DO NOT redesign this bird, do not enlarge its head.
Create a FOUR COLUMNS by THREE ROWS atlas, exactly TWELVE full-body sequential sprites read left to right, then top to bottom, on perfectly flat solid #FF00FF magenta for production keying. No text, numerals, borders, shadow, floor, props or texture. 2048x1536 canvas. Generous equal gutters. Each cell has the SAME camera, body physical scale, sole baseline, head/neck location, and pixel density. Whole sprite around41 logical pixels high, shown in crisp enlarged low-resolution pixel art, with near-black one-pixel contours and simple warm grey/yellow/white clusters. Every complete silhouette fits its cell without touching neighbors. Feather noise or smooth painting forbidden.
The bird always faces RIGHT, long tail points LEFT, standing grounded, never flaps into flight. Make a quiet sleepy ONE-WING stretch, coherent smooth easing, expanding backward/left alongside the tail, NOT vertical airplane wings. Head/neck/chest stay the exact same compact size and stable location in ALL cells. Only near wing and near leg move; far foot remains planted at same location. Closed beak, calm dark eye.
Row1: (1) wings folded and both feet grounded, matching canonical side standing; (2) near shoulder lifts slightly; (3) wing begins to separate backward, tiny gap; (4) wing opens halfway backward, feathers start to fan, near foot begins gentle backward extension.
Row2: (5) wing 70percent open; (6) wing fully stretches backward/down alongside tail, near leg extended back, far foot planted; (7) full gentle stretch hold, feather tips relax a tiny pixel; (8) begin releasing, wing80percent open, near leg begins to return.
Row3: (9) wing60percent closed; (10) wing nearly folded, foot settling; (11) wing folds flush, both feet settle; (12) original calm folded-wing standing exactly as frame1.
These twelve phases form ONE stretch open-hold-close cycle, not twelve unrelated wing postures. Keep tail, torso, head, eye, cheek, crest, skull size and planted foot completely consistent. Max backward wing extension stays within the existing tail length. No horizontal drift, shrinking or growing. Frame6/7 match the natural extended pose of image1 but use the fixed canonical head scale.
```

## 唱歌

编辑目标：修改前 `art/actions/sing.png`。
最终源图：`art/actions/sing.png`。
原件：`C:/Users/panxingyue/.codex/generated_images/01a0eae4-b461-7351-92af-44158cff5ca5/exec-c27748a6-c6e3-4648-9e49-c0fafc63cba4.png`。

```text
Use case: precise-object-edit. The attached image is an existing TWO-cell side-facing pixel cockatiel singing sheet. Edit ONLY each bird's beak and immediate mouth gap, leave every other body/head/eye/cheek/crest/wing/tail/feet pixel and position/size unchanged. Keep the original canvas, two cells side by side, and solid magenta background unchanged.
Problem: the mouth opening disappears when each bird is exported at41pixels tall. Make a SMALL but CLEARLY READABLE open singing beak at that target size. Left bird: beak half-open with a 2 logical pixel dark cavity vertically between small upper and lower grey mandibles. Right bird: beak fully open with a 3 logical pixel dark cavity, lower mandible slightly lower. Cavity is a dark warm charcoal colour, not transparent magenta. Upper beak remains short grey curved hook, lower beak is a tiny grey edge. Preserve a narrow magenta notch outside the gape so the two mandibles read as separated at small size. Mouth should read as an open bird beak, no human smile, tongue, lips, teeth or oversized gape. This must visibly open-and-close when exported with the runtime mouth animation. Both keep the same head angle and skull size. Pixel outlines and palette unchanged. No extra sprites, no effects, no notes, no changes beyond the mouths.
```

缩小后口型由现有 BeakAnimator 在已复核的两列三行嘴部区域生成闭嘴／微张／张嘴三个状态，完全张嘴保留 2×2 深色嘴腔。其余头身像素不动，鸟喙颜色取当前配色。音频播放时继续使用声音口型时间线；无音频的唱歌预览按动作时间展示口型，避免持续被闭嘴覆盖。运行时效果需查看实际程序渲染，不只检查原始 Sing PNG。

本阶段只打包 Debug，完整来源与片段引用见 [ANIMATION_REFERENCES.md](ANIMATION_REFERENCES.md)。


## v3 伸翅头颈冻结修复

模式：内置 imagegen。编辑目标为 v2 的 `art/actions/stretch.png`，第二张参考为 `art/model/model-sheet.png`。
最终项目源图仍为 `art/actions/stretch.png`；生成原件：`C:/Users/panxingyue/.codex/generated_images/01a0eae4-b461-7351-92af-44158cff5ca5/exec-50a1b10c-1e50-4656-bda2-03250384c0d4.png`。

导入保留整张动作帧及逐帧头部交互区域，只按支撑脚设置落点，不再把上半身替换成固定的模型侧面。共同缩放、12 帧 1000ms、首末帧 idle 复用保持不变。唱歌资源和口型逻辑不变。检查必须验证在足部配准后，动作中段头部确实移动，不能再以“各帧头部像素相同”为通过条件。

```text
Use case: precise-object-edit. Image 1 is the EDIT TARGET: an existing 4-column 3-row 12-frame pixel-art cockatiel waking one-wing stretch sheet. Image 2 is the CHARACTER REFERENCE: the top-left neutral side bird sets the compact head volume and face proportions.
Correct the frozen-head animation by editing ONLY each bird's HEAD AND CONNECTING NECK, including their silhouette, over this existing 12-frame sequence. Preserve ALL existing wings, torso below the shoulder, feet, tails, cell positions, image dimensions 1448x1086, magenta background, palette, and pixel grid. Do not regenerate the whole bird. No moving the support foot. No copying the same neutral head into every cell.
Make the head and neck move as one connected organic extension of the stretch: small forward-right reach and gentle downward tilt as the wing opens, relaxing back when it closes. The compact skull retains the exact SAME volume and face identity throughout; don't scale the head, don't inflate cheeks or forehead. Crest follows head tilt naturally. Eye is the same tiny black eye, orange cheek stays same size, little grey beak remains closed. Keep head firmly joined to the grey neck without a cut seam or detached sticker appearance.
Detailed row-major timing (left to right each row):
Frames1,2: neutral stance; frame2 begins a tiny forward lean.
Frame3: head tilts down ~5degrees and reaches forward 1 logical pixel.
Frame4: ~10degrees, forward1 and down1 logical pixel.
Frame5: ~15degrees, forward2 and down2.
Frames6,7 (full stretch): ~20degrees down, forward2 and down3 logical pixels from neutral, neck naturally reaching forward. Crest leans with the head, the beak points slightly down-right. Skull NOT smaller, same head size.
Frame8: relax to ~15degrees, forward2 down2.
Frame9: ~10degrees forward1 down1.
Frame10: ~5degrees forward1.
Frame11: settle nearly neutral.
Frame12: exactly the same neutral head/neck as frame1.
One logical pixel is approximately6 image pixels in this input. Motion should be visibly distinct by the middle frames but subtle and calm, not deep bowing, pecking, bobblehead, bouncing, or a complete turn. Head/neck translation and rotation should follow smooth easing between adjacent cells. Keep the sole baselines and all wing/leg poses unchanged, including the fully extended wing/leg in middle frames. Exact original magenta background, no text, labels, grid, shadows or new objects.
```

