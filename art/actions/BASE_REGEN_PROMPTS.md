# 基础版整套动画生成记录

更新：2026-09-29。模式：内置 imagegen（未使用 CLI/API）。用户选定基础版及基础版低像素稿，授权重生成现有全部动画资源。

## 批准参考

1. `art/model/drafts/pixel-comparison-v1/base-pixel.png`：角色造型、配色及头身比例
2. `art/model/drafts/pixel-comparison-v2/base-low-pixel.png`：低像素块面与细节密度

只使用基础版设计；A／B 团子等其他方案不参与本轮。运行时读取项目内的最终源图与导出帧，不读取生成缓存。

## 最终源文件

缓存目录：`C:/Users/panxingyue/.codex/generated_images/01a0eae4-b461-7351-92af-44158cff5ca5/`。

| 项目最终源文件 | 生成缓存原件 |
| --- | --- |
| `art/model/model-sheet.png` | `exec-719ac43c-ea64-43f3-add0-55f819a952ca.png` |
| `art/actions/look.png` | `exec-e9180a2c-3828-4bf5-b2d5-cfbdac371d38.png` |
| `art/actions/beg.png` | `exec-96624b60-37cb-4b4a-a2eb-1d704c5e5bca.png` |
| `art/actions/pet.png` | `exec-914eab51-d102-48c7-8f62-570726b199c8.png` |
| `art/actions/turn.png` | `exec-509dfbee-b9cb-4e26-bc72-557a45536ab9.png` |
| `art/actions/sleep.png` | `exec-1bbd5b38-9fa7-4b40-8db9-bd9600379f5f.png` |
| `art/actions/walk-rig.png` | `exec-8a643ec6-9e95-4eac-9704-fc190d1c03cd.png` |
| `art/actions/sing.png` | `exec-12b222dd-df51-4922-a06c-e9043d5cf685.png` |
| `art/actions/stretch.png` | `exec-3d283967-e169-4e4a-b30b-c689f73f324f.png` |
| `art/actions/lift.png` | `exec-7f66bbad-e6ed-4207-971d-c258d10ac205.png` |
| `art/actions/bite.png` | `exec-0e775a13-7d4e-43ac-9820-6cabdcdb20fe.png` |
| `art/actions/flight.png` | `exec-b7c1fe14-0a5c-427d-9328-d84bbac7089c.png` |

模型先生成初稿，再只修正斜面角度；各动作生成时引用初稿，最终模型替换后已重新核对。飞行初稿背景为绘制的棋盘格，背景修订后才导入。下面保留实际使用的完整提示词与输入。

## 主模型初稿

输入参考（依次）：`art/model/drafts/pixel-comparison-v1/base-pixel.png`、`art/model/drafts/pixel-comparison-v2/base-low-pixel.png`。
输出缓存：`exec-26353f36-2d42-41a2-8459-bd695730f0a8.png`。

```text
Use case: stylized-concept. Production pixel-art canonical model sprites for a desktop cockatiel pet.
Image 1 is the user-approved BASE character: preserve its compact rounded skull, slender upright grey pear body, long tail, yellow swept crest and orange cheek. Image 2 is the same character's approved LOW-PIXEL treatment: use its simple broad pixel clusters, tiny solid dark eyes and economical feathers. These are the ONLY design references. Do not use a big-head mochi or flat-sticker redesign.
Create exactly SIX isolated full cockatiel sprites in a uniform THREE COLUMNS by TWO ROWS atlas, no labels or text. Plain exact saturated magenta #FF00FF background for production keying, absolutely no shadows, texture or props.
All birds have the SAME PHYSICAL SCALE: 40-41 logical pixels from crest to sole in a neutral standing side pose, shown as crisp approximately 10x nearest-neighbour magnification. Compact skull about 11 logical pixels wide, face about 9 logical pixels tall excluding crest, long graceful grey tail, black beak only 3 logical pixels wide, pink feet. 12-16 solid colours. Muted warm grey feathers, butter yellow head/crest, saturated orange cheek, white folded-wing panel, charcoal-grey outlines. Clean square pixel clusters, 1-logical-pixel contour; no soft gradients or tiny noisy details. Front eyes equal size, solid black 2x2 clusters near outer sides of skull, no highlights. Same face identity across angles.
Row 1 left to right:
1 TRUE RIGHT-FACING SIDE, beak and chest toward RIGHT, tail LEFT; both pink feet grounded.
2 TRUE FRONT standing, symmetric folded wings, compact natural head, widely spaced equal eyes, SMALL beak, no giant triangular forehead.
3 TRUE BACK standing, no eyes/beak, grey nape below yellow crest, foreshortened tail stays at or above foot soles.
Row 2 left to right:
4 THREE-QUARTER BODY angle halfway from right-facing side toward viewer; head and body both rotate, tail foreshortens. Both feet grounded.
5 EXACT SAME FRONT POSE as cell 2 with ONLY eyes changed to two short relaxed horizontal closed-eyelid marks. Same head/body/crest/feet pixel layout.
6 BODY STILL RIGHT-FACING with tail LEFT and feet unchanged, but HEAD turns over shoulder to LEFT, one eye and left-pointing beak. Twist only neck; don't flip body, don't enlarge head.
Each row shares the same sole baseline; all birds fully inside their cell with wide magenta gutters and at least 10 percent clear cell margins. Crest can project differently in rotation but skull size never changes. Six isolated connected silhouettes; each foot connects to body. This is a production sheet, not a labelled reference poster.
```

