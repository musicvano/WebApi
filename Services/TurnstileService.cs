using System.Text.Json;
using Microsoft.Extensions.Options;

namespace WebApi.Services
{
    public class TurnstileService(HttpClient httpClient, IOptions<TurnstileOptions> options)
    {
        private const string VerifyUrl =
            "https://challenges.cloudflare.com/turnstile/v0/siteverify";

        private readonly HttpClient httpClient = httpClient;
        private readonly TurnstileOptions options = options.Value;

        public async Task<bool> VerifyAsync(
            string token, string? remoteIp, CancellationToken cancellationToken)
        {
            var fields = new Dictionary<string, string>
            {
                ["secret"] = options.SecretKey,
                ["response"] = token
            };
            if (!string.IsNullOrEmpty(remoteIp))
            {
                fields["remoteip"] = remoteIp;
            }
            using var content = new FormUrlEncodedContent(fields);
            using var response = await httpClient.PostAsync(VerifyUrl, content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }
            using var json = JsonDocument.Parse(
                await response.Content.ReadAsStringAsync(cancellationToken));
            return json.RootElement.TryGetProperty("success", out var success)
                && success.ValueKind == JsonValueKind.True;
        }
    }
}
