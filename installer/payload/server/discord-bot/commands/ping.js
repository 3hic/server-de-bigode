const { SlashCommandBuilder } = require('discord.js');

module.exports = {
  data: new SlashCommandBuilder().setName('ping').setDescription('Mostra se o bot esta vivo e a latencia'),
  async execute(i) {
    const t = Date.now();
    await i.reply({ content: 'Pong!' });
    await i.editReply(`Pong! API: ${i.client.ws.ping}ms - resposta: ${Date.now() - t}ms`);
  },
};
