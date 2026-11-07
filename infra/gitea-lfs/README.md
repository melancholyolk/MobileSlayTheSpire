# NAS Git LFS

本项目的 Git 代码仓库在 GitHub；NAS 上的 Gitea 用于存储 Git LFS 对象。

## 已部署的服务

- NAS：`192.168.1.7`，绿联 UGOS Pro。
- Docker 项目 / 容器：`gitea-lfs`。
- 镜像：`docker.gitea.com/gitea:28.3.0`，使用 Gitea 官方仓库。
- Web 地址：`http://192.168.1.7:3000/`。
- NAS 配置：`共享文件夹/docker/gitea-lfs/docker-compose.yaml`。
- `./data` 映射到容器 `/data`，数据库、配置、仓库和 LFS 对象均持久化。
- LFS 对象目录：容器内 `/data/git/lfs`。
- SQLite 数据库：`/data/gitea/gitea.db`。
- 应用配置：`/data/gitea/conf/app.ini`。
- 禁止自行注册，网页要求登录，仓库强制私有，SSH 和 Actions 禁用。

`compose.yaml` 是 NAS 已部署配置的副本。首次启动需在 Web 页面完成初始化并创建管理员。
不要把数据库、密码、访问令牌、`app.ini` 或 `data` 目录提交到 Git。

## 项目接入

已完成 Gitea 初始化并创建私有仓库 `liangy/MobileSlayTheSpire-lfs`。
项目根目录 `.lfsconfig` 已配置以下端点：

```text
http://192.168.1.7:3000/liangy/MobileSlayTheSpire-lfs.git/info/lfs
```

新电脑克隆项目后运行 `git lfs install --local`，然后运行 `git lfs pull`。
首次认证使用 NAS Gitea 账号（当前拥有者为 `liangy`），不是 GitHub 账号。
本机已通过 Git Credential Manager 完成认证；密码不写入项目文件或 URL。
可运行 `git lfs env` 核实 Endpoint。代码的 `origin` 仍为 GitHub，正常 `git push`
会先通过 LFS hook 把跟踪的二进制对象上传到 NAS，再将 Git 提交推送到 GitHub。

可使用 PowerShell 重新验证传输：

```powershell
./infra/gitea-lfs/Test-NasLfs.ps1
```

脚本在系统临时目录建立独立仓库，将 1 MiB 随机文件上传至 NAS，再使用没有 LFS 缓存的新克隆
下载并验证 SHA-256 与 `git lfs fsck`。它不推送 Git 提交或分支，不修改游戏项目资源。
每次运行会在 NAS 留下一个测试 LFS 对象，并在临时目录留下验证记录。

2026-11-07 已通过端到端验证，测试文件 SHA-256：
`b818a41d0849d7141f4280c6c2555aeee141586c1030cfc87277277c92c7e1af`。

`.gitattributes` 已跟踪纹理、模型、音视频、字体、原生库等二进制资源；Unity 场景、Prefab、文本 `.asset` 和 `.meta` 保留普通 Git 文本存储。
Unity 当前已启用 Force Text 序列化。

克隆项目后需运行 `git lfs install --local`，首次拉取资源需要 NAS 的访问权限。
如果已有历史 LFS 对象，切换端点之前先从原服务完整下载并备份，再上传到 NAS。
不要为新增跟踪规则直接重写已有 Git 历史。

## 网络和备份

当前服务仅绑定 NAS 的 IPv4 内网地址，使用 HTTP。远程电脑需要先接入同一局域网或可信 VPN；
绿联管理页面的 `ug.link` 登录地址不等于 Gitea 的 LFS API 地址。
对公网提供服务前，需要单独配置 HTTPS、身份认证和可供 Git 客户端访问的域名，并同步更新 ROOT_URL 和 `.lfsconfig`。
建议在路由器为 NAS 保留 `192.168.1.7`，避免 DHCP 地址变化导致连接失败。

备份前停止 `gitea-lfs` 容器，备份完整 `共享文件夹/docker/gitea-lfs` 目录，再启动容器。
恢复时需要同时恢复 SQLite、应用配置和 LFS 对象，不能只恢复 GitHub 代码。
升级前备份，并保持服务端和本文档中的镜像版本一致。
