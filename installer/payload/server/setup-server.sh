#!/data/data/com.termux/files/usr/bin/bash
# Server de Bigode - instalador do servidor dentro do Termux. IDEMPOTENTE: pode rodar de novo sem estragar nada
# (nao troca senha, nao apaga tema, nao mexe em bots que ja existem).
# Uso: bash setup-server.sh [--tailscale] [--bot] [--tema] [--nome "nome do servidor"]
export DEBIAN_FRONTEND=noninteractive
APT="-y -o Dpkg::Options::=--force-confold -o Dpkg::Options::=--force-confdef"
PAY="$HOME/server-payload"
NOME="server de bigode"
OPT_TS=0; OPT_BOT=0; OPT_TEMA=0
while [ $# -gt 0 ]; do
  case "$1" in --tailscale) OPT_TS=1;; --bot) OPT_BOT=1;; --tema) OPT_TEMA=1;; --nome) shift; NOME="$1";; esac
  shift
done
log()  { echo "[$(date +%T)] $*"; }
fail() { echo "SETUP-FALHOU: $*"; exit 1; }
DB="$HOME/.config/filebrowser-server.db"
parar_fb() { tmux kill-session -t filebrowser 2>/dev/null; sleep 1; pkill -x filebrowser 2>/dev/null; sleep 1; }

log "ETAPA 1/8 escolhendo o mirror mais rapido"
best=""; bestspeed=0
for m in https://packages-cf.termux.dev/apt/termux-main https://packages.termux.dev/apt/termux-main https://mirror.mwt.me/termux/main; do
  sp=$(curl -s -m 12 -o /dev/null -w '%{speed_download}' "$m/dists/stable/main/binary-aarch64/Packages" 2>/dev/null); sp=${sp%.*}; sp=${sp:-0}
  log "  $m -> ${sp} B/s"
  if [ "$sp" -gt "$bestspeed" ]; then bestspeed=$sp; best=$m; fi
done
if [ -n "$best" ] && [ "$bestspeed" -gt 0 ]; then
  cp -n "$PREFIX/etc/apt/sources.list" "$PREFIX/etc/apt/sources.list.bak-original" 2>/dev/null
  echo "deb $best stable main" > "$PREFIX/etc/apt/sources.list"
  log "  escolhido: $best"
fi

log "ETAPA 2/8 atualizando o sistema do Termux"
apt-get update 2>&1 | tail -2
apt-get $APT upgrade 2>&1 | tail -2
pkill sshd 2>/dev/null; sleep 1; sshd   # binarios novos do openssh

log "ETAPA 3/8 instalando pacotes (node, python, git, tmux...)"
LOGD="${TMPDIR:-$PREFIX/tmp}"; mkdir -p "$LOGD"
for p in nodejs python git tmux curl wget rsync nano htop termux-services procps iproute2 termux-api openssl; do
  if apt-get $APT install $p > "$LOGD/apt-$p.log" 2>&1; then log "  ok $p"; else log "  FALHOU $p"; fi
done

