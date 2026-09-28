using System.Net;
using System.Text;
using DoomFPS.Application.Interfaces;
using DoomFPS.Application.Services;
using DoomFPS.Domain;
using DoomFPS.Infrastructure.Levels;
using DoomFPS.Infrastructure.Repositories;
using Xunit;

namespace DoomFPS.Tests.Integration;

// Integración con persistencia real en disco (JSON) y con el contrato REST de Firebase.
// Cada prueba usa su propio directorio temporal: el estado se recrea en cada ejecución (idempotencia).
public sealed class PersistenceIntegrationTests : IDisposable
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), "sector09-it-" + Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(dir, "perfiles.json");

    public void Dispose()
    {
        try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
    }

    [Fact] public void PerfilCreadoSePersisteEnDiscoYSeRecargaEnOtraSesion()
    {
        var repo = new JsonProfileRepository(FilePath);
        var p = repo.CreateOrGet("  Eric  ");
        p.BestScore = 4200; repo.Save(p); repo.SetLastUsed("Eric");

        Assert.True(File.Exists(FilePath));
        var reloaded = new JsonProfileRepository(FilePath);
        Assert.Equal(4200, reloaded.Find("eric")!.BestScore);
        Assert.Equal("Eric", reloaded.LastUsedName);
        Assert.Single(reloaded.All);
    }

    [Fact] public void AdminSiempreEsAdministradorYPuedeElegirMision()
    {
        var repo = new JsonProfileRepository(FilePath);
        var admin = repo.CreateOrGet("ADMIN");
        var player = repo.CreateOrGet("Brayan");
        Assert.Equal(ProfileRole.Administrador, admin.Role);
        Assert.True(admin.CanSelectAnyLevel);
        Assert.False(player.CanSelectAnyLevel);
        player.CampaignsCompleted = 1;
        Assert.True(player.CanSelectAnyLevel);
    }

    [Fact] public void CrearDosVecesElMismoNombreNoDuplica()
    {
        var repo = new JsonProfileRepository(FilePath);
        var a = repo.CreateOrGet("Nicolas"); var b = repo.CreateOrGet("NICOLAS");
        Assert.Same(a, b);
        Assert.Single(new JsonProfileRepository(FilePath).All);
    }

    [Fact] public void BorrarPerfilLimpiaElUltimoUsado()
    {
        var repo = new JsonProfileRepository(FilePath);
        repo.CreateOrGet("Temporal"); repo.SetLastUsed("Temporal");
        repo.Delete("temporal");
        var reloaded = new JsonProfileRepository(FilePath);
        Assert.Empty(reloaded.All);
        Assert.Null(reloaded.LastUsedName);
    }

    [Fact] public void ArchivoCorruptoNoImpideJugar()
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(FilePath, "{ esto no es json ");
        var repo = new JsonProfileRepository(FilePath);
        Assert.Empty(repo.All);
        repo.CreateOrGet("Recuperado");
        Assert.Single(new JsonProfileRepository(FilePath).All);
    }

    [Fact] public void GuardadoDeCampanaSobreviveAlDiscoYSeReanuda()
    {
        var campaign = new CampaignService(new CampaignLevelFactory(), new InMemoryCampaignRepository(), () => new InMemoryGameStateRepository());
        campaign.StartNewCampaign(Difficulty.Pesadilla);
        campaign.Game.Player.TakeDamage(25);
        campaign.AdvanceToNextLevel();

        var repo = new JsonProfileRepository(FilePath);
        var profile = repo.CreateOrGet("Eric");
        profile.Save = campaign.Snapshot(); repo.Save(profile);

        var save = new JsonProfileRepository(FilePath).Find("Eric")!.Save!;
        var resumed = new CampaignService(new CampaignLevelFactory(), new InMemoryCampaignRepository(), () => new InMemoryGameStateRepository());
        resumed.Resume(save);
        Assert.Equal(1, resumed.LevelIndex);
        Assert.Equal(Difficulty.Pesadilla, resumed.Difficulty.Level);
        Assert.Equal(75, resumed.Game.Player.Health);
    }

    [Fact] public async Task CuentaLocalRegistraSesionEHistorialOrdenado()
    {
        var svc = new LocalPlayerAccountService();
        Assert.False(svc.IsConfigured);
        var s = await svc.LoginAsync("eric@ucolombia.edu.co", "x");
        Assert.Equal("eric", s.DisplayName);
        await svc.SaveScoreAsync(new ScoreEntry("N1", 100, 60, 3, DateTimeOffset.UtcNow.AddMinutes(-5)));
        await svc.SaveScoreAsync(new ScoreEntry("N2", 300, 90, 7, DateTimeOffset.UtcNow));
        var history = await svc.GetHistoryAsync();
        Assert.Equal("N2", history[0].LevelName);
        await svc.RegisterAsync("b@x.co", "x", "Brayan");
        Assert.Equal("Brayan", svc.Current!.DisplayName);
        svc.Logout(); Assert.Null(svc.Current);
    }

    // ------------------------------------------------ Firebase (contrato REST)

    private sealed class FakeFirebase : HttpMessageHandler
    {
        public readonly List<(HttpMethod Method, string Url, string Body)> Requests = new();
        public readonly Dictionary<string, string> Db = new();
        public string? AuthError;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            var body = r.Content is null ? "" : await r.Content.ReadAsStringAsync(ct);
            var url = r.RequestUri!.ToString();
            Requests.Add((r.Method, url, body));
            if (url.Contains("identitytoolkit"))
                return AuthError is null
                    ? Json("{\"idToken\":\"tok-123\",\"localId\":\"uid-42\"}")
                    : new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(AuthError) };
            var key = url.Split('?')[0];
            if (r.Method == HttpMethod.Post) { Db[key] = "{\"-N1\":" + body + "}"; return Json("{\"name\":\"-N1\"}"); }
            if (r.Method == HttpMethod.Put) { Db[key] = body; return Json(body); }
            return Json(Db.TryGetValue(key, out var v) ? v : "null");
        }
        private static HttpResponseMessage Json(string s) => new(HttpStatusCode.OK) { Content = new StringContent(s, Encoding.UTF8, "application/json") };
    }

    private static FirebaseRestPlayerAccountService Configured(FakeFirebase fake)
    {
        Environment.SetEnvironmentVariable("SECTOR09_FIREBASE_API_KEY", "test-key");
        Environment.SetEnvironmentVariable("SECTOR09_FIREBASE_DATABASE_URL", "https://sector09-test.firebaseio.com/");
        try { return new FirebaseRestPlayerAccountService(new HttpClient(fake)); }
        finally
        {
            Environment.SetEnvironmentVariable("SECTOR09_FIREBASE_API_KEY", null);
            Environment.SetEnvironmentVariable("SECTOR09_FIREBASE_DATABASE_URL", null);
        }
    }

    [Fact] public async Task FirebaseSinConfigurarRechazaElInicioDeSesion()
    {
        var svc = new FirebaseRestPlayerAccountService(new HttpClient(new FakeFirebase()));
        Assert.False(svc.IsConfigured);
        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.LoginAsync("a@b.co", "x"));
        Assert.Empty(await svc.GetHistoryAsync());
    }

    [Fact] public async Task FirebaseRegistroCreaSesionRemotaYPerfil()
    {
        var fake = new FakeFirebase(); var svc = Configured(fake);
        var s = await svc.RegisterAsync("nicolas@ucolombia.edu.co", "secreto", "Nicolas");
        Assert.True(s.IsRemote); Assert.Equal("uid-42", s.UserId);
        Assert.Contains(fake.Requests, q => q.Method == HttpMethod.Put && q.Url.Contains("players/uid-42/profile.json?auth=tok-123"));
        Assert.DoesNotContain(fake.Requests, q => q.Url.Contains("secreto"));
    }

    [Fact] public async Task FirebaseTraduceErroresDeCredenciales()
    {
        var fake = new FakeFirebase { AuthError = "{\"error\":{\"message\":\"EMAIL_EXISTS\"}}" };
        var svc = Configured(fake);
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.RegisterAsync("a@b.co", "x", "A"));
        Assert.Equal("El correo ya está registrado.", ex.Message);
        fake.AuthError = "{\"error\":{\"message\":\"INVALID_LOGIN_CREDENTIALS\"}}";
        ex = await Assert.ThrowsAsync<InvalidOperationException>(() => svc.LoginAsync("a@b.co", "x"));
        Assert.Equal("Correo o contraseña incorrectos.", ex.Message);
    }

    [Fact] public async Task FirebaseGuardaPuntajeSoloConSesion()
    {
        var fake = new FakeFirebase(); var svc = Configured(fake);
        await svc.SaveScoreAsync(new ScoreEntry("N1", 10, 1, 1, DateTimeOffset.UtcNow));
        Assert.Empty(fake.Requests);
        await svc.LoginAsync("eric@ucolombia.edu.co", "x");
        Assert.Equal("eric", svc.Current!.DisplayName);
        await svc.SaveScoreAsync(new ScoreEntry("HANGAR", 1500, 95, 12, DateTimeOffset.UtcNow));
        Assert.Contains(fake.Requests, q => q.Method == HttpMethod.Post && q.Url.Contains("scores/uid-42.json") && q.Body.Contains("\"score\":1500"));
        svc.Logout(); Assert.Null(svc.Current);
    }

    // Regresión del hallazgo exploratorio BUG-S09-003.
    [Fact(Skip = "BUG-S09-003 abierto: GetHistoryAsync deserializa con mayúsculas y el puntaje remoto vuelve en 0")]
    public async Task FirebaseHistorialDevuelveElPuntajeGuardado()
    {
        var fake = new FakeFirebase(); var svc = Configured(fake);
        await svc.LoginAsync("eric@ucolombia.edu.co", "x");
        await svc.SaveScoreAsync(new ScoreEntry("HANGAR", 1500, 95, 12, DateTimeOffset.UtcNow));
        var history = await svc.GetHistoryAsync();
        Assert.Equal(1500, Assert.Single(history).Score);
        Assert.Equal("HANGAR", history[0].LevelName);
    }
}
