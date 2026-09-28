using DoomFPS.Domain;
using DoomFPS.Domain.Entities;
using Xunit;

namespace DoomFPS.Tests.Unit;

public class PlayerAndWorldTests
{
    [Fact] public void VidaNoBajaDeCero()
    { var p = new Player(); p.TakeDamage(150); Assert.Equal(0, p.Health); }

    [Fact] public void BotiquinNoSuperaCien()
    { var p = new Player { Health = 90 }; p.Heal(30); Assert.Equal(100, p.Health); }

    [Fact] public void BotiquinNoReviveJugadorMuerto()
    { var p = new Player { Health = 0 }; p.Heal(30); Assert.Equal(0, p.Health); }

    [Fact] public void DanoNegativoEsRechazado()
    { Assert.Throws<ArgumentOutOfRangeException>(() => new Player().TakeDamage(-1)); }

    [Fact] public void MuertoNoPuedeDisparar()
    { var p = new Player { Health = 0, Ammo = 5 }; Assert.False(p.Shoot(new Enemy(), new Weapon())); Assert.Equal(5, p.Ammo); }

    [Fact] public void EnemigoMuertoNoConsumeMunicion()
    { var p = new Player { Ammo = 5 }; Assert.False(p.Shoot(new Enemy { Health = 0 }, new Weapon())); Assert.Equal(5, p.Ammo); }

    [Fact] public void AgregarTarjetaDosVecesNoLaDuplica()
    { var p = new Player(); p.AddItem("Tarjeta Roja"); p.AddItem("Tarjeta Roja"); Assert.Single(p.Inventory); }

    [Fact] public void EnemigoRespetaTiempoEntreAtaques()
    {
        var p = new Player(); var enemy = new Enemy();
        Assert.True(enemy.TryAttack(p)); Assert.False(enemy.TryAttack(p));
        for (int i = 0; i < 60; i++) enemy.Tick();
        Assert.True(enemy.TryAttack(p)); Assert.Equal(82, p.Health);
    }

    [Fact] public void RayoDetectaMuroConDistanciaExacta()
    { var hit = TestFactory.EmptyWorld().CastRay(2.5, 2.5, 1, 0); Assert.Equal(4.5, hit.Distance, 6); Assert.Equal('#', hit.Material); }

    [Fact] public void RadioDeColisionImpideEntrarEnMuro()
    { Assert.False(TestFactory.EmptyWorld().CanStand(1.1, 2.5)); Assert.True(TestFactory.EmptyWorld().CanStand(1.3, 2.5)); }

    [Fact] public void PuertaCerradaBloqueaYAbiertaDejaPasar()
    {
        var d = new Door("Roja", "Tarjeta Roja");
        var w = new GameWorld(new[] { "#####", "#.R.#", "#####" },
            new[] { new DoorPlacement(2, 1, d) }, Array.Empty<Enemy>(), Array.Empty<Pickup>());
        Assert.False(w.HasLineOfSight(1.5, 1.5, 3.5, 1.5));
        var p = new Player(); p.AddItem("Tarjeta Roja"); d.TryOpen(p);
        Assert.True(w.HasLineOfSight(1.5, 1.5, 3.5, 1.5));
    }
}