## 主模型斜面修订

编辑目标：`exec-26353f36-2d42-41a2-8459-bd695730f0a8.png`；最终输出：`exec-719ac43c-ea64-43f3-add0-55f819a952ca.png`。

```text
Use case: precise-object-edit. This is a six-cell canonical model sheet (3 columns x2rows). Change ONLY the BOTTOM-LEFT bird (cell4). It is currently too close to a side profile. Redraw ONLY that bird into a TRUE THREE-QUARTER VIEW halfway from right-facing side toward front: chest turned 45 degrees TOWARD VIEWER, both wing edges visible (near wing broad, far wing narrow), tail substantially foreshortened projecting diagonally lower-left behind torso, not long horizontal profile. Head ALSO turned halfway towardviewer with the beak projecting slightly RIGHT of facial centre, near eye fully visible and far eye as a smaller dark pixel at far edge. Same COMPACT SKULL SIZE as top-centre front bird and top-left side bird, no new swelling of face. Same body volume and same height, same two planted pink feet at same bottom baseline as other bottom row birds. Keep the style exactly matching all other birds: simple warmgrey, buttery yellow, white wing, orangecheek, dark tinyeyes, chunky pixels.
Every other cell stays EXACTLY UNCHANGED: top row side/front/back, bottom-middle blink, bottom-right head-back. Preserve magenta background, 1536x1024 canvas, all other sprite positions, sizes, palette and details. Do not redraw any other bird. This 45-degree intermediate must visibly bridge side and front for a turn animation.
```

## look

项目文件：`art/actions/look.png`。
输入参考：`exec-26353f36-2d42-41a2-8459-bd695730f0a8.png`。
生成缓存：`exec-e9180a2c-3828-4bf5-b2d5-cfbdac371d38.png`。

```text
Production animation sprite sheet for the SAME BASE cockatiel as the attached canonical model. Preserve its exact compact rounded skull, small solid dark eye, little grey beak, swept thin yellow crest, orange cheek, slender pear-shaped grey body, white folded-wing panel, long tapered tail, small pink feet. BASE DESIGN ONLY, not a large-headed mochi bird. Crisp low-resolution pixel art, ~40 logical pixels tall when standing, 1-pixel charcoal contour, 12-16 solid colours, no gradients, anti-aliasing, texture, fuzzy feathers or tiny highlights. Pixel clusters and face/head scale must match the reference, changes only for the stated pose. Body normally faces RIGHT, tail extends LEFT. No text, labels, borders, grids, shadows, props or extra characters. Exactly the specified isolated sprites, fully inside uniformly arranged cells with wide clear gutters.

Create SIX gaze sprites in THREE columns TWO rows on solid #FF00FF magenta. All six bodies and feet must be IDENTICAL right-facing upright bodies, same size, tail extends left, feet on same baseline. Only rotate or tilt HEAD, centred over the shoulder.
Top row: (1) head forward right side profile; (2) head toward VIEWER with two small equal black eyes, head alone turns front while body stays right; (3) head backward to LEFT, body STILL right. Bottom row: (4) head lifted about25degrees up-right; (5) head tilted up looking toward viewer; (6) head lifted up-left over shoulder. Keep the skull the same compact volume in all poses, not swollen frontal head. Use same black 2x2 eyes, orange cheeks, little grey beak and crest as reference. Crest rotation changes its height naturally; never stretch the head. All six feet/wing/tail silhouettes remain identical. Give generous margins and no touching between birds.
```

