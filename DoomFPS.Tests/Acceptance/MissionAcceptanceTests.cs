using DoomFPS.Domain;
using DoomFPS.Domain.Entities;
using DoomFPS.Tests.Scenarios;
using Xunit;

namespace DoomFPS.Tests.Acceptance;

// Criterios del jugador en formato Dado / Cuando / Entonces.
[Trait("Category", "Acceptance")]
public class MissionAcceptanceTests
{
    [Fact] public void ACE01_RelesHabilitanSalidaSinExigirTodasLasBajas()
    {
        // Dado un enemigo vivo y una salida bloqueada por los relés.
        var enemy = TestFactory.EnemyAt(12.5, 1.5);
        var g = MissionScenario.Create(MissionKind.Relays, enemies: new[] { enemy });
        MissionScenario.EnterExit(g); Assert.Equal(GameStatus.Playing, g.Status);
        // Cuando activo ambos relés y alcanzo la salida.
        MissionScenario.UseAll(g); MissionScenario.EnterExit(g);
        // Entonces gano aunque el enemigo siga vivo.
        Assert.True(enemy.IsAlive); Assert.Equal(GameStatus.Won, g.Status);
    }

    [Fact] public void ACE02_InterruptoresExigenElOrdenIndicado()
    {
        var g = MissionScenario.Create(MissionKind.PowerGrid);
        MissionScenario.UseNode(g, 2);
        Assert.Equal(0, g.Mission!.Completed); Assert.Contains("Orden incorrecto", g.Message);
        MissionScenario.UseAll(g);
        Assert.True(g.CanExit); Assert.False(g.Mission.ElectricityOn);
    }

    [Fact] public void ACE03_SueloElectricoDaniaEnRojoPeroNoEnAzul()
    {
        var g = MissionScenario.Create(MissionKind.PowerGrid);
        g.Player.SetPosition(3.5, 4.5); g.Step(default);
        Assert.Equal(94, g.Player.Health);
        g.Player.SetPosition(2.5, 2.5); MissionScenario.Steps(g, 105);
        Assert.False(g.Mission!.ElectricityOn);
        g.Player.SetPosition(3.5, 4.5); int health = g.Player.Health; g.Step(default);
        Assert.Equal(health, g.Player.Health);
    }

    [Fact] public void ACE04_DronSeDetieneSiMeAlejoYContinuaCuandoRegreso()
    {
        var g = MissionScenario.Create(MissionKind.Escort);
        MissionScenario.UseNode(g, 0); var m = g.Mission!;
        g.Player.SetPosition(1.5, 6.5); double x = m.DroneX, y = m.DroneY;
        MissionScenario.Steps(g, 60); Assert.Equal(x, m.DroneX); Assert.Equal(y, m.DroneY);
        g.Player.SetPosition(x - 0.8, y); g.Step(default);
        Assert.True(m.DroneMoving);
    }

    [Fact] public void ACE05_EscoltarDronHastaDestinoPermiteGanar()
    {
        var g = MissionScenario.Create(MissionKind.Escort); MissionScenario.UseNode(g, 0);
        // El jugador sigue al dron mediante pasos reales de movimiento, sin teletransporte.
        for (int i = 0; i < 1800 && !g.Mission!.EscortArrived; i++)
        {
            var m = g.Mission!;
            double d = Math.Sqrt(Math.Pow(g.Player.X - m.DroneX, 2) + Math.Pow(g.Player.Y - m.DroneY, 2));
            if (d > 0.7) MissionScenario.WalkStep(g, m.DroneX, m.DroneY); else g.Step(default);
        }
        Assert.True(g.Mission!.EscortArrived);
        for (int i = 0; i < 150 && g.Status == GameStatus.Playing; i++)
            MissionScenario.WalkStep(g, 12.5, 5.5);
        Assert.Equal(GameStatus.Won, g.Status);
    }

    [Fact] public void ACE06_ValvulasDetienenFugaAntesDelLimite()
    {
        var g = MissionScenario.Create(MissionKind.Containment, seconds: 2);
        MissionScenario.UseAll(g); MissionScenario.Steps(g, 150);
        Assert.False(g.Mission!.Failed); MissionScenario.EnterExit(g);
        Assert.Equal(GameStatus.Won, g.Status);
    }

    [Fact] public void ACE07_FugaNoControladaTerminaEnDerrota()
    {
        var g = MissionScenario.Create(MissionKind.Containment, seconds: 2);
        MissionScenario.Steps(g, 120);
        Assert.Equal(GameStatus.Lost, g.Status); Assert.True(g.Mission!.Failed);
    }

    [Fact] public void ACE08_TransmisionSePausaFueraDeLaZonaSinPerderProgreso()
    {
        var g = MissionScenario.Create(MissionKind.Uplink);
        MissionScenario.UseNode(g, 0); MissionScenario.Steps(g, 30);
        int progress = g.Mission!.HoldFrames;
        g.Player.SetPosition(11.5, 6.5); MissionScenario.Steps(g, 60);
        Assert.Equal(progress, g.Mission.HoldFrames); Assert.False(g.CanExit);
        MissionScenario.PrepareAtNode(g, 0); MissionScenario.Steps(g, 120);
        MissionScenario.EnterExit(g); Assert.Equal(GameStatus.Won, g.Status);
    }

