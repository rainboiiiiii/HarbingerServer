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

        // Required Headers
        _httpClient.DefaultRequestHeaders.Add("X-Unity-Services-Project-Id", _options.ProjectId);

        // Use the ID from your screenshot image_5deb29.png
        _httpClient.DefaultRequestHeaders.Add("Unity-Environment", "12cb99a8-fc59-4778-8128-e19c6538ebb2");
    }

    public async Task<string> CreateTicketAsync(string queueName, Dictionary<string, object> attributes, List<UnityMatchmakingPlayer> players)
    {
        // 1. Format attributes
        var formattedAttributes = new Dictionary<string, object>();
        foreach (var attr in attributes)
        {
            if (attr.Value is int intVal) formattedAttributes[attr.Key] = (double)intVal;
            else if (attr.Value is float floatVal) formattedAttributes[attr.Key] = (double)floatVal;
            else formattedAttributes[attr.Key] = attr.Value;
        }

        // 2. Format players - ensuring NO extra properties are sent
        var formattedPlayers = players.Select(p => new {
            id = p.Id,
            properties = new Dictionary<string, object>()
        }).ToList();

        // 3. Construct Request Body (Strict v2 Schema)
        var requestBody = new
        {
            queueName = queueName,
            attributes = formattedAttributes,
            players = formattedPlayers // Use the pre-formatted list here
        };

        var json = JsonConvert.SerializeObject(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // 4. Auth and Send
        string accessToken = await _authService.GetAccessTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        _logger.LogInformation("Sending Ticket Body: {Json}", json);

        var response = await _httpClient.PostAsync("v2/tickets", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            var ticketResponse = JsonConvert.DeserializeObject<UnityMatchmakingTicketResponse>(responseContent);
            return ticketResponse?.Id ?? throw new Exception("Ticket ID missing.");
        }

        _logger.LogError("Unity Error 55 Detail: {Error}", responseContent);
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