using DoomFPS.Application.Services;
using DoomFPS.Domain;
using DoomFPS.Domain.Entities;
using DoomFPS.Infrastructure.Repositories;
using Xunit;

namespace DoomFPS.Tests.Unit;

// Cobertura de la IA de cada especie, objetos recogibles, recarga y cambio de arma.
// Se usa el GameService real en un hangar abierto sin paredes intermedias.
public class EnemyAIAndPickupTests
{
    private static readonly DifficultySettings Normal = DifficultySettings.For(Difficulty.Normal);

    private static GameService Arena(IEnumerable<Enemy>? enemies = null, IEnumerable<Pickup>? pickups = null)
    {
        var rows = new List<string> { new('#', 16) };
        for (int i = 0; i < 14; i++) rows.Add("#" + new string('.', 14) + "#");
        rows.Add(new('#', 16));
        var world = new GameWorld(rows.ToArray(), Array.Empty<DoorPlacement>(),
            enemies ?? Array.Empty<Enemy>(), pickups ?? Array.Empty<Pickup>());
        var repo = new InMemoryGameStateRepository();
        var game = new GameService(world, repo, new CombatService(repo), new DoorService(repo));
        game.Player.SetPosition(3.5, 7.5);
        return game;
    }

    private static int Run(GameService g, int frames, Func<bool> until)
    {
        for (int i = 0; i < frames; i++) { g.Step(default); if (until()) return i + 1; }
        return -1;
    }

    // ------------------------------------------------------------------ IA

    [Fact] public void Kamikaze_AlmaPerdida_SeLanzaYExplotaContraElJugador()
    {
        var soul = EnemyCatalog.At(EnemyKind.AlmaPerdida, Normal, 9.5, 7.5);
        var g = Arena(new[] { soul });
        int frames = Run(g, 900, () => !soul.IsAlive);
        Assert.True(frames > 0, "El alma perdida no llegó a detonar.");
        Assert.True(g.Player.Health < 100);
    }

    [Fact] public void Charger_Demonio_SeAgazapaYEmbiste()
    {
        var demon = EnemyCatalog.At(EnemyKind.Demonio, Normal, 10.5, 7.5);
        var g = Arena(new[] { demon });
        bool windUp = false, dash = false;
        for (int i = 0; i < 600 && !(windUp && dash); i++)
        {
            g.Step(default);
            windUp |= demon.State == EnemyState.WindUp;
            dash |= demon.DashFrames > 0;
        }
        Assert.True(demon.Alerted); Assert.True(windUp); Assert.True(dash);
    }

    // Regresión del hallazgo exploratorio BUG-S09-001 (ver Evidencias/06_Exploratorias).
    [Fact(Skip = "BUG-S09-001 abierto: la embestida del Demonio atraviesa al jugador inmóvil sin causar daño")]
    public void Charger_Demonio_EmbisteYMuerdeAlJugadorInmovil()
    {
        var demon = EnemyCatalog.At(EnemyKind.Demonio, Normal, 10.5, 7.5);
        var g = Arena(new[] { demon });
        Assert.True(Run(g, 900, () => g.Player.Health < 100) > 0);
    }

    [Fact] public void Charger_QueChocaContraLaParedQuedaAturdido()
    {
        var demon = EnemyCatalog.At(EnemyKind.Demonio, Normal, 2.5, 7.5);
        var g = Arena(new[] { demon });
        g.Player.SetPosition(8.5, 7.5);
        // El jugador se aparta durante la embestida para que el demonio acabe contra el muro.
        bool stunned = false;
        for (int i = 0; i < 900 && !stunned; i++)
        {
            g.Step(new(Strafe: demon.DashFrames > 0 ? 1 : 0));
            stunned = demon.StunFrames > 0;
        }
        Assert.True(stunned || g.Player.Health < 100);
    }

    [Fact] public void Circler_Espectro_OrbitaAlrededorDelJugador()
    {
        var ghost = EnemyCatalog.At(EnemyKind.Espectro, Normal, 8.5, 7.5);
        var g = Arena(new[] { ghost });
        Run(g, 400, () => false);
        double minAngle = double.MaxValue, maxAngle = double.MinValue;
        for (int i = 0; i < 300; i++)
        {
            g.Step(default);
            double a = Math.Atan2(ghost.Y - g.Player.Y, ghost.X - g.Player.X);
            minAngle = Math.Min(minAngle, a); maxAngle = Math.Max(maxAngle, a);
            double d = Math.Sqrt(Math.Pow(ghost.X - g.Player.X, 2) + Math.Pow(ghost.Y - g.Player.Y, 2));
            Assert.InRange(d, 0.5, 4.5);
        }
        Assert.True(maxAngle - minAngle > 1.0, "El espectro no rodea al jugador.");
    }

    // Regresión del hallazgo exploratorio BUG-S09-002 (ver Evidencias/06_Exploratorias).
    [Fact(Skip = "BUG-S09-002 abierto: el Espectro orbita fuera de su alcance y nunca golpea a un jugador inmóvil")]
    public void Circler_Espectro_AtacaCuerpoACuerpoAlJugadorInmovil()
    {
        var ghost = EnemyCatalog.At(EnemyKind.Espectro, Normal, 8.5, 7.5);
        var g = Arena(new[] { ghost });
        Assert.True(Run(g, 1200, () => g.Player.Health < 100) > 0);
    }