    [Fact] public void ACE09_EnemigoCercanoInterrumpeTransmision()
    {
        var enemy = TestFactory.EnemyAt(5.5, 2.5);
        var g = MissionScenario.Create(MissionKind.Uplink, enemies: new[] { enemy });
        MissionScenario.UseNode(g, 0); MissionScenario.Steps(g, 20);
        Assert.True(g.Mission!.Contested); Assert.Equal(0, g.Mission.HoldFrames);
    }

    [Fact] public void ACE10_UltimaCargaIniciaCuentaAtrasYEscapeDaVictoria()
    {
        var g = MissionScenario.Create(MissionKind.Demolition, seconds: 2);
        MissionScenario.UseNode(g, 0); MissionScenario.Steps(g, 130);
        Assert.Equal(0, g.Mission!.ClockFrames);
        MissionScenario.UseNode(g, 1); MissionScenario.UseNode(g, 2);
        Assert.True(g.Mission.ClockFrames > 0); MissionScenario.EnterExit(g);
        Assert.Equal(GameStatus.Won, g.Status);
    }

    [Fact] public void ACE11_NoEscaparDeLaExplosionDaDerrota()
    {
        var g = MissionScenario.Create(MissionKind.Demolition, seconds: 2);
        MissionScenario.UseAll(g); MissionScenario.Steps(g, 120);
        Assert.Equal(GameStatus.Lost, g.Status);
    }

    [Fact] public void ACE12_PilaresProtegenJefeHastaDesactivarlos()
    {
        var boss = new Enemy { Kind = EnemyKind.Ciberdemonio, Health = 30, MaxHealth = 30, Speed = 0 };
        boss.SetPosition(6.5, 2.5);
        var g = MissionScenario.Create(MissionKind.BossShield, enemies: new[] { boss });
        TestFactory.Aim(g, boss.X, boss.Y); g.Step(new(Fire: true));
        Assert.Equal(30, boss.Health); Assert.False(g.CanExit);
        MissionScenario.UseAll(g); Assert.False(g.Mission!.ShieldActive);
        Assert.False(g.CanExit); // Aún falta derrotar al jefe.
        g.Player.SetPosition(2.5, 2.5); TestFactory.Aim(g, boss.X, boss.Y);
        MissionScenario.Steps(g, 25); g.Step(new(Fire: true));
        Assert.False(boss.IsAlive); MissionScenario.EnterExit(g);
        Assert.Equal(GameStatus.Won, g.Status);
    }

    [Fact] public void ACE13_PausaCongelaCuentaAtrasYObjetivos()
    {
        var g = MissionScenario.Create(MissionKind.Containment, seconds: 2);
        MissionScenario.PrepareAtNode(g, 0); g.SetPaused(true);
        for (int i = 0; i < 180; i++) g.Step(new(Interact: true));
        Assert.Equal(0, g.Mission!.Completed); Assert.Equal(0, g.Mission.ClockFrames);
        g.SetPaused(false); g.Step(new(Interact: true)); Assert.Equal(1, g.Mission.Completed);
    }

    [Fact] public void ACE14_InteraccionRepetidaNoDuplicaObjetivo()
    {
        var g = MissionScenario.Create(MissionKind.Relays);
        MissionScenario.UseNode(g, 0); g.Step(new(Interact: true));
        Assert.Equal(1, g.Mission!.Completed); Assert.False(g.CanExit);
    }

    [Fact] public void ACE15_NoPuedoActivarObjetivoDesdeLejos()
    {
        var g = MissionScenario.Create(MissionKind.Relays);
        g.Player.SetPosition(1.5, 1.5); TestFactory.Aim(g, 4.5, 2.5);
        g.Step(new(Interact: true)); Assert.Equal(0, g.Mission!.Completed);
    }

    [Fact] public void ACE16_MochilaYMunicionSeConservanEnElEquipo()
    {
        var p = new Player { BackpackLevel = 1 };
        p.AddAmmo(AmmoKind.Shells, 130);
        var next = new Player(); next.Apply(p.Capture());
        Assert.Equal(1, next.BackpackLevel); Assert.Equal(198, next.AmmoCap(AmmoKind.Shells));
        Assert.Equal(p.Ammo, next.Ammo);
    }
    [Fact] public void ACE17_ReintentoBorraObjetivosYReiniciaCuentaAtras()
    {
        var c = new DoomFPS.Application.Services.CampaignService(
            new DoomFPS.Infrastructure.Levels.CampaignLevelFactory(),
            new DoomFPS.Infrastructure.Repositories.InMemoryCampaignRepository(),
            () => new DoomFPS.Infrastructure.Repositories.InMemoryGameStateRepository());
        c.StartTraining(3, Difficulty.Facil);
        MissionScenario.UseNode(c.Game, 0); MissionScenario.Steps(c.Game, 5);
        Assert.Equal(1, c.Game.Mission!.Completed);
        c.RetryLevel();
        Assert.Equal(3, c.LevelIndex); Assert.Equal(0, c.Game.Mission!.Completed);
        Assert.Equal(240, c.Game.Mission.RemainingSeconds); Assert.Equal(100, c.Game.Player.Health);
    }
}