## beg

项目文件：`art/actions/beg.png`。
输入参考：`exec-26353f36-2d42-41a2-8459-bd695730f0a8.png`。
生成缓存：`exec-96624b60-37cb-4b4a-a2eb-1d704c5e5bca.png`。

```text
Production animation sprite sheet for the SAME BASE cockatiel as the attached canonical model. Preserve its exact compact rounded skull, small solid dark eye, little grey beak, swept thin yellow crest, orange cheek, slender pear-shaped grey body, white folded-wing panel, long tapered tail, small pink feet. BASE DESIGN ONLY, not a large-headed mochi bird. Crisp low-resolution pixel art, ~40 logical pixels tall when standing, 1-pixel charcoal contour, 12-16 solid colours, no gradients, anti-aliasing, texture, fuzzy feathers or tiny highlights. Pixel clusters and face/head scale must match the reference, changes only for the stated pose. Body normally faces RIGHT, tail extends LEFT. No text, labels, borders, grids, shadows, props or extra characters. Exactly the specified isolated sprites, fully inside uniformly arranged cells with wide clear gutters.

Create exactly FOUR frames of ONE GENTLE BOW-TO-ASK-FOR-HEAD-PETTING action in TWO columns TWO rows on solid #FF00FF magenta. All birds SIDE FACING RIGHT, same pear body, long tail LEFT, same feet baseline, folded wings, stable body size. Top-left frame1: small gentle head bow of25degrees, eye open, lowering crown toward right while body stays planted. Top-right frame2: lower bow45degrees, beak pointing diagonallydown-right, crest sweptback. Bottom-left frame3: comfortable deep bow55degrees, neck nestled forward, crown offered for touch, slight lowered shoulder at most1logicalpixel. Bottom-right frame4: same as frame2. Keep actual skull size unchanged, do not miniaturize whole bird when bowing. Reduce apparent total HEIGHT naturally as head lowers. No full horizontal body flattening. Same orange cheek diameter and body volume as canonical right-facing pose. Each cell feet and tail same position, head moves smoothly around neck, no hands, effects or hearts. These frames will alternate slowly as one calm invitation.
```

## walk-rig

项目文件：`art/actions/walk-rig.png`。
输入参考：`exec-26353f36-2d42-41a2-8459-bd695730f0a8.png`。
生成缓存：`exec-8a643ec6-9e95-4eac-9704-fc190d1c03cd.png`。

```text
Production animation sprite sheet for the SAME BASE cockatiel as the attached canonical model. Preserve its exact compact rounded skull, small solid dark eye, little grey beak, swept thin yellow crest, orange cheek, slender pear-shaped grey body, white folded-wing panel, long tapered tail, small pink feet. BASE DESIGN ONLY, not a large-headed mochi bird. Crisp low-resolution pixel art, ~40 logical pixels tall when standing, 1-pixel charcoal contour, 12-16 solid colours, no gradients, anti-aliasing, texture, fuzzy feathers or tiny highlights. Pixel clusters and face/head scale must match the reference, changes only for the stated pose. Body normally faces RIGHT, tail extends LEFT. No text, labels, borders, grids, shadows, props or extra characters. Exactly the specified isolated sprites, fully inside uniformly arranged cells with wide clear gutters.

Create a production rig of exactly THREE ISOLATED PARTS arranged HORIZONTALLY on plain #FF00FF magenta.
LEFT: the exact canonical RIGHT-facing side bird UPPER BODY from reference (including head, wings, belly, tail LEFT) with both pink legs/feet COMPLETELY REMOVED. Complete continuous belly outline, no pink stumps. This body must have exactly same size and shape as the canonical standing side frame above its legs, not squat, wide or largeheaded. Head41px character scale, body-without-feet ~37 logical pixels tall. Centre of mass above legs; no movement pose.
MIDDLE: ONE small light pink foot with short vertical ankle, side toes pointing RIGHT, about7logicalpixels wide and5 high, forward toes and rear toe, foot flat at bottom.
RIGHT: the identical foot silhouette in slightly darker muted rose for the far foot. Both separate feet much SMALLER than body (one tenth body height), identical pixel density, no giant shoes. Wide magenta gaps between all 3 components. No full bird with feet, no ground. Art layers are to be assembled without redraw for a distance-matched walking cycle.
```

