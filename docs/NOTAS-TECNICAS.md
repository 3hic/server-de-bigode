# Notas técnicas (lições aprendidas)

Coisas que deram trabalho ao montar o servidor num Android 11 e que o instalador já leva em conta.

## Termux
- **Não existe `/tmp`**: use `$TMPDIR` (`$PREFIX/tmp`). Redirecionar para `/tmp/...` falha em silêncio.
- Depois de um `apt upgrade` que atualiza o **openssh**, reinicie o `sshd`; senão novos logins falham com `kex_exchange_identification`.
- Para o Termux ler `/sdcard`, é preciso `pm grant ... READ_EXTERNAL_STORAGE` **e** `cmd appops set ... READ_EXTERNAL_STORAGE allow`. A pasta `/sdcard/Android/data/com.termux/files/` é sempre legível por ele e pelo adb.
- O `adb` não consegue digitar no Termux por API; o instalador usa `input text` + `input keyevent 66` e lê a saída por arquivo.
- O Termux:Boot só registra o receptor depois de ser **aberto uma vez**; o instalador faz isso.

## Android
- O binário oficial do **Tailscale** (Go, linux/arm64) morre com `SIGSYS: bad system call` (seccomp bloqueia `faccessat2`). Solução: compilar dentro do Termux com o Go dele, com `-tags "ts_omit_systray ts_omit_ssh ts_omit_webclient ts_omit_desktop_sessions ts_omit_clientupdate"`, e rodar em modo `--tun=userspace-networking`.
- O File Browser oficial (arm64) roda normalmente.
- `adb shell svc bluetooth disable` falha por falta de `BLUETOOTH_ADMIN` para o shell.
- A depuração USB e a autorização do PC precisam ser dadas **na tela do celular**; não há como contornar pelo PC (é uma trava de segurança do Android).

## File Browser
- Só um processo abre o banco (BoltDB): pare o serviço antes de usar `filebrowser config set` / `users update`.
- Senha mínima padrão: 12 caracteres (`config set --minimumPasswordLength N` muda).
- Tema: pasta `--branding.files` com `custom.css` e `img/`; o navegador guarda logos em cache, então troque o **nome do arquivo** ao trocar uma imagem.

## Windows
- O WebDAV nativo limita arquivos a 4 GB; para ISOs use SFTP (`rclone mount` + WinFsp).
- SSHFS‑Win 3.5 falha ao criar pastas/arquivos contra o OpenSSH novo do Termux; o `rclone mount` funcionou.
