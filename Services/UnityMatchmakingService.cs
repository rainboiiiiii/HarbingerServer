using System.Net.Http.Headers;
using GameBackend.Api.Data;
using GameBackend.Api.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Text;

namespace GameBackend.Api.Services;

public class UnityMatchmakingService
{
    private readonly HttpClient _httpClient;
    private readonly UnityMatchmakingOptions _options;
    private readonly ILogger<UnityMatchmakingService> _logger;
    private readonly UnityAuthService _authService;

    public UnityMatchmakingService(HttpClient httpClient, IOptions<UnityMatchmakingOptions> options, ILogger<UnityMatchmakingService> logger, UnityAuthService authService)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _authService = authService;

        _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");

        // Required Project ID Header
        _httpClient.DefaultRequestHeaders.Add("X-Unity-Services-Project-Id", _options.ProjectId);

        // Required Environment ID Header (Production ID from your dashboard)
        _httpClient.DefaultRequestHeaders.Add("Unity-Environment", "12cb99a8-fc59-4778-8128-e19c6538ebb2");
    }

    public async Task<string> CreateTicketAsync(string queueName, Dictionary<string, object> attributes, List<UnityMatchmakingPlayer> players)
    {
        // 1. Refresh Auth Token
        string accessToken = await _authService.GetAccessTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // 2. Set Impersonation Header (Required for Backend-to-Backend Service Accounts)
        var firstPlayerId = players.FirstOrDefault()?.Id ?? "unknown";
        _httpClient.DefaultRequestHeaders.Remove("impersonated-user-id");
        _httpClient.DefaultRequestHeaders.Add("impersonated-user-id", firstPlayerId);

        // 3. Construct the Payload
        // We use a simplified player object to avoid property-validation errors
        var requestBody = new
        {
            queueName = queueName,
            attributes = attributes ?? new Dictionary<string, object>(),
            players = players.Select(p => new {
                id = p.Id,
                customData = new Dictionary<string, object>() // Use 'customData' instead of 'properties'
            }).ToList()
        };

        var json = JsonConvert.SerializeObject(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation("Attempting Ticket Creation for Queue: {Queue}. Body: {Json}", queueName, json);

        // 4. Send Request
        var response = await _httpClient.PostAsync("v2/tickets", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Unity Error 55 (InvalidRequest). Response: {Error}", responseContent);
            throw new HttpRequestException($"Unity Matchmaking Error: {response.StatusCode} - {responseContent}");
        }

        var ticketResponse = JsonConvert.DeserializeObject<UnityMatchmakingTicketResponse>(responseContent);

        if (ticketResponse == null || string.IsNullOrEmpty(ticketResponse.Id))
        {
            throw new Exception("Unity returned a success code but the Ticket ID was null or empty.");
        }

        return ticketResponse.Id;
    }

    public async Task DeleteTicketAsync(string ticketId)
    {
        string accessToken = await _authService.GetAccessTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.DeleteAsync($"v2/tickets/{ticketId}");

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to delete ticket {Id}: {Error}", ticketId, error);
        }
    }

    public async Task<UnityMatchmakingTicketStatusResponse> GetTicketStatusAsync(string ticketId)
    {
        string accessToken = await _authService.GetAccessTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.GetAsync($"v2/tickets/{ticketId}/status");
        var content = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            return JsonConvert.DeserializeObject<UnityMatchmakingTicketStatusResponse>(content)!;
        }

        throw new HttpRequestException($"Failed to get status for {ticketId}: {content}");
    }

    public async Task<UnityMatchmakingResultsResponse> GetMatchmakingResultsAsync(string matchId)
    {
        string accessToken = await _authService.GetAccessTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var endpoint = $"v2/projects/{_options.ProjectId}/matches/{matchId}/matchmaking-results";

        _logger.LogInformation("Fetching match results from: {Endpoint}", endpoint);
        var response = await _httpClient.GetAsync(endpoint);
        var content = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            return JsonConvert.DeserializeObject<UnityMatchmakingResultsResponse>(content)
                   ?? throw new Exception("Matchmaking results were empty.");
        }

        throw new HttpRequestException($"Failed to get match results: {response.StatusCode} - {content}");
    }
}