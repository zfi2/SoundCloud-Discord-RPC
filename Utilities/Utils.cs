using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Windows.Forms;

// Utils.cs
public class Utils
{
    // !!! FOR TESTING PURPOSES ONLY !!!
    // !!! This will reveal your SoundCloud details in the console in plain text !!!
    public static bool showDebugInfo = false;

    public static string settingsFile = "settings.json";
    public static string clientIdJson = "soundcloud_client_id";
    public static string authTokenJson = "soundcloud_auth_token";
    public static string discordAppIdJson = "discord_application_id";
    public static string updateIntervalSecondsJson = "update_interval_seconds";

    public static string _clientId;
    public static string _authToken;
    public static string _discordAppId;
    public static string _updateIntervalSeconds;

    public static HttpClient _httpClient;

    public static string EncryptString(string plainText)
    {
        /*
        DataProtectionScope.CurrentUser - The protected data is associated with the current user. 
        Only threads running under the current user context can unprotect the data.

        DataProtectionScope.LocalMachine - The protected data is associated with the machine context. 
        Any process running on the computer can unprotect data. 
        This enumeration value is usually used in server-specific applications that run on a server where untrusted users are not allowed access.
        */
        byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] encryptedBytes = ProtectedData.Protect(plainTextBytes, null, DataProtectionScope.CurrentUser);

        return Convert.ToBase64String(encryptedBytes);
    }

    public static string DecryptString(string encryptedText)
    {
        byte[] encryptedBytes = Convert.FromBase64String(encryptedText);
        byte[] decryptedBytes = ProtectedData.Unprotect(encryptedBytes, null, DataProtectionScope.CurrentUser);

        return Encoding.UTF8.GetString(decryptedBytes);
    }

    public static (string clientId, string authToken, string discordAppId, string updateIntervalSeconds) DecryptSettings(string encryptedSettings)
    {
        try
        {
            var settings = JObject.Parse(encryptedSettings);

            string clientId = Utils.DecryptString(settings[clientIdJson].ToString());
            string authToken = Utils.DecryptString(settings[authTokenJson].ToString());
            string discordAppId = Utils.DecryptString(settings[discordAppIdJson].ToString());
            string updateIntervalSeconds = settings[updateIntervalSecondsJson].ToString();

            return (clientId, authToken, discordAppId, updateIntervalSeconds);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error decrypting settings");
            throw;
        }
    }

    public static void CreateSettingsFile(bool useRetrieved, string retrievedAuthToken = null, string retrievedClientId = null)
    {
        /* Don't change this unless you know what you're doing! */
        string discordAppId = Utils.EncryptString("1270073214063743036");

        /* This isn't really ideal, but it's the only way for now, 
        * SoundCloud API key requests have been disabled for like 2 years now 
        * Increase this in the settings file if you're getting rate limited
        * https://soundcloud.com/you/apps/new
        */
        int updateIntervalSeconds = 5;

        if (useRetrieved)
        {
            if (retrievedAuthToken != null && retrievedClientId != null)
            {
                if (retrievedAuthToken.Length < 32 || retrievedClientId.Length < 16)
                {
                    Log.Error("Invalid auth token or client ID.");
                    return;
                }
            }

            var settings = new JObject
            {
                [clientIdJson] = Utils.EncryptString(retrievedClientId),
                [authTokenJson] = Utils.EncryptString(retrievedAuthToken),
                [discordAppIdJson] = discordAppId,
                [updateIntervalSecondsJson] = updateIntervalSeconds
            };
            File.WriteAllText(settingsFile, settings.ToString());
        }
        else
        {
            Log.Information("First-time manual setup required.");
            Console.WriteLine("Please enter your SoundCloud client ID:");
            string clientId = Console.ReadLine();

            Console.WriteLine("Please enter your SoundCloud auth token:");
            string authToken = Console.ReadLine();

            if (authToken.Length < 32 || clientId.Length < 16)
            {
                Log.Error("Invalid auth token or client ID length.");
                return;
            }

            var settings = new JObject
            {
                [clientIdJson] = Utils.EncryptString(clientId),
                [authTokenJson] = Utils.EncryptString(authToken),
                [discordAppIdJson] = discordAppId,
                [updateIntervalSecondsJson] = updateIntervalSeconds
            };
            File.WriteAllText(settingsFile, settings.ToString());
        }

        Log.Information("Settings saved and encrypted.");
    }

    public static bool IsAdministrator()
    {
        return (new WindowsPrincipal(WindowsIdentity.GetCurrent()))
                  .IsInRole(WindowsBuiltInRole.Administrator);
    }

    // Sketchy... lol
    public static void EnsureDisableSystemProxy()
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "reg.exe",
            Arguments = "add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings\" /v ProxyEnable /t REG_DWORD /d 0 /f & " +
                        "reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings\" /v ProxyServer /t REG_SZ /d \"\" /f",
            Verb = "runas",
            UseShellExecute = true,
            CreateNoWindow = true
        };

        try
        {
            Process.Start(psi).WaitForExit();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            MessageBox.Show("Administrative privileges are required to disable the system proxy.", "Elevation Required", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