## flight

项目文件：`art/actions/flight.png`。
输入参考：`exec-26353f36-2d42-41a2-8459-bd695730f0a8.png`。
生成缓存：`exec-4782d533-a483-484d-99a6-261e93f2b00f.png`。此为背景修订前初稿。

```text
Production animation sprite sheet for the SAME BASE cockatiel as the attached canonical model. Preserve its exact compact rounded skull, small solid dark eye, little grey beak, swept thin yellow crest, orange cheek, slender pear-shaped grey body, white folded-wing panel, long tapered tail, small pink feet. BASE DESIGN ONLY, not a large-headed mochi bird. Crisp low-resolution pixel art, ~40 logical pixels tall when standing, 1-pixel charcoal contour, 12-16 solid colours, no gradients, anti-aliasing, texture, fuzzy feathers or tiny highlights. Pixel clusters and face/head scale must match the reference, changes only for the stated pose. Body normally faces RIGHT, tail extends LEFT. No text, labels, borders, grids, shadows, props or extra characters. Exactly the specified isolated sprites, fully inside uniformly arranged cells with wide clear gutters.

Create exactly SIX flight animation frames in THREE columns TWO rows, on a GENUINELY TRANSPARENT alpha background. No checkerboard or opaque background, no shadow.
The bird faces RIGHT, same compact head size and same narrow body length in all six cells, tail extends LEFT. Wings are grey with white flight-feather panels. Head/torso physical scale SAME as grounded canonical character; reserve wider margins for spread wings instead of shrinking the bird.
Top row: (1) TAKEOFF, feet pushing downward, wings lifting upward to launch; (2) FLAP-UP, wings swept high above back, feet tucked; (3) FLAP-MID, wings spread sidewise at shoulderheight, feet tucked.
Bottom row: (4) FLAP-DOWN, wings swept down beside torso, tips beneath belly, head unchanged; (5) BRAKE/LANDING, chest slightly upright, wings forward/up spread braking, feet reaching down ready contact; (6) SETTLE, body in normal right-facing standing proportion, wings nearly folded, feet on ground.
Only change wing angle, slight body pitch and feet appropriate to flight. The face stays compact with one solid dark eye, orange cheek same size, and small grey hooked beak. No extremely long neck, no huge flight head, no human arms. Each bird one connected silhouette, wings connected at shoulders. All six fully visible within cells with wide transparent gutters. Consistent 1pixel contour and simple pixel clusters at same density as reference. No labels. Neutral settle must closely match canonical side.
```

## pet

项目文件：`art/actions/pet.png`。
输入参考：`exec-96624b60-37cb-4b4a-a2eb-1d704c5e5bca.png`。
生成缓存：`exec-914eab51-d102-48c7-8f62-570726b199c8.png`。

```text
Use case: precise-object-edit. Edit the attached four-pose cockatiel BOW sheet into the matching ENJOYING HEAD PETTING sheet. Preserve the exact same four full-bird positions, all dimensions, body/tail/crest/feet shapes, pink sole baselines, palette, pixel density and magenta background. Change ONLY each visible open square eye to a short relaxed closed-eyelid dark pixel arc/line expressing pleasure. Tiny subtle cheek lift if needed but do NOT increase head size. No hands, hearts, effects, extra birds or scale changes. All four are the same underlying bowed frames with closed eyes. The first THREE cells are used for a slow calm breathing loop. It is essential that this image retains the exact canvas dimensions and same body scale as the input.
```

## turn

项目文件：`art/actions/turn.png`。
输入参考：`exec-26353f36-2d42-41a2-8459-bd695730f0a8.png`。
生成缓存：`exec-509dfbee-b9cb-4e26-bc72-557a45536ab9.png`。

