# 当前资源引用与更新流程

更新：2026-09-29。当前整套资源以批准的基础版／基础版低像素设计重生成，共 56 个独立帧、19 个动画片段、3 套配色。运行时唯一入口为 `src/DesktopCock/Assets/animations.json`。

逐帧来源、共享关系、片段首末帧及影响范围见自动生成的 [ANIMATION_REFERENCES.md](ANIMATION_REFERENCES.md)。片段的唯一可编辑定义为 `asset-bindings.json` 的 `clips`，不要在脚本或导出清单中重复维护。构建检查生成文档哈希。

| 姿势 | 当前源文件与共享关系 | 导出资源 |
| --- | --- | --- |
| 闲置侧面、斜面、正面、眨眼 | `art/model/model-sheet.png`；按 `model.json` 统一尺度 | `Idle/idle*.png` |
| 水平侧视 | 完整复用 idle，包含交互区域 | `Look/look.png` |
| 侧身回头 | 模型 rear；注视源图的重复回头格不导入 | `Look/look-back.png` |
| 踏步转身 | 模型斜面／正面头身及两侧尾翼；仅中央脚部来自 `turn.png` | `Turn/turn-*.png` |
| 侧身水平正视 | `look.png` 上排中格的完整头颈和身体；脸部颜色对齐 idle | `Look/look-front.png` |
| 三种仰头 | `look.png` 的独立仰头姿势，记录人工复核参考哈希 | `Look/look-up-*.png` |
| 低头求摸 | `beg.png` 前三格，共用缩放 | `Beg/beg1.png`–`beg3.png` |
| 享受摸头 | `pet.png` 前三格，对应 Beg；共用 Beg 缩放基准 | `Pet/pet*.png` |
| 走路 | 模型 side 的头身 + `walk-rig.png` 的前后脚；12 帧对应 12 像素步幅 | `Walk/walk1.png`–`walk12.png` |
| 起飞、振翅、降落 | `flight.png`；flight-settle 直接复用模型 side | `Flight/flight-*.png` |
| 睡眠 | `sleep.png` 前两格 | `Sleep/sleep1.png`、`sleep2.png` |
| 唱歌 | 新整张源图 `sing.png`，两格 | `Sing/sing1.png`、`sing2.png` |
| 伸翅 | `stretch.png`，4×3 共 12 格；保留每帧完整头颈动作，首末帧复用模型，固定支撑脚 | `Stretch/stretch1.png`–`stretch12.png` |
| 抬爪 | 新源图 `lift.png` | `Lift/lift.png` |
| 啄咬 | 新源图 `bite.png` | `Bite/bite.png` |

旧 `art/actions/Sing/`、`Stretch/`、`Lift/`、`Bite/` 中的独立源图保留作历史内容，当前导出不读取。其他未列入来源声明的旧素材不参与生成。正式源图都保存在项目中，运行时不引用 Codex 缓存。

## 合成和尺寸规则

`asset-bindings.json` 声明图层、尺度分组和人工复核变体。裁切为 [left, top, right, bottom]，右下边界不包含在内，坐标以 64×64 导出帧为准。

- 转身从模型取 y<50 的上半身，以及 y>=50 时 x<22、x>=47 的两侧；仅 [22,50,47,64] 中央脚部来自转身源图。不能整条覆盖底部，否则会把独立源图的尾羽混入。
- 水平正视保留 `look.png` 上排中格的完整姿势，人工复核相对主模型的比例；不能从 idle-front 硬切头部覆盖，否则颈部轮廓和身体接不上。交互区域来自该完整姿势。
- 回头由主模型预先注册，注视源图第三格必须跳过，不能在后续导入中覆盖。
- 走路身体直接复用 idle 的 y<52 区域，脚部来自 rig。着地段脚向后每帧一像素，配合已有按距离驱动的步态；身体只有一像素起伏。
- Beg／Pet 使用相同缩放系数，不能各自按画格拉满。飞行其余姿势共用按脸颊面积计算的系数；flight-settle 与 idle 像素完全一致，仅增加 [16,16] 画布偏移。

地面帧 64×64、脚底 y=56，飞行帧 96×96、脚底 y=72；均为二值透明 alpha。长尾需要时整体平移并同步足部锚点，具体 foot 以 manifest 为准。画布和轮廓大小变化不代表身体放大。

