#!/data/data/com.termux/files/usr/bin/bash
# Troca a senha do usuario "admin" do File Browser. Uso: bash trocar-senha-filebrowser.sh NOVA_SENHA
# O banco so abre com um processo por vez, entao o servico e parado e religado.
DB="$HOME/.config/filebrowser-server.db"
NOVA="$1"
[ -n "$NOVA" ] || { echo "faltou a senha"; exit 1; }
tmux kill-session -t filebrowser 2>/dev/null
sleep 1; pkill -x filebrowser 2>/dev/null; sleep 1
echo "--- minimo de senha nas configuracoes (antes):"
filebrowser -d "$DB" config cat 2>&1 | grep -i 'Minimum Password' | head -2
if [ "${#NOVA}" -lt 12 ]; then
  filebrowser -d "$DB" config set --minimumPasswordLength "${#NOVA}" > /dev/null 2>&1
  echo "minimo ajustado para ${#NOVA}"
fi
echo "--- trocando a senha de admin"
filebrowser -d "$DB" users update admin --password "$NOVA" 2>&1 | tail -4
echo "codigo: ${PIPESTATUS[0]}"
"$HOME/server/start-server.sh"
sleep 3
echo "--- no ar:"; pgrep -a filebrowser | cut -c1-70
