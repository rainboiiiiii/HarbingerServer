using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;

namespace GameBackend.Api.Services;

public class UnityIdentityService
{
    private readonly HttpClient _httpClient;
    private readonly UnityAuthService _authService;
    private readonly string _projectId;

    public UnityIdentityService(HttpClient httpClient, UnityAuthService authService, IConfiguration config)
    {
        _httpClient = httpClient;
        _authService = authService;
        _projectId = config["Unity:ProjectId"] ?? "";
        _httpClient.BaseAddress = new Uri("https://services.api.unity.com/auth/v1/");
    }

    public async Task<string?> CreateUnityPlayerAsync(string localUserId)
    {
        var accessToken = await _authService.GetAccessTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // We link your local Mongo ID as an "External ID" in Unity
        var payload = new { externalId = localUserId };
        var content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync($"projects/{_projectId}/players", content);

        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonConvert.DeserializeObject<dynamic>(json);
        return result?.id; // This is the GUID we need for Matchmaking
    }
}