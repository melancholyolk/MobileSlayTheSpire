# 资源、卡牌与音频

## 加载和所有权

游戏代码通过 `ResourceManager` 创建 `ResourceScope`，用 `LoadAsync<Sprite>(1001)`、`InstantiateAsync(3, parent)` 加载。调用方只传 int；Addressables GUID 字符串封装在目录和后端中。Unity/Addressables 自身仍可能分配字符串，整数 API 不代表整个引擎零 GC。

每个玩家角色实例有独立 scope；相同角色的多个玩家共享底层引用计数。角色退出时 `DisposeAsync()` 清理图片引用、活动对象、池对象、音频事件与 Bank，再释放 Addressables 句柄。最后一个使用者释放后角色 Bundle 卸载；仍被其他玩家使用的资源保留。Unity 和系统内存分配器可能保留已申请页，因此不承诺进程工作集立即下降。

池限制：每 scope 每资源最多 12 个闲置实例；全局最多 128 个实例、256 个加载项、估算 256 MiB 资源预算。超额返回明确错误，不无限增长。估算不覆盖所有 GPU/原生分配，不能作为绝对 OOM 保证。共享 UI 跟随应用 scope，角色资源不可跨角色 scope 加载。引导目录是唯一 ResourceManager 内部 Resources.Load；本地化插件保留自身 Resources 表引导。

## 新增卡牌 / 角色

编辑 `GameData/Excel/Cards.xlsx`：Cards 表的 Class 为角色 int ID，ArtId 为图片 int ID；Characters 表配置 Id、Name、MaxHp、PortraitId。新增角色无需改 HeroClass 枚举，运行时从表发现。每个角色至少配置 5 张 Starter 卡。删除卡牌行后重新打表即可；删除角色需同时删除其卡牌行。

图片放 `Assets/GameResources/Characters/<角色ID>/Images/<资源ID>.png`，导入为 Sprite，资源 ID 全项目唯一。角色音频 Bank 同样放该角色目录下，改为数字 `.bytes`。共享音频在 `Assets/GameResources/Audio`，共享 UI 在 `Assets/GameResources/UI`。现在 1001/2001 为已有战士/猎人肖像，各职业卡暂时复用，可逐卡替换 ArtId；尚未生产每卡独立插画。

运行 `python Tools/Tables/build_tables.py`（或现有批处理）。表工具先增量同步本地化 CSV，再生成整数引用二进制。Unity 自动导入数字角色资源；也可 Tools/Spire/Build Resource Catalog。Tools/Spire/Build Addressables 构建独立 Spire_Character_<ID> Bundle，角色间不混包。Player 构建前自动构建目录和 Addressables。默认编辑器 Fast Mode，实际 Bundle 模式也已测试。

配置可新增现有效果组合、卡牌和角色；全新的战斗机制仍需要实现对应 Effect 逻辑，不是任意代码行为都能凭表生成。

## FMOD

`FmodAudioManager` + `FmodAudioScope` 已接入资源生命周期，事件调用只传整数。`Assets/GameResources/Audio/20.json` 配置 Events（Id / Guid）和 Banks（ResourceId / CharacterId，-1 表示共享）。事件 1 预留菜单点击，2 预留出牌。GUID 只在配置阶段解析。Bank 通过 ResourceManager 加载并调用 FMOD loadBankMemory；角色 scope 释放前先停止/释放事件并卸载 Bank。每 scope 最多保留 64 个事件，系统 real voice 配置 64。

仓库包含官方 2.02.35 的托管绑定（改为 SpireFmod 命名空间，来源与许可见 Assets/ThirdParty/FmodBindings）。官方 GitHub 不包含原生库。本机缺失完整 FMOD Unity SDK，当前无 Bank / 音效素材，因此运行时报告 FMOD unavailable 并继续无声运行，尚不能验收实际播放。

需要提供与绑定版本匹配的官方完整 FMOD for Unity 包/SDK；Windows x64 需要 fmod.dll、fmodstudio.dll，按平台设置 Plugin Importer。移动平台还需各自原生库与导出设置。将 FMOD Studio 导出的 Bank 按数字资源 ID 命名为 .bytes（含实际需要的 Master Bank），在 20.json 配置事件 GUID 与 Bank 所属角色，然后构建并验证发声、场景退出和双同角色实例的 Bank 生命周期。发布前核对 FMOD 许可。

## 联机

当前 TCP 双人：单人 → 选角 → 地图；多人 → 主机/加入 IP → 双方选角 → 客人准备 → 仅主机开局 → 地图投票。允许相同角色；选角变化取消准备。默认端口 7777，仅支持直接 IP，未实现公网中继/NAT 穿透或四人房间。
