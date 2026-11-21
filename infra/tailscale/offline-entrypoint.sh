#!/bin/sh
set -eu
mkdir -p /var/lib/tailscale /var/run/tailscale
tailscaled --tun=userspace-networking --state=/var/lib/tailscale/tailscaled.state &
daemon_pid=$!
trap 'kill "$daemon_pid" 2>/dev/null || true; wait "$daemon_pid" || true' EXIT
trap 'exit 0' TERM INT
until [ -S /var/run/tailscale/tailscaled.sock ]; do
  kill -0 "$daemon_pid"
  sleep 1
done
tailscale up --hostname=ugreen-lfs --accept-dns=false
tailscale serve --bg --tcp=3000 tcp://192.168.1.7:3000
wait "$daemon_pid"
