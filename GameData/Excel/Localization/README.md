# Excel → 本地化 CSV → 整数 ID 二进制

所有业务 Excel 位于 `GameData/Excel`；本地化 CSV 位于子目录 `Localization`。
旧的 `Localization/Game.xlsx` 已迁移为 `Game.csv` 和 `Common.csv`，原有 32 条中英文译文保留。CSV 是本地化唯一数据源，不再维护第二份本地化 Excel。

## 业务表格式

每个工作表是一张业务表，第一行字段名、第二行类型、第三行起数据。例如 `StoryExample.xlsx`：

| Id | Text | Button |
| --- | --- | --- |
| int | string(Story) | string(Common) |
| 1 | 灰烬落下，新的旅程开始。 | 继续 |
| 2 | 灰烬落下，新的旅程开始。 | 继续 |

支持 `int`（int32）、`float`（float64）、`bool`、`string` 和 `string(表名)`。普通 string 保留原文，string(表名) 在二进制中转换成 int32。表名/字段名使用英文标识符；表名在所有工作簿中唯一。以 `_` 开头的工作表视为说明页，其余页均须符合此格式。公式不支持。卡牌 Name 已改为 `string(Common)`。

## 执行流程

双击 `Tools/Tables/build.bat`，或执行 `python Tools/Tables/build_tables.py`，均无需 Unity。

1. 扫描并校验全部业务表和本地化 CSV。
2. 收集所有 `string(Table)` 字段，按指定表及 Source 原文精确匹配。同表相同原文复用 ID；不同表的相同文本分别分配 ID。空格、换行属于原文，不自动归一化。
3. 缺少 CSV 则自动创建；只增量追加缺少的文本，译文留空，运行时回退 Source 原文。不覆盖已有译文、注释和 ID。
4. 先将 `registry.json` 中的 ID 高水位及 CSV 写入磁盘，再重新读取 CSV 编码本地化与业务二进制。
5. 输出内容不变则不重写。业务校验失败不修改 CSV 或旧二进制。单个文件采用原子替换；多个输出文件并非文件系统事务，发布阶段若因磁盘/权限失败，修复后重跑全量打表。构建 Player 前也会重跑并阻止失败的构建。

ID 是全局唯一的正 int32，从 1 开始递增。`registry.json` 持久化 next_id；不能手动降低它。业务文本修改会新增 ID；删除业务引用不会清理旧本地化行或复用旧 ID，避免其他数据引用失效。CSV 与 registry.json 必须一起提交。并行打表通过操作系统文件锁串行保护；并行分支合并产生重复 ID 会报错，需要合并源数据后重新分配冲突的新行。不要手动修改已发布行的 Id/Source。

## 翻译 CSV

列为 `Id,Source,Comment,Symbol,en,zh-Hans`。用支持 UTF-8 的表格编辑器修改语言列即可。CSV 使用 UTF-8 BOM，带逗号、引号、换行的文本按标准 CSV 转义。

- `Id`：整数主键，全局唯一。
- `Source`：业务表原文，用于构建期查重，译文为空时回退至它。
- `Comment`：翻译备注，不进入二进制。
- `Symbol`：可选 C# 常量名，用于没有业务表引用的 UI 文本。生成 `LocIds.cs` 中的 const int，不是运行时字符串 key。保留现有 UI Symbol。
- 语言列：翻译内容。新增语言时更新 registry.json 的 locales 并给所有 CSV 添加对应列。

## 自动构建

- Unity 开着：编辑器监听 Excel、CSV、registry.json 的保存，稳定后调用同一个 Python 构建器并刷新资源。
- Unity 关闭：启动 `Tools/Tables/watch.bat` 或 `python Tools/Tables/build_tables.py --watch`，保持进程运行；Ctrl+C 停止。
- Play 中不热替换已加载的二进制，停止后重新 Play 读取新资产。
- 监听忽略 Excel `~$` 锁文件；构建器写回 CSV 不会形成无限重建。

Python 3.10+，依赖 `python -m pip install -r Tools/Tables/requirements.txt`。批处理支持 `TABLES_PYTHON` 指定解释器。

## 运行时

```csharp
// 卡牌二进制只保存 NameId，不包含名称字符串。
string cardName = ExcelLocalization.Text(card.NameId);
string menu = ExcelLocalization.Text(LocIds.UiMenu);

// 通用业务表：缓存列下标，然后通过 int32 获取本地化引用。
var table = BusinessTable.Read(Resources.Load<TextAsset>("BusinessTables/StoryExample").bytes);
int textColumn = table.Column("Text");
string story = ExcelLocalization.Text(table.Int(0, textColumn));

// 官方 Unity Localization 同样支持数字 ID：
var localized = new LocalizedString("Game", (long)LocIds.UiMenu);
```

`Text(int)` 使用 `Dictionary<int,string[]>`，返回已加载的译文字符串，不拼接 key、不把 ID 转为字符串、不装箱。预热后 10,000 次查询的托管堆分配测试为 0。文本值本身仍须在加载时解码为字符串；表名、语言代码也是每个表/语言加载一次，不是逐条文本字符串 key。官方插件通过 `ITableProvider` 按 CSV 表名提供 `StringTable`，仅使用 long ID 重载（来自 int32 安全提升），不生成字符串 Entry Key。Inspector 的字符串键下拉编辑不作为源数据维护入口。

输出：卡牌 `Assets/Resources/Tables/cards.bytes`（SPCR v2）；通用表 `Assets/Resources/BusinessTables/<工作表名>.bytes`（SPBT v1）；本地化 `Assets/Resources/Localization/catalog.bytes`（SPLC v2）。版本 1 本地化/卡牌二进制不兼容，代码和生成数据应一起部署。删除业务表时清理对应通用表输出，但保留本地化 CSV 历史行。

## 测试

`python -m unittest discover -s Tools/Tables -p 'test_*.py' -v`

覆盖自动建表、增量复用、跨表唯一 ID、先写 CSV 后编码、翻译保留、非法类型/公式/重复 ID 拒绝、构建锁、CSV 变更监听和删除表清理。Unity 测试覆盖真实二进制读取、数字 ID 插件查询、零分配查询以及菜单/卡牌切换和战斗流程。
