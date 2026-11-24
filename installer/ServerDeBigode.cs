// Server de Bigode - instalador com interface. Transforma um celular Android num servidor
// (arquivos pelo navegador, SSH, bot de Discord, acesso de qualquer lugar). C# 5 (compilador que ja vem no Windows).
// Os contatos dos creditos NAO ficam no codigo: sao lidos de creditos.ini (ao lado do .exe).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

// ============================================================ paleta / fontes
static class Pal
{
    public static readonly Color Bg = Color.FromArgb(5, 8, 13);
    public static readonly Color Panel = Color.FromArgb(10, 17, 24);
    public static readonly Color Panel2 = Color.FromArgb(14, 23, 34);
    public static readonly Color Line = Color.FromArgb(28, 44, 60);
    public static readonly Color Text = Color.FromArgb(232, 243, 255);
    public static readonly Color Muted = Color.FromArgb(143, 166, 189);
    public static readonly Color Accent = Color.FromArgb(92, 200, 255);
    public static readonly Color Accent2 = Color.FromArgb(122, 162, 255);
    public static readonly Color Ok = Color.FromArgb(79, 209, 197);
    public static readonly Color Fail = Color.FromArgb(255, 107, 129);
    public static readonly Font Title = new Font("Segoe UI Semibold", 22f, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font Semi11 = new Font("Segoe UI Semibold", 11f, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font Semi10 = new Font("Segoe UI Semibold", 10f, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font Norm10 = new Font("Segoe UI", 10f, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font Small9 = new Font("Segoe UI", 9f, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font Mono9 = new Font("Consolas", 9f, FontStyle.Regular, GraphicsUnit.Point);

    public static GraphicsPath Round(RectangleF r, float rad)
    {
        var p = new GraphicsPath(); float d = rad * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90); p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90); p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure(); return p;
    }
}

// ============================================================ creditos (creditos.ini ao lado do .exe)
class Credits
{
    public string Nome = "Bigode";
    public string Discord = "";
    public string Email = "";
    public static Credits Load()
    {
        var c = new Credits();
        try
        {
            string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "creditos.ini");
            if (File.Exists(p))
                foreach (string l in File.ReadAllLines(p))
                {
                    int i = l.IndexOf('='); if (i <= 0 || l.TrimStart().StartsWith(";")) continue;
                    string k = l.Substring(0, i).Trim().ToLower(), v = l.Substring(i + 1).Trim();
                    if (k == "nome") c.Nome = v; else if (k == "discord") c.Discord = v; else if (k == "gmail" || k == "email") c.Email = v;
                }
        }
        catch { }
        return c;
    }
}

class Options { public bool Tailscale, Bot = true, Tema = true, Teste; public string TesteAlvo; }

// ============================================================ logica da instalacao
class Installer
{
    public Action<string> OnLog; public Action<int, int, string> OnStep; public Action<int> OnProgress;
    public Options Opt = new Options();
    public string Work = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ServerDeBigode");
    public string AdminPass = "", TailscaleUrl = "", ServerIp = "", CredFile = "";
    string adb, ssh, scp, keygen, key, serial, ip, user;

    void Log(string s) { if (OnLog != null) OnLog(s); }
    void Step(int i, int st, string d) { if (OnStep != null) OnStep(i, st, d); }
    void Prog(int p) { if (OnProgress != null) OnProgress(p); }

    // ---------- utilitarios
    string Exec(string exe, string args, int ms)
    {
        var psi = new ProcessStartInfo(exe, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        using (var p = new Process { StartInfo = psi })
        {
            var sb = new StringBuilder();
            p.OutputDataReceived += delegate (object s, DataReceivedEventArgs e) { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
            p.ErrorDataReceived += delegate (object s, DataReceivedEventArgs e) { if (e.Data != null) lock (sb) sb.AppendLine(e.Data); };
            p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine();
            if (!p.WaitForExit(ms)) { try { p.Kill(); } catch { } lock (sb) sb.AppendLine("[tempo esgotado]"); } else p.WaitForExit();
            lock (sb) return sb.ToString();
        }
    }
    string A(string args) { return Exec(adb, (serial != null ? "-s " + serial + " " : "") + args, 90000); }
    string Sh(string cmd, int ms) { return Exec(ssh, "-p 8022 -i \"" + key + "\" -o BatchMode=yes -o StrictHostKeyChecking=accept-new -o ConnectTimeout=8 " + user + "@" + ip + " \"" + cmd.Replace("\"", "\\\"") + "\"", ms); }
    bool SshOk() { return ip != null && user != null && Sh("echo ok-servidor", 25000).Contains("ok-servidor"); }
    void Download(string url, string dest)
    {
        using (var wc = new WebClient()) { wc.Headers["User-Agent"] = "ServerDeBigode/1.0"; wc.DownloadFile(url, dest); }
    }
    string HttpGet(string url)
    {
        using (var wc = new WebClient()) { wc.Encoding = Encoding.UTF8; wc.Headers["User-Agent"] = "ServerDeBigode/1.0"; return wc.DownloadString(url); }
    }
    static string Sha256(string file)
    {
        using (var s = File.OpenRead(file)) { var h = SHA256.Create().ComputeHash(s); var sb = new StringBuilder(); foreach (byte b in h) sb.Append(b.ToString("x2")); return sb.ToString(); }
    }
    string OpenSshTool(string name)
    {
        string win = Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows";
        foreach (string d in new[] { Path.Combine(win, "Sysnative", "OpenSSH"), Path.Combine(win, "System32", "OpenSSH") })
        { string p = Path.Combine(d, name); if (File.Exists(p)) return p; }
        return null;
    }
    string FindAdb()
    {
        string local = Path.Combine(Work, "platform-tools", "adb.exe");
        if (File.Exists(local)) return local;
        string path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (string d in path.Split(';')) { try { string p = Path.Combine(d.Trim(), "adb.exe"); if (File.Exists(p)) return p; } catch { } }
        return null;
    }
    string PayloadDir()
    {
        string dir = Path.Combine(Work, "payload");
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        string zip = Path.Combine(Work, "payload.zip");
        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip"))
        using (FileStream f = File.Create(zip)) s.CopyTo(f);
        ZipFile.ExtractToDirectory(zip, dir);
        return dir;
    }
    bool HasPkg(string pkg) { return A("shell pm list packages " + pkg).Contains("package:" + pkg); }

    // ---------- fluxo
    public bool Run()
    {
        try
        {
            Directory.CreateDirectory(Work);
            if (Opt.Teste) Environment.SetEnvironmentVariable("ANDROID_ADB_SERVER_PORT", "5039");
            if (!S0()) return false; if (!S1()) return false; if (!S2()) return false; if (!S3()) return false;
            if (!S4()) return false; if (!S5()) return false; if (!S6()) return false;
            Prog(100); return true;
        }
        catch (Exception e) { Log("ERRO: " + e.Message); return false; }
    }

    bool S0()
    {
        Step(0, 1, "Procurando as ferramentas..."); Prog(2);
        adb = FindAdb();
        if (adb == null)
        {
            Step(0, 1, "Baixando o adb (ferramentas do Google)...");
            string zip = Path.Combine(Work, "platform-tools.zip");
            Download("https://dl.google.com/android/repository/platform-tools-latest-windows.zip", zip);
            ZipFile.ExtractToDirectory(zip, Work);
            adb = Path.Combine(Work, "platform-tools", "adb.exe");
        }
        ssh = OpenSshTool("ssh.exe"); scp = OpenSshTool("scp.exe"); keygen = OpenSshTool("ssh-keygen.exe");
        if (ssh == null || scp == null || keygen == null) { Step(0, 3, "Falta o OpenSSH do Windows (Recursos opcionais)"); return false; }
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        key = Path.Combine(home, ".ssh", "id_ed25519");
        if (!File.Exists(key))
        {
            Directory.CreateDirectory(Path.Combine(home, ".ssh"));
            Exec(keygen, "-t ed25519 -N \"\" -C server-de-bigode -f \"" + key + "\"", 30000);
            Log("Chave SSH criada em " + key);
        }
        Log("adb: " + adb);
        Step(0, 2, "adb e chave SSH prontos"); Prog(6); return true;
    }

    bool S1()
    {
        Step(1, 1, "Procurando o celular..."); Exec(adb, "start-server", 30000);
        if (Opt.TesteAlvo != null) Exec(adb, "connect " + Opt.TesteAlvo, 15000);
        bool avisou = false;
        for (int t = 0; t < 100; t++)
        {
            string o = Exec(adb, "devices", 20000); string unauth = null, found = null;
            foreach (string l in o.Split('\n'))
            {
                string[] p = l.Trim().Split('\t'); if (p.Length < 2) continue;
                if (p[1] == "device") { if (found == null || (found.Contains(":") && !p[0].Contains(":"))) found = p[0]; }
                else if (p[1] == "unauthorized") unauth = p[0];
            }
            if (found != null) { serial = found; break; }
            if (!avisou)
            {
                Log("Ligue o celular por USB e ative a Depuracao USB: Configuracoes > Sobre o telefone > toque 7x em 'Numero da versao' > Opcoes do desenvolvedor > Depuracao USB.");
                avisou = true;
            }
            Step(1, 1, unauth != null ? "Toque em PERMITIR na tela do celular" : "Ligue o celular por USB (Depuracao USB ativa)");
            Thread.Sleep(2000);
        }
        if (serial == null) { Step(1, 3, "Nenhum celular autorizado encontrado"); return false; }
        string model = A("shell getprop ro.product.model").Trim(), rel = A("shell getprop ro.build.version.release").Trim();
        Log("Celular: " + model + " (Android " + rel + ")");
        Step(1, 2, model + "  -  Android " + rel); Prog(12); return true;
    }

    void InstallFdroid(string pkg)
    {
        string json = HttpGet("https://f-droid.org/api/v1/packages/" + pkg);
        Match m = Regex.Match(json, "\"suggestedVersionCode\"\\s*:\\s*(\\d+)");
        if (!m.Success) throw new Exception("F-Droid nao conhece " + pkg);
        string url = "https://f-droid.org/repo/" + pkg + "_" + m.Groups[1].Value + ".apk";
        string dir = Path.Combine(Work, "apk"); Directory.CreateDirectory(dir);
        string apk = Path.Combine(dir, pkg + ".apk");
        Log("Baixando " + url); Download(url, apk);
        string r = A("install -r \"" + apk + "\"");
        if (!r.Contains("Success")) throw new Exception("Falha ao instalar " + pkg + ": " + r.Trim());
        Log("Instalado: " + pkg);
    }

    bool bootNovo;
    bool S2()
    {
        Step(2, 1, "Verificando os aplicativos..."); int n = 0;
        foreach (string pkg in new[] { "com.termux", "com.termux.boot", "com.termux.api" })
        {
            if (HasPkg(pkg)) { Log("ja instalado: " + pkg); n++; continue; }
            if (Opt.Teste) { Log("[teste] faltaria instalar " + pkg); continue; }
            Step(2, 1, "Instalando " + pkg + "..."); InstallFdroid(pkg); n++;
            if (pkg == "com.termux.boot") bootNovo = true;
            Prog(12 + n * 5);
        }
        Step(2, 2, "Termux, Termux:Boot e Termux:API"); Prog(28); return true;
    }

    bool S3()
    {
        Step(3, 1, "Liberando permissoes e bateria...");
        string[] cmds = {
            "shell pm grant com.termux android.permission.READ_EXTERNAL_STORAGE", "shell pm grant com.termux android.permission.WRITE_EXTERNAL_STORAGE",
            "shell pm grant com.termux.api android.permission.READ_EXTERNAL_STORAGE", "shell pm grant com.termux android.permission.POST_NOTIFICATIONS",
            "shell cmd appops set com.termux READ_EXTERNAL_STORAGE allow", "shell cmd appops set com.termux.api READ_EXTERNAL_STORAGE allow",
            "shell dumpsys deviceidle whitelist +com.termux", "shell dumpsys deviceidle whitelist +com.termux.boot", "shell dumpsys deviceidle whitelist +com.termux.api",
            "shell cmd appops set com.termux RUN_IN_BACKGROUND allow", "shell cmd appops set com.termux RUN_ANY_IN_BACKGROUND allow",
            "shell cmd appops set com.termux.boot RUN_IN_BACKGROUND allow", "shell cmd appops set com.termux.boot RUN_ANY_IN_BACKGROUND allow",
            "shell settings put global wifi_sleep_policy 2", "shell settings put global wifi_scan_always_enabled 0"
        };
        foreach (string c in cmds) A(c);
        if (bootNovo) { A("shell monkey -p com.termux.boot -c android.intent.category.LAUNCHER 1"); Thread.Sleep(2500); A("shell input keyevent KEYCODE_HOME"); }
        Step(3, 2, "Termux liberado para rodar em segundo plano"); Prog(34); return true;
    }

    bool S4()
    {
        Step(4, 1, "Descobrindo o endereco do celular...");
        Match mi = Regex.Match(A("shell ip -f inet addr show wlan0"), @"inet (\d+\.\d+\.\d+\.\d+)");
        if (!mi.Success) { Step(4, 3, "Conecte o celular ao Wi-Fi (o mesmo do PC)"); return false; }
        ip = mi.Groups[1].Value; ServerIp = ip;
        Match mu = Regex.Match(A("shell dumpsys package com.termux"), @"userId=(\d+)");
        if (!mu.Success) { Step(4, 3, "Nao achei o Termux no celular"); return false; }
        user = "u0_a" + (int.Parse(mu.Groups[1].Value) - 10000);
        Log("Celular em " + ip + "  (usuario do Termux: " + user + ")");
        if (SshOk()) { Step(4, 2, "SSH ja funciona em " + ip); Prog(40); return true; }
        Step(4, 1, "Configurando o Termux pela primeira vez...");
        string dir = "/sdcard/Android/data/com.termux/files";
        string payload = PayloadDir();
        A("shell mkdir -p " + dir);
        A("push \"" + Path.Combine(payload, "bootstrap-termux.sh") + "\" " + dir + "/bootstrap-termux.sh");
        A("push \"" + key + ".pub\" " + dir + "/pc_key.pub");
        A("shell rm -f " + dir + "/bootstrap.log");
        A("shell input keyevent KEYCODE_WAKEUP"); A("shell wm dismiss-keyguard");
        A("shell am start -n com.termux/.app.TermuxActivity");
        Log("Aguardando o Termux terminar de se preparar (primeira abertura)...");
        for (int i = 0; i < 28; i++) { Thread.Sleep(2500); Prog(40 + i / 4); }
        bool ok = false;
        for (int tent = 1; tent <= 2 && !ok; tent++)
        {
            Log("Enviando o comando de preparacao ao Termux (tentativa " + tent + ")");
            A("shell input keyevent 66"); Thread.Sleep(600);
            A("shell input text bash%s" + dir + "/bootstrap-termux.sh"); Thread.Sleep(600);
            A("shell input keyevent 66");
            for (int i = 0; i < 100 && !ok; i++)
            {
                Thread.Sleep(3000);
                string l = A("shell cat " + dir + "/bootstrap.log");
                if (l.Contains("BOOTSTRAP-OK")) ok = true;
                Step(4, 1, ok ? "Termux preparado" : "Instalando o SSH no Termux... (alguns minutos)");
            }
        }
        if (!ok) { Step(4, 3, "O Termux nao terminou a preparacao (tela do celular ligada?)"); return false; }
        for (int i = 0; i < 15 && !SshOk(); i++) Thread.Sleep(3000);
        if (!SshOk()) { Step(4, 3, "SSH nao respondeu depois da preparacao"); return false; }
        Step(4, 2, "SSH pronto em " + ip); Prog(46); return true;
    }

    bool S5()
    {
        Step(5, 1, "Enviando os arquivos do servidor...");
        string payload = Directory.Exists(Path.Combine(Work, "payload", "server")) ? Path.Combine(Work, "payload") : PayloadDir();
        string srv = Path.Combine(payload, "server");
        Sh("rm -rf ~/server-payload", 30000);
        string r = Exec(scp, "-r -P 8022 -i \"" + key + "\" -o BatchMode=yes -o StrictHostKeyChecking=accept-new \"" + srv + "\" " + user + "@" + ip + ":server-payload", 120000);
        if (r.Contains("denied") || r.Contains("No such")) { Step(5, 3, "Falha ao enviar os arquivos"); Log(r); return false; }
        Sh("chmod +x ~/server-payload/*.sh ~/server-payload/discord-bot/*.sh", 30000);
        if (Opt.Teste)
        {
            string chk = Sh("for f in ~/server-payload/*.sh ~/server-payload/discord-bot/*.sh; do bash -n $f && echo sintaxe-ok:$(basename $f); done", 60000);
            Log(chk.Trim()); Step(5, 2, "[teste] scripts enviados e sintaxe verificada"); Prog(95); return true;
        }
        string opts = (Opt.Tema ? " --tema" : "") + (Opt.Bot ? " --bot" : "") + (Opt.Tailscale ? " --tailscale" : "");
        Sh("rm -f ~/setup.log; nohup bash ~/server-payload/setup-server.sh" + opts + " > ~/setup.log 2>&1 < /dev/null &", 30000);
        Log("Instalacao do servidor iniciada no celular (pode levar de 5 a 25 minutos)");
        int shown = 0, etapa = 0; string full = ""; DateTime fim = DateTime.Now.AddMinutes(90);
        while (DateTime.Now < fim)
        {
            Thread.Sleep(8000);
            string o = Sh("cat ~/setup.log", 40000); if (string.IsNullOrEmpty(o) || o.Contains("Connection refused") || o.Contains("timed out")) continue;
            full = o; string[] lines = o.Replace("\r", "").Split('\n');
            for (int i = shown; i < lines.Length - 1; i++)
            {
                string l = lines[i]; if (l.Trim().Length == 0) continue; Log(l);
                Match m = Regex.Match(l, @"ETAPA (\d)/8 (.*)");
                if (m.Success) { etapa = int.Parse(m.Groups[1].Value); Step(5, 1, m.Groups[2].Value); Prog(46 + etapa * 6); }
            }
            shown = Math.Max(shown, lines.Length - 1);
            if (o.Contains("SETUP-FALHOU")) { Step(5, 3, "A instalacao falhou (veja o registro)"); return false; }
            if (o.Contains("SETUP-OK")) break;
        }
        if (!full.Contains("SETUP-OK")) { Step(5, 3, "Tempo esgotado esperando a instalacao"); return false; }
        Match mp = Regex.Match(full, @"ADMIN-PASS: (.+)"); if (mp.Success) AdminPass = mp.Groups[1].Value.Trim();
        Match mt = Regex.Match(full, @"TAILSCALE-LOGIN: (\S+)"); if (mt.Success) TailscaleUrl = mt.Groups[1].Value.Trim();
        Step(5, 2, "Servidor instalado e no ar"); Prog(95); return true;
    }

    bool S6()
    {
        Step(6, 1, "Testando o servidor...");
        bool web = false;
        try
        {
            var rq = (HttpWebRequest)WebRequest.Create("http://" + ip + ":8080/"); rq.Timeout = 12000;
            using (var rs = (HttpWebResponse)rq.GetResponse()) web = rs.StatusCode == HttpStatusCode.OK;
        }
        catch { }
        bool sshok = SshOk();
        Log("Site de arquivos: " + (web ? "no ar" : "sem resposta") + "  |  SSH: " + (sshok ? "no ar" : "sem resposta"));
        var sb = new StringBuilder();
        sb.AppendLine("SERVER DE BIGODE - dados do seu servidor"); sb.AppendLine("========================================");
        sb.AppendLine("Site de arquivos:  http://" + ip + ":8080"); sb.AppendLine("Usuario:           admin");
        sb.AppendLine("Senha:             " + (AdminPass.Length > 0 ? AdminPass : "(ja existente)"));
        sb.AppendLine("SSH:               ssh -p 8022 -i \"" + key + "\" " + user + "@" + ip);
        sb.AppendLine("Pastas no celular: ~/server/files  isos  bots  backups");
        if (Opt.Bot) sb.AppendLine("Bot de Discord:    ~/server/bots/discord-bot  (coloque o token no arquivo .env; veja o LEIA-ME.txt)");
        if (TailscaleUrl.Length > 0) sb.AppendLine("Tailscale (acesso de fora): entre uma vez neste link: " + TailscaleUrl);
        sb.AppendLine("Guarde este arquivo em lugar seguro: ele contem a senha.");
        CredFile = Path.Combine(Work, "credenciais-servidor.txt"); File.WriteAllText(CredFile, sb.ToString(), Encoding.UTF8);
        if (!web && !Opt.Teste) { Step(6, 3, "O site de arquivos nao respondeu"); return false; }
        Step(6, 2, web ? "Tudo respondendo em " + ip : "[teste] SSH ok"); return true;
    }

    // ---------- espelhar a tela do celular no PC (scrcpy)
    public string OpenScreen()
    {
        try
        {
            Directory.CreateDirectory(Work);
            if (adb == null) adb = FindAdb();
            if (adb == null) return "Rode a instalacao uma vez (ela baixa o adb).";
            string o = Exec(adb, "devices", 20000), usb = null;
            foreach (string l in o.Split('\n')) { string[] p = l.Trim().Split('\t'); if (p.Length > 1 && p[1] == "device" && !p[0].Contains(":")) usb = p[0]; }
            string target = ServerIp != null && ServerIp.Length > 0 ? ServerIp + ":5555" : null;
            if (usb != null) { string ipm = Regex.Match(Exec(adb, "-s " + usb + " shell ip -f inet addr show wlan0", 20000), @"inet (\d+\.\d+\.\d+\.\d+)").Groups[1].Value; Exec(adb, "-s " + usb + " tcpip 5555", 20000); Thread.Sleep(2500); if (ipm.Length > 0) { target = ipm + ":5555"; ServerIp = ipm; } }
            if (target == null) return "Ligue o celular por USB uma vez para liberar a conexao sem fio.";
            Exec(adb, "connect " + target, 20000);
            string scr = Path.Combine(Work, "scrcpy");
            string exe = null; if (Directory.Exists(scr)) foreach (string f in Directory.GetFiles(scr, "scrcpy.exe", SearchOption.AllDirectories)) exe = f;
            if (exe == null)
            {
                string json = HttpGet("https://api.github.com/repos/Genymobile/scrcpy/releases/latest");
                Match m = Regex.Match(json, "\"browser_download_url\"\\s*:\\s*\"([^\"]*scrcpy-win64-v[0-9.]+\\.zip)\"");
                if (!m.Success) return "Nao achei o scrcpy para baixar.";
                string zip = Path.Combine(Work, "scrcpy.zip"); Download(m.Groups[1].Value, zip);
                Match dg = Regex.Match(json, "scrcpy-win64-v[0-9.]+\\.zip\"[^}]*?\"digest\"\\s*:\\s*\"sha256:([0-9a-f]{64})\"");
                if (dg.Success && Sha256(zip) != dg.Groups[1].Value) { File.Delete(zip); return "O arquivo do scrcpy veio corrompido (hash nao confere)."; }
                ZipFile.ExtractToDirectory(zip, scr);
                foreach (string f in Directory.GetFiles(scr, "scrcpy.exe", SearchOption.AllDirectories)) exe = f;
            }
            var psi = new ProcessStartInfo(exe, "-s " + target + " --window-title=\"Celular - Server de Bigode\"") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(exe) };
            psi.EnvironmentVariables["ADB"] = adb; Process.Start(psi); return null;
        }
        catch (Exception e) { return e.Message; }
    }
}

// ============================================================ controles customizados
class StepList : Control
{
    public string[] Titles, Details; public int[] State; public float Angle;
    public StepList(string[] t) { Titles = t; Details = new string[t.Length]; State = new int[t.Length]; SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(Pal.Panel); int rh = 46;
        for (int i = 0; i < Titles.Length; i++)
        {
            int y = i * rh + 8, cx = 30, cy = y + 20;
            if (i < Titles.Length - 1) using (var p = new Pen(State[i] == 2 ? Pal.Ok : Pal.Line, 2f)) g.DrawLine(p, cx, cy + 15, cx, cy + rh - 15);
            Color c = State[i] == 2 ? Pal.Ok : State[i] == 3 ? Pal.Fail : State[i] == 1 ? Pal.Accent : Pal.Line;
            if (State[i] == 1)
            {
                using (var p = new Pen(Color.FromArgb(50, Pal.Accent), 3f)) g.DrawEllipse(p, cx - 12, cy - 12, 24, 24);
                using (var p = new Pen(Pal.Accent, 3f) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(p, cx - 12, cy - 12, 24, 24, Angle, 110);
            }
            else if (State[i] == 2 || State[i] == 3 || State[i] == 4)
            {
                using (var b = new SolidBrush(State[i] == 4 ? Pal.Line : c)) g.FillEllipse(b, cx - 12, cy - 12, 24, 24);
                using (var p = new Pen(Pal.Bg, 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    if (State[i] == 3) { g.DrawLine(p, cx - 5, cy - 5, cx + 5, cy + 5); g.DrawLine(p, cx + 5, cy - 5, cx - 5, cy + 5); }
                    else g.DrawLines(p, new[] { new PointF(cx - 5.5f, cy), new PointF(cx - 1.5f, cy + 4.5f), new PointF(cx + 6, cy - 4.5f) });
                }
            }
            else
            {
                using (var p = new Pen(Pal.Line, 2f)) g.DrawEllipse(p, cx - 11, cy - 11, 22, 22);
                using (var b = new SolidBrush(Pal.Muted)) g.DrawString((i + 1).ToString(), Pal.Small9, b, cx - 5.5f, cy - 8);
            }
            using (var b = new SolidBrush(State[i] == 0 ? Pal.Muted : Pal.Text)) g.DrawString(Titles[i], Pal.Semi11, b, 58, y + 1);
            var sf = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap };
            using (var b = new SolidBrush(State[i] == 3 ? Pal.Fail : Pal.Muted)) g.DrawString(Details[i] ?? "", Pal.Small9, b, new RectangleF(58, y + 22, Width - 66, 18), sf);
        }
    }
}

class GButton : Control
{
    public bool Primary = true; bool hover, down;
    public GButton() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true); Cursor = Cursors.Hand; Font = Pal.Semi11; }
    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit; g.Clear(Parent != null ? Parent.BackColor : Pal.Bg);
        var r = new RectangleF(1, 1 + (down ? 1 : 0), Width - 3, Height - 3);
        using (var path = Pal.Round(r, 13))
        {
            if (Primary && Enabled)
            {
                Color a = hover ? Color.FromArgb(120, 214, 255) : Pal.Accent, b = hover ? Color.FromArgb(140, 178, 255) : Pal.Accent2;
                using (var br = new LinearGradientBrush(r, a, b, 20f)) g.FillPath(br, path);
                using (var glow = new Pen(Color.FromArgb(hover ? 90 : 40, Pal.Accent), 4f)) g.DrawPath(glow, path);
            }
            else
            {
                using (var br = new SolidBrush(hover && Enabled ? Color.FromArgb(22, 36, 52) : Pal.Panel2)) g.FillPath(br, path);
                using (var p = new Pen(hover && Enabled ? Pal.Accent : Pal.Line, 1.5f)) g.DrawPath(p, path);
            }
        }
        Color tc = !Enabled ? Color.FromArgb(90, 108, 126) : (Primary ? Color.FromArgb(4, 12, 20) : Pal.Text);
        var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        using (var b = new SolidBrush(tc)) g.DrawString(Text, Font, b, new RectangleF(0, down ? 1 : 0, Width, Height), sf);
    }
}

class FlatCheck : Control
{
    public bool Checked; public string Desc = "";
    public FlatCheck() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true); Cursor = Cursors.Hand; }
    protected override void OnClick(EventArgs e) { if (Enabled) { Checked = !Checked; Invalidate(); } base.OnClick(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit; g.Clear(Parent != null ? Parent.BackColor : Pal.Bg);
        var box = new RectangleF(2, 5, 22, 22);
        using (var path = Pal.Round(box, 6))
        {
            if (Checked) using (var br = new LinearGradientBrush(box, Pal.Accent, Pal.Accent2, 45f)) g.FillPath(br, path);
            else { using (var br = new SolidBrush(Pal.Panel2)) g.FillPath(br, path); using (var p = new Pen(Pal.Line, 1.5f)) g.DrawPath(p, path); }
        }
        if (Checked) using (var p = new Pen(Color.FromArgb(4, 12, 20), 2.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round }) g.DrawLines(p, new[] { new PointF(7.5f, 16.5f), new PointF(12, 21), new PointF(19.5f, 11) });
        using (var b = new SolidBrush(Enabled ? Pal.Text : Pal.Muted)) g.DrawString(Text, Pal.Semi10, b, 34, 3);
        using (var b = new SolidBrush(Pal.Muted)) g.DrawString(Desc, Pal.Small9, b, new RectangleF(34, 23, Width - 38, 34));
    }
}

