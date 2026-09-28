using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using DoomFPS.Application.Interfaces;

namespace DoomFPS.Infrastructure.Repositories;

/// Conexión del frontend con Firebase Authentication REST y Realtime Database.
/// Se configura con SECTOR09_FIREBASE_API_KEY y SECTOR09_FIREBASE_DATABASE_URL.
/// Nunca se guardan contraseñas en el juego.
public sealed class FirebaseRestPlayerAccountService : IPlayerAccountService
{
    private readonly HttpClient http;
    private readonly string apiKey;
    private readonly string databaseUrl;
    private PlayerSession? current;
    private string? idToken;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(databaseUrl);
    public PlayerSession? Current => current;

    public FirebaseRestPlayerAccountService(HttpClient? httpClient = null)
    {
        http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        apiKey = Environment.GetEnvironmentVariable("SECTOR09_FIREBASE_API_KEY") ?? "";
        databaseUrl = (Environment.GetEnvironmentVariable("SECTOR09_FIREBASE_DATABASE_URL") ?? "").TrimEnd('/');
    }

    public async Task<PlayerSession> LoginAsync(string email, string password, CancellationToken ct = default)
        => await AuthenticateAsync("signInWithPassword", email, password, "Jugador", ct);

    public async Task<PlayerSession> RegisterAsync(string email, string password, string displayName, CancellationToken ct = default)
    {
        var session = await AuthenticateAsync("signUp", email, password, displayName, ct);
        await PutAsync($"players/{session.UserId}/profile", new { email = session.Email, displayName = session.DisplayName }, ct);
        return session;
    }

    private async Task<PlayerSession> AuthenticateAsync(string operation, string email, string password, string displayName, CancellationToken ct)
    {
        if (!IsConfigured) throw new InvalidOperationException("Firebase no está configurado. Define SECTOR09_FIREBASE_API_KEY y SECTOR09_FIREBASE_DATABASE_URL.");
        var endpoint = $"https://identitytoolkit.googleapis.com/v1/accounts:{operation}?key={Uri.EscapeDataString(apiKey)}";
        using var response = await http.PostAsJsonAsync(endpoint, new { email, password, returnSecureToken = true }, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(FriendlyError(body));
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        idToken = root.GetProperty("idToken").GetString();
        var uid = root.GetProperty("localId").GetString()!;
        var shown = displayName == "Jugador" ? email.Split('@')[0] : displayName;
        current = new PlayerSession(uid, email, shown, true);
        return current;
    }

    public async Task SaveScoreAsync(ScoreEntry score, CancellationToken ct = default)
    {
        if (!IsConfigured || current is null || idToken is null) return;
        await PostAsync($"scores/{current.UserId}", new { level = score.LevelName, score = score.Score, seconds = score.Seconds, kills = score.Kills, createdAt = score.CreatedAt.UtcDateTime.ToString("O") }, ct);
    }

    public async Task<IReadOnlyList<ScoreEntry>> GetHistoryAsync(CancellationToken ct = default)
    {
        if (!IsConfigured || current is null || idToken is null) return Array.Empty<ScoreEntry>();
        using var response = await http.GetAsync($"{databaseUrl}/scores/{current.UserId}.json?auth={Uri.EscapeDataString(idToken)}", ct);
        if (!response.IsSuccessStatusCode) return Array.Empty<ScoreEntry>();
        var json = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(json) || json == "null") return Array.Empty<ScoreEntry>();
        var dict = JsonSerializer.Deserialize<Dictionary<string, ScoreDto>>(json) ?? new();
        return dict.Values.Select(x => new ScoreEntry(x.Level ?? "Sector 09", x.Score, x.Seconds, x.Kills, DateTimeOffset.TryParse(x.CreatedAt, out var d) ? d : DateTimeOffset.UtcNow)).OrderByDescending(x => x.CreatedAt).ToList();
    }

    private async Task PutAsync(string path, object value, CancellationToken ct)
    { using var req = new HttpRequestMessage(HttpMethod.Put, $"{databaseUrl}/{path}.json?auth={Uri.EscapeDataString(idToken!)}") { Content = JsonContent.Create(value) }; await http.SendAsync(req, ct); }
    private async Task PostAsync(string path, object value, CancellationToken ct)
    { using var req = new HttpRequestMessage(HttpMethod.Post, $"{databaseUrl}/{path}.json?auth={Uri.EscapeDataString(idToken!)}") { Content = JsonContent.Create(value) }; await http.SendAsync(req, ct); }
    private static string FriendlyError(string body) => body.Contains("EMAIL_EXISTS", StringComparison.OrdinalIgnoreCase) ? "El correo ya está registrado." : body.Contains("INVALID_PASSWORD", StringComparison.OrdinalIgnoreCase) || body.Contains("INVALID_LOGIN_CREDENTIALS", StringComparison.OrdinalIgnoreCase) ? "Correo o contraseña incorrectos." : "Firebase rechazó la solicitud. Revisa la configuración.";
    private sealed class ScoreDto { public string? Level { get; set; } public int Score { get; set; } public int Seconds { get; set; } public int Kills { get; set; } public string? CreatedAt { get; set; } }
    public void Logout() { current = null; idToken = null; }
}
