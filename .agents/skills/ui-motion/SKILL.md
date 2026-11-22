---
name: ui-motion
description: 为 MobileSlayTheSpire 的 BaseUIView 界面设计和接入 BetterAnimation 开关动效，调整预制体及生成器，并验证渐变、关闭和重复打开行为。用于 UI 动效，不用于角色或战斗骨骼动画。
---

# UI 动效接入

从项目根目录定位以下实现，先核对当前接口再修改：

- `Assets/Scripts/UI/Framework/BaseUIView.cs`：序列化 `openAnimation`、`closeAnimation`、`openClip`、`closeClip`；公开 `OnShow()`、`HideAsync()`、`Transition`。
- `Assets/Scripts/Animation/BetterAnimation.cs`：`SpireAnimation.UiMotion` 参数和播放适配器。项目实际拼写为 BetterAnimation。
- `Assets/Scripts/UI/Framework/UiManager.cs`：管理缓存、显示、隐藏和关闭释放。

## 默认接法

优先配置继承 BaseUIView 的预制体，不另建协程或 Tween 驱动。基类要求根对象包含 CanvasGroup、BetterAnimation，后者要求 Animation。运行时由 UiManager 调用生命周期；业务回调 `OnShown` / `OnHidden` 不重复播放同一个根动效。

简单渐隐使用 `UiMotion`：FromAlpha / ToAlpha 为 0→1 和 1→0，Offset 为零、Scale 为 1、Angle 为零。BetterAnimation 内部使用 EaseInOut，并按 unscaledDeltaTime 推进，因此游戏暂停不阻断 UI 动效。Speed=1 是正常速度。

开始界面参考：`Assets/GameResources/UI/Views/GameStart/GameStartView.prefab`，渐入 0.35 秒、渐出 0.22 秒；背景、标题、按钮整体渐变。参数是参考，不强制套用所有界面。纯透明度效果保持 openClip / closeClip 为空；非空 Clip 优先于 UiMotion，且必须是 legacy AnimationClip。

预制体若有生成器，同步对应生成代码。开始界面生成器为 `Assets/Editor/GameUiPrefabBuilder.cs`，通过 SerializedObject 修改继承字段；不要为改一个动效直接运行会重建其他资源的整个 Build。

## 生命周期注意事项

- `OnShow()` 启动渐入，`Transition` 表示播放完成；`OpenAsync` 完成仅表示实例可用，不代表渐入结束。
- `HideAsync()` 立即禁用根 CanvasGroup 的交互和射线，使用当前 alpha 开始渐出，播放结束才 SetActive(false)。不要提前禁用根对象或回收资源。
- 需要释放时用 `UiManager.CloseAsync`，它等待退出后释放；只隐藏用 Hide，保留缓存供 Show 再次打开。
- 快速重新打开会取消旧动画，基类的 transitionVersion 防止旧关闭任务禁用新界面。当前渐入会从配置的 FromAlpha 重播，并非连续反向动画；若任务要求无跳变反转，再针对该需求实现和验证。
- UiMotion 会写入 Transform 的位移、缩放和 Z 旋转；非零偏移或非单位缩放需要专门验证重复播放是否累积。纯淡入淡出不应顺带加入这些效果。
- 子页面的 SetActive 切换不会触发根 BaseUIView 动效；若需要子页面转场，应单独设计，不要用根界面反复开关替代。

## 验证

按项目 AGENTS.md 和对应 uloop 技能操作，先 status 确认 Ready；不要中断其他正在运行的测试。C# 改动后编译，遇到团结引擎重载误报时核对状态和错误日志，不能把失败当成功。

用实际开始界面验证渐入、中间 alpha、渐出后禁用、渐出期间不能点击、重复打开，以及需要时的 timeScale=0。优先运行现有 `UiFrameworkTests`；验证视觉时用 uloop-record-video 录制并查看连续帧，单张截图不能证明渐变平滑。临时脚本和视频留在 `.uloop/`，不要提交本机缓存。报告中区分通过的行为验证、视觉验证和环境阻塞。
