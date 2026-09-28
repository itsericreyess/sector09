using DoomFPS.Domain;
using DoomFPS.Domain.Entities;
using Xunit;

namespace DoomFPS.Tests.Unit;

// Armas, munición y armadura: las reglas nuevas de la campaña.
public class ArsenalTests
{
    [Fact]
    public void CadaArmaTieneRanuraYMunicionPropias()
    {
        var slots = new HashSet<int>();
        foreach (WeaponKind kind in Enum.GetValues<WeaponKind>())
        {
            var weapon = Weapon.Create(kind);
            Assert.True(slots.Add(weapon.Slot), $"La ranura {weapon.Slot} está repetida.");
            Assert.InRange(weapon.Slot, 1, 6);
            Assert.True(weapon.Damage > 0);
            Assert.True(weapon.CooldownFrames > 0);
        }
        Assert.Equal(6, slots.Count);
    }

    [Fact]
    public void LaEscopetaGastaUnCartuchoYLaRecortadaDos()
    {
        Assert.Equal(1, Weapon.Create(WeaponKind.Escopeta).AmmoPerShot);
        Assert.Equal(2, Weapon.Create(WeaponKind.EscopetaDoble).AmmoPerShot);
        Assert.True(Weapon.Create(WeaponKind.EscopetaDoble).Pellets
            > Weapon.Create(WeaponKind.Escopeta).Pellets);
    }

    [Fact]
    public void RecogerUnArmaLaEquipaYNoSeDuplica()
    {
        var player = new Player();
        Assert.True(player.GiveWeapon(WeaponKind.Plasma));
        Assert.Equal(WeaponKind.Plasma, player.CurrentWeapon.Kind);
        Assert.False(player.GiveWeapon(WeaponKind.Plasma));
        Assert.Equal(2, player.Weapons.Count);
    }

    [Fact]
    public void LasRanurasSeleccionanElArmaCorrecta()
    {
        var player = new Player();
        player.ClearWeapons();
        player.GiveWeapon(WeaponKind.Pistola);
        player.GiveWeapon(WeaponKind.Lanzacohetes);
        Assert.True(player.SelectSlot(1));
        Assert.Equal(WeaponKind.Pistola, player.CurrentWeapon.Kind);
        Assert.True(player.SelectSlot(6));
        Assert.Equal(WeaponKind.Lanzacohetes, player.CurrentWeapon.Kind);
        Assert.False(player.SelectSlot(3));
    }

    [Fact]
    public void CadaTipoDeMunicionSeGuardaPorSeparado()
    {
        var player = new Player();
        player.AddAmmo(AmmoKind.Bullets, 30);
        player.AddAmmo(AmmoKind.Cells, 40);
        Assert.Equal(30, player.AmmoOf(AmmoKind.Bullets));
        Assert.Equal(40, player.AmmoOf(AmmoKind.Cells));
        Assert.Equal(10, player.AmmoOf(AmmoKind.Shells));
        Assert.Equal(0, player.AmmoOf(AmmoKind.Rockets));
    }

    [Fact]
    public void LaMunicionNoPasaDelTopeYLaMochilaLoDuplica()
    {
        var player = new Player();
        player.AddAmmo(AmmoKind.Rockets, 999);
        Assert.Equal(player.AmmoCap(AmmoKind.Rockets), player.AmmoOf(AmmoKind.Rockets));
        int before = player.AmmoCap(AmmoKind.Rockets);
        player.BackpackLevel = 1;
        Assert.Equal(before * 2, player.AmmoCap(AmmoKind.Rockets));
    }

    [Fact]
    public void LaArmaduraAbsorbeParteDelGolpe()
    {
        var player = new Player();
        player.AddArmor(50, 100);
        player.TakeDamage(30);
        Assert.Equal(40, player.Armor);   // absorbe 10
        Assert.Equal(80, player.Health);  // recibe 20
    }

    [Fact]
    public void SinArmaduraElDanoEsElOriginal()
    {
        var player = new Player();
        player.TakeDamage(30);
        Assert.Equal(70, player.Health);
    }

    [Fact]
    public void LaMegaesferaSobrecargaLaSaludHastaDoscientos()
    {
        var player = new Player();
        player.HealUpTo(100, 200);
        Assert.Equal(200, player.Health);
        player.Heal(30);
        Assert.Equal(200, player.Health); // un botiquín normal no la reduce
    }

    [Fact]
    public void ElEquipoViajaDeUnaMisionALaSiguiente()
    {
        var player = new Player();
        player.GiveWeapon(WeaponKind.Escopeta);
        player.AddAmmo(AmmoKind.Bullets, 42);
        player.TakeDamage(10);
        player.AddArmor(25, 100);
        var loadout = player.Capture();

        var next = new Player();
        next.Apply(loadout);
        Assert.Equal(player.Health, next.Health);
        Assert.Equal(25, next.Armor);
        Assert.Equal(42, next.AmmoOf(AmmoKind.Bullets));
        Assert.True(next.HasWeapon(WeaponKind.Escopeta));
    }

    [Fact]
    public void ElProyectilNoAceptaDireccionNula()
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => new Projectile(1, 1, 0, 0, 5, 10, true, ProjectileKind.Plasma));

    [Fact]
    public void ElProyectilAvanzaEnLineaRecta()
    {
        var shot = new Projectile(1, 1, 1, 0, 6, 10, true, ProjectileKind.Plasma);
        shot.Advance(2);
        Assert.Equal(3, shot.X, 6);
        Assert.Equal(1, shot.Y, 6);
        Assert.True(shot.Alive);
        shot.Kill();
        Assert.False(shot.Alive);
    }
}
