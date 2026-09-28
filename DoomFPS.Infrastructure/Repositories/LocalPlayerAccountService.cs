using DoomFPS.Application.Interfaces;
namespace DoomFPS.Infrastructure.Repositories;

/// Fallback local para probar la navegación sin credenciales; sustituir por Firebase al configurar variables.
public sealed class LocalPlayerAccountService : IPlayerAccountService
{
    private readonly List<ScoreEntry> scores = new();
    public bool IsConfigured => false;
    public PlayerSession? Current { get; private set; }
    public Task<PlayerSession> LoginAsync(string email, string password, CancellationToken ct = default) { Current = new("local-demo", email, email.Split('@')[0], false); return Task.FromResult(Current); }
    public Task<PlayerSession> RegisterAsync(string email, string password, string displayName, CancellationToken ct = default) { Current = new("local-demo", email, displayName, false); return Task.FromResult(Current); }
    public Task SaveScoreAsync(ScoreEntry score, CancellationToken ct = default) { scores.Add(score); return Task.CompletedTask; }
    public Task<IReadOnlyList<ScoreEntry>> GetHistoryAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ScoreEntry>>(scores.OrderByDescending(x => x.CreatedAt).ToList());
    public void Logout() => Current = null;
}
