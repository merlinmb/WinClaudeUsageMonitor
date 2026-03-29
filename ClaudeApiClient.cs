using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace ClaudeUsageBar
{
    public class ClaudeApiClient
    {
        private readonly string apiKey;
        private static readonly string endpoint = "https://api.anthropic.com/v1/usage";

        public ClaudeApiClient(string apiKey)
        {
            this.apiKey = apiKey;
        }

        public async Task<JObject?> GetUsageAsync()
        {
            using (var client = new HttpClient())
            {
                // Anthropic API requires x-api-key and anthropic-version headers
                client.DefaultRequestHeaders.Add("x-api-key", apiKey);
                client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                // Also send Bearer for any proxies/wrappers that expect it
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                var resp = await client.GetAsync(endpoint);
                System.Diagnostics.Debug.WriteLine($"[DEBUG] API response: {(int)resp.StatusCode} {resp.ReasonPhrase}");
                if (!resp.IsSuccessStatusCode)
                {
                    var errBody = await resp.Content.ReadAsStringAsync();
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Error body: {errBody}");
                    return null;
                }
                var json = await resp.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Response JSON: {json}");
                return JObject.Parse(json);
            }
        }
    }
}
