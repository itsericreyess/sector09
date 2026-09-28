namespace DoomFPS.Application.Interfaces;

public sealed record PlayerSession(string UserId, string Email, string DisplayName, bool IsRemote);
public sealed record ScoreEntry(string LevelName, int Score, int Seconds, int Kills, DateTimeOffset CreatedAt);

public interface IPlayerAccountService
{
    bool IsConfigured { get; }
    PlayerSession? Current { get; }
    Task<PlayerSession> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<PlayerSession> RegisterAsync(string email, string password, string displayName, CancellationToken cancellationToken = default);
    Task SaveScoreAsync(ScoreEntry score, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScoreEntry>> GetHistoryAsync(CancellationToken cancellationToken = default);
    void Logout();
}
