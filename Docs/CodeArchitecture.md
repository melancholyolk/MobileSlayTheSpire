# 代码架构与开发指南

本指南对应 2026-11-23 的代码整理。先读职责表，再沿调用链进入具体实现。目录对应业务职责，asmdef 对应编译依赖；新增功能优先扩展所属模块。

## 从哪里开始读

1. `Presentation/GameApp.cs`：启动、创建基础服务、加载视图、更新和销毁。
2. `Presentation/GameApp.Session.cs`：单人、主机、加入、重连和指令提交。
3. `Runtime/Spire/Run.cs`：一局游戏的数据、初始化、合法指令入口。
4. `Run.Map.cs`、`Run.Combat.cs`、`Run.Rooms.cs`、`Run.PhysicalCombat.cs`：按功能阅读规则。
5. `Presentation/GameApp.Rendering.cs`：规则状态如何切换为可见界面。
6. `Animation/BattlePresentation.cs` 与 `Presentation/Views/Gameplay/BattleHandLayout.cs`：规则事件如何驱动角色和手牌表现。

上述路径均相对于 `Assets/Scripts/`。原 `Core.cs` 改名为 `Run.cs`，原 `PhysicalCombat.cs` 改名为 `Run.PhysicalCombat.cs`，两个脚本保留原 `.meta`。挂载组件的原脚本路径、类型名和 GUID 保留。

## 模块职责

| 目录 / 核心类型 | 负责什么 | 开发边界 |
| --- | --- | --- |
| `Events/EventManager` | 强类型同步通知、监听生命周期和每种事件的对象池 | 见 [事件总线指南](EventManager.md)；事件发布后自动归还，只能在回调期间读取 |
| `Runtime/Spire/Run*` | 地图、投票、卡牌效果、回合、奖励、房间、命中结算 | 规则代码不依赖 Unity；随机统一使用 `RandomSource` |
| `Card`、`Hero`、`Enemy`、`MapNode` | 配置和运行状态数据 | 字段名称是既有接口，修改前检查表读取、测试及调用方 |
| `CardTable`、`BusinessTable` | 带版本、长度与字段校验的二进制读取 | Excel 生成在 `Tools/Tables/`，运行时不读取 Excel |
| `CoopSession` | 双人大厅、权威指令处理、历史重放、断线恢复 | 主线程 `Pump`；客户端请求由主机验证 |
| `Peer`、`Packet`、`CommandRecord` | TCP 分帧、线协议、可重放指令数据 | JSON 字段和协议版本属于兼容性约定 |
| `Presentation/GameApp*` | 组装服务、持有会话、协调界面与表现 | 规则变更进入 `Run`，输入转为 `Send` |
| `Presentation/Views` | 开始菜单、地图、战斗 HUD、卡牌绑定、手牌交互 | 页面依赖应用协调器，展示逻辑留在对应视图 |
| `UI/Framework` | 视图打开、显示、隐藏、关闭以及子元素生命周期 | 通过 `UiManager` 和 `BaseUIView` 统一管理 |
| `Resources/ResourceManager` | Addressables 引用、内存预算、实例预算 | 调用方通过资源 ID 和作用域使用资源 |
| `Resources/ResourceScope` | 一组资源租约、实例池、附属对象、异步释放 | 谁创建作用域，谁负责结束它；`Own` 的对象随之释放 |
| `Audio/FmodAudioManager` | 音频配置、FMOD 初始化、事件和 Bank 管理 | FMOD 不可用时维持现有降级行为 |
| `Audio/FmodAudioScope` | 作用域内声音实例与 Bank 的回收 | 与角色或应用 `ResourceScope` 绑定 |
| `Localization/Catalog` | 本地化二进制目录读取和校验 | 保持 SPLC 格式兼容 |
| `Localization/ExcelLocalization` | Unity Localization 初始化、语言切换、按 ID 取文案 | 文案来源为打表产物，避免业务代码新增散落字符串 |
| `Animation/BodyAssembly`、`BodyCatalog` | 身体拓扑、接口兼容性和表配置 | 容量限制与连接规则由该模块维护 |
| `Animation/SkeletonView`、`BodyAssemblyView` | 关节动作与骨骼表现 | 当前实现包含兼容路径，修改前检查骨骼测试 |
| `Animation/CombatBody`、`AttackHitbox`、`BodyPartHurtbox` | 攻击形状、部件受击区和命中查询 | 查询结果回传权威规则层结算生命值 |
| `Animation/SkinnedActorView`、`ActorExpressionView` | 蒙皮角色的 UI 显示、编辑器预览、表情 | 保持预览和运行时资源生命周期分开 |
| `Animation/BetterAnimation` | 卡牌与 UI 的补间播放、取消和过渡 | 被视图及战斗表现复用 |
| `Animation/BattlePresentation` | 顺序消费战斗信号，管理角色和卡牌表现过程 | 动画结束与规则阶段切换需要协调 |
| `Animation/GraftMonster*` | 拼装怪物原型状态与交互展示 | 原型需求独立阅读，接入主流程前明确规则归属 |
| `Aot/GameDriver` | 当前为空的 AOT 生命周期占位组件 | 实际游戏入口是 `GameApp.Bootstrap` |
| `Assets/Editor` | 打表、资源目录、UI、骨骼、皮肤及表情资产构建 | 修改生成资产时同步评估生成器，避免下次重建覆盖 |
| `Assets/Tests` | 规则、网络、资源、骨骼、UI 和流程验证 | 选择与功能相关的 EditMode / PlayMode 测试 |