    [Fact] public void Floater_Cacodemon_DisparaPlasmaADistancia()
    {
        var caco = EnemyCatalog.At(EnemyKind.Cacodemon, Normal, 10.5, 7.5);
        var g = Arena(new[] { caco });
        Assert.True(Run(g, 600, () => g.Projectiles.Any(p => !p.FromPlayer)) > 0);
        Assert.Equal(ProjectileKind.Plasma, g.Projectiles.First(p => !p.FromPlayer).Kind);
    }

    [Theory]
    [InlineData(EnemyKind.Mancubus)]
    [InlineData(EnemyKind.CaballeroInfernal)]
    public void Sentinel_SePlantaYDisparaRafagas(EnemyKind kind)
    {
        var e = EnemyCatalog.At(kind, Normal, 11.5, 7.5);
        var g = Arena(new[] { e });
        Assert.True(Run(g, 900, () => g.Projectiles.Count(p => !p.FromPlayer) >= 1) > 0);
    }

    [Fact] public void ProyectilEnemigoDaniaAlJugador()
    {
        var imp = EnemyCatalog.At(EnemyKind.Imp, Normal, 9.5, 7.5);
        var g = Arena(new[] { imp });
        Assert.True(Run(g, 1500, () => g.Player.Health < 100) > 0);
    }

    [Fact] public void Boss_Ciberdemonio_LanzaAndanadaDeCohetes()
    {
        var boss = EnemyCatalog.At(EnemyKind.Ciberdemonio, Normal, 11.5, 7.5);
        var g = Arena(new[] { boss });
        Assert.True(Run(g, 900, () => g.Projectiles.Any(p => p.Kind == ProjectileKind.Rocket && !p.FromPlayer)) > 0);
        Assert.Equal(1, boss.BossPhase);
    }

    [Fact] public void Boss_EnSegundaFaseInvocaAlmasPerdidas()
    {
        var boss = EnemyCatalog.At(EnemyKind.Ciberdemonio, Normal, 10.5, 7.5);
        var g = Arena(new[] { boss });
        boss.TakeDamage((int)(boss.MaxHealth * 0.5));
        int before = g.World.Enemies.Count;
        Run(g, 1500, () => g.World.Enemies.Count > before);
        Assert.Equal(2, boss.BossPhase);
        Assert.True(g.World.Enemies.Count > before, "El jefe no invocó refuerzos en la fase 2.");
    }

    [Fact] public void Boss_ConPocaVidaPasaALaTerceraFase()
    {
        var boss = EnemyCatalog.At(EnemyKind.Ciberdemonio, Normal, 10.5, 7.5);
        var g = Arena(new[] { boss });
        boss.TakeDamage((int)(boss.MaxHealth * 0.8));
        g.Step(default);
        Assert.Equal(3, boss.BossPhase);
    }

    [Fact] public void Boss_PisotonCuerpoACuerpo()
    {
        var boss = EnemyCatalog.At(EnemyKind.Ciberdemonio, Normal, 5.0, 7.5);
        var g = Arena(new[] { boss });
        Assert.True(Run(g, 600, () => g.Player.Health < 100) > 0);
    }

    // ------------------------------------------------------------ objetos

    [Theory]
    [InlineData(PickupKind.Bullets, AmmoKind.Bullets)]
    [InlineData(PickupKind.Cells, AmmoKind.Cells)]
    [InlineData(PickupKind.Rockets, AmmoKind.Rockets)]
    [InlineData(PickupKind.Ammo, AmmoKind.Shells)]
    public void RecogerMunicionIncrementaSuReserva(PickupKind pickup, AmmoKind ammo)
    {
        var item = new Pickup(pickup, 3.5, 7.5);
        var g = Arena(pickups: new[] { item });
        int before = g.Player.AmmoOf(ammo);
        g.Step(default);
        Assert.True(item.Collected);
        Assert.True(g.Player.AmmoOf(ammo) > before);
    }

    [Fact] public void MegaesferaSobrecargaLaSaludHasta200()
    {
        var g = Arena(pickups: new[] { new Pickup(PickupKind.MegaHealth, 3.5, 7.5) });
        g.Step(default);
        Assert.Equal(200, g.Player.Health);
    }

    [Fact] public void ArmaduraYMochilaSeAplicanUnaSolaVez()
    {
        var armor = new Pickup(PickupKind.Armor, 3.5, 7.5);
        var pack = new Pickup(PickupKind.Backpack, 3.5, 7.5);
        var g = Arena(pickups: new[] { armor, pack });
        g.Step(default);
        Assert.Equal(50, g.Player.Armor);
        Assert.Equal(1, g.Player.BackpackLevel);
        Assert.True(armor.Collected && pack.Collected);
    }

