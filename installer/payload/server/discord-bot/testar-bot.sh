#!/data/data/com.termux/files/usr/bin/bash
# Testa o bot sem token de verdade: confere dependencias e se o login falha de forma limpa.
cd "$(dirname "$0")" || exit 1
echo "--- npm install (fim do log):"; tail -4 npm-install.log 2>/dev/null
echo "--- modulos instalados: $(ls node_modules 2>/dev/null | wc -l)"
node -e 'const fs=require("fs");console.log("discord.js", JSON.parse(fs.readFileSync("node_modules/discord.js/package.json")).version, "| node", process.version)'
echo "--- carga dos comandos:"
node -e 'const fs=require("fs");for(const f of fs.readdirSync("commands")){const c=require("./commands/"+f);console.log(" ",f,"->","/"+c.data.name)}'
echo "--- teste de login com token falso (esperado: falha limpa, sem travar):"
DISCORD_TOKEN=token.falso.de.teste timeout 40 node index.js 2>&1 | tail -3
echo "codigo: ${PIPESTATUS[0]}"
