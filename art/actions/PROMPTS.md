# 求摸与睡眠源素材生成记录

2026-09-29，使用 Codex 内置 imagegen。当前使用的源图为 `beg.png` 和 `sleep.png`，洋红底由生成器转为透明。Beg 采用前三格，Sleep 采用前两格；其他候选不进入运行时。

生成时的角色参考为 `../archive/cockatiel-atlas.png`；后续修改使用 [当前统一造型](../model/README.md)。Idle 与仰头的当前提示词见 [模型生成记录](../model/PROMPTS.md)，走路见 [分层步态素材](MOTION_PROMPTS.md)。

## Sleep

Use case: precise-object-edit. Correct sleeping respiration sheet. Keep four identical sleeping birds in exact same 2x2 grid, grey/yellow cockatiel pixel art and flat magenta backdrop. All FOUR heads must remain in EXACT same tucked-down sleeping pose as TOP LEFT bird, same closed curved eye, head never lifts or rotates at all. EXACT same crest angle in all cells. Feet and tail absolutely stationary. Body outline and wing absolutely same as top-left except for tiny breathing: top-left exhale; top-right expand ONLY lower-right chest by 1 tiny logical pixel; bottom-left expand lower-right chest by 2 logical pixels MAXIMUM; bottom-right expand chest by 1 pixel. Tiny chest motion, not head bobbing. No shifting or stretching the whole sprite, no body size changes, no enlarged wings. Identical baseline and scale. Solid magenta RGB255,0,255 no patterns/shadows/text.

## Beg 前缀

Use case: precise-object-edit. Asset: ONE action animation sheet for this exact pixel cockatiel desktop pet. Attached atlas is character/style reference. Match grey body, white wing stripe, yellow face/crest, orange cheeks, charcoal beak and outlines, pink feet, long tail extending left. Crisp chunky low resolution pixel art, consistent scale/body silhouette between frames. Pure uniform RGB(255,0,255) magenta chroma-key backdrop, no checkerboard, no shadows, no text/grid lines. All cells equal-sized with wide empty gutters. Feet planted on same baseline at 86% of cell height, body centered near 55% of cell width; whole bird fully visible including tail. Output square 1024x1024.

## Beg 后缀

Exactly FOUR sprites in 2x2 grid, ONLY low bowed head begging to be petted, matching reference second row third sprite. Body, head, eye, wing, feet and beak must remain exactly identical in all four frames; beak low and to right, neck bent forward, yellow face sideways with one round black open eye. ONLY three thin crest feathers move: top-left relaxed angle forward/up; top-right feather tips 1 pixel farther forward; bottom-left tips slightly farther forward and splayed; bottom-right tips halfway back. Subtle gentle crest flutter. Keep bird same size as standing reference, just head lowered. No hands.
