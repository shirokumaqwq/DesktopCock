# Debug 构建与诊断

```powershell
./tools/build.ps1 -Configuration Debug
./tools/build.ps1 -Configuration Debug -Publish
./tools/build.ps1 -Configuration Release -Publish
```

省略 Configuration 时为 Release。输出目录和 ZIP 为 `dist/DesktopCock-win-x64-Debug`、`dist/DesktopCock-win-x64-Release` 及对应 `.zip`。发布从干净暂存目录生成，不混用两个配置的输出。

| 功能 | Debug | Release |
| --- | --- | --- |
| 自主飞行、平台识别、换色、交互 | 有 | 有 |
| 观察室、强制预览、模拟夜间/空闲 | 有 | 编译排除 |
| 飞行覆盖层、Alt 选点 | 有 | 编译排除 |
| GPU 计时查询和性能采样 | 有 | 编译排除 |
| 设置目录 | DesktopCock.Debug | DesktopCock |

仅 Debug 支持 `--preview`、`--flight-debug`。Release 不解析这些调试参数，也没有对应类型或菜单。托盘双击只恢复显示。调试与正式版仍遵守同一个单实例约束，切换前退出当前宠物。

## 观察室与飞行覆盖层

从托盘「动作预览 / 调试」打开观察室，或使用 `DesktopCock.exe --preview` 启动。观察室可切换羽色、强制预览动作、模拟夜间与空闲；站立预览包含侧面、斜面、正面和眨眼，另有抬头、回头和踏步转身按钮。点击宠物会取消空闲模拟。

强制预览使用独立 PreviewSession，期间动作不会自然转换；「恢复自然活动」或关闭观察室回到自然活动，不修改正式决策策略。

从托盘或观察室开启飞行 Debug，或使用 `DesktopCock.exe --flight-debug` 启动。绿色区间表示合法脚底落点，蓝色表示当前平台，橙色表示飞行目标；Alt 悬停显示鸟身占位，Alt＋左键试飞，飞行中可以重新选点。其他点击穿透至原应用。

覆盖层开启期间暂停自主选点，保留平台跟随与失效逃离；关闭后恢复原设置，开启状态不跨启动保存。手动试飞遵守暂停、隐藏、锁屏和全屏限制。覆盖层要求 Windows 10 2004 或更新系统并支持屏幕捕获排除；不满足时会说明原因。鼠标钩子仅在覆盖层开启时安装，只消费合法区域内的 Alt＋左键及其抬起，不记录轨迹。

## 性能采样

性能文本最多每秒刷新 4 次；每项固定保存最近 256 个样本，显示最新值及 P95。采集的 CPU 时间、GPU 检测/追踪时间、UI 主循环 CPU 时间、调度延迟、感知结果年龄分别统计；未获得样本显示“—”。平台覆盖层可更快重绘，但统计文本不逐帧重算。

GPU 时间使用已有异步时间戳查询，不等待 GPU；CPU 捕获时间包含当次调用开销，不等于 GPU 执行时间。窗口枚举和 CPU 后处理也有采样接口，完整曲线面板和自动调频留到后续。