```text
Production animation sprite sheet for the SAME BASE cockatiel as the attached canonical model. Preserve its exact compact rounded skull, small solid dark eye, little grey beak, swept thin yellow crest, orange cheek, slender pear-shaped grey body, white folded-wing panel, long tapered tail, small pink feet. BASE DESIGN ONLY, not a large-headed mochi bird. Crisp low-resolution pixel art, ~40 logical pixels tall when standing, 1-pixel charcoal contour, 12-16 solid colours, no gradients, anti-aliasing, texture, fuzzy feathers or tiny highlights. Pixel clusters and face/head scale must match the reference, changes only for the stated pose. Body normally faces RIGHT, tail extends LEFT. No text, labels, borders, grids, shadows, props or extra characters. Exactly the specified isolated sprites, fully inside uniformly arranged cells with wide clear gutters.
Create FOUR full-body sprites in TWO columns TWO rows on plain #FF00FF magenta of one IN-PLACE STEPPING TURN. Scale matched to model.
Frame1 top-left: body and head THREE-QUARTER RIGHT, about45degrees towardviewer, near foot raised a tiny1logicalpixel, farfoot planted. Tail foreshortened extends diagonallyleft behind body.
Frame2 top-right: FULL FRONT, left foot slightly lifted/bent toes, rightfoot planted.
Frame3 bottom-left: same fullfront pose, other foot slightly lifts, firstfoot planted.
Frame4 bottom-right: same threequarter RIGHT pose as frame1, BOTH feet down.
Pivot stays centred between feet, feet no more than1-2logicalpixels lift, subtle repositioning step not jump. Head/body consistent size throughout. Four sole baselines equal, never scale sprites separately. No rear views. This sheet is for foot animation; upper body will share canonical model exactly, so keep feet attached at consistent belly height.
```

## sleep

项目文件：`art/actions/sleep.png`。
输入参考：`exec-26353f36-2d42-41a2-8459-bd695730f0a8.png`。
生成缓存：`exec-1bbd5b38-9fa7-4b40-8db9-bd9600379f5f.png`。

```text
Production animation sprite sheet for the SAME BASE cockatiel as the attached canonical model. Preserve its exact compact rounded skull, small solid dark eye, little grey beak, swept thin yellow crest, orange cheek, slender pear-shaped grey body, white folded-wing panel, long tapered tail, small pink feet. BASE DESIGN ONLY, not a large-headed mochi bird. Crisp low-resolution pixel art, ~40 logical pixels tall when standing, 1-pixel charcoal contour, 12-16 solid colours, no gradients, anti-aliasing, texture, fuzzy feathers or tiny highlights. Pixel clusters and face/head scale must match the reference, changes only for the stated pose. Body normally faces RIGHT, tail extends LEFT. No text, labels, borders, grids, shadows, props or extra characters. Exactly the specified isolated sprites, fully inside uniformly arranged cells with wide clear gutters.
Create FOUR frames in TWO columns TWO rows of ONE quiet SLEEP action, solid #FF00FF magenta. Bird SIDE RIGHT, long tail left, wings folded, feet down. Head gently tucked downward toward its breast/shoulder, eyelid closed in relaxed short dark mark, crest lying backward. Grey body stays SAME physical size as standing model, feathers only slightly relaxed, not a ball or miniature chick. Top-left frame1: asleep still, neck softlytucked. Top-right frame2: tiny breathing expansion of breast at most1logicalpixel while feet and head staystill. Bottom-left repeatsframe1. Bottom-right repeatsframe2. This is a subtle two-frame breathing loop, no growing/shrinking and no head bob, no Zzz or symbols. Maintain same foot baseline and original head volume.
```

## sing

项目文件：`art/actions/sing.png`。
输入参考：`exec-26353f36-2d42-41a2-8459-bd695730f0a8.png`。
生成缓存：`exec-12b222dd-df51-4922-a06c-e9043d5cf685.png`。

