# UI 框架

`UiManager` 使用 ResourceManager 的整数资源 ID 异步加载预制体。调用 `Configure(parent, scope)` 后使用 `OpenAsync<GameStartView>(ResourceIds.GameStart)`，以及 Hide / Show / Close。并发请求共享同一界面实例；关闭加载中的界面后，迟到的实例会回收。Close 销毁界面实例，资源句柄由所属 scope 生命周期控制。

`BaseUIView` 实现 `IUiElement`，管理 Initialize / OnShow / OnHide / Release。静态子元素初始化时注册，动态元素使用 Register / Unregister，销毁前解绑。原生 uGUI 通过 UiElement 适配。

每界面独立目录：
- `Assets/GameResources/UI/Views/GameStart`：开始页预制体和专属 Art/101.png。
- `Assets/GameResources/UI/Views/Gameplay`：游戏页预制体。
- `Assets/GameResources/UI/Views/Card`：共享卡牌控件 3.prefab。
- `Assets/GameResources/Characters/<ID>/Images`：角色肖像和卡面数字图片。
- `Assets/Scripts/Presentation/Views`：视图脚本。

开始页只提供单人/多人两个游戏入口；单人进入选角，多人进入 IP 连接及准备房间。角色选项来自表。多人允许相同职业，只有双方已选且客人准备后主机可开局。

Tools/Spire/Rebuild UI Prefabs 是初始布局重建工具，会覆盖预制体。日常布局编辑预制体，无需重建。默认横屏 1280x720，Canvas Expand。UiReady / CardsReady 可等待异步 UI 和卡面加载；本地化使用 int LocIds。

更多资源生命周期、表配置、Bundle 和 FMOD 接入见 [Resources.md](../Resources.md)。
