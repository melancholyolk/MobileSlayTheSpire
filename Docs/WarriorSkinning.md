# 战士 Sprite Skin 与 IK 工作流

战士运行时资源：`Assets/GameResources/Characters/0/Skin/13000.prefab`，角色资源目录和 Addressables 作用域仍为 0。`Animation/21.json` 将战士指向 13000；其他职业与怪物继续使用原有 BodyAssemblyView。战士不再运行 BodyMotions 关节对旋转配方。

## 在 Unity 内编辑

1. `Tools > Spire > Warrior Skin > Build Missing Assets` 构建缺失素材、蒙皮预制体和资源目录；已有骨骼、权重、预制体及动画不会被常规构建覆盖。
2. 选中 Skin 目录中的 13020–13030 PNG，打开 Sprite Editor > Skinning Editor，调整骨骼、网格和权重并 Apply。源图片复用已有本地管线切片；不需要 Photoshop 或 PSB。
3. `Tools > Spire > Warrior Skin > Open Prefab` 打开角色。根对象默认关闭，供 ResourceManager 初始化后激活；在预制体模式临时开启可查看静态角色，编辑后保持默认关闭。
4. 手脚目标位于 `RenderSpace/Motion/Controls`，四条 LimbSolver2D 由 IKManager2D 管理。修改 Target 位置与 solver 的 Flip 调整肘膝朝向。骨骼和权重在 Sprite Editor 编辑，角色目标与动画在预制体/Animation 窗口编辑。
5. 13001–13010.anim 对应 Idle、Attack、Hit、Death、Guard、Walk、Skill、Dodge、Victory、Spawn。关键帧绑定目标位置、整体重心和头部；不要重新为上下臂/大腿小腿添加旋转曲线，否则会与 IK 竞争。
6. 动作通过 SkeletonView.Play(MotionId) 进入现有 Playables 混合和生命周期。Update 采样控制目标，LateUpdate 中 Unity IK 求解后由 SpriteSkin 更新网格。

## 资源和内存

每张 Sprite 都有真实网格（81 顶点）和导入期骨骼/绑定权重，数据保存在 PNG 的 .meta 中，可以继续使用原生 Skinning Editor。四条 IK 链共享角色骨架；上下肢接缝附近采用双骨混合，其余区域以所属部件为主。当前素材是原切片的首次蒙皮迁移，复杂大幅度关节遮挡和装备还需要美术精修。

SkinnedActorView 使用预制体自带的 SpriteRenderer、相机和 RawImage，将角色透明画面合成到已有 Overlay Canvas，避免改变其他 UI。每个激活角色分配一张 512×512 ARGB32、16 位深度的 RenderTexture，最多同时 8 个；纹理禁用/销毁即释放。Sprite、材质、Clip 和预制体由角色 ResourceScope 持有。预留渲染层 30；相机只渲染该层，各角色空间相隔 100 单位。直接在场景使用 SpriteRenderer 时无需这一 UI 合成层。

此阶段只迁移战士。还未实现蒙皮部件的运行时拆装、换骨骼后自动重绑定、全角色一键迁移，以及 Unity 内发起 AI 出图的完整窗口；现有静态蒙皮与 IK 编辑可以全部在 Unity 中完成。

## 验证

40 项 EditMode 与 9 项既有 PlayMode 回归通过；新增蒙皮专项 PlayMode 通过，验证 10 个动作的有效蒙皮顶点、可达 IK 目标误差、攻击顶点位移、真实渲染的非空/透明背景、返回菜单释放 RenderTexture。死亡动作从脚底旋转导致出界，已改为围绕身体中心旋转并重新验证。动作图片保存在本机 `.uloop/skin-preview/`，不提交测试输出。

2026-11-22 关节方向修正：四条近共线骨骼不能使用叉积的浮点符号推导弯曲侧，改为明确的左侧 Flip=true、右侧 Flip=false。已验证四肢在三个压缩比例、正向与镜像下的方向及目标可达性（4/4），10 动作蒙皮/释放专项（1/1）；防御与行走姿势截图确认方向修正。切片关节遮挡与边缘精修仍是独立美术工作。
