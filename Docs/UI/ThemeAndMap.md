# UI 图片样式与地图

共享图片位于 `Assets/GameResources/UI/Shared/Skins`，由 UiTheme（资源 ID 30）统一管理。

- Primary：单人、多人、创建、加入、准备、开始、重连、结束回合。
- Secondary：返回上页、语言、房间选择、跳过奖励。
- Destructive：地图/战斗返回主菜单（离开当前局）。
- 卡牌使用独立 CardFrame；地图房间使用同类房间图标。

普通按钮使用九宫格图片和 SpriteSwap，包含普通、悬停/选中、按下和禁用资源。动态按钮在创建时显式传入 ButtonIntent，不通过显示文字推测用途。

MapUIView 由 UiManager 管理；CurvedRoad 使用贝塞尔曲线生成网格，并按路径长度重复纹理。Run.Route 保存经过的房间，用于区分已走路线、可选节点及不可达节点。BaseUIView 默认通过 BetterAnimation 播放 CanvasGroup 淡入淡出。

复建入口：Tools/Spire/Build Map and UI Skins。地图主题独立放在 MapTheme.cs，确保 Unity 重载后脚本引用有效。

验证：2026-11-21，27 个 EditMode、8 个 PlayMode 测试通过；实机检查开始界面→地图→战斗。团结引擎下 uloop 编译响应在域重载时误报断开；重连后确认 scriptCompilationFailed=false，并执行测试。Console 仍有编辑器 ScriptableSingleton 已存在的日志，未将其视为游戏逻辑通过的依据。按钮预制体各状态图片及地图主题加载另行检查通过。

本次仅交付地图、界面动效与按钮图片模块；BodyPart 拼装、组合动画、新职业及逐卡独立插画不属于本模块完成项。
