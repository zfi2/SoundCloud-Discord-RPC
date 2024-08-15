using Serilog;
using System;
using System.Linq;
using System.Threading.Tasks;
using Titanium.Web.Proxy.Models;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using System.Threading;
using System.Net;

// Proxy.cs
namespace ProxyService
{
    internal class Proxy
    {
        public static ManualResetEvent requestFoundEvent = new ManualResetEvent(false);

        public static string capturedClientId = null;
        public static string capturedAuthorization = null;
        public static bool captureEnabled = false;

        public static Task DeployProxyServer()
        {
            captureEnabled = true;

            try
            {
                var proxyServer = new ProxyServer();
                var explicitEndPoint = new ExplicitProxyEndPoint(IPAddress.Any, 8000, true);

                proxyServer.CertificateManager.RemoveTrustedRootCertificateAsAdmin();
                proxyServer.CertificateManager.ClearRootCertificate();
                proxyServer.CertificateManager.CertificateStorage.Clear();

                // Set up the proxy
                proxyServer.CertificateManager.CreateRootCertificate(true);
                proxyServer.CertificateManager.TrustRootCertificate(true);

                proxyServer.BeforeRequest += OnRequest;
                proxyServer.AddEndPoint(explicitEndPoint);

                proxyServer.Start();

                // Set the proxy as the system proxy
                proxyServer.SetAsSystemProxy(explicitEndPoint, ProxyProtocolType.AllHttp);

                Log.Information($"Proxy server started on {explicitEndPoint.IpAddress}:8000");
                Log.Warning("System proxy has been set. You WILL NEED TO restart your browser!");
                Log.Information("Waiting for SoundCloud API requests, visit/refresh the SoundCloud website in your browser to proceed.");

                requestFoundEvent.WaitOne();

                Log.Information("SoundCloud API details retrieved and saved successfully!");

#if UNSAFE
                if (Utils.showDebugInfo)
                {
                    Log.Warning($"client_id: {capturedClientId}");
                    Log.Warning($"auth_token: {capturedAuthorization}");
                }
#endif

                // Clean up
                proxyServer.Stop();
                proxyServer.DisableAllSystemProxies();
                proxyServer.Dispose();

                Log.Information("Proxy server stopped and system proxy settings restored.");
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Proxy server fatal error");
            }

            return Task.CompletedTask;
        }

        public static Task OnRequest(object sender, SessionEventArgs e)
        {
            var request = e.HttpClient.Request;

            if (request.RequestUri.Host.Contains("api-v2.soundcloud.com"))
            {
                capturedClientId = request.RequestUri.Query
                    .Split('&')
                    .FirstOrDefault(q => q.StartsWith("client_id="))
                    ?.Split('=')[1];

                var authHeader = request.Headers.FirstOrDefault(h => h.Name.ToLower() == "authorization");

                // The Authorization header contains "OAuth" at the beginning, so we remove it and the space that follows
                capturedAuthorization = authHeader?.Value.Replace("OAuth ", "");

                if (capturedClientId != null && capturedAuthorization != null)
                {
                    requestFoundEvent.Set();
                }
            }
            return Task.CompletedTask;
        }
    }
}