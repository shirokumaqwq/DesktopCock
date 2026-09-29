# 闭眼摸头变体生成记录

2026-09-29，使用内置 imagegen 局部编辑。运行时缩放与透明导出由 `tools/prepare_assets.py` 完成。

## 素材与提示词

输入：当前 beg.png。
生成文件：C:/Users/panxingyue/.codex/generated_images/01a0eae4-b461-7351-92af-44158cff5ca5/exec-276b16ab-d8f6-47ec-81fc-285dbb2e0dab.png
项目文件：art/actions/pet.png。采用前三格，与 Beg 共用身体尺寸基准；第四格冠羽变化过大，不用于循环。

提示词：

Use case: precise-object-edit. Edit target is the attached FOUR-cell cockatiel bowed-head animation sheet. Produce the petting/relaxed-eye variant of these exact four poses. Change ONLY the black OPEN EYE in each cell to a small relaxed CLOSED curved black eyelid in the same location, expressing enjoyment of gentle head petting. Keep the precise head shape, head angle, crest position in each respective cell, neck, grey body, white wing stripe, long tail, beak, feet, outlines, all pixel placements, bird scale, cell layout and magenta background exactly unchanged. The approved body and all poses must be reused without redrawing, rescaling or repositioning. The wing and feet absolutely must not change, and no bird may become larger or smaller. All four eyes closed, no sparkles, hands, hearts, text or new props. Same original crisp low-resolution pixel art.

## 导出与依赖

Beg 与 Pet 共用宽度 53 像素的缩放基准；Pet 片段只引用 `pet`、`pet2`、`pet3`。修改 `beg.png` 后，需要人工复核 `pet.png` 的身体与闭眼姿势，再更新审核哈希。

完整依赖见 [动画引用清单](ANIMATION_REFERENCES.md)，维护步骤见 [资源管理](ASSET_MANAGEMENT.md)，机器声明位于 `asset-bindings.json`。
