using DoomFPS.Application.Services;
using DoomFPS.Domain;
using DoomFPS.Infrastructure.Levels;
using DoomFPS.Infrastructure.Repositories;
using Reqnroll;
using Xunit;

namespace DoomFPS.BDD.Steps;

// Pasos de MisionesSector09.feature. Usan los servicios y repositorios reales.
[Binding]
public sealed class MisionesSteps
{
    private CampaignService _campaign = Nueva();
    private Exception? _error;

    private static CampaignService Nueva() =>
        new(new CampaignLevelFactory(), new InMemoryCampaignRepository(), () => new InMemoryGameStateRepository());

    [Given(@"una campaña nueva preparada en dificultad (Facil|Normal|Pesadilla)")]
    public void DadoCampanaEnDificultad(string dificultad) =>
        _campaign.StartNewCampaign(Enum.Parse<Difficulty>(dificultad));

    [Given(@"que el jugador abre el modo práctica en la misión (\d+)")]
    public void DadoModoPractica(int mision) => _campaign.StartTraining(mision - 1, Difficulty.Facil);

    [When(@"el jugador recibe (\d+) puntos de daño")]
    public void CuandoRecibeDanio(int danio) => _campaign.Game.Player.TakeDamage(danio);

    [When(@"el jugador reintenta la misión")]
    public void CuandoReintenta() => _campaign.RetryLevel();

    [Given(@"el jugador avanza a la siguiente misión")]
    [When(@"el jugador avanza a la siguiente misión")]
    public void CuandoAvanza() => Assert.True(_campaign.AdvanceToNextLevel());

    [When(@"el jugador intenta practicar la misión (\d+)")]
    public void CuandoIntentaPracticar(int mision)
    {
        try { _campaign.StartTraining(mision - 1, Difficulty.Normal); }
        catch (Exception e) { _error = e; }
    }

    [When(@"se guarda la partida y se reanuda en una sesión nueva")]
    public void CuandoGuardaYReanuda()
    {
        var save = _campaign.Snapshot();
        _campaign = Nueva();
        _campaign.Resume(save);
    }

    [When(@"el jugador pausa la partida y transcurren (\d+) cuadros")]
    public void CuandoPausa(int cuadros)
    {
        _campaign.Game.SetPaused(true);
        for (int i = 0; i < cuadros; i++) _campaign.Game.Step(default);
    }

    [Then(@"la misión activa debe ser la número (\d+)")]
    public void EntoncesMisionActiva(int numero) => Assert.Equal(numero, _campaign.Game.Level.Number);

    [Then(@"la dificultad aplicada debe ser (.*)")]
    public void EntoncesDificultad(string nombre) => Assert.Equal(nombre, _campaign.Difficulty.Name);

    [Then(@"el jugador debe tener (\d+) puntos de salud")]
    public void EntoncesSalud(int salud) => Assert.Equal(salud, _campaign.Game.Player.Health);

    [Then(@"la misión debe tener (\d+) objetivos completados")]
    public void EntoncesObjetivos(int n) => Assert.Equal(n, _campaign.Game.Mission!.Completed);

    [Then(@"el modo práctica debe estar activo")]
    public void EntoncesPracticaActiva() => Assert.True(_campaign.TrainingMode);

    [Then(@"el modo práctica no debe estar activo")]
    public void EntoncesPracticaInactiva() => Assert.False(_campaign.TrainingMode);

    [Then(@"el jugador debe tener al menos (\d+) armas")]
    public void EntoncesArmas(int n) => Assert.True(_campaign.Game.Player.Weapons.Count >= n);

    [Then(@"el sistema debe rechazar la solicitud por rango inválido")]
    public void EntoncesRechazo() => Assert.IsType<ArgumentOutOfRangeException>(_error);

    [Then(@"el reloj de la misión debe marcar (\d+) cuadros")]
    public void EntoncesReloj(int n) => Assert.Equal(n, _campaign.Game.ElapsedFrames);

    [Then(@"la partida debe quedar en estado (Paused|Playing|Won|Lost)")]
    public void EntoncesEstado(string estado) => Assert.Equal(Enum.Parse<GameStatus>(estado), _campaign.Game.Status);
}
