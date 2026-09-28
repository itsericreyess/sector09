using DoomFPS.Domain;
using DoomFPS.Domain.Entities;
using DoomFPS.Infrastructure.Levels;
using Xunit;

namespace DoomFPS.Tests.Integration;

public class FpsGameIntegrationTests
{
    [Fact] public void DisparoVisibleReduceVidaYConsumeUnCartucho()
    {
        var enemy = TestFactory.EnemyAt(4.5, 2.5); var g = TestFactory.Game(TestFactory.EmptyWorld(new[] { enemy }));
        TestFactory.Aim(g, enemy.X, enemy.Y); g.Step(new(Fire: true));
        Assert.Equal(30, enemy.Health); Assert.Equal(23, g.Player.Ammo);
    }

    [Fact] public void DisparoFallidoTambienConsumeMunicion()
    { var g = TestFactory.Game(TestFactory.EmptyWorld()); g.Step(new(Fire: true)); Assert.Equal(23, g.Player.Ammo); }

    [Fact] public void DisparoNoAtraviesaPared()
    {
        var enemy = TestFactory.EnemyAt(4.5, 2.5);
        var w = new GameWorld(new[] { "#######", "#..#..#", "#..#..#", "#..#..#", "#######" },
            Array.Empty<DoorPlacement>(), new[] { enemy }, Array.Empty<Pickup>());
        var g = TestFactory.Game(w); TestFactory.Aim(g, 4.5, 2.5); g.Step(new(Fire: true));
        Assert.Equal(60, enemy.Health); Assert.Equal(23, g.Player.Ammo);
    }

    [Fact] public void DisparoSoloDaniaEnemigoMasCercano()
    {
        var near = TestFactory.EnemyAt(4, 2.5); var far = TestFactory.EnemyAt(5, 2.5);
        var g = TestFactory.Game(TestFactory.EmptyWorld(new[] { near, far }));
        TestFactory.Aim(g, 5, 2.5); g.Step(new(Fire: true));
        Assert.Equal(30, near.Health); Assert.Equal(60, far.Health);
    }

    [Fact] public void MantenerDisparoRespetaCadencia()
    {
        var g = TestFactory.Game(TestFactory.EmptyWorld());
        for (int i = 0; i < 25; i++) g.Step(new(Fire: true));
        Assert.Equal(23, g.Player.Ammo); g.Step(new(Fire: true)); Assert.Equal(22, g.Player.Ammo);
    }

    [Fact] public void SinMunicionNoDaniaAlEnemigo()
    {
        var enemy = TestFactory.EnemyAt(4.5, 2.5); var g = TestFactory.Game(TestFactory.EmptyWorld(new[] { enemy }));
        g.Player.Ammo = 0; TestFactory.Aim(g, 4.5, 2.5); g.Step(new(Fire: true));
        Assert.Equal(60, enemy.Health); Assert.Equal(0, g.Player.Ammo);
    }

    [Fact] public void PausaCongelaTiempoPosicionMunicionYEnemigos()
    {
        var enemy = TestFactory.EnemyAt(4.5, 2.5); var g = TestFactory.Game(TestFactory.EmptyWorld(new[] { enemy }));
        g.SetPaused(true); g.Step(new(Forward: 1, Fire: true));
        Assert.Equal(0, g.ElapsedFrames); Assert.Equal(2.5, g.Player.Y); Assert.Equal(24, g.Player.Ammo); Assert.Equal(4.5, enemy.X);
    }

    [Fact] public void MovimientoDiagonalNoEsMasRapido()
    {
        var g = TestFactory.Game(TestFactory.EmptyWorld()); var d = TestFactory.Game(TestFactory.EmptyWorld());
        g.Step(new(Forward: 1)); d.Step(new(Forward: 1, Strafe: 1));
        double distance = Math.Sqrt(Math.Pow(d.Player.X - 2.5, 2) + Math.Pow(d.Player.Y - 2.5, 2));
        Assert.Equal(g.Player.Y - 2.5, distance, 6);
    }

    [Fact] public void MovimientoNoCruzaPared()
    {
        var g = TestFactory.Game(TestFactory.EmptyWorld());
        for (int i = 0; i < 200; i++) g.Step(new(Forward: 1));
        Assert.True(g.Player.Y <= 5.8); Assert.True(g.World.CanStand(g.Player.X, g.Player.Y));
    }

    [Fact] public void RecogerTarjetaSeProcesaUnaSolaVez()
    {
        var item = new Pickup(PickupKind.RedKey, 2.5, 2.5); var g = TestFactory.Game(TestFactory.EmptyWorld(pickups: new[] { item }));
        g.Step(default); g.Step(default);
        Assert.True(item.Collected); Assert.Single(g.Player.Inventory); Assert.Contains("Tarjeta Roja", g.Player.Inventory);
    }

    [Fact] public void BotiquinSeConservaSiLaSaludEstaCompleta()
    {
        var item = new Pickup(PickupKind.Health, 2.5, 2.5); var g = TestFactory.Game(TestFactory.EmptyWorld(pickups: new[] { item }));
        g.Step(default); Assert.False(item.Collected);
        g.Player.TakeDamage(20); g.Step(default); Assert.True(item.Collected); Assert.Equal(100, g.Player.Health);
    }

    [Fact] public void EnemigoSinVisionNoHaceDanoATravesDePared()
    {
        var e = TestFactory.EnemyAt(3.5, 2.5);
        var w = new GameWorld(new[] { "#####", "#.#.#", "#.#.#", "#####" }, Array.Empty<DoorPlacement>(), new[] { e }, Array.Empty<Pickup>());
        var g = TestFactory.Game(w); g.Player.SetPosition(1.5, 2.5);
        for (int i = 0; i < 120; i++) g.Step(default);
        Assert.Equal(100, g.Player.Health); Assert.Equal(3.5, e.X);
    }

    [Fact] public void SaludCeroFinalizaLaPartida()
    {
        var g = TestFactory.Game(TestFactory.EmptyWorld()); g.Player.TakeDamage(100); g.Step(default);
        Assert.Equal(GameStatus.Lost, g.Status); int frame = g.ElapsedFrames;
        g.Step(new(Forward: 1)); Assert.Equal(frame, g.ElapsedFrames);
    }

    [Fact] public void SalidaSinEnemigosProduceVictoria()
    {
        var g = TestFactory.Game(TestFactory.EmptyWorld()); g.Player.SetPosition(6.5, 5.5); g.Step(default);
        Assert.Equal(GameStatus.Won, g.Status);
    }

    [Fact] public void SalidaConEnemigosVivosNoProduceVictoria()
    {
        var g = TestFactory.Game(TestFactory.EmptyWorld(new[] { TestFactory.EnemyAt(1.5, 1.5) }));
        g.Player.SetPosition(6.5, 5.5); g.Step(default); Assert.Equal(GameStatus.Playing, g.Status);
    }

    [Fact] public void NuevoJuegoNoHeredaBajasNiTarjetas()
    {
        var a = TestFactory.Game(StationLevelFactory.Create()); a.Player.AddItem("Tarjeta Roja");
        var b = TestFactory.Game(StationLevelFactory.Create());
        Assert.Empty(b.Player.Inventory); Assert.Equal(0, b.DefeatedEnemies); Assert.Equal(100, b.Player.Health);
    }
}