`Assets/Scripts/Editor/UnsafeCodeTest*.cs` 是独立的 unsafe 示例；第三方绑定在 `Assets/ThirdParty`，嵌入依赖在 `Packages`，不作为业务扩展入口。

## 当前程序集依赖

下面只列项目内依赖，省略 Unity 与插件程序集。

```text
Spire.Presentation → AssemblyRuntime, Spire.UI, Spire.Resources,
                     Spire.Audio, Spire.Animation, Spire.Localization
Spire.UI           → Spire.Resources, Spire.Animation
Spire.Animation    → AssemblyRuntime, Spire.Resources
Spire.Audio        → Spire.Resources, Spire.FmodBindings
Spire.Resources    → Addressables
Spire.Localization → Unity Localization, Addressables
AssemblyRuntime    → UnityEngine（CoopSession 的 JsonUtility）
AssemblyAot        → 无项目内引用，当前为空入口
Spire.Events       → 纯 C#，无 Unity 引用；由需要通知的模块显式引用
```

`Run` 自身是纯 C# 规则；`AssemblyRuntime` 整体仍包含使用 Unity JSON 的网络代码。新增依赖前查看对应 `.asmdef`，避免规则反向引用表现层或视图框架引用具体页面。

## 核心调用链与不变量

### 一次出牌

```text
HandCardInteraction → BattleHandLayout → GameApp.Send("play", handIndex)
    单人：Run.Apply
    联机：CoopSession.Submit → 主机 Accept → Run.Apply → 广播并重放
Run 更新状态和 Revision，产生 BattleCue
GameApp.Render → BattlePresentation.Observe
    手牌信号：BattleHandLayout.Present
    物理攻击：CombatBody 查询 → AttackResolved
    权威端：Run.ResolveAttack / CoopSession.ResolveAttack
```

- `Apply` 验证玩家、阶段和操作。成功时推进 `Revision`；不要从视图直接扣能量或生命值。
- 英雄 actor ID 为席位，敌人 actor ID 为 `-1 - enemyIndex`。身体命中 `-1` 表示未命中。
- 实际游戏启用物理攻击；无表现的规则模拟保留即时结算模式。等待命中时禁止新的规则操作，命中按请求 ID 顺序结算。
- `Run.Cues` 是最近一次操作的表现信号，不是完整历史。完整联机重放使用 `CommandRecord`。
- 卡牌排序、随机调用次数、投票平局规则和结算先后都会影响确定性；重构时保持它们稳定。
- UI 的预排队、移动和动画完成不等于主机接受指令；手牌逻辑索引与可见对象由 `BattleHandLayout` 协调。

### 应用与资源生命周期

```text
GameApp.Awake → ResourceManager / 应用 ResourceScope / UiManager
LoadViews → 音频配置、主题、卡表、地图、战斗页、开始页
CharacterScope → 角色资源 + 角色音频
ReturnToMenu / StartSolo / OnDestroy → 相应会话与作用域清理
```

异步回调中的 `renderVersion`、`generation`、`IsDisposed` 检查用于阻止旧页面或旧角色加载完成后回写。新异步加载必须检查结果仍属于当前请求；失效实例通过所属作用域归还。资源释放等待 Unity 延迟销毁与正在进行的加载完成，新增调用方应理解 `Dispose` 与 `DisposeAsync` 的区别。

## GameApp 文件分工

| 文件 | 内容 |
| --- | --- |
| `GameApp.cs` | 共享状态、Bootstrap、Awake、加载视图、Update、语言切换、OnDestroy |
| `GameApp.Session.cs` | 导航、返回菜单、开始单人、发送指令、主机与加入 |
| `GameApp.Resources.cs` | 清理可见卡牌、角色作用域、角色音频加载 |
| `GameApp.Rendering.cs` | 阶段路由、房间与奖励页内容、等待战斗表现结束 |
| `GameApp.Widgets.cs` | 动态 Label / Button / Box 和奖励卡牌实例化 |
| `GameApp.Battle.cs` | 预制体战斗 HUD 刷新与头像加载 |

