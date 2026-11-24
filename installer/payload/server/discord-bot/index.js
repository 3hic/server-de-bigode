// Bot de Discord pronto para rodar 24h no servidor.
// Preencha o .env (token) e ele sobe sozinho. Comandos novos = um arquivo novo em ./commands
require('dotenv').config();
const fs = require('fs');
const path = require('path');
const { Client, Collection, Events, GatewayIntentBits, REST, Routes } = require('discord.js');

const token = process.env.DISCORD_TOKEN;
const clientId = process.env.CLIENT_ID;
const guildId = process.env.GUILD_ID;
const log = (...a) => console.log(new Date().toISOString(), ...a);

const client = new Client({ intents: [GatewayIntentBits.Guilds] });
client.commands = new Collection();

// carrega todos os comandos de ./commands
const commandsDir = path.join(__dirname, 'commands');
for (const file of fs.readdirSync(commandsDir).filter((f) => f.endsWith('.js'))) {
  const cmd = require(path.join(commandsDir, file));
  if (cmd.data && cmd.execute) client.commands.set(cmd.data.name, cmd);
}

async function registrarComandos() {
  if (!clientId) { log('CLIENT_ID vazio: pulando o registro dos comandos /'); return; }
  const rest = new REST({ version: '10' }).setToken(token);
  const body = [...client.commands.values()].map((c) => c.data.toJSON());
  const rota = guildId ? Routes.applicationGuildCommands(clientId, guildId) : Routes.applicationCommands(clientId);
  await rest.put(rota, { body });
  log(`${body.length} comando(s) registrado(s) ${guildId ? 'no servidor ' + guildId : 'globalmente (pode levar ate 1h para aparecer)'}`);
}

client.once(Events.ClientReady, async (c) => {
  log(`Online como ${c.user.tag}`);
  try { await registrarComandos(); } catch (e) { log('Erro ao registrar comandos:', e.message); }
});

client.on(Events.InteractionCreate, async (i) => {
  if (!i.isChatInputCommand()) return;
  const cmd = client.commands.get(i.commandName);
  if (!cmd) return;
  try {
    await cmd.execute(i);
  } catch (e) {
    log('Erro no comando', i.commandName, e);
    const msg = { content: 'Deu erro nesse comando.', flags: 64 };
    if (i.replied || i.deferred) await i.followUp(msg).catch(() => {});
    else await i.reply(msg).catch(() => {});
  }
});

process.on('unhandledRejection', (e) => log('unhandledRejection:', e));
process.on('uncaughtException', (e) => { log('uncaughtException:', e); process.exit(1); });
for (const s of ['SIGINT', 'SIGTERM']) process.on(s, () => { log('Encerrando...'); client.destroy(); process.exit(0); });

client.login(token).catch((e) => { log('Falha no login (token invalido?):', e.message); process.exit(1); });
