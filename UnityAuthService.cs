using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;

public class UnityAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UnityAuthService> _logger;

    private string? _accessToken;
    private DateTime _expiresAtUtc;

    // Hard-coded credentials - Double check these for leading/trailing spaces!
    private const string ClientId = "d04df0da-5968-459d-aa33-3946402f637d";
    private const string ClientSecret = "KzYQh8nvk5LjYqKxurPi9lgSDqC-JAJU";
    private const string ProjectId = "3f735ce7-0797-4b51-98c9-e7abfcb3b585";
    private const string TokenUrl = "https://services.api.unity.com/auth/v1/token-exchange";

    public UnityAuthService(HttpClient httpClient, ILogger<UnityAuthService> logger)
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

        _logger.LogInformation("Requesting Unity Services token exchange for Project: {ProjectId}", ProjectId);

        // Ensure ProjectId is used in the query string
        var requestUrl = $"{TokenUrl}?projectId={ProjectId.Trim()}";
        var request = new HttpRequestMessage(HttpMethod.Post, requestUrl);

        // 1. Properly format Basic Auth
        // Unity docs recommend UTF8 for the Base64 conversion
        var rawCredentials = $"{ClientId.Trim()}:{ClientSecret.Trim()}";
        var authHeaderValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawCredentials));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeaderValue);

        // 2. Body - The API expects a JSON object. 
        // Providing scopes as p:projectId is the standard for service-to-service auth.
        var body = new
        {
            scopes = new[] { $"p:{ProjectId.Trim()}" }
        };

        var jsonPayload = JsonSerializer.Serialize(body);
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            // Log the Request ID if available to help Unity Support if needed
            _logger.LogError("Unity Auth Failed. Status: {Status}, Content: {Content}", response.StatusCode, responseContent);
            throw new Exception($"Unity Authentication Failed (401). Check if the Key is Active in the Dashboard.");
        }

        var tokenResponse = JsonSerializer.Deserialize<UnityTokenResponse>(responseContent);

        _accessToken = tokenResponse?.accessToken;
        _expiresAtUtc = DateTime.UtcNow.AddMinutes(50); // Refresh slightly before 1 hour expiry

        _logger.LogInformation("Successfully obtained Unity Services token.");
        return _accessToken ?? throw new Exception("Token was null in successful response.");
    }

    private class UnityTokenResponse
    {
        public string accessToken { get; set; } = default!;
    }
}