这些 `partial` 文件仍是同一个协调器，成员保持原来的访问权限和行为。共享字段集中在主文件以保持初始化顺序；跨文件操作通过功能命名定位。继续扩展时，若某项职责拥有独立状态和生命周期，再提取为独立对象。

## 新功能放在哪里

| 需求 | 修改入口 | 验证重点 |
| --- | --- | --- |
| 现有效果的新卡牌或角色 | Excel、打表与美术配置 | TableTests、ProductionRunTests、对应角色 UI |
| 新卡牌效果 | `Effect`、`Run.Combat.Play`、表枚举校验、必要的 `BattleCue` | 效果边界、非法操作、确定性、联机重放、表现 |
| 新房间 | `RoomKind` / `Phase`、`Run.Map`、`Run.Rooms`、页面路由 | 路线生成、团队完成条件、重复结算 |
| 新界面 | `Presentation/Views/<功能>`、`BaseUIView`、资源目录、预制体生成器 | 打开/关闭/重复打开、资源释放、过渡取消 |
| 新攻击或身体部件 | 身体表、`BodyCatalog`、`CombatBody`、攻击配置 | 端口兼容、命中部件、顺序结算、主机权限 |
| 新网络指令 | `Run.Apply`、`CommandRecord`、`CoopSession` | 过期 Revision、越权、重连历史、协议兼容 |
| 新音效 | 音频配置、资源目录、相应作用域调用 | 不可用降级、Bank 和声音实例释放 |

## 统一书写与修改约定

- 使用根目录 `.editorconfig`：4 空格、UTF-8、CRLF、展开多语句块、规范空格。
- 类和方法使用 PascalCase，局部变量及参数使用有意义的 camelCase。已有 JSON 字段与 Unity 序列化字段保留名称；重命名前明确迁移策略。
- 按功能组织文件；数据类型优先独立命名。仅供父类型使用的辅助类型可以嵌套。小型紧密关联的枚举和数据记录允许同文件。
- 一个方法描述一个流程步骤；复杂分支先说明约束，再按业务步骤拆分。注释解释顺序、所有权、确定性或兼容原因。
- 新增常量优先明确单位和含义。数值、文案、资源映射逐步进入已有表或配置体系。
- 本次整理保持现有公开字段、序列化字段、协议、资源 ID 和方法体行为。更改访问权限、接口或算法应单独提交，并提供相关行为测试。

## 仍需持续治理的边界

目前 `Run` 对外仍暴露可变集合，`GameApp` 仍协调多个生命周期，部分房间文案、怪物数值及资源 ID 仍写在代码内。后续可分别推进只读状态接口、会话对象提取和配置化。每项改造应独立验证确定性、网络重放和资源回收，避免一次改动同时改变结构和玩法。

## 验证方式

代码重排先检查语法树和成员标记保持一致，再使用项目 uloop 技能编译并运行相关测试。脚本移动必须携带 `.meta`，挂载组件的脚本尤其需要核对预制体引用。测试使用 `--unsaved-changes fail`，保护编辑器中尚未保存的场景和预制体。

团结引擎程序集重载可能让 CLI 误报 `UNITY_NOT_RUNNING`；只有恢复后的实际编译证据和测试结果才算验收，不能把断连视为成功。网络测试使用本机真实 socket，不代表跨设备或公网验收。

### 本次验证记录（2026-11-23）

- 结构整理前后以 Roslyn 比较成员标记：编辑器配置 970 个、非编辑器配置 965 个成员均保持一致。
- 新增 EventManager 的 12 项 EditMode 测试全部通过；预热后 1000 次发布测得 0 字节线程托管分配。
- 最终 EditMode 为 67/68 通过；PlayMode 为 14/16 通过。失败项为 `DrawShufflePlayAndDiscardCuesFollowAcceptedCommands`、`LanguageButtonRefreshesMenuAndCardNamesWithoutChangingRun` 和 `MenuToMapCombatAndDefeatUsesVisibleButtons`，均已使用修改前的源码复现，未在本次结构整理中修改这些业务行为。
- CLI 编译遇到已知重载断连；恢复 Ready 后核验当前程序集输出时间覆盖源码，并通过 Unity TestRunnerApi 运行上述测试。Console 仍有既有 Tilemap `ScriptableSingleton already exists` 错误，不能宣称全部检查通过。
