using Microsoft.Playwright;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using TeamsInjector;
using TeamsInjector.Configs;

namespace MsTeamsInjector
{
    class Program
    {
        private const string ConfigFileName = "InjectorConfig.json";

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_NOMOVE = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        private static InjectorConfig _config = new();

        static async Task Main(string[] args)
        {
            Console.Clear();

            _config = LoadOrCreateConfig();
            TeamsPathHelper.DiscoverTeamsPathIfMissing(_config);
            SaveConfig();

            KillTeamsProcess();
            LaunchTeams();

            await Task.Delay(_config.InitialDelayMs);
            await InjectScriptsIntoAllPages();

            while (true)
            {
                Console.Write("> ");
                string input = Console.ReadLine()?.Trim() ?? string.Empty;
                string[] parts = input.Split(' ', 2);
                switch (parts[0].ToLower())
                {
                    case "help":
                        Console.WriteLine("Available commands:");
                        Console.WriteLine("  help - Show this help message");
                        Console.WriteLine("  kill - Kill the Teams process");
                        Console.WriteLine("  launch - Launch Teams with remote debugging");
                        Console.WriteLine("  update - Update script files");
                        Console.WriteLine("  reinject - Reinject scripts into all pages");
                        Console.WriteLine("  setimage <path> - Set background image from file path");
                        Console.WriteLine("  exit - Exit the program");
                        break;
                    case "reinject":
                        Log.Info("Reinjecting scripts...");
                        await Task.Delay(_config.ReinjectDelayMs);
                        await InjectScriptsIntoAllPages();
                        break;
                    case "kill":
                        KillTeamsProcess();
                        break;
                    case "launch":
                        LaunchTeams();
                        break;
                    case "exit":
                        Log.Info("Exiting...");
                        return;
                    default:
                        Log.Warning($"Unknown command: {parts[0]}");
                        break;
                }
            }
        }

        private static InjectorConfig LoadOrCreateConfig()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MsTeamsInjector");
            string path = Path.Combine(dir, ConfigFileName);
            if (File.Exists(path))
            {
                string json = File.ReadAllText(path);
                InjectorConfig cfg = JsonSerializer.Deserialize<InjectorConfig>(json) ?? new InjectorConfig();
                return cfg;
            }
            Directory.CreateDirectory(dir);
            return new InjectorConfig();
        }

        private static void SaveConfig()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MsTeamsInjector");
            string path = Path.Combine(dir, ConfigFileName);
            string json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        private static void KillTeamsProcess()
        {
            Process[] procs = Process.GetProcessesByName("ms-teams");
            if (procs.Length == 0)
            {
                procs = Process.GetProcessesByName("Teams");
                if (procs.Length == 0)
                {
                    Log.Info("No running Teams process found.");
                }
            }

            foreach (Process proc in procs)
            {
                Log.Warning($"Killing Teams process (ID: {proc.Id})");
                proc.Kill();
                proc.WaitForExit();
            }

            if(procs.Length != 0)
            {
                Log.Success("Teams processes terminated.");
            }
        }

        private static void LaunchTeams()
        {
            string args = "--remote-debugging-port=" + _config.RemoteDebuggingPort;
            if (_config.EnableLogging)
            {
                args += " --enable-logging";
            }

            ProcessStartInfo psi = new(_config.TeamsExePath, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            try
            {
                Process.Start(psi);
                Log.Success("Launched Teams with remote debugging.");
            }
            catch (Exception ex)
            {
                Log.Error("Launch failed: " + ex.Message);
            }
        }

        private static IntPtr GetWindowHandleByTitle(string title)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((IntPtr hWnd, IntPtr lParam) =>
            {
                StringBuilder sb = new(256);
                GetWindowText(hWnd, sb, sb.Capacity);
                if (sb.ToString().Contains(title, StringComparison.OrdinalIgnoreCase))
                {
                    found = hWnd;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            return found;
        }

        private static void ResizeWindow(string title)
        {
            IntPtr hwnd = GetWindowHandleByTitle(title);
            if (hwnd == IntPtr.Zero)
            {
                return;
            }
            if (GetWindowRect(hwnd, out RECT rect))
            {
                int width = rect.Right - rect.Left + 1;
                int height = rect.Bottom - rect.Top;
                SetWindowPos(hwnd, IntPtr.Zero, rect.Left, rect.Top, width, height, SWP_NOZORDER | SWP_NOMOVE);
            }
        }

        private static async Task InjectScriptsIntoAllPages()
        {
            IPlaywright playwright = await Playwright.CreateAsync();
            string url = "http://127.0.0.1:" + _config.RemoteDebuggingPort;
            IBrowser browser = await playwright.Chromium.ConnectOverCDPAsync(url);
            IBrowserContext context = browser.Contexts.FirstOrDefault() ?? await browser.NewContextAsync();

            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _config.ScriptsDirectory);

            string mainScriptFile = Path.Combine(root, "betterteams-main.js");
            if (!File.Exists(mainScriptFile))
            {
                Log.Error($"Script file not found: {mainScriptFile}");
                return;
            }

            foreach (IPage page in context.Pages)
            {

                string contentMain = File.ReadAllText(mainScriptFile);
                try
                {
                    await page.EvaluateAsync(contentMain);
                }
                catch
                {
                }

                foreach (string folder in Directory.GetDirectories(root))
                {
                    foreach (string subFolder in Directory.GetDirectories(folder))
                    {
                        string scriptFile = Path.Combine(subFolder, "main.js");
                        if (File.Exists(scriptFile))
                        {
                            string content = File.ReadAllText(scriptFile);
                            try
                            {
                                await page.EvaluateAsync(content);
                            }
                            catch
                            {
                            }
                        }
                    }
                }
            }
            ResizeWindow("Microsoft Teams");
            await browser.CloseAsync();
        }
    }
}