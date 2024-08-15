using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using System.Reflection;
using System.Windows.Forms;
//
using Serilog;
using DiscordRichPresence;
using SoundCloudAPI;
using ProxyService;
using DiscordRPC;

// Program.cs

namespace SoundCloudDiscordRPCConsole
{
    class Program : IDisposable
    {
        public static CancellationTokenSource _cts = new CancellationTokenSource();
        public static System.Threading.Timer _timer;

        static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .CreateLogger();

            try
            {
                if (!File.Exists(Utils.settingsFile))
                {
                    if (!Utils.IsAdministrator())
                    {
                        Log.Warning("Detected that the program isn't being ran as administrator, this could cause issues when you're running the program for the first time");
                    }
                    Log.Warning("Settings file not found. Creating new settings.");

                    Log.Information("Would you like to deploy a proxy server to automatically capture the SoundCloud API details? (y/n)");
                    string proxy_prompt = Console.ReadLine().ToLower();
                    if (proxy_prompt == "y")
                    {
                        MessageBox.Show("You will get a pop-up about installing a new certificate, you have to install it in order for the program to work.\n\n" +
                            "You will also get a pop-up about the program trying to access the firewall, and you will also have to give it access in order for it to work.\n\n" +
                            "If you don't get those pop-ups, restart the app as administrator, or try to proceed.\n\n" +
                            "This is only a first-time procedure, unless you delete the settings.json file.", "Important!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        await Task.Run(Proxy.DeployProxyServer);
                    }

                    if (Proxy.captureEnabled)
                        Utils.CreateSettingsFile(true, Proxy.capturedAuthorization, Proxy.capturedClientId);
                    else
                        Utils.CreateSettingsFile(false);
                }

                string encryptedSettings = File.ReadAllText(Utils.settingsFile);
                var (clientId, authToken, discordAppId, updateIntervalSeconds) = Utils.DecryptSettings(encryptedSettings);

// Print decrypted settings, only for debugging purposes, this will reveal your soundcloud data in the console in plain text!
// Compile in the Unsafe mode and enable showDebugInfo to view.
#if UNSAFE
                if(Utils.showDebugInfo)
                {
                    Console.Write("\n----------------------------\n");
                    Log.Warning("Decrypted soundcloud_client_id: {ClientId}", clientId);
                    Log.Warning("Decrypted soundcloud_auth_token: {AuthToken}", authToken);
                    Console.Write("----------------------------\n\n");
                }
#endif

                var program = new Program(clientId, authToken, discordAppId, updateIntervalSeconds);
                await program.RunAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application terminated unexpectedly");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public Program(string clientId, string authToken, string discordAppId, string updateIntervalSeconds)
        {
            Utils._clientId = clientId;
            Utils._authToken = authToken;
            Utils._discordAppId = discordAppId;
            Utils._updateIntervalSeconds = updateIntervalSeconds;
            Utils._httpClient = new HttpClient();
            _timer = new System.Threading.Timer(async _ => await SoundCloud.CheckTrackAsync(), null, TimeSpan.Zero, TimeSpan.FromSeconds(Convert.ToInt32(Utils._updateIntervalSeconds)));
            RPC._discord = new Presence(discordAppId);
        }

        public async Task RunAsync()
        {
            Log.Information("Starting SoundCloud Discord RPC...");

            Console.OutputEncoding = Encoding.UTF8;
            Console.CancelKeyPress += (s, e) =>
            {
                DialogResult result = MessageBox.Show("Do you want to clear the system proxy settings? If you don't do this, " +
                    "you may suddenly lose your internet connection.", "Clear system proxy settings", MessageBoxButtons.YesNo, MessageBoxIcon.Asterisk);
                if (result == DialogResult.Yes)
                {
                    Utils.EnsureDisableSystemProxy();
                }

                e.Cancel = true;
                Dispose();
            };

            Log.Information("RPC started. Press Ctrl+C to exit.");

            while (!_cts.Token.IsCancellationRequested)
            {
                await Task.Delay(1000, _cts.Token).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _cts.Cancel();
                _timer.Dispose();
                Utils._httpClient?.Dispose();
                RPC._discord?.Dispose();
            }
        }
    }
}