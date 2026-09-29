# 玄凤 · DesktopCock

Windows 10/11 x64 像素玄凤桌宠。支持手型注视、接鸟随手移动、摸头、睡眠、散步、换色、自主飞行与真实玄凤叫声；声音与嘴部张合同步，运行无需联网。托盘可静音，并提供本地教学与短语记忆原型，详见 [音频说明](docs/AUDIO.md)。

## 运行

解压 `dist/DesktopCock-win-x64-Release.zip`，运行其中的 `DesktopCock.exe`，保留同目录的 Assets 和运行库。托盘右键可隐藏、暂停、调整大小、切换羽色、设置自主飞行或退出；双击恢复显示。

更新或切换配置前，从托盘退出正在运行的桌宠，再启动新包中的程序。Debug 与 Release 共用单实例约束。

完整操作与设置见 [使用说明](docs/USER_GUIDE.md)。观察室、飞行覆盖层和模拟输入仅存在于 Debug，见 [构建与调试](docs/DEBUGGING.md)。

按 `Alt+1` 切换接鸟手型，靠近鸟脚停留后抬脚上手；按 `Alt+2` 切换摸头手型，靠近头部会低头并接受抚摸。按住 Alt 再按 Esc 下方的反引号／`·` 键恢复鼠标。鸟站在手上时换手或退出手型，会飞往最近的有效落点。

## 构建

安装 .NET 10 SDK，在项目根目录执行：

```powershell
./tools/build.ps1 -Configuration Debug
./tools/build.ps1 -Configuration Debug -Publish
./tools/build.ps1 -Configuration Release -Publish
```

省略 `-Configuration` 时使用 Release。脚本检查资源并编译；`-Publish` 生成 Windows x64 自包含程序及 ZIP。脚本优先使用 `%LOCALAPPDATA%\DesktopCock\dotnet` 中的 SDK，否则使用系统 `dotnet`。本地行为测试独立运行，不作为源码发布或构建依赖。

两种配置分别输出至 `dist/DesktopCock-win-x64-Debug`、`dist/DesktopCock-win-x64-Release`，发布从干净暂存目录生成。Release 在编译阶段排除诊断实现，正式飞行、GPU 感知、换色和错误日志均保留。

## 项目目录

| 目录 | 当前职责 |
| --- | --- |
| `src/DesktopCock.Core` | 纯 .NET 模型、语义交互、决策策略、动作状态机、运动与运行协调 |
| `src/DesktopCock` | WPF 表现、输入、屏幕感知、Windows 接口、配置及 Debug 诊断 |
| `art/model` | 统一角色源图、尺度声明、三视图及角度预览 |
| `art/actions` | 动作源素材、唯一动画声明、生成引用文档与维护流程 |
| `art/skins` | 配色与逐帧换色区域声明 |
| `art/previews` | 本地生成的动作、步态和配色预览，不纳入源码发布 |
| `art/archive` | 本地源素材备份，不纳入源码发布 |
| `docs` | 架构、行为、使用与调试说明 |
| `tools` | 构建、资源生成、一致性检查及运行采样 |
| `tests` | 本地行为测试；不纳入源码发布和程序包 |
| `artifacts` | 本地验证脚本和产物；不纳入源码发布和程序包 |
| `dist` | 按配置隔离的发布目录和 ZIP |

行为采用独立决策层与动作状态机，Core 不引用 WPF、Windows API 或动画资源。表现层读取只读快照；转身按连续进度选帧，走路按实际位移驱动步态。手型会话负责注意力、上手与离手；手套素材通过构建链接到 Assets/Hands，直接复用 art/interaction/hands 中选定的两张 pixel-v2 源图。

## 资源维护

`art/model/model-sheet.png` 是闲置、回头和转身角度的统一造型来源；`model.json` 声明共同尺度。其他动作的实际源图、整帧复用、局部合成、缩放基准及人工复核依赖以引用清单为准。

动画片段的唯一可编辑来源是 `art/actions/asset-bindings.json` 的 `clips`。生成器输出 `src/DesktopCock/Assets/animations.json`、帧图、换色映射、预览及引用文档。当前包含 62 张帧、22 个片段、3 套羽色；地面帧为 64×64，飞行帧为 96×96。接近竖直的起落使用正面飞行动画，横向飞行使用侧面；翼下按原始灰成年雄性玄凤的灰色层次绘制。

修改源图或声明后，安装 Python 与 Pillow，并执行：

```powershell
python tools/prepare_assets.py
./tools/build.ps1 -Configuration Release
```

基准变化时，先人工复核独立变体和眼睛／鸟喙换色区域，再更新相应审核哈希。构建只验证源文件、导出资源和生成文档的一致性，不自动生成或刷新审核记录。

- [架构与扩展边界](docs/ARCHITECTURE.md)
- [默认行为规则](docs/BEHAVIOR.md)
- [资源维护流程](art/actions/ASSET_MANAGEMENT.md)与[生成引用清单](art/actions/ANIMATION_REFERENCES.md)
- [统一造型规范](art/model/README.md)与[羽色维护](art/skins/README.md)

文档按当前实现维护：功能被完整替代时更新原说明并移除过时结论，不在使用和验收文档中叠加旧版本记录。源素材备份、尚在使用的生成提示词和本地验证产物独立保留。
