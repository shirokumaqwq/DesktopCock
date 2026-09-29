# 玄凤声音来源

首批声音均来自 Joseph SARDIN / BigSoundBank，页面许可为 CC0-1.0。原始文件为 48 kHz、24-bit 单声道实录；没有使用合成鸟声或人声升调。

| 原始录音 | 用途 | 页面 |
| --- | --- | --- |
| 2770 / Cockatiel #2 | 轻叫 | https://bigsoundbank.com/cockatiel-parakeet-2-s2770.html |
| 2788 / Cockatiel #20 | 双叫 | https://bigsoundbank.com/sound-2788-perruche-calopsitte-20.html |
| 2773 / Cockatiel #5 | 连续短叫 | https://bigsoundbank.com/sound-2773-perruche-calopsitte-5.html |
| 2776 / Cockatiel #8 | 明亮短叫、4.28 秒自然口哨唱段 | https://bigsoundbank.com/sound-2776-perruche-calopsitte-8.html |

`sources.json` 保存裁剪范围和按波形标注的发声音节起止。`tools/prepare_audio.py --sources <原始WAV目录>` 只生成 `src/DesktopCock/Assets/Audio/` 的 PCM WAV 和 `catalog.json`，不会在构建时联网。原始下载格式为 `https://bigsoundbank.com/UPLOAD/bwf-en/<编号>.wav`。

处理包含去直流、180 Hz 温和高通、RMS/峰值约束、8 ms 首尾渐变及 16-bit PCM 输出；不改变音高或速度。每个运行时条目记录源地址、作者、CC0、源文件/输出哈希和裁剪起止。

口型起止由波形辅助标注后固定为时间轴，三个姿态为闭嘴、小开、大开。波形检查不等于听感验收；首次发布前仍应由用户试听确认叫声风格与唱段自然程度。