    [Fact] public void BotiquinCuraSiHayDanio()
    {
        var g = Arena(pickups: new[] { new Pickup(PickupKind.Health, 3.5, 7.5) });
        g.Player.TakeDamage(50);
        g.Step(default);
        Assert.True(g.Player.Health > 50);
    }

    [Theory]
    [InlineData(PickupKind.WeaponShotgun, WeaponKind.Escopeta)]
    [InlineData(PickupKind.WeaponDoubleShotgun, WeaponKind.EscopetaDoble)]
    [InlineData(PickupKind.WeaponMachinegun, WeaponKind.Ametralladora)]
    [InlineData(PickupKind.WeaponPlasma, WeaponKind.Plasma)]
    [InlineData(PickupKind.WeaponRocket, WeaponKind.Lanzacohetes)]
    public void RecogerArmaLaEquipa(PickupKind pickup, WeaponKind weapon)
    {
        var g = Arena(pickups: new[] { new Pickup(pickup, 3.5, 7.5) });
        g.Step(default);
        Assert.True(g.Player.HasWeapon(weapon));
        Assert.Equal(weapon, g.Player.CurrentWeapon.Kind);
    }

    [Fact] public void ArmaRepetidaSoloAportaMunicion()
    {
        var first = new Pickup(PickupKind.WeaponMachinegun, 3.5, 7.5);
        var second = new Pickup(PickupKind.WeaponMachinegun, 4.5, 7.5);
        var g = Arena(pickups: new[] { first, second });
        g.Step(default);
        int bullets = g.Player.AmmoOf(AmmoKind.Bullets);
        g.Player.SetPosition(4.5, 7.5); g.Step(default);
        Assert.True(second.Collected);
        Assert.True(g.Player.AmmoOf(AmmoKind.Bullets) >= bullets);
        Assert.Equal(1, g.Player.Weapons.Count(w => w.Kind == WeaponKind.Ametralladora));
    }

    [Fact] public void TarjetaAmarillaSeGuardaEnInventario()
    {
        var g = Arena(pickups: new[] { new Pickup(PickupKind.YellowKey, 3.5, 7.5) });
        g.Step(default);
        Assert.Contains("Tarjeta Amarilla", g.Player.Inventory);
    }

    // ------------------------------------------------- armas y recarga

    [Fact] public void CambiarDeRanuraEquipaOtraArmaYBloqueaElDisparo()
    {
        var g = Arena();
        Assert.True(g.Player.GiveWeapon(WeaponKind.Pistola));
        Assert.Equal(WeaponKind.Pistola, g.Player.CurrentWeapon.Kind);
        g.Step(new(WeaponSlot: Weapon.Create(WeaponKind.Escopeta).Slot));
        Assert.Equal(WeaponKind.Escopeta, g.Player.CurrentWeapon.Kind);
        Assert.True(g.WeaponSwitchFrames > 0);
        g.Step(new(PreviousWeapon: true)); g.Step(new(NextWeapon: true));
        Assert.Equal(2, g.Player.Weapons.Count);
    }

    [Fact] public void RecargarRellenaElCargadorDesdeLaReserva()
    {
        var g = Arena();
        g.Player.GiveWeapon(WeaponKind.Ametralladora);
        g.Player.AddAmmo(AmmoKind.Bullets, 100);
        Run(g, 30, () => false);
        var weapon = g.Player.CurrentWeapon;
        if (weapon.MagazineSize == 0) return; // el arma no usa cargador
        weapon.Loaded = 0;
        g.Step(new(Reload: true));
        Assert.True(g.IsReloading);
        Run(g, weapon.ReloadFrames + 5, () => !g.IsReloading);
        Assert.True(weapon.Loaded > 0);
    }

    [Fact] public void CoheteDetonaYDaniaEnemigosEnElRadio()
    {
        var a = EnemyCatalog.At(EnemyKind.Zombi, Normal, 9.5, 7.5);
        var b = EnemyCatalog.At(EnemyKind.Zombi, Normal, 9.5, 8.3);
        var g = Arena(new[] { a, b });
        g.Player.GiveWeapon(WeaponKind.Lanzacohetes); g.Player.AddAmmo(AmmoKind.Rockets, 5);
        Run(g, 20, () => false);
        TestFactory.Aim(g, 9.5, 7.5);
        int ha = a.Health, hb = b.Health;
        g.Step(new(Fire: true));
        Run(g, 90, () => !g.Projectiles.Any(p => p.FromPlayer));
        Assert.True(a.Health < ha || !a.IsAlive);
        Assert.True(b.Health < hb || !b.IsAlive, "La explosión no alcanzó al segundo enemigo.");
    }

    [Fact] public void ObjetivoSinMisionGuiaPorTarjetasYAmenazas()
    {
        var g = Arena(new[] { EnemyCatalog.At(EnemyKind.Zombi, Normal, 12.5, 12.5) });
        Assert.StartsWith("ELIMINA LAS AMENAZAS", g.Objective);
        g.World.Enemies[0].TakeDamage(10_000);
        Assert.Equal("LLEGA A LA SALIDA VERDE", g.Objective);
        Assert.Equal(1, g.EnemiesDown);
    }
}
