---
name: project-lfs
description: 维护 MobileSlayTheSpire 项目的 Git LFS、绿联 NAS Gitea 对象存储和仅供 Gitea/LFS 使用的 Tailscale 接入。用于调整资源跟踪规则、部署或升级 LFS 服务、排查上传下载、迁移端点及验证恢复；普通游戏代码修改无需使用。
---

# 项目 LFS 运维

## 先确定实际状态

从当前 checkout 的仓库根目录操作，不依赖原电脑的绝对路径。技能目录位于 `.agents/skills/project-lfs`，根目录为其上三级。
先读取本技能及相关配置，检查 `git status --short`，保护已有改动。本文是运维入口，配置文件和现场状态优先于记录中的地址、版本或完成标记。

| 任务 | 应读取的项目文件（相对仓库根目录） |
| --- | --- |
| 客户端接入、资源规则 | `.lfsconfig`、`.gitattributes`、`ProjectSettings/EditorSettings.asset` |
| Gitea 部署、认证、备份恢复 | `infra/gitea-lfs/README.md`、`infra/gitea-lfs/compose.yaml` |
| 上传下载验证 | `infra/gitea-lfs/Test-NasLfs.ps1` |
| Tailscale 或外网端点 | `infra/tailscale/README.md`、`infra/tailscale/compose.yaml`，以及上述 Gitea 配置 |

上述文件在当前项目中复用，不复制脚本和 Compose 到技能目录，以免出现多个配置源。

用以下只读命令核实有效配置；`git lfs env` 可能包含环境信息，分享输出前去掉凭据及无关环境变量。

```powershell
git status --short
git remote -v
git config --file .lfsconfig --get lfs.url
git config --show-origin --get-regexp '^(lfs\.(url|pushurl)|remote\..*\.lfs(url|pushurl))$'
git lfs env
git lfs ls-files
```

没有匹配配置时 `git config --get-regexp` 返回 1 属于正常情况。留意本地、全局或 remote 的 LFS URL 覆盖，不能只看 `.lfsconfig` 判断实际端点。

## 架构与已知状态

- Git 代码仓库保留在 GitHub；NAS 上的私有 Gitea 仓库 `liangy/MobileSlayTheSpire-lfs` 只承载此项目的 LFS 对象。不要把 `origin` 改成 NAS 来解决 LFS 问题。
- 已配置内网端点：`http://192.168.1.7:3000/liangy/MobileSlayTheSpire-lfs.git/info/lfs`。这是 2026-11-21 读取到的值，后续以现场为准。
- Gitea Docker 项目及容器为 `gitea-lfs`；数据持久化到 NAS `共享文件夹/docker/gitea-lfs/data`。容器中的 SQLite、应用配置和 LFS 对象必须一起备份；详见 Gitea README。
- 历史记录表明 2026-11-07 内网上传、无缓存下载及 SHA-256 校验通过。历史成功不能代替本次变更后的验证。
- 2026-11-21 Tailscale 已提交部署，最近观察仍在拉取官方镜像；未确认容器启动、账号绑定、节点 IP 或外网传输。继续任务应先检查现有 `tailscale` 项目，避免重复创建。此状态须随实际验证更新。
- 用户选择的 Tailscale 范围是**仅 Gitea/LFS**。现有设计为用户态网络加 TCP 3000 转发到 `192.168.1.7:3000`，容器名 `tailscale-lfs`、请求节点名 `ugreen-lfs`。不擅自扩展为主机网络、NET_ADMIN、TUN、子网路由、出口节点或公网 Funnel。
- NAS 管理入口是 `https://dh4300plus-cb1a-htk3.cn34.ug.link/desktop/`；它不是 LFS API。浏览器登录可能过期，按当前工具的认证交接规则处理。

## 资源跟踪与客户端

`.gitattributes` 已包含大小写兼容的图片、模型、音视频、字体和原生库规则。Unity 场景、Prefab、文本 `.asset` 与 `.meta` 保留普通 Git 文本，当前 `m_SerializationMode: 2` 对应 Force Text。

