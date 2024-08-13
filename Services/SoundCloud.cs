using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Threading;
using System.Threading.Tasks;
using SoundCloudDiscordRPCConsole;
using DiscordRPC;

// SoundCloud.cs
namespace SoundCloudAPI
{
    internal class SoundCloud
    {
        public static (string title, string artist, string artworkUrl, string songUrl) _lastTrack = (null, null, null, null);
        public static async Task CheckTrackAsync()
        {
            try
            {
                var (title, artist, artworkUrl, songUrl) = await GetCurrentTrackAsync(Program._cts.Token).ConfigureAwait(false);

                if (title != _lastTrack.title || artist != _lastTrack.artist)
                {
                    await RPC.UpdateDiscordActivityAsync(title, artist, artworkUrl, songUrl).ConfigureAwait(false);
                    _lastTrack = (title, artist, artworkUrl, songUrl);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating presence");
            }
        }

        public static (string title, string artist, string artworkUrl, string songUrl) ParseTrackInfo(string content)
        {
            var json = JObject.Parse(content);

            if (json["collection"] is JArray tracks && tracks.Count > 0)
            {
                var track = tracks[0]["track"];
                string title = track["title"]?.ToString();
                string artist = track["user"]?["username"]?.ToString();
                string artworkUrl = track["artwork_url"]?.ToString();
                string songUrl = track["permalink_url"]?.ToString();

                artworkUrl = string.IsNullOrEmpty(artworkUrl) ? "missing_artwork" : artworkUrl.Replace("-large.jpg", "-t500x500.jpg");

                if (title != _lastTrack.title)
                {
                    Log.Information("Retrieved track: {Title} by {Artist}", title, artist);
                }

                return (title, artist, artworkUrl, songUrl);
            }
            else
            {
                Log.Information("No tracks found");
                return (null, null, null, null);
            }
        }

        public static async Task<(string title, string artist, string artworkUrl, string songUrl)> GetCurrentTrackAsync(CancellationToken cancellationToken)
        {
            string builtUrl = $"https://api-v2.soundcloud.com/me/play-history/tracks?client_id={Utils._clientId}&limit=1&offset=0&linked_partitioning=0&app_version=1722430138&app_locale=en&auth_token={Utils._authToken}";

            try
            {
                var response = await Utils._httpClient.GetAsync(builtUrl, cancellationToken).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return SoundCloud.ParseTrackInfo(content);
                }
                else
                {
                    Log.Warning("API request failed with status code: {StatusCode}", response.StatusCode);
                    return (null, null, null, null);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error getting current track");
                return (null, null, null, null);
            }
        }
    }
}