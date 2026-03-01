using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public class UnityAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<UnityAuthService> _logger;
    private string? _accessToken;

    // 1. MUST BE THE 'KEY ID' from image_6dd884.png (starting with 4698...)
    private const string ClientId = "2725d93b-a83d-45ff-bb47-0fbbd9d324a5";

    // 2. MUST BE THE 'SECRET' shown only ONCE when you created the key above
    private const string ClientSecret = "Gsgx6hddm6Da6mtw-5PkUgvJbffWH23o";

    // 3. MUST BE THE 'PROJECT ID' from Project Settings > General
    private const string ProjectId = "3f735ce7-0797-4b51-98c9-e7abfcb3b585";

    public UnityAuthService(HttpClient httpClient, ILogger<UnityAuthService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenAsync()
    {
        if (!string.IsNullOrEmpty(_accessToken)) return _accessToken;

        _logger.LogInformation("Attempting token exchange...");

        // Project ID is required in the URL
        var url = $"https://services.api.unity.com/auth/v1/token-exchange?projectId={ProjectId}";

        var request = new HttpRequestMessage(HttpMethod.Post, url);

        // Basic Auth: base64(ClientId:ClientSecret)
        var authBytes = Encoding.UTF8.GetBytes($"{ClientId}:{ClientSecret}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

        // Sending an empty JSON object {} is the simplest valid body
        request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Auth Failed: {Body}", body);
            throw new Exception("Unity rejected the Key ID or Secret.");
        }

        // Quick manual parse to avoid deserialization issues
        using var doc = JsonDocument.Parse(body);
        _accessToken = doc.RootElement.GetProperty("accessToken").GetString();

        return _accessToken!;
    }
}