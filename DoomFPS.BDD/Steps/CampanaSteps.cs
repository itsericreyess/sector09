using DoomFPS.Application.Services;
using DoomFPS.Domain;
using DoomFPS.Infrastructure.Levels;
using DoomFPS.Infrastructure.Repositories;
using Reqnroll;
using Xunit;

namespace DoomFPS.BDD.Steps;

[Binding]
public sealed class CampanaSteps
{
    private Difficulty _difficulty;
    private CampaignService? _campaign;

    [Given("que el jugador selecciona la dificultad normal")]
    public void DadoQueSeleccionaDificultadNormal() => _difficulty = Difficulty.Normal;

    [When("inicia una campaña nueva")]
    public void CuandoIniciaUnaCampanaNueva()
    {
        _campaign = new CampaignService(new CampaignLevelFactory(), new InMemoryCampaignRepository(), () => new InMemoryGameStateRepository());
        _campaign.StartNewCampaign(_difficulty);
    }

    [Then("la campaña debe contener 7 niveles")]
    public void EntoncesContieneSieteNiveles() => Assert.Equal(7, _campaign!.LevelCount);

    [Then("el jugador debe iniciar con vida")]
    public void EntoncesJugadorIniciaConVida() => Assert.True(_campaign!.Game.Player.IsAlive);

    [Then("el estado del juego debe ser Playing")]
    public void EntoncesEstadoPlaying() => Assert.Equal(GameStatus.Playing, _campaign!.Game.Status);
}
