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

        // Ensure BaseAddress is set to the root (e.g., https://matchmaker.services.api.unity.com)
        _httpClient.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");

        // This header is required for all Unity Service calls
        _httpClient.DefaultRequestHeaders.Add("X-Unity-Services-Project-Id", _options.ProjectId);
    }

    public async Task<string> CreateTicketAsync(string queueName, Dictionary<string, object> attributes, List<UnityMatchmakingPlayer> players)
    {
        var requestBody = new UnityMatchmakingTicketRequest
        {
            QueueName = queueName,
            Attributes = attributes,
            Players = players
        };

        var json = JsonConvert.SerializeObject(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Get fresh token from our working AuthService
        string accessToken = await _authService.GetAccessTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // FIX 1: Use 'v2/tickets' (no leading slash) to avoid the double-slash // bug
        // FIX 2: Unity current standard is the v2 endpoint
        const string endpoint = "v2/tickets";

        _logger.LogInformation("Creating Unity ticket. Queue: {Queue}, URL: {FullUri}", queueName, _httpClient.BaseAddress + endpoint);

        var response = await _httpClient.PostAsync(endpoint, content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            var ticketResponse = JsonConvert.DeserializeObject<UnityMatchmakingTicketResponse>(responseContent);
            _logger.LogInformation("Successfully created ticket: {TicketId}", ticketResponse?.Id);
            return ticketResponse?.Id ?? throw new Exception("Ticket ID missing from response.");
        }

        _logger.LogError("Failed to create ticket. Status: {Status}, Error: {Error}", response.StatusCode, responseContent);
        throw new HttpRequestException($"Unity Matchmaking Error: {response.StatusCode} - {responseContent}");
    }

    public async Task DeleteTicketAsync(string ticketId)
    {
        string accessToken = await _authService.GetAccessTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        // Remove leading slash to prevent //tickets
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

        // Use v2 and remove leading slash
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

        // Note: The results endpoint usually requires the project ID in the path
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