# 项目技能

处理本项目 Git LFS、NAS Gitea、LFS 上传下载或 Tailscale 远程接入相关任务时，先阅读并使用 [project-lfs](.agents/skills/project-lfs/SKILL.md)。该技能链接到实际配置、验证脚本和未完成部署事项；执行前核对当前状态。

普通游戏功能开发无需加载该技能。技能不扩大当前用户请求或执行授权的范围。

卡牌美术设计、AI 出图、批量候选筛选和美术资源接入时，先读 [local-art-pipeline](.agents/skills/local-art-pipeline/SKILL.md)。该技能关联 `D:/AiDraw` 的本地 `card-game-art`、`comfyui-draw` 技能；美术生产环境留在外部目录，本游戏 Git 仅管理关联说明，不纳入管线、模型、环境或候选图片。

# Unity 编辑器操作

本项目已安装 Unity CLI Loop，使用 `.agents/skills/uloop-*` 中的对应技能操作当前编辑器。

- 在项目根目录运行 `uloop status`，确认 `Ready` 后再执行编辑器命令；通过 `uloop list` 查看实际可用命令。
- 当前 Windows 安装位置为 `C:/Users/Yuki/AppData/Local/Programs/uloop/bin/uloop.exe`。旧终端尚未刷新 PATH 时，PowerShell 使用 `& 'C:/Users/Yuki/AppData/Local/Programs/uloop/bin/uloop.exe' <命令>`。
- 项目使用团结引擎，进程名为 `Tuanjie.exe`。已验证场景层级读取、动态 C# 执行和 Console 日志读取可用。
- 已知兼容性限制：`uloop compile`（包括 `--no-wait-for-domain-reload`）在程序集重载时可能误报 `UNITY_NOT_RUNNING`。不要因此重启编辑器或立即重复编译；先确认 `Tuanjie.exe` 仍在运行，等待重载后重新查询 `uloop status` 和 `uloop get-logs --log-type Error`。编译命令返回失败不能视为编译验证通过。
- `uloop launch` 的 Unity Hub 自动发现尚未验证支持团结引擎；已有编辑器实例时直接连接，不另开实例。
- `.uloop/` 与 `UserSettings/` 为本机缓存和设置，不提交。

# 修改后的交付

本项目修改完成后默认执行 [commit-and-push](.agents/skills/commit-and-push/SKILL.md)：验证本次改动、仅提交当前任务范围并推送 Git。用户明确要求暂不提交或暂不推送时除外。
