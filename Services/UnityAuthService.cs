using System.Net.Http.Headers;
using System.Text.Json;

public class UnityAuthService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly ILogger<UnityAuthService> _logger;

    private string? _accessToken;
    private DateTime _expiresAtUtc;

    public UnityAuthService(
        HttpClient httpClient,
        IConfiguration config,
        ILogger<UnityAuthService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        if (!string.IsNullOrEmpty(_accessToken) &&
            DateTime.UtcNow < _expiresAtUtc)
        {
            return _accessToken;
        }

        _logger.LogInformation("Requesting new Unity OAuth access token...");

        var request = new HttpRequestMessage(HttpMethod.Post,
            _config["UnityAuth:TokenUrl"]);

        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "grant_type", "client_credentials" },
            { "client_id", _config["UnityAuth:ClientId"]! },
            { "client_secret", _config["UnityAuth:ClientSecret"]! }
        });

        request.Content = content;

        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Unity OAuth failed: {Status} {Body}",
                response.StatusCode, responseBody);
            throw new Exception("Failed to authenticate with Unity.");
        }

        var tokenResponse = JsonSerializer.Deserialize<UnityTokenResponse>(responseBody);

        _accessToken = tokenResponse!.access_token;

        // Refresh 1 minute before expiry
        _expiresAtUtc = DateTime.UtcNow.AddSeconds(tokenResponse.expires_in - 60);

        _logger.LogInformation("Successfully obtained Unity access token.");

        return _accessToken!;
    }

    private class UnityTokenResponse
    {
        public string access_token { get; set; } = default!;
        public int expires_in { get; set; }
        public string token_type { get; set; } = default!;
    }
}