```text
Production animation sprite sheet for the SAME BASE cockatiel as the attached canonical model. Preserve its exact compact rounded skull, small solid dark eye, little grey beak, swept thin yellow crest, orange cheek, slender pear-shaped grey body, white folded-wing panel, long tapered tail, small pink feet. BASE DESIGN ONLY, not a large-headed mochi bird. Crisp low-resolution pixel art, ~40 logical pixels tall when standing, 1-pixel charcoal contour, 12-16 solid colours, no gradients, anti-aliasing, texture, fuzzy feathers or tiny highlights. Pixel clusters and face/head scale must match the reference, changes only for the stated pose. Body normally faces RIGHT, tail extends LEFT. No text, labels, borders, grids, shadows, props or extra characters. Exactly the specified isolated sprites, fully inside uniformly arranged cells with wide clear gutters.
Create exactly TWO sprites side by side in ONE ROW on #FF00FF magenta: ONE SING/CHIRP animation. Both the exact normal right-facing side bird from reference, head/crest/body/tail/feet at same scale and position.
Frame1 beak just slightly open singing, neck natural and eye bright tiny soliddark.
Frame2 beak a little wider open, crown raised no more than1logicalpixel, chest gently lifted, pinkfeet planted exactlysame.
Keep beak tiny, no huge open mouth, no visible human tongue or teeth. Same skin colour and pixel density. No musicnotes, effects or props. Wide horizontal gutters and margin; both complete birds identical physical size.
```

## stretch

项目文件：`art/actions/stretch.png`。
输入参考：`exec-26353f36-2d42-41a2-8459-bd695730f0a8.png`。
生成缓存：`exec-3d283967-e169-4e4a-b30b-c689f73f324f.png`。

```text
Production animation sprite sheet for the SAME BASE cockatiel as the attached canonical model. Preserve its exact compact rounded skull, small solid dark eye, little grey beak, swept thin yellow crest, orange cheek, slender pear-shaped grey body, white folded-wing panel, long tapered tail, small pink feet. BASE DESIGN ONLY, not a large-headed mochi bird. Crisp low-resolution pixel art, ~40 logical pixels tall when standing, 1-pixel charcoal contour, 12-16 solid colours, no gradients, anti-aliasing, texture, fuzzy feathers or tiny highlights. Pixel clusters and face/head scale must match the reference, changes only for the stated pose. Body normally faces RIGHT, tail extends LEFT. No text, labels, borders, grids, shadows, props or extra characters. Exactly the specified isolated sprites, fully inside uniformly arranged cells with wide clear gutters.
Create TWO full sprites in ONE HORIZONTAL ROW on #FF00FF magenta of a natural ONE-WING STRETCH, both body/head RIGHT, tail LEFT, feetplanted. Same bird physical scale/head size as model.
Frame1: near wing slightly lifted from body and extends a little backward left, white wing stripe visible.
Frame2: near wing extends diagonally BACK towardleft/down slightly, fanning grey and white feather shapes alongside tail. Bird gently stretches one leg back too but other foot plants. Keep wing and tail fully within modest width, no massive flight wings; crest, skull and chest remain originalscale, mouthclosed. No arm-like gestures, no wing overface. Ground baseline same. Two gentle consecutive phases of opening a wing, no stretching body itself, no labels.
```

## lift

项目文件：`art/actions/lift.png`。
输入参考：`exec-26353f36-2d42-41a2-8459-bd695730f0a8.png`。
生成缓存：`exec-7f66bbad-e6ed-4207-971d-c258d10ac205.png`。

```text
Production animation sprite sheet for the SAME BASE cockatiel as the attached canonical model. Preserve its exact compact rounded skull, small solid dark eye, little grey beak, swept thin yellow crest, orange cheek, slender pear-shaped grey body, white folded-wing panel, long tapered tail, small pink feet. BASE DESIGN ONLY, not a large-headed mochi bird. Crisp low-resolution pixel art, ~40 logical pixels tall when standing, 1-pixel charcoal contour, 12-16 solid colours, no gradients, anti-aliasing, texture, fuzzy feathers or tiny highlights. Pixel clusters and face/head scale must match the reference, changes only for the stated pose. Body normally faces RIGHT, tail extends LEFT. No text, labels, borders, grids, shadows, props or extra characters. Exactly the specified isolated sprites, fully inside uniformly arranged cells with wide clear gutters.
Create ONE full-body sprite on #FF00FF magenta: exact RIGHT-FACING neutral side bird from reference with near PINK FOOT lifted gently forward, curled toes by lower belly, balancing on far foot. The grey body, yellow head, beak, eye, wing and long left tail are exactly canonical shape/scale, not changed to a different bird. Raisedfoot no higherthan bottomthirdofbody. Farfoot remains firmly grounded. Calm curious cute expression, mouthclosed. Use a centred square image with complete sprite and generousmargin. Only one bird, no extra poses.
```

