#!/data/data/com.termux/files/usr/bin/bash
# Compila tailscale/tailscaled no Termux (o binario oficial e bloqueado pelo seccomp do Android).
export DEBIAN_FRONTEND=noninteractive
echo "$(date +%T) instalando golang"
apt-get -y -o Dpkg::Options::=--force-confold install golang git > /dev/null 2>&1
go version
export GOPATH="$HOME/go" GOFLAGS=-mod=mod GOTOOLCHAIN=local
echo "$(date +%T) compilando (varios minutos)..."
go install -tags "ts_omit_systray ts_omit_ssh ts_omit_webclient ts_omit_desktop_sessions ts_omit_clientupdate" -ldflags="-s -w" tailscale.com/cmd/tailscale@latest 2>&1 | tail -5
go install -tags "ts_omit_systray ts_omit_ssh ts_omit_webclient ts_omit_desktop_sessions ts_omit_clientupdate" -ldflags="-s -w" tailscale.com/cmd/tailscaled@latest 2>&1 | tail -5
ls -la "$GOPATH/bin"
echo "$(date +%T) FIM-COMPILACAO"
