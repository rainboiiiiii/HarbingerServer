using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;

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

        _httpClient.DefaultRequestHeaders.Add("User-Agent", "UnityGameBackend-Harbinger");
    }

    public async Task<string> GetAccessTokenAsync()
    {
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _expiresAtUtc)
        {
            return _accessToken;
        }

        _logger.LogInformation("Requesting Unity Services token exchange...");

        var clientId = _config["d04df0da-5968-459d-aa33-3946402f637d"]!;
        var clientSecret = _config["KzYQh8nvk5LjYqKxurPi9lgSDqC-JAJU"]!;
        var projectId = _config["3f735ce7-0797-4b51-98c9-e7abfcb3b585"]!;

        var request = new HttpRequestMessage(HttpMethod.Post, _config["UnityAuth:TokenUrl"]);

        // 1. Basic Auth Header
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        // 2. JSON Body (REQUIRED for token-exchange)
        // We provide the project scope so the token actually has permission to matchmake
        var body = new
        {
            scopes = new[] { $"p:{projectId}" }
        };

        var jsonPayload = JsonSerializer.Serialize(body);
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Unity Token Exchange failed: {Status} {Body}", response.StatusCode, responseBody);
            throw new Exception("Failed to authenticate with Unity Services.");
        }

        // 3. Deserialize (Note the property name change for this endpoint)
        var tokenResponse = JsonSerializer.Deserialize<UnityTokenResponse>(responseBody);

        _accessToken = tokenResponse!.accessToken;
        // Manual expiry set to 55 mins (tokens usually last 1 hour)
        _expiresAtUtc = DateTime.UtcNow.AddMinutes(55);

        _logger.LogInformation("Successfully obtained Unity Services token.");

        return _accessToken!;
    }

    // Update your response class to match the JSON returned by token-exchange
    private class UnityTokenResponse
    {
        public string accessToken { get; set; } = default!;
    }
}

    