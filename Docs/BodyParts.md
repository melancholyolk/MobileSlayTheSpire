# BodyPart 与组合骨骼

配置源：GameData/Excel/BodyParts.xlsx，四张表第一行为字段名、第二行为类型。

- BodyParts：Id、Role（动画角色）、ArtId（整数图片资源）、分片尺寸和偏移。
- BodyPorts：每行一个部件接口，Part/Port 唯一；Type 与另一接口的 Accepts 双向匹配才允许连接。接口数量由行数决定，X/Y/Angle 表示连接锚点。
- BodyNodes：Owner 为角色资源作用域（英雄 ID，怪物 1000+ID），Parent 为实例 ID；ParentPort/Port 指定双方接口，Group 为动画组。
- BodyMotions：最小单位为 ParentRole + ChildRole 的关节对。Motion 为 0..9，Duration/Peak/Middle/End 描述旋转轨迹；ParentRole=0 可作通配。

运行 Tools/Tables/build.bat 可在 Unity 外构建。原有 watcher 会检测 Excel 修改；打表先检查接口重复占用、兼容性、跨角色父节点、环路、容量和动作范围，再发布二进制。运行时通过 ResourceManager 加载 23..26，不读取 Excel。角色进入战斗时直接按最新二进制重建关节与图片，无需为角色写独立动画脚本。

BodyAssembly 支持 Add、Reparent、Detach（返回独立子树）、MergeGroup；树形拓扑不限定头/手/脚的数量，默认最多 512 个部件，可按实例调到最多 4096。所有节点 API 返回副本；无效连接不修改原结构。

BodyAssemblyView 将每对关节的曲线绑定到当前实际路径，合成十个运行时 Clip，由现有 SkeletonView 的 Playables Mixer 播放。PlayGroup 可以将合并子树作为一个动作单元；当前是单个组的动作选择，未实现多个组同时播放不同时间轴的分层混合。Attach 接受调用者已加载并由 ResourceScope 管理的关节；Detach 隐藏分离子树，不擅自释放其他对象共享的图片。禁用时停止 Graph 并释放运行时 Clip，重新启用从初始装配配置重建。

十四个现有角色/怪物使用这套运行时机制；旧 .anim 保留作兼容样例，不再是战斗运行时的动作来源。视觉资源仍按角色/怪物目录分包。

2026-11-22 验证：EditMode 31/31、PlayMode 8/8；新增 200 部件长链、接口占用/环路原子拒绝、组合关节真实旋转、12 个生产装配表、10 Clip 及 Graph 释放检查。团结引擎域重载会让 uloop compile 误报断开，重连确认无编译失败后执行测试。

四职业扩展：Owner 2/3 为法师/射手，每个 11 部件，共 154 个部件、294 个接口；继续复用原有 110 条关节对动作。角色归属来自 Rigs.json 的 Owner，不再以列表位置推导。

