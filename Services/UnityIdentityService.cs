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
        _projectId = config["Unity:ProjectId"] ?? "";
        // Note: Using the base address for the Identity Admin API
        _httpClient.BaseAddress = new Uri("https://services.api.unity.com/auth/v1/");
    }

    public async Task<string?> CreateUnityPlayerAsync(string localUserId)
    {
        try
        {
            var accessToken = await _authService.GetAccessTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            // REQUIRED: Specify the environment (Production vs Development)
            // Without this, Unity doesn't know where to "create" the player
            _httpClient.DefaultRequestHeaders.Remove("Unity-Environment");
            _httpClient.DefaultRequestHeaders.Add("Unity-Environment", "production");

            var payload = new { externalId = localUserId };
            var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"projects/{_projectId}/players", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Unity Player Creation Failed: {Status} - {Content}", response.StatusCode, responseContent);
                return null;
            }

            // Using a concrete class for deserialization is safer than dynamic
            var result = JsonConvert.DeserializeObject<UnityPlayerResponse>(responseContent);
            return result?.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception creating Unity player for local ID {Id}", localUserId);
            return null;
        }
    }
}

// Helper class for parsing
public class UnityPlayerResponse
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;
}