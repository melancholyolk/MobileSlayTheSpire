# Tailscale：仅访问 Gitea / LFS

用户选择仅将 Gitea 接入 Tailscale。此配置通过官方 Tailscale 容器的用户态网络和 TCP 转发，
把 Tailscale 节点的 3000 端口转发到 NAS `192.168.1.7:3000`。
Gitea 网页和 LFS API 共用此端口，仍需要 Gitea 账号认证。

- Docker 项目：`tailscale`；容器：`tailscale-lfs`。
- 官方镜像：`ghcr.io/tailscale/tailscale:v1.104.7`。
- 节点请求名称：`ugreen-lfs`。
- NAS 项目目录：`共享文件夹/docker/tailscale`。
- 登录状态：`./state`，映射到 `/var/lib/tailscale`。
- 自动重启：`unless-stopped`，重启后使用已持久化的身份。
- 未使用主机网络、NET_ADMIN、TUN、出口节点、子网路由或公网 Funnel。
- 本配置不向 Tailscale 提供 NAS 管理页面、SMB 或其他 NAS 服务。

首次启动后，从容器日志打开 Tailscale 登录链接，在自己的账号中授权设备。
外网访问设备也需要安装 Tailscale 并加入获准访问该节点的网络。
绑定前不能确定节点的最终 IP 和 MagicDNS 名称，因此不能提前写入外网 LFS 地址。

登录状态包含设备密钥，不要提交 `state`、认证密钥或登录链接到 Git。
Tailscale 的设备密钥可能过期；若设备离线，先在管理控制台检查设备状态和有效期。

## 后续验证

绑定后检查容器的 `tailscale status` 和 `tailscale serve status`。
通过 Tailscale 访问 Gitea，并验证 LFS batch 返回的上传/下载 URL 也能从外网访问。
仅更换 `.lfsconfig` 而保留 Gitea 的内网 ROOT_URL，可能导致 LFS 实际传输仍指向内网地址。
服务端 ROOT_URL 和项目端点需要协调配置并实际测试。
