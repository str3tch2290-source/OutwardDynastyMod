using System;
using System.Text;

namespace OutwardDynasty
{
    /// <summary>
    /// Join code codec used to avoid raw IP:Port entry in-game.
    /// Companion generates a code that encodes "host:port" in URL-safe base64 without padding.
    /// Format: DYNASTYID + "." + base64url(host:port)
    /// </summary>
    internal static class JoinCodeCodec
    {
        public static string Make(string dynastyId, string host, int port)
        {
            dynastyId = (dynastyId ?? "").Trim();
            if (dynastyId.Length == 0) dynastyId = "UNKNOWN";

            string payload = $"{host}:{port}";
            string b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            return dynastyId + "." + b64;
        }

        public static bool TryParse(string joinCode, out string dynastyId, out string host, out int port)
        {
            dynastyId = "";
            host = "";
            port = 0;

            if (string.IsNullOrWhiteSpace(joinCode)) return false;
            joinCode = joinCode.Trim();

            int dot = joinCode.IndexOf('.');
            if (dot <= 0 || dot >= joinCode.Length - 1) return false;

            dynastyId = joinCode.Substring(0, dot).Trim();
            string b64 = joinCode.Substring(dot + 1).Trim()
                .Replace('-', '+').Replace('_', '/');

            // pad
            switch (b64.Length % 4)
            {
                case 2: b64 += "=="; break;
                case 3: b64 += "="; break;
            }

            byte[] bytes;
            try { bytes = Convert.FromBase64String(b64); }
            catch { return false; }

            string payload = Encoding.UTF8.GetString(bytes);
            int colon = payload.LastIndexOf(':');
            if (colon <= 0 || colon >= payload.Length - 1) return false;

            host = payload.Substring(0, colon);
            if (!int.TryParse(payload.Substring(colon + 1), out port)) return false;

            return dynastyId.Length > 0 && host.Length > 0 && port > 0 && port < 65536;
        }
    }
}
