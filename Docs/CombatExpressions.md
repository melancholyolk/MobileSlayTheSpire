# 战斗表情

当前成品接入战士；ActorExpressionView 为可复用组件，其他角色需要配置各自 Neutral/Pain/Happy/Focus Sprite 后才能显示。

- Hit 的实际掉血量 > 0：痛苦；完全格挡不触发。
- Heal 的实际回血量 > 0：开心；满血治疗不触发。
- Attack、Guard：专注。物理攻击和普通攻击经过相同表现入口。
- 默认 0.8 秒后恢复中性；期间痛苦/开心优先于攻击、防御。连续伤害刷新持续时间。死亡固定痛苦，禁用或池化复用时重置。

战士表情位于 Assets/GameResources/Characters/0/Expressions，数字 ID 13101（痛苦）、13102（开心）、13103（专注）。通过角色预制体依赖由 ResourceManager/ResourceScope 统一持有，切换时没有临时异步加载、材质实例化或纹理复制。表情与默认头部使用相同大小、透明轮廓、网格、骨骼及权重，切换后仍由 SpriteSkin 蒙皮。

菜单 Tools > Spire > Warrior Skin > Build Expressions 从默认头部复制蒙皮数据并更新预制体；重建侧面战士时自动恢复表情组件。替换头部尺寸后必须同步重新制作全部表情图。

美术来自 D:/AiDraw 本地参考图重绘；原始候选、提示词、seed 与合成脚本在 D:/AiDraw/docs/art/runs/warrior-expressions-20261122。DreamShaperXL Turbo，8 步，CFG 3，denoise 0.48；seed 22112631/32/33。最终仅合成眉眼和嘴部，保留原人物发型、疤痕、轮廓与 alpha。运行时资源存游戏仓库，生产管线不入库。

验证：ExpressionTests 4 项通过；ExpressionPlayTests 1 项通过，覆盖实际战斗队列触发四类表情、SpriteSkin 有效顶点、自动恢复和角色渲染资源释放。同期回归 15 项中 14 项通过，既有 BattleCueTests.DrawShufflePlayAndDiscardCuesFollowAcceptedCommands 仍要求开战首事件为 Shuffle，与已有提交 1f697f8（取消开局洗牌表现）冲突；本次未改其断言。
