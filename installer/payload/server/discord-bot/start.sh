#!/data/data/com.termux/files/usr/bin/bash
# Roda o bot. Sem token no .env ele so espera (nao fica caindo em loop). O start-server.sh reinicia se cair.
cd "$(dirname "$0")" || exit 1
mkdir -p logs
LOG=logs/bot.log
# mantem o log pequeno (troca quando passa de ~5 MB)
if [ -f "$LOG" ] && [ "$(wc -c < "$LOG")" -gt 5000000 ]; then mv -f "$LOG" "$LOG.old"; fi
if ! grep -qE '^DISCORD_TOKEN=.+' .env 2>/dev/null; then
  echo "$(date '+%F %T') sem token no .env: preencha DISCORD_TOKEN (veja LEIA-ME.txt). Verifico de novo em 60 s."
  sleep 60
  exit 0
fi
[ -d node_modules ] || npm install --omit=dev --no-audit --no-fund
exec node index.js >> "$LOG" 2>&1
