using GameBackend.Api.Data;
using GameBackend.Api.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace GameBackend.Api.Services;

public class MatchReportService
{
    private readonly MongoDbContext _db;
    private readonly ProgressionService _progressionService;
    private readonly ProgressionOptions _progressionOptions;
    private readonly ILogger<MatchReportService> _logger;

    public MatchReportService(
        MongoDbContext db, 
        ProgressionService progressionService, 
        IOptions<ProgressionOptions> progressionOptions,
        ILogger<MatchReportService> logger)
    {
        _db = db;
        _progressionService = progressionService;
        _progressionOptions = progressionOptions.Value;
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
            var (accountXp, seasonPassXp, dustAward, crystalsAward) = CalculateRewards(summary);
            var progression = await _progressionService.AddRewardsAsync(summary.UserId, accountXp, seasonPassXp, dustAward, crystalsAward, ct);
            
            awards.Add(new MatchAward
            {
                UserId = summary.UserId,
                AccountXpAwarded = accountXp,
                SeasonPassXpAwarded = seasonPassXp,
                DustAwarded = dustAward,
                CrystalsAwarded = crystalsAward,
                NewXp = progression.Xp,
                NewLevel = progression.Level,
                NewDust = progression.Dust,
                NewCrystals = progression.Crystals,
                NewSeasonPassXp = progression.SeasonPassXp,
                NewSeasonPassLevel = progression.SeasonPassLevel
            });
        }

        // Optional match state update to reflect reporting
        try
        {
            var updateBuilder = Builders<Match>.Update.Set(m => m.State, "reported");
            
            if (!string.IsNullOrEmpty(request.Map))
            {
                updateBuilder = updateBuilder.Set(m => m.Map, request.Map);
            }

            await _db.Matches.UpdateOneAsync(m => m.Id == match.Id, updateBuilder, cancellationToken: ct);
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

    private (long accountXp, long seasonPassXp, int dust, int crystals) CalculateRewards(PlayerSummary summary)
    {
        var scaling = _progressionOptions.RewardScaling;

        // Calculate Account XP
        var accountXp = (summary.WavesCleared * scaling.XpPerWave) + (summary.Kills * scaling.XpPerKill);
        accountXp = Math.Min(accountXp, scaling.MaxXpPerMatch);

        // Calculate Season Pass XP (For now, same as Account XP, but can be scaled differently)
        var seasonPassXp = accountXp; 

        // Calculate Dust
        var dust = summary.WavesCleared * scaling.DustPerWave;
        dust = Math.Min(dust, scaling.MaxDustPerMatch);

        // Calculate Crystals
        var crystals = summary.WavesCleared * scaling.CrystalsPerWave;
        crystals = Math.Min(crystals, scaling.MaxCrystalsPerMatch);

        return (accountXp, seasonPassXp, dust, crystals);
    }
}

public class MatchAward
{
    public string UserId { get; set; } = string.Empty;
    public long AccountXpAwarded { get; set; }
    public long SeasonPassXpAwarded { get; set; }
    public int DustAwarded { get; set; }
    public int CrystalsAwarded { get; set; }
    public long NewXp { get; set; }
    public int NewLevel { get; set; }
    public int NewDust { get; set; }
    public int NewCrystals { get; set; }
    public long NewSeasonPassXp { get; set; }
    public int NewSeasonPassLevel { get; set; }
}
