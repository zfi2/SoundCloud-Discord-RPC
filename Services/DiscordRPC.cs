using System.Threading.Tasks;
using DiscordRichPresence;
using Serilog;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

// DiscordRPC.cs
namespace DiscordRPC
{
    internal class RPC
    {
        public static Presence _discord;
        public static async Task UpdateDiscordActivityAsync(string title, string artist, string artworkUrl, string songUrl)
        {
            var activity = new JObject
            {
                ["type"] = 2,
                ["details"] = title,
                ["state"] = $"by {artist}",
                ["assets"] = new JObject
                {
                    ["large_image"] = artworkUrl ?? "missing_artwork",
                    ["large_text"] = "made with <3 by lain",
                    ["small_image"] = "soundcloud_logo",
                    ["small_text"] = title,
                },
                ["buttons"] = new JArray
                {
                    new JObject
                    {
                        ["label"] = "Open on SoundCloud",
                        ["url"] = songUrl,
                    }
                }
            };

            string activityJson = activity.ToString(Formatting.None);

            // If you want to view the retrieved SoundCloud track info in the JSON format, you can remove this debug check
            if (Utils.showDebugInfo)
                Log.Information("Attempting to update Discord activity: {ActivityJson}", activityJson);
            else
                Log.Information("Attempting to update Discord activity...");

            try
            {
                await Task.Run(() => _discord.Set(activityJson)).ConfigureAwait(false);
                Log.Information("Successfully updated Discord activity!");
            }
            catch (PresenceException ex)
            {
                Log.Error(ex, "Failed to update Discord activity");
            }
        }
    }
}
