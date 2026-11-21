# Tailscale：仅访问 Gitea / LFS

用户选择仅将 Gitea 接入 Tailscale。用户态网络将节点 TCP 3000 转发到 NAS `192.168.1.7:3000`，Gitea 网页和 LFS API 仍要求 Gitea 认证。

## 当前状态（2026-11-21）

已导入离线镜像并启动 `tailscale` 项目、`tailscale-lfs` 容器。启动日志已成功连接 Tailscale 控制服务并显示账号绑定入口。当前等待用户绑定；尚未验证节点 IP、转发或外网 LFS。不要把“运行中”当成已接入。

原 GHCR 拉取长期无进展，Docker Hub 返回 `registry-1.docker.io` 请求超时。因此当前使用本地打包的官方二进制镜像，不是未经修改的官方容器镜像。

- 实际配置：`compose.yaml`，镜像 `local/tailscale-lfs:1.104.7-arm64`，`pull_policy: never`。
- 官方容器备用配置：`compose.official.yaml`；仅在镜像源可达时考虑切换，切换前核对并保留 `state`。
- 请求节点名：`ugreen-lfs`；项目目录：NAS `共享文件夹/docker/tailscale`。
- 身份目录：`./state` → `/var/lib/tailscale`；重启策略：`unless-stopped`。
- 不使用主机网络、NET_ADMIN、TUN、出口节点、子网路由或公网 Funnel。
- 仅配置 TCP 3000，不发布 NAS 管理页面、SMB 或其他服务。

`offline-entrypoint.sh` 启动用户态 tailscaled，再运行 `tailscale up`。首次登录未完成时停在此处是正常等待；绑定后才执行 `tailscale serve --bg --tcp=3000 tcp://192.168.1.7:3000`。日志中的绑定链接、`state` 和 auth key 不提交 Git。设备密钥过期时先检查 Tailscale 控制台。

## 离线镜像来源和重建

`build-offline-image.py` 使用 Python 标准库，校验输入后生成 Docker 可导入的 tar.gz，不需要电脑安装 Docker。当前固定为 Linux ARM64、Tailscale 1.104.7；升级时必须同时核实版本、架构、摘要和启动参数。

1. 官方静态包：`https://pkgs.tailscale.com/stable/tailscale_1.104.7_arm64.tgz`，SHA-256 `d6bed2720310b328b861f943d7b4016c5358a9bad60575eb51e8143b79068484`（对照官方 `.sha256` 校验）。
2. 基础层来自 `ghcr.io/tailscale/tailscale:v1.104.7` 的 ARM64 manifest `sha256:8934bed5f68e80c5f9c881f62c65909223ee660432946c10ad212d958fc2c2bb`。从官方 GHCR OCI blob 接口下载下表两个压缩层，以摘要作为文件名放入缓存目录；需要时使用 GHCR 返回的匿名 pull challenge，不保存令牌。不能使用未完成的下载。

| 压缩层 SHA-256 / 文件名 | 字节数 |
| --- | ---: |
| `fea5b91588ca539ff8a89d7d5df6c6d65d6ab7bd89145ba57f32c302cf02ffbe` | 4127385 |
| `f579224f18ef2647c0271f4955565ee7abb910e50c130ee7eaec41d317b7abce` | 2165097 |

脚本也校验解压后的 diff ID 及二进制 ELF ARM64 标识，追加当前 `offline-entrypoint.sh`，保留基础层的 CA 证书和网络工具。

```powershell
python infra/tailscale/build-offline-image.py --layers-dir <已下载基础层目录> --static-tgz <官方静态包路径>
```

输出位于基础层目录下：`tailscale-lfs-offline-1.104.7-arm64.tar.gz`。压缩包元数据可能随构建时间变化，不能要求每次 archive 摘要相同；核对输入摘要与输出镜像 config ID。此次部署 config ID 为 `bb40ba5bfe8cb714e8fb69b83578c05cbdb9c88f8c71561779bf714ee39b42a7`，上传包 SHA-256 为 `d4f7af050d6c94da35644e8adc901dd599cccdfc7eee9e3d7527a8a6401fb3ee`。

将输出上传至 NAS 项目目录，Docker → 镜像 → 本地镜像 → 添加镜像 → 从 NAS 导入。确认 `local/tailscale-lfs:1.104.7-arm64` 存在后，在原 `tailscale` 项目应用 `compose.yaml`，重新部署时不勾选“拉取最新镜像”。保留 `state`。不要再次创建同名项目或清空持久化目录。

## 绑定和后续验证

1. 从当前容器日志打开登录入口，由用户完成登录和设备授权。链接过期时从现场获取，不能复用文档中的旧链接。
2. 在容器终端执行 `tailscale status`、`tailscale ip -4`、`tailscale serve status`，确认实际地址和仅 TCP 3000 的转发。若绑定完成而未转发，先检查启动日志，不扩大容器权限。
3. 访问端也需安装并登录获准的 Tailscale 网络。当前电脑尚未验证接入，局域网访问成功不能作为外网验证。
4. 验证目标设备可访问 Gitea，再协调修改 Gitea `ROOT_URL` 和 `.lfsconfig`，保留原内网配置作回退。仅修改客户端可能让 LFS batch 响应继续返回 `192.168.1.7` 的对象 URL。
5. 检查实际上传和下载地址，再运行 `infra/gitea-lfs/Test-NasLfs.ps1` 完成真实上传、无缓存下载、SHA-256 和 fsck 校验，才能记录外网接入完成。

当前 `.lfsconfig` 和 Gitea ROOT_URL 仍保留原内网端点，待上述验证后再切换。备份 Tailscale `state` 时按凭据保护，不将其放入 Git 或共享日志。
