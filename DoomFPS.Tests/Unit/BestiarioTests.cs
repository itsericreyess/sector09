using DoomFPS.Domain;
using DoomFPS.Domain.Entities;
using Xunit;

namespace DoomFPS.Tests.Unit;

// Cada especie tiene ficha propia: vida, movimiento, ataque y animación.
public class BestiarioTests
{
    private static readonly DifficultySettings Normal = DifficultySettings.For(Difficulty.Normal);

    [Fact]
    public void HayNueveEspeciesYNingunaRepiteNombre()
    {
        var names = Enum.GetValues<EnemyKind>()
            .Select(k => EnemyCatalog.Create(k, Normal).Type).ToList();
        Assert.Equal(9, names.Count);
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void CadaEspecieUsaSuPropioPatronDeMovimientoOAtaque()
    {
        var combos = Enum.GetValues<EnemyKind>()
            .Select(k => EnemyCatalog.Create(k, Normal))
            .Select(e => (e.Behavior, e.Attack))
            .ToList();
        // Solo el caballero y el mancubus comparten patrón de movimiento,
        // y aun así atacan de forma distinta.
        Assert.True(combos.Distinct().Count() >= 8);
        Assert.Contains(combos, c => c.Behavior == EnemyBehavior.Kamikaze);
        Assert.Contains(combos, c => c.Behavior == EnemyBehavior.Floater);
        Assert.Contains(combos, c => c.Attack == AttackKind.Hitscan);
        Assert.Contains(combos, c => c.Attack == AttackKind.Burst);
        Assert.Contains(combos, c => c.Attack == AttackKind.BossVolley);
    }

    [Fact]
    public void LaDificultadEscalaVidaDanoYVelocidad()
    {
        var easy = EnemyCatalog.Create(EnemyKind.Imp, DifficultySettings.For(Difficulty.Facil));
        var normal = EnemyCatalog.Create(EnemyKind.Imp, Normal);
        var hard = EnemyCatalog.Create(EnemyKind.Imp, DifficultySettings.For(Difficulty.Pesadilla));
        Assert.True(easy.Health < normal.Health && normal.Health < hard.Health);
        Assert.True(easy.Damage < normal.Damage && normal.Damage < hard.Damage);
        Assert.True(easy.Speed < hard.Speed);
    }

    [Fact]
    public void ElJefeEsMuchoMasDuroQueCualquierOtro()
    {
        var boss = EnemyCatalog.Create(EnemyKind.Ciberdemonio, Normal);
        int strongest = Enum.GetValues<EnemyKind>()
            .Where(k => k != EnemyKind.Ciberdemonio)
            .Max(k => EnemyCatalog.Create(k, Normal).Health);
        Assert.True(boss.Health > strongest * 4);
        Assert.Equal(EnemyBehavior.Boss, boss.Behavior);
    }

    [Fact]
    public void ElEnemigoColocadoGuardaSuVidaMaxima()
    {
        var enemy = EnemyCatalog.At(EnemyKind.Demonio, Normal, 4.5, 6.5);
        Assert.Equal(4.5, enemy.X);
        Assert.Equal(6.5, enemy.Y);
        Assert.Equal(enemy.Health, enemy.MaxHealth);
        Assert.Equal(1, enemy.HealthFraction);
        enemy.TakeDamage(enemy.Health / 2);
        Assert.InRange(enemy.HealthFraction, 0.45, 0.55);
    }

    [Fact]
    public void RecibirDanoDespiertaAlEnemigoYCambiaSuAnimacion()
    {
        var enemy = EnemyCatalog.At(EnemyKind.Imp, Normal, 2.5, 2.5);
        Assert.False(enemy.Alerted);
        Assert.Equal(EnemyState.Idle, enemy.State);
        enemy.TakeDamage(5);
        Assert.True(enemy.Alerted);
        Assert.Equal(EnemyState.Pain, enemy.State);
        Assert.True(enemy.HitFlashFrames > 0);
    }

    [Fact]
    public void AlMorirElEnemigoPasaAlEstadoDeCadaverYCuentaSuAnimacion()
    {
        var enemy = EnemyCatalog.At(EnemyKind.Zombi, Normal, 2.5, 2.5);
        enemy.TakeDamage(enemy.Health);
        Assert.False(enemy.IsAlive);
        Assert.Equal(EnemyState.Dead, enemy.State);
        for (int i = 0; i < 5; i++) enemy.Tick();
        Assert.Equal(5, enemy.DeathTimer);
    }

    [Fact]
    public void ElRelojDelEnemigoAvanzaSuAnimacion()
    {
        var enemy = EnemyCatalog.At(EnemyKind.Cacodemon, Normal, 2.5, 2.5);
        int before = enemy.AnimTimer;
        for (int i = 0; i < 30; i++) enemy.Tick();
        Assert.Equal(before + 30, enemy.AnimTimer);
    }

    [Fact]
    public void ElAtaqueADistanciaSeReservaSinAplicarDano()
    {
        var enemy = EnemyCatalog.At(EnemyKind.Imp, Normal, 2.5, 2.5);
        var player = new Player();
        Assert.True(enemy.TryBeginRangedAttack());
        Assert.Equal(100, player.Health);
        Assert.False(enemy.TryBeginRangedAttack());
    }

    [Fact]
    public void LosVoladoresEstanMarcadosComoTales()
    {
        Assert.True(EnemyCatalog.Create(EnemyKind.Cacodemon, Normal).Flying);
        Assert.True(EnemyCatalog.Create(EnemyKind.AlmaPerdida, Normal).Flying);
        Assert.False(EnemyCatalog.Create(EnemyKind.Demonio, Normal).Flying);
    }

    [Fact]
    public void ElEspectroEsSemitransparente()
    {
        Assert.True(EnemyCatalog.Create(EnemyKind.Espectro, Normal).Alpha < 0.6);
        Assert.Equal(1, EnemyCatalog.Create(EnemyKind.Imp, Normal).Alpha);
    }
}
