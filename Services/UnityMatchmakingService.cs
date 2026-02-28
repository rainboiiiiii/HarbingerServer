using GameBackend.Api.Data;
using GameBackend.Api.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace GameBackend.Api.Services;

public class UnityMatchmakingService
{
    private readonly HttpClient _httpClient;
    private readonly UnityMatchmakingOptions _options;
    private readonly ILogger<UnityMatchmakingService> _logger;

    public UnityMatchmakingService(HttpClient httpClient, IOptions<UnityMatchmakingOptions> options, ILogger<UnityMatchmakingService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_options.ServiceAccountKey}");
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
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        _logger.LogInformation("Sending CreateTicket request to Unity Matchmaking API: {RequestUri}", _httpClient.BaseAddress + "/tickets");
        _logger.LogDebug("CreateTicket Request Body: {RequestBody}", json);

        var response = await _httpClient.PostAsync("/tickets", content);

        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            var ticketResponse = JsonConvert.DeserializeObject<UnityMatchmakingTicketResponse>(responseContent);
            _logger.LogInformation("Successfully created Unity matchmaking ticket: {TicketId}", ticketResponse?.Id);
            return ticketResponse?.Id ?? throw new InvalidOperationException("Unity Matchmaking ticket ID was null.");
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to create Unity matchmaking ticket. Status: {StatusCode}, Content: {ErrorContent}", response.StatusCode, errorContent);
            throw new HttpRequestException($"Failed to create Unity matchmaking ticket: {response.StatusCode} - {errorContent}");
        }
    }

    public async Task DeleteTicketAsync(string ticketId)
    {
        _logger.LogInformation("Sending DeleteTicket request to Unity Matchmaking API for ticket: {TicketId}", ticketId);
        var response = await _httpClient.DeleteAsync($"/tickets?id={ticketId}");

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to delete Unity matchmaking ticket {TicketId}. Status: {StatusCode}, Content: {ErrorContent}", ticketId, response.StatusCode, errorContent);
            throw new HttpRequestException($"Failed to delete Unity matchmaking ticket: {response.StatusCode} - {errorContent}");
        }

        _logger.LogInformation("Successfully deleted Unity matchmaking ticket: {TicketId}", ticketId);
    }

    public async Task<UnityMatchmakingTicketStatusResponse> GetTicketStatusAsync(string ticketId)
    {
        _logger.LogInformation("Sending GetTicketStatus request to Unity Matchmaking API for ticket: {TicketId}", ticketId);
        var response = await _httpClient.GetAsync($"/tickets/{ticketId}/status");

        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            var statusResponse = JsonConvert.DeserializeObject<UnityMatchmakingTicketStatusResponse>(responseContent);
            _logger.LogInformation("Successfully retrieved Unity matchmaking ticket status for {TicketId}. Status: {Status}", ticketId, statusResponse?.Status);
            return statusResponse ?? throw new InvalidOperationException("Unity Matchmaking ticket status response was null.");
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to get Unity matchmaking ticket status for {TicketId}. Status: {StatusCode}, Content: {ErrorContent}", ticketId, response.StatusCode, errorContent);
            throw new HttpRequestException($"Failed to get Unity matchmaking ticket status: {response.StatusCode} - {errorContent}");
        }
    }

    public async Task<UnityMatchmakingResultsResponse> GetMatchmakingResultsAsync(string matchId)
    {
        _logger.LogInformation("Sending GetMatchmakingResults request to Unity Matchmaking API for project {ProjectId} and match {MatchId}", _options.ProjectId, matchId);
        var response = await _httpClient.GetAsync($"/projects/{_options.ProjectId}/matches/{matchId}/matchmaking-results");

        if (response.IsSuccessStatusCode)
        {
            var responseContent = await response.Content.ReadAsStringAsync();
            var resultsResponse = JsonConvert.DeserializeObject<UnityMatchmakingResultsResponse>(responseContent);
            _logger.LogInformation("Successfully retrieved Unity matchmaking results for match {MatchId}", matchId);
            return resultsResponse ?? throw new InvalidOperationException("Unity Matchmaking results response was null.");
        }
        else
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to get Unity matchmaking results for match {MatchId}. Status: {StatusCode}, Content: {ErrorContent}", matchId, response.StatusCode, errorContent);
            throw new HttpRequestException($"Failed to get Unity matchmaking results: {response.StatusCode} - {errorContent}");
        }
    }
}