## 更新步骤

1. 修改真正被消费的源文件。角色造型先改主模型，低头体型先改 Beg，具体动作使用上表对应整张源图。
2. 基准改变后目视复核依赖的独立变体，再更新 `reviewedVariants[].referenceSha256`；不能只为通过检查刷新哈希。
3. 新生成动作须检查原始灰与主模型的脸部色板，必要时加入 `paletteGroups.originalFace.frames`。执行 `python tools/prepare_assets.py`，导出共享模型、动作、manifest、皮肤遮罩和预览。换色区域位于 `art/skins/regions.json`，需逐帧复核眼睛／鸟喙与源图哈希。闭眼是轮廓，不作为虹膜换色。
4. idle、sing1、sing2 改变时，同步检查 `src/DesktopCock/Audio/BeakAnimator.cs` 的三个源哈希及口型像素位置；否则现有安全检查会停用口型。
5. 检查 `art/model/ThreeViews.png`、`TurnAngles.png`，以及 Idle、Turn、LookFront、LookBack、WalkGround、Beg、Pet、Flying、Landing 的实际导出预览和三套配色。
6. 测试阶段只使用 Debug。执行 `tools/check_assets.ps1` 与 `tools/build.ps1 -Configuration Debug`。构建核验源图、脚本、依赖声明和导出 PNG；源图漏导出、手改帧、额外运行时 PNG 都会失败。
7. 需要更新本地发布包时使用 `tools/build.ps1 -Configuration Debug -Publish`，连同应用与 Assets 完整导出；不要只替换发布目录中的一张 PNG。当前阶段保留正在运行的旧 Debug 实例，输出独立的 Debug 迭代包后手动切换。

本轮完整内置 imagegen 提示词及生成原件映射见 [BASE_REGEN_PROMPTS.md](BASE_REGEN_PROMPTS.md)。原 PROMPTS、MOTION_PROMPTS、REFERENCE_SYNC_PROMPTS、FLIGHT_PROMPT 等文档保留为历史记录；当前源图与引用以本页及生成清单为准。

## 原始灰色板

原始灰的运行时 palette 保持为空，直接显示导出 PNG，因此源图之间的偏色必须在资源导出阶段修正。`paletteGroups.originalFace` 当前包含水平正视及 stretch2–stretch11，以 idle 的实际黄色和橙色为基准。`tools/normalize_palette.py` 在姿势合成后校准各材料的颜色中值，再选取基准中的实际颜色；不移动像素，不改变透明度或头颈动作。首末伸翅帧已经直接复用 idle，无需重复校色。

颜色基准以 `palette` 依赖登记到逐帧引用清单。源图、校色脚本或基准改变后须重新导出，并检查原始灰的站姿→动作→站姿连续预览，再核对其他配色；不能用复制固定头部来解决色差。

## 伸翅与唱歌增量

伸翅为 12 帧、1900ms：帧 1–5 用 500ms 伸开，帧 6/7 在最大幅度分别停留 350/450ms，帧 8–12 用 600ms 收回。唤醒行为通过 AnimationBank 读取清单总时长，动作完整结束后才继续响应，不能另写固定秒数导致收翅被截断。首末帧直接复用 idle，中间帧以不动的支撑脚配准，完整保留源图的头颈前探、下压与回正；头部交互区域逐帧提取，眼睛／鸟喙换色区域逐帧复核。禁止把 y<37 的部分统一覆盖成 idle，否则会再次造成头部冻结。预览按实际 foot 对齐，不能把所有 PNG 左上角直接对齐来判断抖动。

唱歌保留声音的 MouthTimeline，静音／无音频预览使用动作时间驱动闭嘴、微张、张嘴；真实音频间隙继续闭嘴。口型处理范围是 x=42..43、y=30..32，完整张嘴的深色嘴腔为 2×2 像素；鸟喙填色取当前皮肤，不能写到头部或羽毛。必须通过实际 AnimationBank/BeakAnimator 渲染检查，静态 Sing 源图并不包含全部口型。

本阶段内置 imagegen 源图、提示词及实际导出规则见 [STRETCH_SING_PROMPTS.md](STRETCH_SING_PROMPTS.md)。
