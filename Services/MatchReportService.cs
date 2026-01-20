using GameBackend.Api.Data;
using GameBackend.Api.Models;
using MongoDB.Driver;

namespace GameBackend.Api.Services;

public class MatchReportService
{
    private readonly MongoDbContext _db;
    private readonly ProgressionService _progressionService;
    private readonly ILogger<MatchReportService> _logger;

    public MatchReportService(MongoDbContext db, ProgressionService progressionService, ILogger<MatchReportService> logger)
    {
        _db = db;
        _progressionService = progressionService;
        _logger = logger;
    }

    public async Task<(Match match, List<MatchAward> awards)> ReportMatchAsync(string callerUserId, MatchReportRequest request, CancellationToken ct = default)
    {
        var match = await _db.Matches.Find(m => m.Id == request.MatchId).FirstOrDefaultAsync(ct);
        if (match == null)
        {
            throw new InvalidOperationException("Match not found");
        }

        if (!string.Equals(request.HostId, callerUserId, StringComparison.Ordinal))
        {
            throw new UnauthorizedAccessException("Only the host can report results");
        }

        if (!match.Players.Contains(request.HostId))
        {
            throw new UnauthorizedAccessException("Host is not part of the match");
        }

        var playerIds = new HashSet<string>(match.Players);
        foreach (var summary in request.PlayerSummaries)
        {
            ValidateSummary(summary);
            if (!playerIds.Contains(summary.UserId))
            {
                throw new InvalidOperationException("Player not part of match");
            }
        }

        var awards = new List<MatchAward>();
        foreach (var summary in request.PlayerSummaries)
        {
            var xpAward = CalculateXp(summary);
            var progression = await _progressionService.AddXpAsync(summary.UserId, xpAward, ct);
            awards.Add(new MatchAward
            {
                UserId = summary.UserId,
                XpAwarded = xpAward,
                NewXp = progression.Xp,
                NewLevel = progression.Level
            });
        }

        // Optional match state update to reflect reporting
        try
        {
            var update = Builders<Match>.Update.Set(m => m.State, "reported");
            await _db.Matches.UpdateOneAsync(m => m.Id == match.Id, update, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to mark match {MatchId} as reported", match.Id);
        }

        return (match, awards);
    }

    private static void ValidateSummary(PlayerSummary summary)
    {
        if (summary.DurationSeconds < 60 || summary.DurationSeconds > 7200)
        {
            throw new ArgumentOutOfRangeException(nameof(summary.DurationSeconds), "durationSeconds must be between 60 and 7200");
        }

        if (summary.WavesCleared < 0 || summary.WavesCleared > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(summary.WavesCleared), "wavesCleared must be between 0 and 100");
        }

        if (summary.Kills < 0 || summary.Kills > 100000)
        {
            throw new ArgumentOutOfRangeException(nameof(summary.Kills), "kills must be between 0 and 100000");
        }
    }

    private static long CalculateXp(PlayerSummary summary)
    {
        var xp = summary.WavesCleared * 100L + summary.Kills * 2L;
        return Math.Min(xp, 50_000);
    }
}

public class MatchAward
{
    public string UserId { get; set; } = string.Empty;
    public long XpAwarded { get; set; }
    public long NewXp { get; set; }
    public int NewLevel { get; set; }
}
