using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace DualScreenKiosk
{
    internal static class Program
    {
#if CHROME_ONLY
        internal static readonly bool ChromeOnly = true;
#else
        internal static readonly bool ChromeOnly = false;
#endif
        internal static readonly string AppName = ChromeOnly ? "Dual Chrome Kiosk" : "Dual Screen Kiosk";
        internal static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ChromeOnly ? "DualChromeKiosk" : "DualScreenKiosk");
        internal static readonly string ConfigFile = Path.Combine(Root, "config.ini");
        internal static readonly string LogFile = Path.Combine(Root, "kiosk.log");
        internal static readonly string MaintenanceFile = Path.Combine(Root, "maintenance.flag");
        internal static readonly string ChromeProfile = Path.Combine(Root, "Chrome-Tela1");
        internal static readonly string EdgeProfile = Path.Combine(Root, ChromeOnly ? "Chrome-Tela2" : "Edge-Tela2");
        internal static readonly string ThirdProfile = Path.Combine(Root, "Chrome-Tela3");
        internal static readonly string RunName = ChromeOnly ? "DualChromeKiosk" : "DualScreenKiosk";

        [STAThread]
        private static void Main(string[] args)
        {
            Directory.CreateDirectory(Root);
            bool watchdog = Array.Exists(args, delegate(string x) { return string.Equals(x, "--watchdog", StringComparison.OrdinalIgnoreCase); });
            if (watchdog)
            {
                KioskEngine.Run();
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        internal static void Log(string message)
        {
            try { File.AppendAllText(LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine); }
            catch { }
        }

        internal static string Quote(string value) { return "\"" + value.Replace("\"", "\\\"") + "\""; }
    }

    internal sealed class KioskConfig
    {
        internal string ChromeUrl = "https://teste.com.br";
        internal string EdgeUrl = "https://teste2.com.br";
        internal string ThirdUrl = "https://teste3.com.br";
        internal int ScreenCount = 2;
        internal bool UseScreen1 = true;
        internal bool UseScreen2 = true;
        internal bool UseScreen3 = false;
        internal int RefreshMinutes = 10;
        internal bool RotateTabs = false;
        internal int RotateSeconds = 30;

        internal static KioskConfig Load()
        {
            KioskConfig c = new KioskConfig();
            bool explicitScreenSelection = false;
            try
            {
                if (!File.Exists(Program.ConfigFile)) return c;
                foreach (string raw in File.ReadAllLines(Program.ConfigFile))
                {
                    int p = raw.IndexOf('=');
                    if (p < 1) continue;
                    string key = raw.Substring(0, p).Trim();
                    string value = raw.Substring(p + 1).Trim();
                    if (key.Equals("ChromeUrl", StringComparison.OrdinalIgnoreCase)) c.ChromeUrl = value;
                    if (key.Equals("EdgeUrl", StringComparison.OrdinalIgnoreCase)) c.EdgeUrl = value;
                    if (key.Equals("ThirdUrl", StringComparison.OrdinalIgnoreCase)) c.ThirdUrl = value;
                    if (key.Equals("ChromeUrls64", StringComparison.OrdinalIgnoreCase)) c.ChromeUrl = Decode(value, c.ChromeUrl);
                    if (key.Equals("SecondUrls64", StringComparison.OrdinalIgnoreCase)) c.EdgeUrl = Decode(value, c.EdgeUrl);
                    if (key.Equals("ThirdUrls64", StringComparison.OrdinalIgnoreCase)) c.ThirdUrl = Decode(value, c.ThirdUrl);
                    int number;
                    if (key.Equals("ScreenCount", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out number) && number >= 1 && number <= 3) c.ScreenCount = number;
                    if (key.Equals("UseScreen1", StringComparison.OrdinalIgnoreCase)) { c.UseScreen1 = value.Equals("true", StringComparison.OrdinalIgnoreCase); explicitScreenSelection = true; }
                    if (key.Equals("UseScreen2", StringComparison.OrdinalIgnoreCase)) { c.UseScreen2 = value.Equals("true", StringComparison.OrdinalIgnoreCase); explicitScreenSelection = true; }
                    if (key.Equals("UseScreen3", StringComparison.OrdinalIgnoreCase)) { c.UseScreen3 = value.Equals("true", StringComparison.OrdinalIgnoreCase); explicitScreenSelection = true; }
                    if (key.Equals("RefreshMinutes", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out number) && (number == 0 || number == 1 || number == 3 || number == 5 || number == 10)) c.RefreshMinutes = number;
                    if (key.Equals("RotateTabs", StringComparison.OrdinalIgnoreCase)) c.RotateTabs = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    if (key.Equals("RotateSeconds", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out number) && (number == 15 || number == 30 || number == 60 || number == 120)) c.RotateSeconds = number;
                }
            }
            catch (Exception ex) { Program.Log("Falha ao ler configuracao: " + ex.Message); }
            if (!explicitScreenSelection)
            {
                c.UseScreen1 = c.ScreenCount >= 1;
                c.UseScreen2 = c.ScreenCount >= 2;
                c.UseScreen3 = c.ScreenCount >= 3;
            }
            return c;
        }

        internal void Save()
        {
            Directory.CreateDirectory(Program.Root);
            File.WriteAllLines(Program.ConfigFile, new[] {
                "ChromeUrls64=" + Encode(ChromeUrl),
                "SecondUrls64=" + Encode(EdgeUrl),
                "ThirdUrls64=" + Encode(ThirdUrl),
                "ScreenCount=" + ScreenCount,
                "UseScreen1=" + UseScreen1.ToString().ToLowerInvariant(),
                "UseScreen2=" + UseScreen2.ToString().ToLowerInvariant(),
                "UseScreen3=" + UseScreen3.ToString().ToLowerInvariant(),
                "RefreshMinutes=" + RefreshMinutes,
                "RotateTabs=" + RotateTabs.ToString().ToLowerInvariant(),
                "RotateSeconds=" + RotateSeconds
            });
        }

        private static string Encode(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string Decode(string value, string fallback)
        {
            try { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
            catch { return fallback; }
        }
    }

    internal sealed class MainForm : Form
    {
        private readonly TextBox chromeUrl = new TextBox();
        private readonly TextBox edgeUrl = new TextBox();
        private readonly TextBox thirdUrl = new TextBox();
        private readonly CheckBox useScreen1 = new CheckBox();
        private readonly CheckBox useScreen2 = new CheckBox();
        private readonly CheckBox useScreen3 = new CheckBox();
        private readonly ComboBox refreshMinutes = new ComboBox();
        private readonly CheckBox rotateTabs = new CheckBox();
        private readonly ComboBox rotateSeconds = new ComboBox();
        private readonly CheckBox autoStart = new CheckBox();
        private readonly Label status = new Label();
        private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();

        internal MainForm()
        {
            Text = Program.AppName;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(650, Program.ChromeOnly ? 650 : 330);
            MinimumSize = new Size(666, Program.ChromeOnly ? 689 : 369);
            Font = new Font("Segoe UI", 10F);
            Icon = SystemIcons.Application;

            Label title = new Label { Text = Program.AppName, Font = new Font("Segoe UI Semibold", 18F), AutoSize = true, Location = new Point(24, 20) };
            Label subtitle = new Label { Text = Program.ChromeOnly ? "Google Chrome nas telas físicas selecionadas, com perfis independentes" : "Chrome na tela principal e Edge na tela secundária", AutoSize = true, ForeColor = Color.DimGray, Location = new Point(27, 58) };
            int urlStart = Program.ChromeOnly ? 180 : 98;
            Label countLabel = new Label { Text = "Telas físicas utilizadas", AutoSize = true, Location = new Point(27, 98), Visible = Program.ChromeOnly };
            useScreen1.Text = "Tela 1"; useScreen1.AutoSize = true; useScreen1.Location = new Point(30, 121); useScreen1.Visible = Program.ChromeOnly;
            useScreen2.Text = "Tela 2"; useScreen2.AutoSize = true; useScreen2.Location = new Point(105, 121); useScreen2.Visible = Program.ChromeOnly;
            useScreen3.Text = "Tela 3"; useScreen3.AutoSize = true; useScreen3.Location = new Point(180, 121); useScreen3.Visible = Program.ChromeOnly;
            Label refreshLabel = new Label { Text = "Atualizar a cada (minutos)", AutoSize = true, Location = new Point(335, 98), Visible = Program.ChromeOnly };
            refreshMinutes.DropDownStyle = ComboBoxStyle.DropDownList;
            refreshMinutes.Items.AddRange(new object[] { "Não atualizar", "1", "3", "5", "10" });
            refreshMinutes.SetBounds(338, 119, 140, 30);
            refreshMinutes.Visible = Program.ChromeOnly;
            Label urlHint = new Label { Text = "Digite uma URL por linha para abrir várias abas na mesma tela.", AutoSize = true, ForeColor = Color.DimGray, Location = new Point(30, 153), Visible = Program.ChromeOnly };
            int urlGap = Program.ChromeOnly ? 100 : 60;
            int urlHeight = Program.ChromeOnly ? 70 : 30;
            Label lc = new Label { Text = Program.ChromeOnly ? "URL do Chrome — Tela 1" : "URL do Chrome", AutoSize = true, Location = new Point(27, urlStart) };
            chromeUrl.SetBounds(30, urlStart + 22, Program.ChromeOnly ? 590 : 540, urlHeight);
            Label le = new Label { Text = Program.ChromeOnly ? "URL do Chrome — Tela 2" : "URL do Edge", AutoSize = true, Location = new Point(27, urlStart + urlGap) };
            edgeUrl.SetBounds(30, urlStart + urlGap + 22, Program.ChromeOnly ? 590 : 540, urlHeight);
            Label lt = new Label { Text = "URL do Chrome — Tela 3", AutoSize = true, Location = new Point(27, urlStart + (urlGap * 2)), Visible = Program.ChromeOnly };
            thirdUrl.SetBounds(30, urlStart + (urlGap * 2) + 22, 590, urlHeight);
            thirdUrl.Visible = Program.ChromeOnly;
            if (Program.ChromeOnly)
            {
                chromeUrl.Multiline = edgeUrl.Multiline = thirdUrl.Multiline = true;
                chromeUrl.ScrollBars = edgeUrl.ScrollBars = thirdUrl.ScrollBars = ScrollBars.Vertical;
                chromeUrl.AcceptsReturn = edgeUrl.AcceptsReturn = thirdUrl.AcceptsReturn = true;
            }
            rotateTabs.Text = "Alternar abas automaticamente";
            rotateTabs.AutoSize = true;
            rotateTabs.Location = new Point(30, 505);
            rotateTabs.Visible = Program.ChromeOnly;
            Label rotateLabel = new Label { Text = "Intervalo entre abas (segundos)", AutoSize = true, Location = new Point(300, 507), Visible = Program.ChromeOnly };
            rotateSeconds.DropDownStyle = ComboBoxStyle.DropDownList;
            rotateSeconds.Items.AddRange(new object[] { "15", "30", "60", "120" });
            rotateSeconds.SetBounds(510, 502, 110, 30);
            rotateSeconds.Visible = Program.ChromeOnly;
            autoStart.Text = "Iniciar automaticamente ao entrar no Windows";
            autoStart.AutoSize = true;
            autoStart.Location = new Point(30, Program.ChromeOnly ? 545 : 220);

            int buttonY = Program.ChromeOnly ? 585 : 260;
            Button save = new Button { Text = "Salvar", Location = new Point(30, buttonY), Size = new Size(125, 38) };
            Button start = new Button { Text = "Iniciar", Location = new Point(165, buttonY), Size = new Size(125, 38), BackColor = Color.FromArgb(46, 125, 50), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
            Button stop = new Button { Text = "Parar", Location = new Point(300, buttonY), Size = new Size(125, 38) };
            status.SetBounds(470, buttonY + 6, 150, 30);
            status.TextAlign = ContentAlignment.MiddleRight;

            Controls.AddRange(new Control[] { title, subtitle, countLabel, useScreen1, useScreen2, useScreen3, refreshLabel, refreshMinutes, urlHint, lc, chromeUrl, le, edgeUrl, lt, thirdUrl, rotateTabs, rotateLabel, rotateSeconds, autoStart, save, start, stop, status });
            Load += delegate { LoadSettings(); RefreshStatus(); };
            save.Click += delegate { SaveSettings(true); };
            start.Click += delegate { StartKiosk(); };
            stop.Click += delegate { StopKiosk(); };
            autoStart.CheckedChanged += delegate { if (Visible) SetAutoStart(autoStart.Checked); };
            useScreen1.CheckedChanged += delegate { UpdateScreenFields(); };
            useScreen2.CheckedChanged += delegate { UpdateScreenFields(); };
            useScreen3.CheckedChanged += delegate { UpdateScreenFields(); };
            rotateTabs.CheckedChanged += delegate { UpdateScreenFields(); };
            timer.Interval = 2000;
            timer.Tick += delegate { RefreshStatus(); };
            timer.Start();
        }

        private void LoadSettings()
        {
            KioskConfig c = KioskConfig.Load();
            chromeUrl.Text = c.ChromeUrl;
            edgeUrl.Text = c.EdgeUrl;
            thirdUrl.Text = c.ThirdUrl;
            useScreen1.Checked = c.UseScreen1;
            useScreen2.Checked = c.UseScreen2;
            useScreen3.Checked = c.UseScreen3;
            refreshMinutes.SelectedItem = c.RefreshMinutes == 0 ? "Não atualizar" : c.RefreshMinutes.ToString();
            rotateTabs.Checked = c.RotateTabs;
            rotateSeconds.SelectedItem = c.RotateSeconds.ToString();
            UpdateScreenFields();
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                autoStart.Checked = key != null && key.GetValue(Program.RunName) != null;
        }

        private bool SaveSettings(bool showMessage)
        {
            int count = Program.ChromeOnly ? (useScreen1.Checked ? 1 : 0) + (useScreen2.Checked ? 1 : 0) + (useScreen3.Checked ? 1 : 0) : 2;
            if (Program.ChromeOnly && count == 0)
            { MessageBox.Show(this, "Selecione pelo menos uma tela física.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
            int refresh = Program.ChromeOnly ? (Convert.ToString(refreshMinutes.SelectedItem) == "Não atualizar" ? 0 : Convert.ToInt32(refreshMinutes.SelectedItem)) : 10;
            KioskConfig previous = KioskConfig.Load();
            string urls1 = !Program.ChromeOnly || useScreen1.Checked ? NormalizeUrls(chromeUrl.Text, "Tela 1") : previous.ChromeUrl;
            if (urls1 == null) return false;
            string urls2 = !Program.ChromeOnly || useScreen2.Checked ? NormalizeUrls(edgeUrl.Text, "Tela 2") : previous.EdgeUrl;
            if (urls2 == null) return false;
            string urls3 = Program.ChromeOnly && useScreen3.Checked ? NormalizeUrls(thirdUrl.Text, "Tela 3") : previous.ThirdUrl;
            if (urls3 == null) return false;
            new KioskConfig { ChromeUrl = urls1, EdgeUrl = urls2, ThirdUrl = urls3, ScreenCount = count, UseScreen1 = !Program.ChromeOnly || useScreen1.Checked, UseScreen2 = !Program.ChromeOnly || useScreen2.Checked, UseScreen3 = Program.ChromeOnly && useScreen3.Checked, RefreshMinutes = refresh, RotateTabs = Program.ChromeOnly && rotateTabs.Checked, RotateSeconds = Program.ChromeOnly ? Convert.ToInt32(rotateSeconds.SelectedItem) : 30 }.Save();
            if (showMessage) MessageBox.Show(this, "URLs salvas com sucesso.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }

        private void StartKiosk()
        {
            if (!SaveSettings(false)) return;
            try { if (File.Exists(Program.MaintenanceFile)) File.Delete(Program.MaintenanceFile); } catch { }
            StopWatchdogs();
            ProcessTools.StopProfileBrowsers();
            Thread.Sleep(500);
            Process.Start(new ProcessStartInfo { FileName = Application.ExecutablePath, Arguments = "--watchdog", UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });
            status.Text = "Iniciando...";
            status.ForeColor = Color.DarkOrange;
        }

        private void StopKiosk()
        {
            try { File.WriteAllText(Program.MaintenanceFile, DateTime.Now.ToString("O")); } catch { }
            StopWatchdogs();
            ProcessTools.StopProfileBrowsers();
            RefreshStatus();
        }

        private static void StopWatchdogs()
        {
            int current = Process.GetCurrentProcess().Id;
            foreach (ManagementObject p in ProcessTools.QueryProcesses(null))
            {
                try
                {
                    int id = Convert.ToInt32(p["ProcessId"]);
                    string cmd = Convert.ToString(p["CommandLine"]);
                    string name = Convert.ToString(p["Name"]);
                    if (id != current && name.Equals(Path.GetFileName(Application.ExecutablePath), StringComparison.OrdinalIgnoreCase) && cmd.IndexOf("--watchdog", StringComparison.OrdinalIgnoreCase) >= 0)
                        Process.GetProcessById(id).Kill();
                }
                catch { }
            }
        }

        private void SetAutoStart(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))
                {
                    if (enabled) key.SetValue(Program.RunName, Program.Quote(Application.ExecutablePath) + " --watchdog");
                    else key.DeleteValue(Program.RunName, false);
                }
            }
            catch (Exception ex) { MessageBox.Show(this, "Não foi possível alterar a inicialização automática:\n" + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        private void UpdateScreenFields()
        {
            if (!Program.ChromeOnly) return;
            chromeUrl.Enabled = useScreen1.Checked;
            edgeUrl.Enabled = useScreen2.Checked;
            thirdUrl.Enabled = useScreen3.Checked;
            rotateSeconds.Enabled = rotateTabs.Checked;
        }

        private string NormalizeUrls(string input, string screenLabel)
        {
            List<string> result = new List<string>();
            foreach (string raw in input.Replace("\r", string.Empty).Split('\n'))
            {
                string value = raw.Trim();
                if (value.Length == 0) continue;
                Uri uri;
                if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
                {
                    MessageBox.Show(this, "URL inválida em " + screenLabel + ":\n" + value + "\n\nUse endereços iniciados por http:// ou https://.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return null;
                }
                result.Add(uri.AbsoluteUri);
            }
            if (result.Count == 0)
            {
                MessageBox.Show(this, "Informe pelo menos uma URL para " + screenLabel + ".", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }
            return string.Join("\n", result.ToArray());
        }

        private void RefreshStatus()
        {
            bool running = ProcessTools.IsWatchdogRunning();
            status.Text = running ? "● Em execução" : "● Parado";
            status.ForeColor = running ? Color.Green : Color.Firebrick;
        }
    }

    internal static class ProcessTools
    {
        internal static IEnumerable<ManagementObject> QueryProcesses(string name)
        {
            string q = name == null ? "SELECT Name,ProcessId,CommandLine FROM Win32_Process" : "SELECT Name,ProcessId,CommandLine FROM Win32_Process WHERE Name='" + name + "'";
            using (ManagementObjectSearcher s = new ManagementObjectSearcher(q))
                foreach (ManagementObject p in s.Get()) yield return p;
        }

        internal static bool IsWatchdogRunning()
        {
            string exe = Path.GetFileName(Application.ExecutablePath);
            foreach (ManagementObject p in QueryProcesses(exe))
            {
                string cmd = Convert.ToString(p["CommandLine"]);
                if (cmd.IndexOf("--watchdog", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        internal static List<int> ProfileProcessIds(string exeName, string profile)
        {
            List<int> result = new List<int>();
            foreach (ManagementObject p in QueryProcesses(exeName))
            {
                string cmd = Convert.ToString(p["CommandLine"]);
                if (cmd.IndexOf(profile, StringComparison.OrdinalIgnoreCase) >= 0) result.Add(Convert.ToInt32(p["ProcessId"]));
            }
            return result;
        }

        internal static void StopProfileBrowsers()
        {
            foreach (int id in ProfileProcessIds("chrome.exe", Program.ChromeProfile)) try { Process.GetProcessById(id).Kill(); } catch { }
            string secondExe = Program.ChromeOnly ? "chrome.exe" : "msedge.exe";
            foreach (int id in ProfileProcessIds(secondExe, Program.EdgeProfile)) try { Process.GetProcessById(id).Kill(); } catch { }
            if (Program.ChromeOnly)
                foreach (int id in ProfileProcessIds("chrome.exe", Program.ThirdProfile)) try { Process.GetProcessById(id).Kill(); } catch { }
        }
    }

    internal static class KioskEngine
    {
        [DllImport("user32.dll")] private static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] private static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);
        [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr hWnd, int command);
        private static readonly Dictionary<int, int> ActiveTabIndexes = new Dictionary<int, int>();

        internal static void Run()
        {
            bool created;
            using (Mutex mutex = new Mutex(true, Program.ChromeOnly ? @"Local\DualChromeKiosk-Windows-v1" : @"Local\DualScreenKiosk-Windows-v4", out created))
            {
                if (!created) return;
                try
                {
                    if (File.Exists(Program.MaintenanceFile)) return;
                    Directory.CreateDirectory(Program.ChromeProfile);
                    Directory.CreateDirectory(Program.EdgeProfile);
                    if (Program.ChromeOnly) Directory.CreateDirectory(Program.ThirdProfile);
                    try { SetProcessDPIAware(); } catch { }
                    Program.Log("=== Inicio do " + Program.AppName + " ===");
                    KioskConfig initialConfig = KioskConfig.Load();
                    int requiredScreens = Program.ChromeOnly ? HighestSelectedScreen(initialConfig) : 2;
                    DateTime limit = DateTime.Now.AddSeconds(60);
                    while (Screen.AllScreens.Length < requiredScreens && DateTime.Now < limit && !File.Exists(Program.MaintenanceFile)) Thread.Sleep(2000);
                    if (Screen.AllScreens.Length < requiredScreens) throw new InvalidOperationException(requiredScreens + " monitor(es) nao foram detectados. Use o modo Estender.");
                    int activeRefreshMinutes = initialConfig.RefreshMinutes;
                    DateTime nextRefresh = activeRefreshMinutes > 0 ? DateTime.Now.AddMinutes(activeRefreshMinutes) : DateTime.MaxValue;
                    DateTime nextRotation = DateTime.Now.AddSeconds(initialConfig.RotateSeconds);
                    while (!File.Exists(Program.MaintenanceFile))
                    {
                        KioskConfig c = KioskConfig.Load();
                        if (c.RefreshMinutes != activeRefreshMinutes)
                        {
                            activeRefreshMinutes = c.RefreshMinutes;
                            nextRefresh = activeRefreshMinutes > 0 ? DateTime.Now.AddMinutes(activeRefreshMinutes) : DateTime.MaxValue;
                            Program.Log(activeRefreshMinutes > 0 ? "Atualizacao automatica alterada para " + activeRefreshMinutes + " minuto(s)." : "Atualizacao automatica desativada.");
                        }
                        requiredScreens = Program.ChromeOnly ? HighestSelectedScreen(c) : 2;
                        List<Screen> screens = GetOrderedScreens();
                        if (screens.Count >= requiredScreens)
                        {
                            if (!Program.ChromeOnly || c.UseScreen1)
                                EnsureBrowser("chrome.exe", FindChrome(), Program.ChromeProfile, c.ChromeUrl, screens[0], false, Program.ChromeOnly ? 9222 : 0);
                            if (!Program.ChromeOnly || c.UseScreen2)
                                EnsureBrowser(Program.ChromeOnly ? "chrome.exe" : "msedge.exe", Program.ChromeOnly ? FindChrome() : FindEdge(), Program.EdgeProfile, c.EdgeUrl, screens[1], !Program.ChromeOnly, Program.ChromeOnly ? 9223 : 0);
                            if (Program.ChromeOnly && c.UseScreen3)
                                EnsureBrowser("chrome.exe", FindChrome(), Program.ThirdProfile, c.ThirdUrl, screens[2], false, 9224);

                            if (activeRefreshMinutes > 0 && DateTime.Now >= nextRefresh)
                            {
                                if (!Program.ChromeOnly || c.UseScreen1)
                                    RefreshBrowser("chrome.exe", Program.ChromeProfile, Program.ChromeOnly ? "Chrome/Tela 1" : "Chrome", UrlCount(c.ChromeUrl), Program.ChromeOnly ? 9222 : 0);
                                if (!Program.ChromeOnly || c.UseScreen2)
                                    RefreshBrowser(Program.ChromeOnly ? "chrome.exe" : "msedge.exe", Program.EdgeProfile, Program.ChromeOnly ? "Chrome/Tela 2" : "Edge", UrlCount(c.EdgeUrl), Program.ChromeOnly ? 9223 : 0);
                                if (Program.ChromeOnly && c.UseScreen3)
                                    RefreshBrowser("chrome.exe", Program.ThirdProfile, "Chrome/Tela 3", UrlCount(c.ThirdUrl), 9224);
                                nextRefresh = DateTime.Now.AddMinutes(activeRefreshMinutes);
                            }

                            if (Program.ChromeOnly && c.RotateTabs && DateTime.Now >= nextRotation)
                            {
                                if (c.UseScreen1) RotateBrowser(9222, "Chrome/Tela 1");
                                if (c.UseScreen2) RotateBrowser(9223, "Chrome/Tela 2");
                                if (c.UseScreen3) RotateBrowser(9224, "Chrome/Tela 3");
                                nextRotation = DateTime.Now.AddSeconds(c.RotateSeconds);
                            }
                            else if (!c.RotateTabs) nextRotation = DateTime.Now.AddSeconds(c.RotateSeconds);
                        }
                        else Program.Log("Aguardando " + requiredScreens + " monitor(es); detectados: " + screens.Count + ".");
                        Thread.Sleep(5000);
                    }
                }
                catch (Exception ex) { Program.Log("ERRO FATAL: " + ex.Message); }
                finally { try { mutex.ReleaseMutex(); } catch { } }
            }
        }

        private static string FindChrome()
        {
            return Find(new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Google\Chrome\Application\chrome.exe"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Google\Chrome\Application\chrome.exe"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\Application\chrome.exe") }, "Google Chrome");
        }

        private static int HighestSelectedScreen(KioskConfig config)
        {
            if (config.UseScreen3) return 3;
            if (config.UseScreen2) return 2;
            return 1;
        }

        private static List<Screen> GetOrderedScreens()
        {
            List<Screen> result = new List<Screen>();
            Screen primary = Screen.PrimaryScreen;
            if (primary != null) result.Add(primary);
            List<Screen> secondary = new List<Screen>();
            foreach (Screen screen in Screen.AllScreens) if (!screen.Primary) secondary.Add(screen);
            secondary.Sort(delegate(Screen a, Screen b)
            {
                int x = a.Bounds.X.CompareTo(b.Bounds.X);
                return x != 0 ? x : a.Bounds.Y.CompareTo(b.Bounds.Y);
            });
            result.AddRange(secondary);
            return result;
        }

        private static string FindEdge()
        {
            return Find(new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft\Edge\Application\msedge.exe"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft\Edge\Application\msedge.exe"), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\Application\msedge.exe") }, "Microsoft Edge");
        }

        private static string Find(string[] candidates, string label)
        {
            foreach (string p in candidates) if (File.Exists(p)) return p;
            throw new FileNotFoundException(label + " nao encontrado.");
        }

        private static void EnsureBrowser(string exeName, string exePath, string profile, string url, Screen screen, bool edge, int debugPort)
        {
            IntPtr handle = FindWindow(exeName, profile);
            if (handle != IntPtr.Zero) return;
            foreach (int id in ProcessTools.ProfileProcessIds(exeName, profile)) try { Process.GetProcessById(id).Kill(); } catch { }
            Rectangle b = screen.Bounds;
            string args = "--user-data-dir=" + Program.Quote(profile) + " --kiosk --new-window " + UrlArguments(url) + " --no-first-run --disable-background-mode --window-position=" + b.X + "," + b.Y + " --window-size=" + b.Width + "," + b.Height;
            if (debugPort > 0) args += " --remote-debugging-port=" + debugPort + " --remote-allow-origins=*";
            if (edge) args += " --edge-kiosk-type=fullscreen"; else args += " --no-default-browser-check --disable-session-crashed-bubble";
            Process.Start(new ProcessStartInfo { FileName = exePath, Arguments = args, UseShellExecute = false });
            DateTime limit = DateTime.Now.AddSeconds(35);
            while (handle == IntPtr.Zero && DateTime.Now < limit && !File.Exists(Program.MaintenanceFile)) { Thread.Sleep(500); handle = FindWindow(exeName, profile); }
            if (handle != IntPtr.Zero)
            {
                ShowWindowAsync(handle, 9); Thread.Sleep(300);
                MoveWindow(handle, b.X, b.Y, b.Width, b.Height, true); Thread.Sleep(300);
                ShowWindowAsync(handle, 3);
                Program.Log((edge ? "Edge" : "Chrome") + " posicionado em " + screen.DeviceName + ".");
            }
        }

        private static IntPtr FindWindow(string exeName, string profile)
        {
            foreach (int id in ProcessTools.ProfileProcessIds(exeName, profile))
            {
                try { Process p = Process.GetProcessById(id); p.Refresh(); if (p.MainWindowHandle != IntPtr.Zero) return p.MainWindowHandle; } catch { }
            }
            return IntPtr.Zero;
        }

        private static int UrlCount(string urls)
        {
            return urls.Replace("\r", string.Empty).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
        }

        private static string UrlArguments(string urls)
        {
            List<string> result = new List<string>();
            foreach (string url in urls.Replace("\r", string.Empty).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries))
                result.Add(Program.Quote(url.Trim()));
            return string.Join(" ", result.ToArray());
        }

        private sealed class ChromeTarget
        {
            public string id { get; set; }
            public string type { get; set; }
            public string webSocketDebuggerUrl { get; set; }
        }

        private static List<ChromeTarget> GetChromeTabs(int port)
        {
            using (WebClient client = new WebClient())
            {
                client.Proxy = null;
                string json = client.DownloadString("http://127.0.0.1:" + port + "/json/list");
                ChromeTarget[] targets = new JavaScriptSerializer().Deserialize<ChromeTarget[]>(json);
                List<ChromeTarget> tabs = new List<ChromeTarget>();
                foreach (ChromeTarget target in targets)
                    if (target != null && target.type == "page" && !string.IsNullOrEmpty(target.id)) tabs.Add(target);
                tabs.Reverse();
                return tabs;
            }
        }

        private static void ActivateTab(int port, string targetId)
        {
            using (WebClient client = new WebClient())
            {
                client.Proxy = null;
                client.DownloadString("http://127.0.0.1:" + port + "/json/activate/" + Uri.EscapeDataString(targetId));
            }
        }

        private static void RotateBrowser(int port, string label)
        {
            try
            {
                List<ChromeTarget> tabs = GetChromeTabs(port);
                if (tabs.Count < 2) return;
                int current;
                if (!ActiveTabIndexes.TryGetValue(port, out current)) current = 0;
                int next = (current + 1) % tabs.Count;
                ActivateTab(port, tabs[next].id);
                ActiveTabIndexes[port] = next;
                Program.Log(label + ": exibindo aba " + (next + 1) + " de " + tabs.Count + ".");
            }
            catch (Exception ex) { Program.Log(label + ": falha ao alternar aba: " + ex.Message); }
        }

        private static void ReloadTarget(ChromeTarget target)
        {
            if (string.IsNullOrEmpty(target.webSocketDebuggerUrl)) return;
            using (ClientWebSocket socket = new ClientWebSocket())
            {
                socket.ConnectAsync(new Uri(target.webSocketDebuggerUrl), CancellationToken.None).GetAwaiter().GetResult();
                byte[] bytes = Encoding.UTF8.GetBytes("{\"id\":1,\"method\":\"Page.reload\",\"params\":{\"ignoreCache\":false}}");
                socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None).GetAwaiter().GetResult();
            }
        }

        private static void RefreshBrowser(string exeName, string profile, string label, int tabCount, int debugPort)
        {
            try
            {
                if (debugPort > 0)
                {
                    List<ChromeTarget> tabs = GetChromeTabs(debugPort);
                    foreach (ChromeTarget tab in tabs) ReloadTarget(tab);
                    Program.Log(label + ": " + tabs.Count + " aba(s) atualizada(s) diretamente pelo Chrome.");
                    return;
                }
                Program.Log(label + ": atualizacao automatica por abas requer a versao somente Chrome.");
            }
            catch (Exception ex) { Program.Log(label + ": falha ao atualizar abas: " + ex.Message); }
        }
    }
}
