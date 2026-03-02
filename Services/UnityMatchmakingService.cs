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
        string accessToken = await _authService.GetAccessTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var firstPlayerId = players.FirstOrDefault()?.Id ?? "unknown";
        _httpClient.DefaultRequestHeaders.Remove("impersonated-user-id");
        _httpClient.DefaultRequestHeaders.Add("impersonated-user-id", firstPlayerId);

        // FIX: Construct the body with the most basic possible structure
        var requestBody = new
        {
            queueName = queueName,
            players = players.Select(p => new { id = p.Id }).ToList()
        };

        var json = JsonConvert.SerializeObject(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // FIX: Try the Project-Specific URL path
        // Some Unity regions require the Project ID in the URL to route the ticket correctly
        var projectSpecificUrl = $"v2/projects/{_options.ProjectId}/tickets";

        _logger.LogInformation("Posting to: {Url}", projectSpecificUrl);

        var response = await _httpClient.PostAsync(projectSpecificUrl, content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            // If the project-specific URL fails, try the global one as a fallback
            _logger.LogWarning("Project URL failed, trying global v2/tickets fallback...");
            response = await _httpClient.PostAsync("v2/tickets", content);
            responseContent = await response.Content.ReadAsStringAsync();
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Unity Error 55 Final Detail: {Error}", responseContent);
            throw new HttpRequestException($"Unity Matchmaking Error: {response.StatusCode} - {responseContent}");
        }

        var ticketResponse = JsonConvert.DeserializeObject<UnityMatchmakingTicketResponse>(responseContent);
        return ticketResponse?.Id;
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