log "ETAPA 4/8 instalando o File Browser (gerenciador de arquivos pelo navegador)"
if ! command -v filebrowser > /dev/null; then
  case "$(uname -m)" in aarch64|arm64) A=arm64;; armv7l|armv8l) A=armv7;; x86_64) A=amd64;; *) A=arm64;; esac
  URL=$(curl -fsSL https://api.github.com/repos/filebrowser/filebrowser/releases/latest | grep -o "https://[^\"]*linux-$A-filebrowser.tar.gz" | head -1)
  [ -n "$URL" ] || fail "nao encontrei o File Browser para $A"
  mkdir -p "$LOGD/fb"
  curl -fsSL -o "$LOGD/fb/fb.tgz" "$URL" || fail "download do File Browser falhou"
  tar -xzf "$LOGD/fb/fb.tgz" -C "$LOGD/fb" filebrowser || fail "nao consegui extrair o File Browser"
  install -m 755 "$LOGD/fb/filebrowser" "$PREFIX/bin/filebrowser" || fail "nao consegui instalar o File Browser"
fi
log "  $(filebrowser version 2>&1 | head -1)"

log "ETAPA 5/8 pastas, usuario e senha"
mkdir -p "$HOME/.config" "$HOME/server/files" "$HOME/server/isos" "$HOME/server/bots" "$HOME/server/backups"
PASS=""
if [ ! -f "$DB" ]; then
  filebrowser -d "$DB" config init > /dev/null 2>&1
  filebrowser -d "$DB" config set -a 0.0.0.0 -p 8080 -r "$HOME/server" > /dev/null 2>&1
  PASS=$(head -c 96 /dev/urandom | base64 | tr -dc 'A-Za-z0-9' | head -c 16)
  filebrowser -d "$DB" users add admin "$PASS" --perm.admin > /dev/null 2>&1 || fail "nao consegui criar o usuario admin"
  echo "$PASS" > "$HOME/.fb-admin-pass"; chmod 600 "$HOME/.fb-admin-pass"
  log "  usuario admin criado (senha aleatoria)"
else
  log "  banco ja existe: mantendo usuarios e senha"
  [ -f "$HOME/.fb-admin-pass" ] && PASS=$(cat "$HOME/.fb-admin-pass")
fi

log "ETAPA 6/8 inicio automatico (Termux:Boot) e scripts"
cp "$PAY/start-server.sh" "$HOME/server/start-server.sh"; chmod +x "$HOME/server/start-server.sh"
mkdir -p "$HOME/.termux/boot" "$HOME/.termux/boot-backup"
[ -f "$HOME/.termux/boot/start-services.sh" ] && mv "$HOME/.termux/boot/start-services.sh" "$HOME/.termux/boot-backup/start-services.sh.old"
printf '#!/data/data/com.termux/files/usr/bin/bash\nexec "$HOME/server/start-server.sh"\n' > "$HOME/.termux/boot/start-server"
chmod +x "$HOME/.termux/boot/start-server"
cp "$PAY/trocar-senha-filebrowser.sh" "$HOME/server/trocar-senha-filebrowser.sh" 2>/dev/null

log "ETAPA 7/8 extras escolhidos (tema, bot, Tailscale)"
if [ "$OPT_TEMA" = 1 ]; then
  if [ ! -f "$HOME/.config/filebrowser-branding/custom.css" ]; then
    mkdir -p "$HOME/.config/filebrowser-branding"
    cp -r "$PAY/theme/." "$HOME/.config/filebrowser-branding/"
    parar_fb
    filebrowser -d "$DB" config set --branding.name "$NOME" --branding.files "$HOME/.config/filebrowser-branding" \
      --branding.theme dark --branding.color "#5cc8ff" --branding.disableExternal > /dev/null 2>&1
    log "  tema escuro aplicado"
  else log "  tema ja existe: mantido"; fi
fi
if [ "$OPT_BOT" = 1 ]; then
  if [ ! -d "$HOME/server/bots/discord-bot" ]; then
    cp -r "$PAY/discord-bot" "$HOME/server/bots/"
    ( cd "$HOME/server/bots/discord-bot" && chmod +x start.sh testar-bot.sh && cp -n .env.example .env && chmod 600 .env \
      && npm install --omit=dev --no-audit --no-fund 2>&1 | tail -2 )
    log "  bot de Discord instalado (falta so o token no .env)"
  else log "  bot ja existe: mantido"; fi
fi
TSURL=""
if [ "$OPT_TS" = 1 ]; then
  if [ ! -x "$HOME/go/bin/tailscaled" ]; then
    log "  compilando o Tailscale (leva alguns minutos)"
    bash "$PAY/compilar-tailscale.sh" 2>&1 | tail -4
  fi
fi

log "ETAPA 8/8 iniciando os servicos"
"$HOME/server/start-server.sh"
sleep 4
if [ "$OPT_TS" = 1 ] && [ -x "$HOME/go/bin/tailscale" ]; then
  S="$HOME/.tailscale/tailscaled.sock"
  if ! "$HOME/go/bin/tailscale" --socket="$S" ip -4 > /dev/null 2>&1; then
    TSURL=$(timeout 25 "$HOME/go/bin/tailscale" --socket="$S" up --hostname="$(echo "$NOME" | tr ' ' '-')" 2>&1 | grep -o 'https://[^ ]*' | head -1)
  fi
fi
IP=$(ip -f inet addr show wlan0 2>/dev/null | awk '/inet /{print $2}' | cut -d/ -f1)
echo "SETUP-OK"
echo "ADMIN-USER: admin"
echo "ADMIN-PASS: ${PASS:-(ja existente)}"
echo "SERVER-IP: $IP"
[ -n "$TSURL" ] && echo "TAILSCALE-LOGIN: $TSURL"
exit 0