class SlimBar : Control
{
    public double Target, Shown;
    public SlimBar() { SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true); }
    public void Tick() { double d = Target - Shown; if (Math.Abs(d) > 0.05) { Shown += d * 0.12; Invalidate(); } }
    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Parent != null ? Parent.BackColor : Pal.Bg);
        var r = new RectangleF(0, 3, Width - 1, Height - 6);
        using (var path = Pal.Round(r, r.Height / 2)) using (var br = new SolidBrush(Pal.Panel2)) g.FillPath(br, path);
        float w = (float)(r.Width * Shown / 100.0);
        if (w > r.Height)
        {
            var f = new RectangleF(r.X, r.Y, w, r.Height);
            using (var path = Pal.Round(f, r.Height / 2)) using (var br = new LinearGradientBrush(f, Pal.Accent, Pal.Accent2, 0f)) g.FillPath(br, path);
        }
    }
}

// ============================================================ janelas
class InfoForm : Form
{
    public InfoForm(string title, string body, KeyValuePair<string, Action>[] buttons, Icon icon)
    {
        Text = title; BackColor = Pal.Bg; ForeColor = Pal.Text; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(560, 330); ShowInTaskbar = false; if (icon != null) Icon = icon;
        var lt = new Label { Text = title, Font = Pal.Semi11, ForeColor = Pal.Accent, AutoSize = true, Location = new Point(28, 22) }; Controls.Add(lt);
        var lb = new Label { Text = body, Font = Pal.Norm10, ForeColor = Pal.Text, Location = new Point(28, 58), Size = new Size(504, 200) }; Controls.Add(lb);
        int x = 28;
        foreach (var kv in buttons)
        {
            var action = kv.Value; var b = new GButton { Text = kv.Key, Primary = false, Size = new Size(kv.Key.Length > 12 ? 168 : 120, 40), Location = new Point(x, 270) };
            b.Click += delegate { if (action != null) action(); }; Controls.Add(b); x += b.Width + 10;
        }
        var close = new GButton { Text = "Fechar", Size = new Size(110, 40), Location = new Point(560 - 28 - 110, 270) }; close.Click += delegate { Close(); }; Controls.Add(close);
    }
}