## bite

项目文件：`art/actions/bite.png`。
输入参考：`exec-26353f36-2d42-41a2-8459-bd695730f0a8.png`。
生成缓存：`exec-0e775a13-7d4e-43ac-9820-6cabdcdb20fe.png`。

```text
Production animation sprite sheet for the SAME BASE cockatiel as the attached canonical model. Preserve its exact compact rounded skull, small solid dark eye, little grey beak, swept thin yellow crest, orange cheek, slender pear-shaped grey body, white folded-wing panel, long tapered tail, small pink feet. BASE DESIGN ONLY, not a large-headed mochi bird. Crisp low-resolution pixel art, ~40 logical pixels tall when standing, 1-pixel charcoal contour, 12-16 solid colours, no gradients, anti-aliasing, texture, fuzzy feathers or tiny highlights. Pixel clusters and face/head scale must match the reference, changes only for the stated pose. Body normally faces RIGHT, tail extends LEFT. No text, labels, borders, grids, shadows, props or extra characters. Exactly the specified isolated sprites, fully inside uniformly arranged cells with wide clear gutters.
Create ONE full-body sprite on #FF00FF magenta: a short playful NIP/PECK action, bird facing RIGHT and longtail LEFT. Bothfeet grounded samephysical scale as canonical reference. Neck/head lean a little FORWARD and DOWN, beak slightly open reaching towardright at chestheight, skull remains SAME compact size, body remains samegrey pear shape with white wing. Alert small solidblackeye, not angryhumaneyebrows. No food, target, cursor, hands, effects, oversizedbeak, gore or teeth. Modest forwardlean, not a flattened bird. Centre complete sprite on squarecanvas, marginallaround.
```

## 飞行背景修订

编辑目标：`exec-4782d533-a483-484d-99a6-261e93f2b00f.png`；最终输出：`exec-b7c1fe14-0a5c-427d-9328-d84bbac7089c.png`。

```text
Use case: background-extraction. Edit the attached six-frame pixel cockatiel flight sheet. It unfortunately has a painted GREY CHECKERBOARD background rather than real transparency. Replace ALL background checkerboard pixels and all empty spaces between wings/body/feet with a perfectly UNIFORM FULLY OPAQUE MAGENTA #FF00FF background for chroma-key import. Preserve every bird's pose, size, position, silhouette, pixel colours, white wing feathers, grey wings, dark outlines, pink feet and yellow face. Do not change or redraw the six birds. Keep exact 1536x1024 canvas and 3 columns by2rows. No checkerboard remaining, no grey fringing, no shadow. Pure magenta background only, nothing else changed.
```

## 导出与实际消费

- 导入时去除品红背景，最近邻缩放，运行时保留二值透明 alpha。地面帧 64×64，飞行帧 96×96；站姿侧面高 41 原始像素。
- Idle／水平侧视／回头来自统一模型。走路复用模型侧面头身，仅足部来自 walk-rig。转身复用模型斜面或正面，只从 turn.png 取中央脚部。
- 飞行落地末帧复用同一站姿，画布四周各增加 16 像素。其他飞行姿势按脸颊面积与标准站姿确定共同缩放，不能用展开翅膀大小缩放身体。
- Beg／Pet 共用 Beg 的缩放基准；独立仰头、闭眼摸头经目视复核后记录参考哈希。眼睛／鸟喙换色区域与口型源哈希同时更新。
- 旧 `art/actions/Sing/`、`Stretch/`、`Lift/`、`Bite/` 中的独立源图保留在本地，当前导入不再读取。其他旧提示词文档仅为历史记录。
- 完整真实来源、帧序、时长与影响范围以 [ANIMATION_REFERENCES.md](ANIMATION_REFERENCES.md) 和 [ASSET_MANAGEMENT.md](ASSET_MANAGEMENT.md) 为准；不要从提示词推断导出片段。

