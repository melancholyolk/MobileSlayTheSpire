# Ashen Ascent / MobileSlayTheSpire

团结引擎 2022.3.62t17（1.11.0）的可玩卡牌爬塔框架。当前为功能原型，不是杀戮尖塔的完整内容复刻。

## 运行

打开 `Assets/Scenes/GameStart.unity`，点击 Play。启动器自动创建 uGUI 界面，选 Warrior 或 Hunter 进入地图。选择亮起的节点进入房间，战斗中点击卡牌与 End turn。战斗胜利后选卡或跳过；营地、商店和事件结算后继续，最终 Boss 决定通关。

当前默认种子为 42；代码 `GameApp.StartSolo(class, seed)` 支持其他种子。包含 8 层分支地图、战斗／精英／营地／商店／事件／Boss 房间，战士和猎人各 8 种卡、各 10 张初始牌。单个战斗当前只有一个敌人；商店仅出售治疗，营地提供治疗或最大生命，不含卡牌升级。力量、格挡、中毒、抽牌、弃牌重洗、消耗和能量均有实现。

## Excel → 二进制

配置源为 `GameData/Excel/Cards.xlsx` 的 `Cards` 工作表。不要重新运行初始化脚本覆盖策划修改。安装 Python 3 和 `openpyxl` 后在根目录运行：

```powershell
python Tools/Tables/build_tables.py
python -m unittest discover -s Tools/Tables -p 'test_*.py'
```

输出 `Assets/Resources/Tables/cards.bytes`。编译器校验列头、重复 ID、枚举、数值范围和初始牌组，失败不覆盖旧文件；不接受公式字段，以免 Excel 缓存导致过期数值。运行时读取 SPCR v2 二进制，卡牌 Name 字段声明为 string(Common)，打表时替换为 int32 NameId，不需要 Excel 库。业务表第 1 行为字段名、第 2 行为类型、第 3 行起为数据。完整本地化 CSV 增量流程见 [本地化说明](GameData/Excel/Localization/README.md)。

表头：`Id, Name, Class, Cost, Effect, Amount, Hits, Exhaust, StarterCopies`。职业为 Warrior／Hunter；Effect 为 Attack／Block／Strength／Poison／Draw／Heal／Energy。Exhaust 必须是 Excel 布尔值。Hits 仅用于攻击。新增卡牌时同步维护美术主题表。

## 双人联机

主机选择 Host co-op（战士），另一实例输入主机 IP 并选择 Join co-op（猎人），TCP 7777；同机使用 `127.0.0.1`。这是直连局域网原型，没有大厅服务、Relay、NAT 穿透或互联网部署。

主机按顺序验证指令；连接绑定玩家席位，客户端不能替他人行动。双方独立手牌与能量，共享路线与敌人；全员投票后进入房间、全员结束回合后敌人行动。票数相同按节点 ID 决定。双方卡表 SHA-256 必须相同。并发过期指令会拒绝并提示重试。

客户端断线后主机暂停接受操作。客户端点 Reconnect 以保存在内存中的席位令牌恢复，主机重放已接受指令恢复客户端状态。主机退出、客户端进程重启后没有持久化恢复，也没有主机迁移。倒下角色本局不复活；仍有存活队友时继续。

已安装官方 ParrelSync，菜单 `ParrelSync > Clones Manager`。源码以嵌入包固定在 `Packages/com.veriorpies.parrelsync`，来源和版本差异见包内 PROVENANCE.md。通过插件创建克隆、打开第二个编辑器后双端进入 GameStart。插件菜单已验证；独立双编辑器窗口和跨机器网络尚未作为本次自动化验收的一部分。

## 代码边界

- `Assets/Scripts/Runtime/Spire/Core.cs`：不依赖 Unity 的确定性地图、战斗、卡牌、房间和团队规则。
- `CardTable.cs`：带版本与边界检查的二进制表读取。
- `CoopSession.cs`：主机权威 TCP 指令传输与重连重放。
- `Assets/Scripts/Presentation/GameApp.cs`：uGUI 功能原型与网络入口。
- `Assets/Tests`：规则、正式卡表、通关、TCP 和 PlayMode 界面测试。

## 验证记录（2026-11-21）

15 项 EditMode 测试通过：确定性与非法操作、团队回合、胜败、二进制校验、每张正式卡的效果、双职业正式数值通关、中毒结算，以及真实 loopback TCP 的同步／非法指令／重连／败北／双人通关。1 项 PlayMode 测试通过：通过界面按钮从菜单到地图、战斗、出牌和败北结算。2 项 Python 编译器测试通过。

TCP 测试使用真实系统 socket，但两端在同一测试进程；不能等同于公网、跨设备或独立双编辑器验证。当前没有移动端打包或设备适配验收。

团结引擎重载时 uloop compile 会误报 UNITY_NOT_RUNNING；本次没有将其视为编译成功，而是在进程恢复 Ready、读取 Console 后运行已导入程序集的 Test Runner（`--skip-compile`，绕开 CLI 再次触发重载）。测试通过是本次编译和逻辑验收依据。重载时曾出现 Tilemap 包自身的 GridPaintingState 单例错误；业务测试未产生该错误。

## 尚未完成

AI 卡图实际生产和 20 选 1 尚未执行，320 个待生成任务与风格规范见 `ArtProduction/`。当前没有绘图工具入口；没有用程序占位图代替 AI 绘图。当前也不含完整商业游戏所需的遗物、药水、存档、多幕内容、完整职业卡池、账号与在线大厅。