class MainForm : Form
{
    Installer inst = new Installer(); Credits cr = Credits.Load(); Thread worker; Icon appIcon;
    StepList steps; GButton btnInstall, btnScreen, btnOpen; FlatCheck ckTema, ckBot, ckTs; SlimBar bar; Label lblStatus; TextBox log; System.Windows.Forms.Timer anim;
    Panel header, card; float angle, shimmer; bool running, done;
    readonly string[] titles = { "Preparar o PC", "Conectar o celular", "Instalar os aplicativos", "Liberar o celular", "Ligar o acesso remoto", "Montar o servidor", "Conferir tudo" };
    readonly string[] hints = { "adb e chave SSH", "USB com depuracao ligada", "Termux, Termux:Boot, Termux:API", "permissoes e bateria", "SSH dentro do Termux", "arquivos, extras e inicio automatico", "testes finais" };

    Label FootText(Panel foot, string text, ref int x)
    {
        var l = new Label { Text = text, Font = Pal.Small9, ForeColor = Pal.Muted, BackColor = Pal.Bg, AutoSize = true, Location = new Point(x, 14) };
        foot.Controls.Add(l); x += TextRenderer.MeasureText(text, Pal.Small9, new Size(2000, 30), TextFormatFlags.NoPadding).Width + 2; return l;
    }

