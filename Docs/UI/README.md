# UI 框架

入口：Assets/Scripts/UI/Framework。主线程调用：

    var view = await manager.OpenAsync<GameStartView>(GameStartView.Path);
    manager.Hide(GameStartView.Path);
    manager.Show(GameStartView.Path);
    manager.Close(GameStartView.Path);

UiManager 用 Resources.LoadAsync 加载预制体。同一路径的并发请求共用加载和实例；Hide 保留实例，Close 释放生命周期并销毁实例。加载途中 Close 会取消等待，迟到的加载不会创建孤儿对象。失败的路径会移除缓存，允许重试。销毁 manager 自动关闭所有界面。Resources 由 Unity 缓存；Close 不承诺立即卸载纹理，不调用全局 UnloadUnusedAssets。

BaseUIView 实现 IUiElement，统一 Initialize / OnShow / OnHide / Release；派生界面重写 OnInitialize / OnShown / OnHidden / OnReleased。静态子元素在初始化时收集；动态元素通过 Register / Unregister 管理。Unity 原生 Graphic、Button、InputField 通过同物体上的 UiElement 适配接口；有逻辑的控件继承 UiElement。一个元素只属于一个界面，界面不嵌套其他 BaseUIView。

界面目录：
- Assets/Resources/UI/Views/GameStart：开始界面预制体及专属 Art/Background.png。
- Assets/Resources/UI/Views/Gameplay：游戏流程界面预制体及其内容容器。现有地图、战斗、奖励内容仍由 GameApp 动态填充。
- Assets/Scripts/Presentation/Views/<界面名>：对应视图脚本。
- Assets/UI/Shared/Portraits：多个界面可复用的战士、猎人肖像。

新界面创建独立目录和预制体，挂载 BaseUIView 子类，设置资源路径，在编辑器中编辑布局与序列化绑定。预制体根节点保存为 inactive，由框架显式初始化和显示。GameApp.UiReady 可等待首屏加载结束。

Tools/Spire/Rebuild UI Prefabs 是显式的初始布局重建工具，会覆盖上述两个预制体；日常美术和布局直接编辑预制体，不要为了运行游戏执行重建。运行时没有 Editor 依赖。横屏参考布局 1280x720，画布 Expand 缩放，开始页主体居中。

开始页标题、副标题、地址标签和按钮文案通过整数 LocIds 访问本地化 CSV/二进制；新增文案仍走 Tools/Tables/build_tables.py。

验证：UiFrameworkTests 覆盖缓存、显示隐藏、关闭、类型冲突、缺失路径、动态元素解绑和预制体适配器。FlowTests 等待 UiReady，再覆盖开始页→地图→战斗→失败、本地化切换。
