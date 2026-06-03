// Builds the stdin JSON envelope for a user turn.
// Pure logic: only System deps, NUnit-testable.
using System;
using System.Text;

namespace UnityMCP.Editor.Chat
{
    public static class UserTurnBuilder
    {
        /// <summary>Text-only user turn. Returns single JSON line ending with \n.</summary>
        public static string Build(string text)
        {
            var sb = new StringBuilder();
            sb.Append("{\"type\":\"user\",\"message\":{\"role\":\"user\",\"content\":[");
            AppendTextBlock(sb, text);
            sb.Append("]}}");
            sb.Append('\n');
            return sb.ToString();
        }

        /// <summary>User turn with attached PNG screenshot.</summary>
        public static string Build(string text, byte[] pngData)
        {
            if (pngData == null || pngData.Length == 0)
                return Build(text);

            var b64 = Convert.ToBase64String(pngData);
            var sb  = new StringBuilder();
            sb.Append("{\"type\":\"user\",\"message\":{\"role\":\"user\",\"content\":[");
            AppendTextBlock(sb, text);
            sb.Append(",{\"type\":\"image\",\"source\":{\"type\":\"base64\",\"media_type\":\"image/png\",\"data\":\"");
            sb.Append(b64);
            sb.Append("\"}}");
            sb.Append("]}}");
            sb.Append('\n');
            return sb.ToString();
        }

        private static void AppendTextBlock(StringBuilder sb, string text)
        {
            sb.Append("{\"type\":\"text\",\"text\":\"");
            sb.Append(JsonHelper.EscapeJson(text ?? ""));
            sb.Append("\"}");
        }
    }
}
