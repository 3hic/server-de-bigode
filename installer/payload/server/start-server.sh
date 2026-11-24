#!/data/data/com.termux/files/usr/bin/bash
# Server de Bigode - sobe SSH, File Browser, Tailscale (se instalado) e os bots.
# Roda no boot (Termux:Boot) e pode ser chamado a mao. Cada bot = pasta em ~/server/bots/* com start.sh EXECUTAVEL.
LOG="$HOME/server/start.log"
echo "$(date '+%F %T') start-server.sh iniciado" >> "$LOG"
termux-wake-lock
pgrep -x sshd > /dev/null || sshd
# File Browser: reinicia sozinho se cair
tmux has-session -t filebrowser 2>/dev/null || tmux new-session -d -s filebrowser \
  "while true; do filebrowser -d $HOME/.config/filebrowser-server.db; sleep 3; done"
# vigia: garante o sshd a cada 30 s
tmux has-session -t vigia 2>/dev/null || tmux new-session -d -s vigia \
  "while true; do pgrep -x sshd > /dev/null || sshd; sleep 30; done"
# Tailscale (acesso de qualquer lugar, gratis), so se foi instalado
if [ -x "$HOME/go/bin/tailscaled" ]; then
  mkdir -p "$HOME/.tailscale"
  tmux has-session -t tailscale 2>/dev/null || tmux new-session -d -s tailscale \
    "while true; do $HOME/go/bin/tailscaled --tun=userspace-networking --state=$HOME/.tailscale/tailscaled.state --socket=$HOME/.tailscale/tailscaled.sock; sleep 5; done"
fi
# bots: cada um reinicia sozinho se cair
for d in "$HOME"/server/bots/*/; do
  [ -x "${d}start.sh" ] || continue
  n="bot-$(basename "$d")"
  tmux has-session -t "$n" 2>/dev/null || tmux new-session -d -s "$n" -c "$d" "while true; do ./start.sh; sleep 5; done"
done
echo "$(date '+%F %T') servicos no ar" >> "$LOG"
