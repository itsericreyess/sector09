using DoomFPS.Domain;
using DoomFPS.Domain.Entities;
using DoomFPS.Infrastructure.Levels;
using DoomFPS.Tests.Scenarios;
using Xunit;

namespace DoomFPS.Tests.Acceptance;

[Trait("Category", "Acceptance")]
public class SpaceshipAcceptanceTests
{
    [Fact] public void NAV01_MirarEnAmbosEjesNoDesplazaAlJugador()
    {
        var game = TestFactory.Game(TestFactory.EmptyWorld());
        double x = game.Player.X, y = game.Player.Y, angle = game.Player.Angle;
        game.Step(new(Turn: 0.2, Look: 0.3));
        Assert.Equal(0.3, game.Player.Pitch, 6); Assert.NotEqual(angle, game.Player.Angle);
        Assert.Equal(x, game.Player.X); Assert.Equal(y, game.Player.Y);
        game.Step(new(CenterView: true)); Assert.Equal(0, game.Player.Pitch);
    }
    [Fact] public void NAV02_LimitesVerticalesYPausa()
    {
        var game = TestFactory.Game(TestFactory.EmptyWorld());
        for (int n = 0; n < 10; n++) game.Step(new(Look: 0.5));
        Assert.Equal(Player.MaxPitch, game.Player.Pitch);
        game.SetPaused(true); game.Step(new(Look: -0.5)); Assert.Equal(Player.MaxPitch, game.Player.Pitch);
        game.SetPaused(false);
        for (int n = 0; n < 10; n++) game.Step(new(Look: -0.5));
        Assert.Equal(-Player.MaxPitch, game.Player.Pitch);
    }
    [Fact] public void NAV03_DispararAlTechoNoImpactaAlEnemigoFrontal()
    {
        var enemy = new Enemy { Health = 100, Speed = 0 }; enemy.SetPosition(5.5, 2.5);
        var game = TestFactory.Game(TestFactory.EmptyWorld(new[] { enemy }));
        TestFactory.Aim(game, enemy.X, enemy.Y);
        game.Step(new(Look: 0.5, Fire: true)); Assert.Equal(100, enemy.Health);
        for (int n = 0; n < 30; n++) game.Step(default);
        game.Step(new(CenterView: true, Fire: true)); Assert.True(enemy.Health < 100);
    }
    [Fact] public void NAV04_PlasmaSigueLaAlturaDeLaMiraYChocaConElTecho()
    {
        var game = TestFactory.Game(TestFactory.EmptyWorld());
        game.Player.GiveWeapon(WeaponKind.Plasma); game.Player.AddAmmo(AmmoKind.Cells, 30);
        game.Step(new(Look: 0.4, Fire: true));
        var shot = Assert.Single(game.Projectiles);
        Assert.True(shot.Height > 0.5); Assert.True(shot.VerticalSlope > 0);
        for (int n = 0; n < 20; n++) game.Step(default);
        Assert.Empty(game.Projectiles);
    }
    [Fact] public void NAV05_LosSietePlanosSonDistintosYLosVentanalesSonSolidos()
    {
        var factory = new CampaignLevelFactory(); var layouts = new HashSet<string>();
        for (int i = 0; i < 7; i++)
        {
            var level = factory.Create(i, DifficultySettings.For(Difficulty.Normal));
            var world = level.World; string map = ""; int windows = 0;
            for (int y = 0; y < world.Height; y++) for (int x = 0; x < world.Width; x++)
            {
                char tile = world.Tile(x, y); map += tile;
                if (tile != 'W') continue;
                windows++; Assert.True(world.IsSolid(x, y)); Assert.False(world.CanStand(x + 0.5, y + 0.5));
            }
            Assert.True(windows > 0); Assert.True(layouts.Add(map));
        }
    }
    [Fact] public void NAV06_PerderPorImpactoAunqueElEscudoEsteDesactivado()
    {
        var game = MissionScenario.Create(MissionKind.BossShield, seconds: 2);
        MissionScenario.UseAll(game); MissionScenario.Steps(game, 120);
        Assert.True(game.Mission!.Complete); Assert.Equal(GameStatus.Lost, game.Status);
    }
    [Fact] public void NAV07_RecuperarElTimonATiempoSalvaLaNave()
    {
        var game = MissionScenario.Create(MissionKind.BossShield, seconds: 2);
        MissionScenario.UseAll(game); MissionScenario.EnterExit(game);
        Assert.Equal(GameStatus.Won, game.Status);
    }
    [Fact] public void NAV08_PuenteTieneCincoMinutosYReactorUnMinutoParaEvacuar()
    {
        var factory = new CampaignLevelFactory(); var normal = DifficultySettings.For(Difficulty.Normal);
        Assert.Equal(300, factory.Create(6, normal).Mission!.DurationSeconds);
        Assert.Equal(60, factory.Create(5, normal).Mission!.DurationSeconds);
    }
}
