# Server de Bigode

Transforme um **celular Android velho** em um servidor, com **um clique**: um instalador para Windows (com interface) que prepara o celular, instala tudo e entrega os acessos.

<p align="center"><i>escuro · tons frios · sem complicação</i></p>

## O que ele monta

- **Site de arquivos** (File Browser) para guardar e baixar arquivos e ISOs pelo navegador, com tema escuro e animações.
- **SSH** no celular, só por chave (sem senha).
- **Modelo de bot de Discord** (discord.js) pronto para rodar 24h, que reinicia sozinho se cair.
- **Início automático**: depois de ligar o celular, tudo sobe sozinho (Termux:Boot).
- **Acesso de qualquer lugar e de graça** (opcional) com o Tailscale.
- **Ver a tela do celular no PC** (scrcpy), com mouse e teclado.

## Como usar

1. No celular: ative a **Depuração USB** (Configurações → Sobre o telefone → toque 7× em *Número da versão* → Opções do desenvolvedor → Depuração USB).
2. Ligue o celular ao PC por **USB** e conecte-o ao **mesmo Wi‑Fi** do PC.
3. Abra `ServerDeBigode.exe`, marque o que quer instalar e clique em **Instalar servidor**.
4. Quando terminar, o programa mostra o endereço do site, o usuário e a **senha gerada** (também salva em `%LOCALAPPDATA%\ServerDeBigode\credenciais-servidor.txt`).

O instalador é **idempotente**: dá para rodar de novo sem estragar nada (não troca senha, não apaga tema nem bots existentes).

## Requisitos

- Windows 10/11 com o **cliente OpenSSH** (já vem no Windows).
- Celular Android 7+ com internet e a Depuração USB ligada.
- O PC baixa o `adb` (Google), os APKs do Termux (F‑Droid) e, se pedir, o scrcpy.

## Compilar o `.exe`

Não precisa instalar nada: o script usa o `csc.exe` que já vem no Windows.

```powershell
cd installer
.\build.ps1        # gera installer\dist\ServerDeBigode.exe
```

Os contatos mostrados no rodapé e na janela *Créditos* vêm do arquivo **`creditos.ini`** (ao lado do `.exe`). Edite `Nome`, `Discord` e `Gmail` e abra o programa de novo.

## Estrutura

```
installer/
  ServerDeBigode.cs     interface e logica do instalador (C#, WinForms)
  build.ps1             compila e embute o payload no .exe
  creditos.ini          nome e contatos que aparecem nos creditos
  payload/
    bootstrap-termux.sh primeiro passo dentro do Termux (SSH + chave do PC)
    server/
      setup-server.sh   instala pacotes, File Browser, extras (idempotente)
      start-server.sh   sobe SSH, site, Tailscale e bots (e no boot)
      theme/            tema escuro do File Browser (CSS)
      discord-bot/      modelo de bot de Discord
docs/
  NOTAS-TECNICAS.md     licoes aprendidas (Termux, Android, Tailscale...)
```

## Segurança

- A senha do site é **aleatória** (nada de `admin/admin`) e só aparece para você.
- O SSH aceita **só a chave do seu PC**.
- O token do bot fica no arquivo `.env` **dentro do celular**, nunca neste repositório.
- Quem acessa de fora precisa estar na sua conta do Tailscale.

## Créditos

Criado por **Bigode**. Contatos no `creditos.ini` (e na janela *Créditos* do programa).

Feito com software livre: [Termux](https://termux.dev), [F‑Droid](https://f-droid.org), [File Browser](https://filebrowser.org), [Tailscale](https://tailscale.com), [scrcpy](https://github.com/Genymobile/scrcpy) e o `adb` do Android.

## Licença

MIT, veja [LICENSE](LICENSE).
