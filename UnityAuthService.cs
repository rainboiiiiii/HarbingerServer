using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;

public class UnityAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UnityAuthService> _logger;

    private string? _accessToken;
    private DateTime _expiresAtUtc;

    // Hard-coded credentials
    private const string ClientId = "d04df0da-5968-459d-aa33-3946402f637d";
    private const string ClientSecret = "KzYQh8nvk5LjYqKxurPi9lgSDqC-JAJU";
    private const string ProjectId = "3f735ce7-0797-4b51-98c9-e7abfcb3b585";
    private const string TokenUrl = "https://services.api.unity.com/auth/v1/token-exchange";

    public UnityAuthService(
        HttpClient httpClient,
        ILogger<UnityAuthService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "UnityGameBackend-Harbinger");
        }
    }

    public async Task<string> GetAccessTokenAsync()
    {
        if (!string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _expiresAtUtc)
        {
            return _accessToken;
        }

        _logger.LogInformation("Requesting Unity Services token exchange...");

        // ProjectId MUST be a query parameter
        var requestUrl = $"{TokenUrl}?projectId={ProjectId}";
        var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

        // Fixed the casing here to match the 'private const' above
        var authString = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{ClientId}:{ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authString);

        var body = new
        {
            scopes = new[] { $"p:{ProjectId}" }
        };

        var jsonPayload = JsonSerializer.Serialize(body);
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Unity Token Exchange failed: {Status} {Body}", response.StatusCode, responseBody);
            throw new Exception($"Failed to authenticate with Unity Services. Status: {response.StatusCode}");
        }

        var tokenResponse = JsonSerializer.Deserialize<UnityTokenResponse>(responseBody);

        if (tokenResponse == null || string.IsNullOrEmpty(tokenResponse.accessToken))
        {
            throw new Exception("Unity returned an empty or invalid token response.");
        }

        _accessToken = tokenResponse.accessToken;
        _expiresAtUtc = DateTime.UtcNow.AddMinutes(55);

        _logger.LogInformation("Successfully obtained Unity Services token.");

        return _accessToken;
    }

    private class UnityTokenResponse
    {
        public string accessToken { get; set; } = default!;
    }
}