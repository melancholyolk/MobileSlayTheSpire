# 2D 关节动画与过程表现

角色使用 11 个关节的刚性分片骨骼，图层绑定到 Transform 父子关节；不是序列帧，也不是整张图片平移伪装动作。当前使用 uGUI Image 渲染，以便放在现有 Canvas 战斗视图内。属于 cutout 骨骼，不是 SpriteSkin 网格权重变形；大角度动作可能显示拼接边缘，后续可替换为精修分层素材。

SkeletonView 用 PlayableGraph + AnimationMixerPlayable + AnimationClipPlayable，运行时 Play(MotionId, blend) 任意切换；最多保留两个 Clip，禁用/销毁时销毁 Graph。死亡保持末帧，其他一次性动作回到 Idle。动作回调包含 ticket、Motion、Started / Impact / Completed / Cancelled。循环动作在切换或释放时取消；被打断的一次性动作不会回调 Completed。

战士、猎人每人十个：Idle、Attack、Hit、Death、Guard、Walk、Skill、Dodge、Victory、Spawn。十种怪物每个五个：Idle、Attack、Hit、Death、Guard。共 12 个预制体 / 70 个独立 AnimationClip。动作按角色调整幅度，猎人 Attack 使用双臂拉弓姿态。只有已有战斗事件对应的动作自动触发，Walk/Dodge 等可由行为系统调用。

资源：Characters/0、Characters/1、Monsters/0..9 的 Rig 目录；预制体 ID 10000+序号*100，动画 +1..10，分片图片 +20..30。怪物资源作用域 owner=1000+怪物 VisualId，每种怪物独立 Bundle。共享 21.json 自动映射角色/怪物到骨骼预制体，不需运行时类型分支。

GameData/Art/Rigs.json 保存关节和分片参数；Tools/Spire/Build 2D Skeleton Assets 重建预制体、动作和映射。工具覆盖生成的资源，手工精修前另存副本。原始生成图、工作流、seed、收据、透明分片脚本留在 D:/AiDraw/docs/art/runs/spire-rigs-20261121；游戏仓库只保存最终透明分片和关节参数。

BattleCue 是确定性逻辑产生的表现记录，按接受的命令发出抽牌、洗牌、出牌、弃牌、消耗、攻击、受击、死亡等事件。BattlePresentation 有界队列容量 128；过程 Lifecycle 依次 Queued → Started → Impact → Completed，卸载或超容量则 Cancelled，每个 ticket 只终结一次。监听异常隔离，事件不会修改战斗 RNG。联机每条接受命令立即通知表现层，重连不重播完整历史。

非角色的卡牌飞行使用 BetterAnimation 封装 Unity Animation；其运行时 legacy clip 由适配层生成/采样/销毁，支持速度、暂停、恢复、取消。UI 层不直接创建动画状态。抽牌从牌堆飞入、出牌上移淡出、弃牌飞向弃牌堆、消耗缩小淡出、洗牌翻转。

已验证：EditMode 27/27、PlayMode 8/8；包括 12 骨骼/70 Clip 数量、无 Sprite 替换曲线、关节实际旋转、频繁切换 Graph 数量上限、所有卡牌过程唯一终止回调、退出释放。另完成实机攻击及待机视频抽帧检查。
