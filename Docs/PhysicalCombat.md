# 战斗攻击盒与部位受击

实机战斗启用 `Run.UsePhysicalAttacks`。出牌/敌方回合先产生 `AttackRequest`，伤害、格挡、死亡、胜利和下一回合都等待有效帧的碰撞结果。无界面模拟仍可使用原来的立即结算模式。

## 判定流程

- `AttackHitbox` 是值类型旋转矩形，不创建 GameObject 或攻击 Collider。
- `BattlePresentation.PhysicalAttack` 在前摇开始时锁定目标当时的位置；有效窗口逐帧查询，首次命中即提交。每段攻击独立判定一次，不因重叠多个部位而重复扣血。
- `CombatBody.Query` 使用 `Physics2D.OverlapBox` 和真实部位 BoxCollider2D；只接受当前目标所属的部位，最近部位优先，距离相同按 NodeId 排序。禁用部位 Collider、移出范围或设置 `CombatBody.Invulnerable` 会落空。
- 攻击有效帧在 Scene Gizmos 显示红色框。关闭 `ShowHitboxes` 可隐藏；没有有效判定的帧不保留旧框。部位框使用 Unity 自带 Collider Gizmos。
- 当前每张攻击牌仍以已有目标选择规则选择敌人；判定盒是锁定落点的攻击，不要求近战位移。未来近战可改为武器/骨骼提供盒的位置，保留同一碰撞与结算接口。

## 配置与角色接入

`CombatBody.DefaultAttack` 提供默认攻击数据；`CardAttacks` 可按 CardId 覆盖。Size/Offset 使用战斗舞台 UI 单位；Angle 为角度；Windup/ActiveTime/Recovery 为秒。Reaction 是相对受击部位尺寸的偏移，ReactionDuration 为恢复时间。可以在角色预制体上添加 CombatBody 持久配置这些字段；未添加时运行时使用默认值。

装配角色在 `BodyAssemblyView.Rebuild` 时为每个当前节点绑定 `BodyPartHurtbox` 与 trigger BoxCollider2D，节点拆卸后禁用对象即退出判定。蒙皮角色加载后按 BodyCatalog 骨骼绑定；Collider 尺寸由实际 Sprite 单位换算，不使用 UI 表的缩放尺寸直接作为世界尺寸。

蒙皮角色位于离屏 RenderSpace，判定会把舞台中的攻击盒经过 RawImage 和正交摄像机映射回真实骨骼坐标，避免把相隔很远的离屏角色误认为不可攻击。现有正交摄像机、矩形显示与无倾斜的舞台布局是该映射的适用范围。

`BodyPartHurtbox.ReactionTarget` 优先绑定包含该骨骼的 IK chain target。头部/躯干等无 IK 的节点使用自身 Transform。偏移在动画后、IK 前叠加，下一帧动画前移除，结束/禁用时恢复，不重置全身动作。

## 联机与时序

只由主机提交 `ResolveAttack(id, bodyPart)`，`-1` 为落空。命中结果作为协议 3 的 impact 记录广播和存档；客户端及重连回放不重新用本地物理计算伤害，客户端不能提交 impact。多段攻击按 ID 顺序解决，重复结果无效。待判定期间拒绝其他回合指令；未来即时闪避输入应走独立动作通道，不通过当前出牌/结束回合指令。

验证：EditMode 覆盖判定、去重、多段、回合推进及真实 TCP 命中结果重连；PlayMode 覆盖实机伤害延迟、位移避开固定落点、无敌落空、蒙皮角色受击及局部偏移恢复。