    public MainForm()
    {
        Text = "Server de Bigode"; BackColor = Pal.Bg; ForeColor = Pal.Text; FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(940, 690); Opacity = 0; DoubleBuffered = true;
        try { appIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); Icon = appIcon; } catch { }

        header = new Panel { Location = new Point(0, 0), Size = new Size(940, 118), BackColor = Pal.Bg }; header.Paint += PaintHeader; Controls.Add(header);

        steps = new StepList(titles) { Size = new Size(432, 336) }; for (int i = 0; i < titles.Length; i++) steps.Details[i] = hints[i];
        var stepsCard = new Panel { Location = new Point(28, 138), Size = new Size(432, 336), BackColor = Pal.Panel }; stepsCard.Paint += CardBorder; Controls.Add(stepsCard);
        steps.Location = new Point(0, 0); stepsCard.Controls.Add(steps);

        card = new Panel { Location = new Point(480, 138), Size = new Size(432, 336), BackColor = Pal.Panel }; card.Paint += CardBorder; Controls.Add(card);
        var lt = new Label { Text = "O que instalar", Font = Pal.Semi11, ForeColor = Pal.Accent, AutoSize = true, Location = new Point(22, 16), BackColor = Pal.Panel }; card.Controls.Add(lt);
        ckTema = new FlatCheck { Text = "Tema escuro no site de arquivos", Desc = "Visual preto com tons frios e animacoes.", Checked = true, Location = new Point(20, 50), Size = new Size(392, 54), BackColor = Pal.Panel };
        ckBot = new FlatCheck { Text = "Modelo de bot de Discord", Desc = "Pasta pronta para rodar 24h (falta so o seu token).", Checked = true, Location = new Point(20, 104), Size = new Size(392, 54), BackColor = Pal.Panel };
        ckTs = new FlatCheck { Text = "Acesso de qualquer lugar (Tailscale)", Desc = "Gratis. Compila no celular: demora uns 10 minutos.", Checked = false, Location = new Point(20, 158), Size = new Size(392, 54), BackColor = Pal.Panel };
        card.Controls.Add(ckTema); card.Controls.Add(ckBot); card.Controls.Add(ckTs);
        btnInstall = new GButton { Text = "Instalar servidor", Location = new Point(22, 228), Size = new Size(388, 54) }; btnInstall.Click += delegate { StartInstall(); }; card.Controls.Add(btnInstall);
        btnScreen = new GButton { Text = "Ver tela do celular", Primary = false, Location = new Point(22, 290), Size = new Size(188, 38), Font = Pal.Semi10 }; btnScreen.Click += delegate { OpenScreen(); }; card.Controls.Add(btnScreen);
        btnOpen = new GButton { Text = "Abrir o servidor", Primary = false, Location = new Point(222, 290), Size = new Size(188, 38), Font = Pal.Semi10, Enabled = false }; btnOpen.Click += delegate { try { Process.Start("http://" + inst.ServerIp + ":8080"); } catch { } }; card.Controls.Add(btnOpen);

