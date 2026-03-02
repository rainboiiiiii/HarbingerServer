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

            // Ensure the environment GUID is present
            _httpClient.DefaultRequestHeaders.Remove("Unity-Environment");
            _httpClient.DefaultRequestHeaders.Add("Unity-Environment", "12cb99a8-fc59-4778-8128-e19c6538ebb2");

            // Custom ID payload structure
            var payload = new
            {
                externalId = localUserId
            };

            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

            // The specific "Server" endpoint for backend-to-backend Custom ID registration
            // This is the V1 Player Auth Server API
            var endpoint = $"v1/projects/{_projectId}/authentication/server/custom-id";

            var response = await _httpClient.PostAsync(endpoint, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Unity Player Creation Failed: {Status} - {Content}", response.StatusCode, responseContent);
                return null;
            }

            // Unity returns a 'userId' or 'id' depending on the specific Auth endpoint
            // The Player Auth Server API usually returns an object with 'userId'
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

// Updated Helper class to match the Player Auth Server response format
public class UnityPlayerAuthResponse
{
    [JsonProperty("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonProperty("id")] // Fallback in case of variant API version
    public string Id { set { UserId = value; } }
}