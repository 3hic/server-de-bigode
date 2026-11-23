#!/data/data/com.termux/files/usr/bin/bash
# Server de Bigode - primeiro passo dentro do Termux: instala o SSH e libera a chave do PC.
# Rodado pelo instalador (digitado no Termux via adb). Log: /sdcard/Android/data/com.termux/files/bootstrap.log
D=/sdcard/Android/data/com.termux/files
exec > "$D/bootstrap.log" 2>&1
export DEBIAN_FRONTEND=noninteractive
termux-wake-lock 2>/dev/null
echo "== atualizando indices"
yes | pkg update -y
apt-get -y -o Dpkg::Options::=--force-confold upgrade
echo "== instalando openssh e curl"
apt-get -y -o Dpkg::Options::=--force-confold install openssh curl
mkdir -p "$HOME/.ssh"; chmod 700 "$HOME/.ssh"
if [ -f "$D/pc_key.pub" ]; then
  tr -d '\r' < "$D/pc_key.pub" >> "$HOME/.ssh/authorized_keys"
  sort -u "$HOME/.ssh/authorized_keys" -o "$HOME/.ssh/authorized_keys"
  chmod 600 "$HOME/.ssh/authorized_keys"
fi
pkill sshd 2>/dev/null; sleep 1; sshd
sleep 1
echo "BOOTSTRAP-OK $(whoami)"
