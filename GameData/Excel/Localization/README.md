# 本地化表流程

本目录是所有本地化 Excel 的源目录，卡牌源表在上一级 `Cards.xlsx`。
开发者只修改 `.xlsx`，不要手动修改 `Assets/Resources/Localization/catalog.bytes`。

## 配表

打开 `Game.xlsx` 的 `Strings` 页。第一行为固定表头：`Key | Comment | en | zh-Hans`。

- Key 是稳定的唯一标识（字母、数字、点、下划线、短横线），例如 `card.w_strike.name`。
- Comment 是给翻译人员的说明，不进入运行时数据。
- en 是必填英文回退；其他语言留空时使用对应英文。
- 单元格必须为文本；不允许公式。换行使用 Excel 的 Alt+Enter。
- 可以添加其他 `.xlsx`（也可放子目录），都必须有 `Strings` 页和相同顺序的语言列；Key 在整个目录中唯一。
- 添加语言：给所有表添加相同的语言列，例如 `ja`。语言代码需要 Unity/.NET 支持。
- Excel 临时锁文件 `~$*.xlsx` 被忽略。删除、重命名表或键后会全量重建，不残留旧键。

## 打表（无需打开 Unity）

从项目根目录双击 `Tools/Tables/build.bat`，或运行：

```powershell
python -m pip install -r Tools/Tables/requirements.txt
python Tools/Tables/build_tables.py
```

Python 要求 3.10+。批处理优先读取 `TABLES_PYTHON`，然后使用项目 `.venv`、已安装 Python，最后尝试本机 Codex 自带 Python。可将 `TABLES_PYTHON` 设为自己环境的 python.exe 完整路径。

输出为 `Assets/Resources/Tables/cards.bytes` 和 `Assets/Resources/Localization/catalog.bytes`。本地化二进制格式是 SPLC v1：魔数、版本、语言数、键数、语言代码，然后按 Key 排序的键和各语言值。整数为 32 位小端，文本为长度前缀 UTF-8。输出稳定且仅内容变化才替换文件；校验失败保留上次可用资产并返回非零退出码。

## 自动构建

- Unity 开着：编辑器自动检测 `GameData/Excel` 下保存的 Excel，稳定两次采样后启动同一个离线脚本，成功后刷新资源。菜单 `Tools > Game Tables > Build Excel Tables` 可手动重试。构建 Player 前也强制验证表，失败会阻止构建。
- Unity 关闭：运行 `Tools/Tables/watch.bat`，保持窗口运行。保存 Excel 后自动打表，Ctrl+C 停止。也可运行 `python Tools/Tables/build_tables.py --watch`。监听本身不需要 Unity，也不需要联网。
- 监听不是 Windows 常驻服务；关闭监听窗口且未打开 Unity 时不会自动运行。
- Play Mode 中不热替换已缓存的字符串表；停止并再次 Play 后读取新资产。

## Unity 接入

已安装官方 `com.unity.localization` 1.5.3（团结仓库支持版本）。`ExcelLocalization` 在场景加载前读取二进制并通过官方 `ITableProvider` 创建真正的 `StringTable`，使用集合名 `Game`。语言列表也来自二进制，无需额外 Addressables 打包、CSV 导入或手动配置 Locale 资产。

```csharp
using SpireLocalization;
ExcelLocalization.Select("zh-Hans");
string title = ExcelLocalization.Text("ui.title");
string cardName = ExcelLocalization.Text("card.w_strike.name", "Iron Strike");
```

也支持 `new UnityEngine.Localization.LocalizedString("Game", "ui.title")` 以及插件的 `StringChanged` 事件。引用本项目封装的 asmdef 加入 `Spire.Localization`；直接使用插件 API 时加入 `Unity.Localization`。

示例表含 32 条中英文文本，已接入主菜单、关键战斗按钮、结局和 16 张卡牌名称，右上角语言按钮可切换并保存选择。其余战斗统计、房间描述、联网诊断仍是原型英文，可按相同 Key 流程逐步迁移。原型 UI 使用系统字体候选列表；移动端正式发布时应引入具有目标语言字形且授权可随包分发的字体，不能保证所有设备的系统字体覆盖。

## 验证

```powershell
python -m unittest discover -s Tools/Tables -p 'test_*.py' -v
```

Unity 测试包括二进制读取、损坏数据拒绝、真实 StringTable Provider、中英文切换以及现有游戏流程回归。仅提交通过验证的源表、构建器、生成二进制和 Unity `.meta`；不提交缓存、监听日志或 Python 环境。
