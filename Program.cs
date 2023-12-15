using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Timers;
using System.Reflection;
using System.Security.Principal;
using System.Net.Sockets;

namespace RainbowIIS
{
    public class Program
    {
        private static System.Timers.Timer logTimer;
        private const uint ENABLE_QUICK_EDIT_MODE = 0x0040;
        private const int STD_INPUT_HANDLE = -10;

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    webBuilder.UseSetting(WebHostDefaults.DetailedErrorsKey, "true");
                    webBuilder.UseUrls("http://*:0-65535");
                })
                .UseWindowsService(options =>
                {
                    options.ServiceName = Assembly.GetEntryAssembly().GetName().Name;
                });

        public static void Main(string[] args)
        {
            if (!IsRunningAsAdministrator())
            {
                RestartAsAdministrator(args);
                return;
            }

            AppDomain.CurrentDomain.ProcessExit += new EventHandler(OnProcessExit);
            Console.CancelKeyPress += new ConsoleCancelEventHandler(OnCancelKeyPress);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                DisableCloseButton();
            }

            logTimer = new System.Timers.Timer(1800000);
            logTimer.Elapsed += OnLogTimerElapsed;
            logTimer.AutoReset = true;
            logTimer.Enabled = true;

            DisableQuickEditMode();

            bool shouldAddFirewallRule = true;
            if (shouldAddFirewallRule)
            {
                AddFirewallRule();
            }

            Task.Run(async () =>
            {
                int attempt = 0;
                while (true)
                {
                    try
                    {
                        var host = CreateHostBuilder(args).Build();
                        OpenBrowser("http://localhost");
                        LogServerStart();

                        host.Start();

                        await CheckOpenPortsAsync();

                        host.WaitForShutdown();
                        break;
                    }
                    catch (Exception ex)
                    {
                        attempt++;

                        Console.WriteLine($"[ERROR - {DateTime.Now}] Server crashed. Attempt: {attempt}");
                        Console.WriteLine($"Exception Type: {ex.GetType().Name}");
                        Console.WriteLine($"Message: {ex.Message}");
                        Console.WriteLine($"Stack Trace: {ex.StackTrace}");

                        int delay = CalculateBackoffDelay(attempt);
                        Console.WriteLine($"Waiting {delay}ms before next attempt...");

                        Thread.Sleep(delay);
                    }
                }
            }).GetAwaiter().GetResult();

            Thread.Sleep(Timeout.Infinite);
        }

        private static bool IsRunningAsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static void RestartAsAdministrator(string[] args)
        {
            var exeName = Process.GetCurrentProcess().MainModule.FileName;
            var startInfo = new ProcessStartInfo(exeName)
            {
                UseShellExecute = true,
                Verb = "runas",
                Arguments = string.Join(" ", args)
            };

            try
            {
                Process.Start(startInfo);
            }
            catch
            {
                Console.WriteLine("Application requires administrator privileges. Please restart the application as administrator.");
            }

            Environment.Exit(0);
        }

        internal class Worker : BackgroundService
        {
            protected override async Task ExecuteAsync(CancellationToken stoppingToken)
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    Console.WriteLine($"Worker running at: {DateTimeOffset.Now}");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            Console.WriteLine("Process exiting. Attempting to prevent shutdown...");
        }

        private static void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            Console.WriteLine("Shutdown attempt detected. Attempting to prevent...");
            e.Cancel = true;
        }

        private static void DisableCloseButton()
        {
            IntPtr handle = GetConsoleWindow();
            IntPtr sysMenu = GetSystemMenu(handle, false);

            if (handle != IntPtr.Zero)
            {
                DeleteMenu(sysMenu, SC_CLOSE, MF_BYCOMMAND);
            }
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern IntPtr GetSystemMenu(IntPtr hWnd, bool bRevert);

        [DllImport("user32.dll")]
        private static extern bool DeleteMenu(IntPtr hMenu, uint uPosition, uint uFlags);

        private const uint SC_CLOSE = 0xF060;
        private const uint MF_BYCOMMAND = 0x00000000;

        private static void LogServerStart()
        {
            Console.WriteLine($"[SERVER START - {DateTime.Now}] Server is starting...");
        }

        private static void OnLogTimerElapsed(Object source, ElapsedEventArgs e)
        {
            Console.WriteLine($"[LOG - {DateTime.Now}] Server running. Next log in 30 minutes.");

            if (DateTime.Now.Minute == 0)
            {
                Console.WriteLine($"[HOURLY LOG - {DateTime.Now}] Additional details...");
            }
        }
        private static void DisableQuickEditMode()
        {
            IntPtr consoleHandle = GetStdHandle(STD_INPUT_HANDLE);
            uint consoleMode;
            GetConsoleMode(consoleHandle, out consoleMode);
            consoleMode &= ~ENABLE_QUICK_EDIT_MODE;
            SetConsoleMode(consoleHandle, consoleMode);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        private static void OpenBrowser(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch (Exception)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }
        }

        private static void AddFirewallRule()
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "advfirewall firewall add rule name=\"Open All Ports\" dir=in action=allow protocol=TCP localport=0-65535",
                Verb = "runas"
            };

            try
            {
                using var process = Process.Start(startInfo);
                process?.WaitForExit();

                Console.WriteLine($"[FIREWALL RULES APPLIED - {DateTime.Now}] All ports open from range 0-65535. Run with caution. Server entering regular operation checks.");

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR - {DateTime.Now}] Failed to apply firewall rules. Exception: {ex.Message}");
            }
        }

        private static async Task CheckOpenPortsAsync()
        {
            int[] portsToCheck = { 80, 443, 8080 };
            var tasks = new List<Task>();

            foreach (int port in portsToCheck)
            {
                tasks.Add(CheckPortAsync("localhost", port, AddressFamily.InterNetwork));
                tasks.Add(CheckPortAsync("localhost", port, AddressFamily.InterNetworkV6));
            }

            await Task.WhenAll(tasks);
        }

        private static async Task CheckPortAsync(string host, int port, AddressFamily addressFamily)
        {
            using (var client = new TcpClient(addressFamily))
            {
                try
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    await client.ConnectAsync(host, port);
                    watch.Stop();

                    var connectionType = addressFamily == AddressFamily.InterNetwork ? "IPv4" : "IPv6";
                    Console.WriteLine($"[PORT CHECK - {DateTime.Now}] {connectionType} Port {port} is open (firewall allows and service is listening). Connection established in {watch.ElapsedMilliseconds} ms.");
                }
                catch (SocketException ex)
                {
                    var connectionType = addressFamily == AddressFamily.InterNetwork ? "IPv4" : "IPv6";
                    Console.WriteLine($"[PORT CHECK - {DateTime.Now}] {connectionType} Firewall allows, but no service is listening on port {port}. Error: {ex.SocketErrorCode}");
                }
                catch (Exception ex)
                {
                    var connectionType = addressFamily == AddressFamily.InterNetwork ? "IPv4" : "IPv6";
                    Console.WriteLine($"[PORT CHECK - {DateTime.Now}] {connectionType} Error checking port {port}: {ex.Message}");
                }
            }
        }

        private static void LogException(Exception ex)
        {
            Console.WriteLine($"Server crashed with exception: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
        }

        private static int CalculateBackoffDelay(int attempt)
        {
            return (int)Math.Min(Math.Pow(2, attempt) * 1000, 30000);
        }
    }
}
