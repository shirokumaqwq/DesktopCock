# 羽色与区域映射

原始动作 PNG 保持不变；所有羽色共用全部运行时帧和动画片段及同一组锚点/交互区域。

- `palettes.json` 是手工维护的配色源：原始灰 `original`、黑牛（白脸原始）`whiteface`、黄化 `lutino`。
- `regions.json` 是逐帧审核的眼睛、喙矩形及原始帧 SHA-256；矩形为 `[left, top, right, bottom]`，右/下边界不包含。闭眼姿势不标眼球，保持闭眼线条。
- `../../tools/prepare_skins.py` 按像素颜色和审核区域，拆分轮廓、身体、头冠/脸部、脸颊、白色羽斑、眼球、眼睛高光、喙、脚。输出 `src/DesktopCock/Assets/skins.json` 内与各帧同尺寸的字符区域图：地面 64×64、飞行 96×96，不改变源图。
- 每个配色只声明需要改变的部位，其他部位原样保留。每条色阶由 `[亮度, "#RRGGBB"]` 节点组成，必须从 0 到 255 严格递增；节点之间线性插值，保留源帧明暗层次。
- 换色使用亮度 `(54R + 183G + 19B + 128) / 256` 的整数部分。原始灰直接使用源位图，换色帧按需缓存；透明度、动作时长、脚底坐标和鼠标命中保持不变。

## 维护

只改颜色：编辑 `palettes.json`，运行 `python tools/prepare_skins.py`，查看 `art/previews/skins/comparison.png` 和两种羽色文件夹中的逐动作 PNG/GIF。

修改动作：运行 `python tools/prepare_assets.py`。如果原始帧变更，生成器会要求先逐帧复核 `regions.json` 的眼睛/喙区域，再更新对应审核哈希，之后重新运行。不要仅为通过检查刷新哈希。新帧也必须补齐区域条目。

`art/previews/skins/regions` 是区域检查图：蓝色身体、黄色头部、橙色脸颊、紫色眼球、青色喙、粉色脚、深色轮廓、浅色羽斑、白色眼睛高光。当前区域是为现有像素画审核的颜色分类结果，不是可以直接套在任意新角色上的自动分割器。

原始动画清单记录 `skins.json` 的哈希；该文件记录配色源、区域源和生成脚本的哈希，并将每张区域图绑定到对应原始帧。构建与启动会检查一致性，避免将旧区域图用在新动画上。

## 外观参考

- 黑牛按白脸原始处理：灰色身体、白色脸与冠羽，去掉黄/橙色脸部色素。[Kaytee 的羽色说明](https://www.kaytee.com/learn-care/pet-birds/cockatiel-colors-the-different-colors)
- 黄化按奶油白/浅黄羽毛、黄色脸、橙色脸颊、红眼及浅粉色喙脚处理；深色暖棕线条用于延续像素画的可读轮廓。[Cockatiel Color Palette：Lutino](https://kirstenmunson.com/cockatiels/mutations/lutino/)

仅使用资料确认配色特征，未复制参考照片为游戏素材。羽色通过区域映射转换，不重新生成角色或动作。
