using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Text;

public class UnityIdentityService
{
    private readonly HttpClient _httpClient;
    private readonly UnityAuthService _authService;
    private readonly string _projectId;
    private readonly ILogger<UnityIdentityService> _logger;

    public UnityIdentityService(HttpClient httpClient, UnityAuthService authService, IConfiguration config, ILogger<UnityIdentityService> logger)
    {
        _httpClient = httpClient;
        _authService = authService;
        _logger = logger;

        // Hardcoding for immediate testing
        _projectId = "3f735ce7-0797-4b51-98c9-e7abfcb3b585";

        // IMPORTANT: The Player Auth API uses a different subdomain than the general Admin API
        _httpClient.BaseAddress = new Uri("https://player-auth.services.api.unity.com/");
    }

    public async Task<string?> CreateUnityPlayerAsync(string localUserId)
    {
        try
        {
            var accessToken = await _authService.GetAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // ADJUSTMENT 1: Use 'UnityEnvironment' (no hyphen) as per image_193151.png
            _httpClient.DefaultRequestHeaders.Remove("UnityEnvironment");
            _httpClient.DefaultRequestHeaders.Add("UnityEnvironment", "production");

            // ADJUSTMENT 2: Ensure payload matches image_193151.png
            var payload = new { externalId = localUserId };
            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

            // The specific "Server" endpoint for backend-to-backend Custom ID registration
            var endpoint = $"v1/projects/{_projectId}/authentication/server/custom-id";

            var response = await _httpClient.PostAsync(endpoint, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                // If you still get 403 here, double-check that the "Player Authentication Token Issuer" 
                // role is assigned to your Service Account in the dashboard.
                _logger.LogError("Unity Player Creation Failed: {Status} - {Content}", response.StatusCode, responseContent);
                return null;
            }

            // ADJUSTMENT 3: Map to the documentation's response format (idToken)
            var result = JsonConvert.DeserializeObject<UnityPlayerAuthResponse>(responseContent);

            _logger.LogInformation("Successfully synced Unity Player ID: {UnityId}", result?.UserId);

            return result?.UserId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception creating Unity player for local ID {Id}", localUserId);
            return null;
        }
    }
}

// Updated Helper class to match image_193151.png response
public class UnityPlayerAuthResponse
{
    // The server API returns an idToken, which acts as the unique identifier
    [JsonProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty("idToken")]
    public string IdToken { set { UserId = value; } }

    [JsonProperty("sessionToken")]
    public string? SessionToken { get; set; }
}