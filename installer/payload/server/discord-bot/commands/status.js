const os = require('os');
const { SlashCommandBuilder } = require('discord.js');

const fmt = (s) => {
  const d = Math.floor(s / 86400), h = Math.floor((s % 86400) / 3600), m = Math.floor((s % 3600) / 60);
  return `${d}d ${h}h ${m}m`;
};

module.exports = {
  data: new SlashCommandBuilder().setName('status').setDescription('Ha quanto tempo o servidor e o bot estao no ar'),
  async execute(i) {
    const mem = process.memoryUsage().rss / 1048576;
    await i.reply(`Servidor no ar ha ${fmt(os.uptime())} - bot ha ${fmt(process.uptime())} - memoria do bot ${mem.toFixed(0)} MB`);
  },
};
