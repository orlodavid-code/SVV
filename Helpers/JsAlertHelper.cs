// SVV/Helpers/JsAlertHelper.cs
using System.Net;
using Newtonsoft.Json; // Asegúrate de que esta línea esté presente

namespace SVV.Helpers
{
    public static class JsAlertHelper
    {
        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            try
            {
                var decoded = WebUtility.HtmlDecode(text);
                return decoded?.Replace("'", "\\'")
                              .Replace("\"", "\\\"")
                              .Replace("\n", "\\n")
                              .Replace("\r", "\\r") ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
        public static string CleanForJson(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "null";

            try
            {
                var decoded = WebUtility.HtmlDecode(text);
                return JsonConvert.SerializeObject(decoded); // Newtonsoft.Json.JsonConvert
            }
            catch
            {
                return "null";
            }
        }
    }
}