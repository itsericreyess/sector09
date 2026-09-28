using DoomFPS.Application.Services;
using DoomFPS.Domain;
using DoomFPS.Infrastructure.Levels;
using DoomFPS.Infrastructure.Repositories;
using DoomFPS.Tests.Scenarios;
using Xunit;

namespace DoomFPS.Tests.Smoke;

// Selección rápida de funciones críticas; no sustituye las pruebas de aceptación.
[Trait("Category", "Smoke")]
public class SmokeTests
{
    [Fact] public void HUM01_IniciarCampanaConDependenciasReales()
    {
        var c = new CampaignService(new CampaignLevelFactory(), new InMemoryCampaignRepository(), () => new InMemoryGameStateRepository());
        c.StartNewCampaign(Difficulty.Normal);
        Assert.Equal(7, c.LevelCount); Assert.Equal(GameStatus.Playing, c.Game.Status);
        Assert.Equal(MissionKind.Relays, c.Game.Mission!.Plan.Kind); Assert.True(c.Game.Player.IsAlive);
    }
    [Fact] public void HUM02_SeCarganLosSieteMapasYObjetivos()
    {
        var factory = new CampaignLevelFactory();
        for (int i = 0; i < 7; i++)
        {
            var level = factory.Create(i, DifficultySettings.For(Difficulty.Normal));
            Assert.NotNull(level.Mission); Assert.NotEmpty(level.Mission!.Points);
            Assert.True(level.World.CanStand(level.Info.SpawnX, level.Info.SpawnY));
        }
    }
    [Fact] public void HUM03_ElJugadorSeMueveYDispara()
    {
        var g = TestFactory.Game(TestFactory.EmptyWorld());
        double y = g.Player.Y; int ammo = g.Player.Ammo;
        g.Step(new(Forward: 1, Fire: true));
        Assert.True(g.Player.Y > y); Assert.Equal(ammo - 1, g.Player.Ammo);
    }
    [Fact] public void HUM04_InteraccionActualizaObjetivo()
    {
        var g = MissionScenario.Create(MissionKind.Relays);
        MissionScenario.PrepareAtNode(g, 0);
        g.Step(new(Interact: true));
        Assert.Equal(1, g.Mission!.Completed); Assert.NotEmpty(g.LastEvents);
    }
    [Fact] public void HUM05_PausaYReanudacionFuncionan()
    {
        var g = MissionScenario.Create(MissionKind.Containment);
        g.SetPaused(true); g.Step(new(Forward: 1, Fire: true)); Assert.Equal(0, g.ElapsedFrames);
        g.SetPaused(false); g.Step(default); Assert.Equal(1, g.ElapsedFrames);
    }
    [Fact] public void HUM06_ReintentarRestableceMision()
    {
        var c = new CampaignService(new CampaignLevelFactory(), new InMemoryCampaignRepository(), () => new InMemoryGameStateRepository());
        c.StartNewCampaign(Difficulty.Normal); var previous = c.Game;
        c.Game.Player.TakeDamage(30); c.RetryLevel();
        Assert.NotSame(previous, c.Game); Assert.Equal(100, c.Game.Player.Health);
        Assert.Equal(0, c.Game.Mission!.Completed);
    }
    [Fact] public void HUM07_ModoPracticaCargaCualquierMision()
    {
        var c = new CampaignService(new CampaignLevelFactory(), new InMemoryCampaignRepository(), () => new InMemoryGameStateRepository());
        for (int i = 0; i < 7; i++)
        {
            c.StartTraining(i, Difficulty.Facil);
            Assert.Equal(i + 1, c.Game.Level.Number); Assert.True(c.TrainingMode);
            Assert.Equal(GameStatus.Playing, c.Game.Status); Assert.True(c.Game.Player.Weapons.Count >= 2);
        }
    }
}
