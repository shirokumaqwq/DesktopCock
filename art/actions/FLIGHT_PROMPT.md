# 飞行素材与真实羽色参考

源图使用内置 imagegen 生成／编辑：`flight.png`（侧面）与 `flight-front.png`（正面）。正面参考 `art/model/model-sheet.png` 上排正面，侧面保留原动作构图。

## 羽色依据

原始灰成年雄性玄凤的翼下以灰色为主；雌鸟或幼鸟可能有浅色斑点。本角色按成年雄鸟处理：移除展开翼内侧的大块纯白，改为灰色覆羽与较深飞羽；仅在可见翼缘保留窄浅色标记，折翼站姿保留标准白色翼斑。像素表现是简化，非逐羽解剖图。

- [Petco：雄鸟翼下纯灰、雌鸟灰底带白／黄斑](https://www.petco.com/pet-education/caresheets/cockatiel)
- [Animal Diversity Web：原始灰羽色与白色翼部标记](https://animaldiversity.org/accounts/Nymphicus_hollandicus/)
- [John Daniels / Ardea：真实飞行形态照片](https://www.ardeaprints.com/birds/cockatiel-flight-644752.html)

照片仅作观察参考，不作为源图复制进游戏。

## 导出与运行

两张源图均为 3×2：起飞、上拍、平展、下拍、制动、站姿。导出器以相应正面／侧面标准脸颊面积统一尺度，固定头部位置；末帧直接复用标准模型，放入 96×96 画布四边各留 16 像素。脸部统一到 idle 色板，所有新增眼睛／喙区域逐帧复核。

三个正面片段为 TakingOffFront、FlyingFront、LandingFront。飞行请求按目标方向选初始姿态；空中按实际速度选择，水平／竖直比不大于 0.30 时进入正面，大于 0.55 时回侧面，低速及落地阶段保留朝向。正面不受左右镜像影响。近竖直起飞的水平速度按目标差值收敛，避免一像素误差触发满速横移。

## 侧面编辑提示词

Use case: precise-object-edit. Edit target image 1 is the existing 3 columns x 2 rows pixel-art cockatiel flight sheet. Preserve EXACT six poses, silhouette, scale, face, beak, crest, feet, cell layout and solid magenta #FF00FF background. Change ONLY wing underside plumage: current large brilliant white patches on the exposed underside of raised/spread wings are incorrect for adult male normal grey cockatiel. Replace these broad inner-wing white panels in cells 1-5 with layered medium charcoal / warm grey coverts and darker grey primary feathers, subtle grey highlights. Only a narrow restrained off-white outer wing marking where the dorsal wing edge is visible; no large white underwing panel, no yellow dots/bars. Cell 6 folded-wing standing pose retains its white folded-wing stripe. Same crisp coarse pixel clusters, no antialiasing, no shadows, no text. 1536x1024 sheet.

## 正面生成提示词

Use case: stylized-concept. Asset type: production pixel-art sprite sheet for existing cockatiel desktop pet. Reference image 1 is character model: use its TOP MIDDLE front view as exact identity and proportion reference. Reference image 2 is existing SIDE flight sheet for style and six-pose organization only. Create NEW FRONT VIEW flight sheet, 1536x1024, exactly 3 equal columns x 2 equal rows, all six birds face straight toward camera. Same compact skull/torso, narrow tall swept yellow crest, two small black eyes, centered dark grey beak, small orange patches at left and right face edges, warm grey body, pink feet, long narrow grey tail directly behind/below torso. Same coarse crisp pixel clusters and limited palette as references, not smooth illustration. Absolutely same head/torso scale in all cells, head centered at identical local position, wings fit fully in cell with margins. Six poses reading order: (1) takeoff wings raised diagonal V, feet stretching downward; (2) upstroke both wings fully raised high V, feet tucked; (3) midstroke wings extended symmetrically sideways, feet tucked; (4) downstroke both wings sweep DOWN AND OUT, not folded on chest, feet tucked; (5) landing brake wings broad high V, tail slightly fanned, both feet forward and toes open; (6) settled standing FRONT VIEW with wings folded and feet planted, match model front. Wings are anatomically feathered tapered avian wings attached at shoulders, same lengths through cycle. IMPORTANT adult male normal grey cockatiel UNDERSIDES: grey layered underwing coverts, medium/dark charcoal flight feathers with subtle grey highlights; no broad white panels on visible underside and no yellow dots or bars. Limited off-white strip only at visible outer/dorsal edge, folded standing pose can have white wing side patches. Solid flat pure magenta #FF00FF background for importer, no checkerboard, no shadows, no text, no gridlines, no labels. Separate all six silhouettes. Wingspan at widest around 2.4 times full standing bird height, skull width about 1/3 torso-to-crest height. Do not make body or head grow in flight.