调整规则前用 `git check-attr filter diff merge -- <具体资源路径>` 检查实际匹配。不要使用 `*` 把 Unity 文本及元数据一并转成 LFS。新规则不会自动迁移历史对象；确需转换已跟踪文件时，在明确的文件范围内实施并检查 staged diff。

新 checkout 使用 `git lfs install --local` 后再 `git lfs pull`。现有自定义 Git hook 应先检查并保留。Gitea 认证与 GitHub 认证分开；已知 Gitea 用户为 `liangy`，凭据由 Git Credential Manager 或用户登录处理，不写入 URL、技能或 Compose。

## 继续 Tailscale 和切换端点

1. 检查 NAS Docker 项目、容器及日志。镜像下载中不代表部署成功；遇到失败先查原因，重试前确认现有项目状态。
2. 容器成功启动后，通过日志中的登录入口让用户绑定账号。不要持久化或提交登录链接、auth key、`state` 或其他设备密钥。
3. 在 NAS 的容器终端核实 `tailscale status`、`tailscale ip -4`、`tailscale serve status`。确认仅发布预期的 3000 端口，并记录实际 IP/名称；不要按请求 hostname 猜最终 DNS 名称。
4. 访问端也需加入获准的 Tailscale 网络。当前电脑未接入时，不能把局域网测试报告成外网验证；按当前授权处理客户端安装与登录。
5. 在切换前记录可回退的 `.lfsconfig`、Gitea Compose 及实际配置，并确认目标设备可达。协调更新 Gitea `ROOT_URL`（必要时 `DOMAIN`）与项目 `.lfsconfig`，检查本地覆盖。
6. 检查 LFS batch 响应中的上传、下载地址是否仍指向 `192.168.1.7`。只改变客户端端点可能让握手成功而实际对象传输失败。调试输出不得保留授权头或令牌。
7. 完成真实上传及无缓存下载验证后，才将状态写为接入成功。失败时定位是容器、Tailscale、认证还是 Gitea 地址问题；必要时恢复此次修改的配置，保留持久化数据和原可用端点。

HTTP 经 Tailscale 传输受到隧道保护，仍保留 Gitea 私有仓库认证。用户态转发不等同于公网开放；不要为此默认配置公网端口映射或关闭认证。

## 验证、备份与迭代

仅编写文档或技能时做结构及配置一致性检查，不为文档变更运行会上传对象的测试。涉及端点、服务或传输行为的变更，在授权范围内使用已有脚本：

```powershell
./infra/gitea-lfs/Test-NasLfs.ps1
```

脚本从仓库 `.lfsconfig` 读取端点，在系统临时目录建立隔离仓库，上传默认 1 MiB 随机对象，再从无缓存的新克隆下载，检查 SHA-256 和 `git lfs fsck`。它不推送 Git 提交，但每次会在 NAS 留下测试对象和本机临时记录。检查输出中的 endpoint、status、sha256 和验证时间；该脚本不保证原项目所有历史对象齐全，也不验证项目本地 URL 覆盖。

排错先区分网络不可达、401/403 凭据或权限、404 路径以及对象缺失。用 `git lfs logs last` 辅助定位，分享前清除秘密信息。对象缺失先确认原端点和缓存；迁移前从原服务完整取回所需历史对象并备份，再上传目标服务。不要默认执行历史重写、强推、prune 或清空数据目录。

升级、恢复或重建前按 Gitea README 做一致性备份，保留 SQLite、app.ini、仓库和 LFS 对象。重建容器时保留数据挂载；Tailscale 身份保存在独立 `state` 目录。技能不授予删除数据、扩展访问范围或对外发布的额外权限。

每次迭代同步维护实际配置副本与对应 README：记录已验证的地址、版本、验证结果和仍未完成的步骤。架构或用户范围变化时更新本技能；不要把计划写成已完成。交付时说明修改、验证与剩余阻塞，并检查 diff 中没有凭据、app.ini、数据库或状态目录。