        bar = new SlimBar { Location = new Point(28, 492), Size = new Size(884, 14), BackColor = Pal.Bg }; Controls.Add(bar);
        lblStatus = new Label { Text = "Pronto para instalar. Ligue o celular ao PC por USB (com a Depuracao USB ativa) e clique em Instalar servidor.", Font = Pal.Norm10, ForeColor = Pal.Muted, AutoSize = false, Location = new Point(28, 512), Size = new Size(884, 24), BackColor = Pal.Bg }; Controls.Add(lblStatus);
        log = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.None, Font = Pal.Mono9, BackColor = Pal.Panel, ForeColor = Pal.Muted, BorderStyle = BorderStyle.None, Location = new Point(28, 544), Size = new Size(884, 88) }; Controls.Add(log);

        var foot = new Panel { Location = new Point(0, 644), Size = new Size(940, 46), BackColor = Pal.Bg }; foot.Paint += PaintFooter; Controls.Add(foot);
        int x = 28;
        FootText(foot, "Criado por " + cr.Nome + "   |   Discord: " + (cr.Discord.Length > 0 ? cr.Discord : "(defina em creditos.ini)") + "   |   Gmail: ", ref x);
        if (cr.Email.Length > 0)
        {
            var lm = new LinkLabel { Text = cr.Email, Font = Pal.Small9, LinkColor = Pal.Accent, ActiveLinkColor = Pal.Accent2, VisitedLinkColor = Pal.Accent, LinkBehavior = LinkBehavior.HoverUnderline, AutoSize = true, BackColor = Pal.Bg, Location = new Point(x, 14) };
            lm.Click += delegate { try { Process.Start("mailto:" + cr.Email); } catch { } }; foot.Controls.Add(lm);
        }
        else FootText(foot, "(defina em creditos.ini)", ref x);
        var lc = new LinkLabel { Text = "Creditos", Font = Pal.Small9, LinkColor = Pal.Accent, ActiveLinkColor = Pal.Accent2, VisitedLinkColor = Pal.Accent, LinkBehavior = LinkBehavior.HoverUnderline, AutoSize = true, BackColor = Pal.Bg, Location = new Point(852, 14) };
        lc.Click += delegate { ShowCredits(); }; foot.Controls.Add(lc);

        inst.OnLog = Log; inst.OnStep = OnStep; inst.OnProgress = delegate (int p) { bar.Target = p; };
        anim = new System.Windows.Forms.Timer { Interval = 33 }; anim.Tick += Tick; anim.Start();
    }

    public void Demo()   // so para gerar a imagem de conferencia (--captura)
    {
        steps.State = new[] { 2, 2, 2, 1, 0, 0, 0 }; steps.Details[1] = "moto g(8) power  -  Android 11"; steps.Details[3] = "Liberando permissoes e bateria...";
        bar.Target = 34; bar.Shown = 34; lblStatus.Text = "Liberar o celular: Liberando permissoes e bateria..."; steps.Angle = 40;
        log.Text = "13:02:11  adb: C:\\Users\\voce\\AppData\\Local\\ServerDeBigode\\platform-tools\\adb.exe\r\n13:02:14  Celular: moto g(8) power (Android 11)\r\n13:02:15  ja instalado: com.termux\r\n13:02:15  ja instalado: com.termux.boot\r\n13:02:16  ja instalado: com.termux.api";
        Opacity = 1;
    }

    void CardBorder(object s, PaintEventArgs e)
    {
        var p = (Control)s; e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var path = Pal.Round(new RectangleF(0.5f, 0.5f, p.Width - 2, p.Height - 2), 14)) using (var pen = new Pen(Pal.Line, 1.5f)) e.Graphics.DrawPath(pen, path);
    }
    void PaintHeader(object s, PaintEventArgs e)
    {
        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        var r = new Rectangle(0, 0, header.Width, header.Height);
        using (var br = new LinearGradientBrush(r, Color.FromArgb(10, 22, 36), Pal.Bg, 90f)) g.FillRectangle(br, r);
        using (var br = new LinearGradientBrush(new Rectangle(0, 0, 940, 2), Pal.Accent, Pal.Accent2, 0f)) g.FillRectangle(br, 0, 0, 940, 2);
        var lr = new RectangleF(30, 22, 70, 70);
        using (var br = new SolidBrush(Color.FromArgb(6, 16, 26))) g.FillEllipse(br, lr);
        using (var pen = new Pen(Pal.Accent, 3f)) g.DrawEllipse(pen, lr);
        using (var pen = new Pen(Pal.Accent2, 3.4f)) { g.DrawPath(pen, Pal.Round(new RectangleF(47, 40, 36, 11), 4)); g.DrawPath(pen, Pal.Round(new RectangleF(47, 56, 36, 11), 4)); g.DrawLine(pen, 56, 76, 74, 76); }
        using (var br = new SolidBrush(Pal.Accent)) { g.FillEllipse(br, 53, 43, 4, 4); g.FillEllipse(br, 53, 59, 4, 4); }
        using (var br = new SolidBrush(Pal.Text)) g.DrawString("Server de Bigode", Pal.Title, br, 118, 18);
        using (var br = new SolidBrush(Pal.Muted)) g.DrawString("Transforme um celular Android em servidor: arquivos no navegador, bot de Discord e acesso de qualquer lugar.", Pal.Norm10, br, 121, 66);
        float x = (float)((shimmer % 1.4) * header.Width) - 220;
        var rect = new RectangleF(x, header.Height - 2, 220, 2);
        using (var br = new LinearGradientBrush(rect, Color.Transparent, Pal.Accent, 0f))
        { var cb = new ColorBlend(3); cb.Colors = new[] { Color.Transparent, Pal.Accent, Color.Transparent }; cb.Positions = new[] { 0f, 0.5f, 1f }; br.InterpolationColors = cb; g.FillRectangle(br, rect); }
    }
    void PaintFooter(object s, PaintEventArgs e)
    {
        var g = e.Graphics; using (var pen = new Pen(Pal.Line)) g.DrawLine(pen, 28, 0, 912, 0);
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit; using (var br = new SolidBrush(Pal.Line)) g.DrawString("v1.0", Pal.Small9, br, 812, 14);
    }

    void Tick(object s, EventArgs e)
    {
        angle = (angle + 9) % 360; steps.Angle = angle; shimmer += 0.006f;
        bool any = false; foreach (int st in steps.State) if (st == 1) any = true;
        if (any) steps.Invalidate(); bar.Tick(); header.Invalidate(new Rectangle(0, header.Height - 3, header.Width, 3));
        if (Opacity < 1) Opacity = Math.Min(1, Opacity + 0.06);
    }
    void Log(string s)
    {
        if (IsDisposed) return;
        string line = DateTime.Now.ToString("HH:mm:ss") + "  " + s;
        try { File.AppendAllText(Path.Combine(inst.Work, "instalador.log"), line + Environment.NewLine); } catch { }
        BeginInvoke((Action)delegate { log.AppendText(line + Environment.NewLine); });
    }
    void OnStep(int i, int st, string d)
    {
        BeginInvoke((Action)delegate { steps.State[i] = st; steps.Details[i] = d; steps.Invalidate(); lblStatus.Text = titles[i] + ": " + d; lblStatus.ForeColor = st == 3 ? Pal.Fail : Pal.Muted; });
    }
    void StartInstall()
    {
        if (running) return; running = true; done = false; btnInstall.Enabled = false; btnInstall.Text = "Instalando...";
        for (int i = 0; i < steps.State.Length; i++) { steps.State[i] = 0; steps.Details[i] = hints[i]; }
        inst.Opt = new Options { Tema = ckTema.Checked, Bot = ckBot.Checked, Tailscale = ckTs.Checked }; bar.Target = 0; bar.Shown = 0; log.Clear();
        worker = new Thread(delegate ()
        {
            bool ok = inst.Run();
            if (!IsDisposed) BeginInvoke((Action)delegate { Finish(ok); });
        }) { IsBackground = true }; worker.Start();
    }
    void Finish(bool ok)
    {
        running = false; done = ok; btnInstall.Enabled = true; btnInstall.Text = ok ? "Instalar de novo (seguro)" : "Tentar de novo";
        if (!ok) { lblStatus.Text = "A instalacao parou. Veja o registro abaixo, corrija e clique em Tentar de novo (ele continua de onde parou)."; lblStatus.ForeColor = Pal.Fail; return; }
        bar.Target = 100; btnOpen.Enabled = true; lblStatus.Text = "Servidor pronto! Abra http://" + inst.ServerIp + ":8080 (usuario admin). Dados salvos em: " + inst.CredFile; lblStatus.ForeColor = Pal.Ok;
        var btns = new List<KeyValuePair<string, Action>>();
        btns.Add(new KeyValuePair<string, Action>("Abrir servidor", delegate { try { Process.Start("http://" + inst.ServerIp + ":8080"); } catch { } }));
        if (inst.AdminPass.Length > 0) btns.Add(new KeyValuePair<string, Action>("Copiar senha", delegate { try { Clipboard.SetText(inst.AdminPass); } catch { } }));
        if (inst.TailscaleUrl.Length > 0) btns.Add(new KeyValuePair<string, Action>("Entrar no Tailscale", delegate { try { Process.Start(inst.TailscaleUrl); } catch { } }));
        string body = "Site de arquivos:  http://" + inst.ServerIp + ":8080\nUsuario:  admin\nSenha:  " + (inst.AdminPass.Length > 0 ? inst.AdminPass : "(ja existente)") + "\n\nOs dados tambem foram salvos em:\n" + inst.CredFile
            + (inst.TailscaleUrl.Length > 0 ? "\n\nPara acessar de qualquer lugar, entre uma vez no Tailscale (botao abaixo)." : "");
        new InfoForm("Servidor pronto!", body, btns.ToArray(), appIcon).ShowDialog(this);
    }
    void OpenScreen()
    {
        btnScreen.Enabled = false; lblStatus.Text = "Abrindo a tela do celular..."; lblStatus.ForeColor = Pal.Muted;
        new Thread(delegate ()
        {
            string err = inst.OpenScreen();
            if (!IsDisposed) BeginInvoke((Action)delegate { btnScreen.Enabled = true; lblStatus.Text = err == null ? "Tela do celular aberta em outra janela." : err; lblStatus.ForeColor = err == null ? Pal.Ok : Pal.Fail; });
        }) { IsBackground = true }.Start();
    }
    void ShowCredits()
    {
        string d = cr.Discord.Length > 0 ? cr.Discord : "(defina em creditos.ini)", m = cr.Email.Length > 0 ? cr.Email : "(defina em creditos.ini)";
        string body = "Server de Bigode  v1.0\nInstalador automatico de servidor em celular Android.\n\nCriado por  " + cr.Nome + "\nDiscord:  " + d + "\nGmail:  " + m
            + "\n\nFeito com ajuda de software livre: Termux, F-Droid, File Browser, Tailscale, scrcpy e adb.";
        var btns = new List<KeyValuePair<string, Action>>();
        if (cr.Discord.Length > 0) btns.Add(new KeyValuePair<string, Action>("Copiar Discord", delegate { try { Clipboard.SetText(cr.Discord); } catch { } }));
        if (cr.Email.Length > 0) btns.Add(new KeyValuePair<string, Action>("Escrever e-mail", delegate { try { Process.Start("mailto:" + cr.Email); } catch { } }));
        new InfoForm("Creditos", body, btns.ToArray(), appIcon).ShowDialog(this);
    }
}

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
        if (args.Length > 0 && args[0] == "--teste")
        {
            var inst = new Installer(); inst.Opt = new Options { Teste = true, Bot = false, Tema = false, Tailscale = false, TesteAlvo = args.Length > 1 ? args[1] : null };
            Directory.CreateDirectory(inst.Work); string lf = Path.Combine(inst.Work, "teste.log"); File.WriteAllText(lf, "");
            inst.OnLog = delegate (string s) { File.AppendAllText(lf, s + Environment.NewLine); };
            inst.OnStep = delegate (int i, int st, string d) { File.AppendAllText(lf, "[etapa " + (i + 1) + " estado " + st + "] " + d + Environment.NewLine); };
            bool ok = inst.Run(); File.AppendAllText(lf, ok ? "RESULTADO: OK" : "RESULTADO: FALHOU"); return ok ? 0 : 1;
        }
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        if (args.Length > 1 && args[0] == "--captura")
        {
            var f = new MainForm(); f.Demo(); f.ShowInTaskbar = false; f.StartPosition = FormStartPosition.Manual; f.Location = new Point(-3000, -3000); f.Show();
            for (int i = 0; i < 6; i++) { Application.DoEvents(); Thread.Sleep(80); }
            var bmp = new Bitmap(f.Width, f.Height); f.DrawToBitmap(bmp, new Rectangle(0, 0, f.Width, f.Height)); bmp.Save(args[1], System.Drawing.Imaging.ImageFormat.Png); return 0;
        }
        Application.Run(new MainForm()); return 0;
    }
}
