using System.Text;

namespace UnityMCP.Editor
{
    public static partial class JsonHelper
    {
        internal static string FormatResponse(string id, bool ok, string data, string error)
        {
            var sb = new StringBuilder();
            sb.Append("{\"id\":\"").Append(EscapeJson(id ?? "")).Append("\"");
            sb.Append(",\"ok\":").Append(ok ? "true" : "false");
            if (ok)
                sb.Append(",\"data\":\"").Append(EscapeJson(data ?? "")).Append("\"");
            else
                sb.Append(",\"err\":\"").Append(EscapeJson(error ?? "")).Append("\"");
            sb.Append("}");
            return sb.ToString();
        }

        internal static string FormatFileResponse(string id, string filePath)
        {
            var sb = new StringBuilder();
            sb.Append("{\"id\":\"").Append(EscapeJson(id ?? "")).Append("\"");
            sb.Append(",\"ok\":true,\"data\":\"\",\"file\":\"");
            sb.Append(EscapeJson(filePath)).Append("\"}");
            return sb.ToString();
        }

        internal static string FormatFileResponseWithData(string id, string filePath, string data)
        {
            var sb = new StringBuilder();
            sb.Append("{\"id\":\"").Append(EscapeJson(id ?? "")).Append("\"");
            sb.Append(",\"ok\":true,\"data\":\"").Append(EscapeJson(data ?? "")).Append("\"");
            sb.Append(",\"file\":\"").Append(EscapeJson(filePath)).Append("\"}");
            return sb.ToString();
        }

        internal static string FormatBusyResponse(string id, string message, int retryMs)
        {
            return $"{{\"id\":\"{EscapeJson(id)}\",\"ok\":false,\"err\":\"{EscapeJson(message)}\",\"retry\":{retryMs}}}";
        }
    }
}
