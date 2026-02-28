using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using Microsoft.Extensions.Options;
using GameBackend.Api.Data;

namespace GameBackend.Api.Services;

public class GameServerService
{
    private readonly ILogger<GameServerService> _logger;
    private readonly GameServerOptions _gameServerOptions;
    private readonly ConcurrentDictionary<string, Process> _activeGameServers = new();

    public GameServerService(ILogger<GameServerService> logger, IOptions<GameServerOptions> gameServerOptions)
    {
        _logger = logger;
        _gameServerOptions = gameServerOptions.Value;
    }

    public async Task<(string serverIp, int serverPort)> ProvisionServerAsync(string matchId, string map, CancellationToken ct = default)
    {
        _logger.LogInformation("Attempting to provision game server for Match {MatchId} on map {Map}", matchId, map);

        var serverIp = "127.0.0.1";
        var serverPort = GetAvailablePort();

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet", // Assuming dotnet is in PATH
            Arguments = $"{_gameServerOptions.ExecutablePath} --matchId={matchId} --map={map} --port={serverPort}",
            WorkingDirectory = Path.Combine(AppContext.BaseDirectory, _gameServerOptions.WorkingDirectory),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        try
        {
            var process = new Process { StartInfo = startInfo };

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger.LogInformation("[DummyGameServer - Match {MatchId}]: {Output}", matchId, e.Data);
                }
            };
            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    _logger.LogError("[DummyGameServer - Match {MatchId} ERROR]: {Error}", matchId, e.Data);
                }
            };

            if (process.Start())
            {
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (_activeGameServers.TryAdd(matchId, process))
                {
                    _logger.LogInformation("Game server process started for Match {MatchId} at {IpAddress}:{Port}. PID: {ProcessId}", matchId, serverIp, serverPort, process.Id);
                    return (serverIp, serverPort);
                }
                else
                {
                    _logger.LogWarning("Failed to add game server process to active list for Match {MatchId}. Terminating process.", matchId);
                    process.Kill();
                    throw new InvalidOperationException($"Game server for match {matchId} could not be added to active list.");
                }
            }
            else
            {
                _logger.LogError("Failed to start game server process for Match {MatchId}.", matchId);
                throw new InvalidOperationException($"Game server for match {matchId} could not be started.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while provisioning game server for Match {MatchId}", matchId);
            throw;
        }
    }

    public Task ShutdownServerAsync(string matchId)
    {
        if (_activeGameServers.TryRemove(matchId, out var process))
        {
            try
            {
                if (!process.HasExited)
                {
                    _logger.LogInformation("Attempting to terminate game server process for Match {MatchId}. PID: {ProcessId}", matchId, process.Id);
                    process.Kill(true); // Terminate the process and its descendants
                    _logger.LogInformation("Game server process terminated for Match {MatchId}. PID: {ProcessId}", matchId, process.Id);
                }
                else
                {
                    _logger.LogInformation("Game server process for Match {MatchId} (PID: {ProcessId}) already exited.", matchId, process.Id);
                }
                process.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while terminating game server process for Match {MatchId}. PID: {ProcessId}", matchId, process.Id);
            }
        }
        else
        {
            _logger.LogWarning("Attempted to shut down non-existent or already shut down game server for Match {MatchId}", matchId);
        }
        return Task.CompletedTask;
    }

    private int GetAvailablePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

