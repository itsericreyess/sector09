using DoomFPS.Application.Interfaces;
using DoomFPS.Domain;
using DoomFPS.Domain.Entities;

namespace DoomFPS.Application.Services;

// Reglas de una misión: entrada del jugador, combate, inteligencia enemiga y resultado.
// No dibuja nada ni sabe de Windows Forms.
public sealed class GameService
{
    public const int FramesPerSecond = 60;

    private readonly IGameStateRepository repository;
    private readonly CombatService combat;
    private readonly DoorService doors;
    private readonly List<GameEvent> events = new();
    private readonly List<Projectile> projectiles = new();
    private readonly List<Particle> particles = new();
    private readonly Random random = new(20260909);
    private readonly int initialEnemies;
    private readonly MissionService? missionService;
    public MissionState? Mission => missionService?.State;
    public bool CanExit => ExitUnlocked();
    private int fireCooldown;
    private int weaponSwitchFrames;
    private bool missionCompleteAnnounced;
    private bool lowHealthAnnounced;
    private int lastBossPhase;
    private double strideDistance;

    public GameWorld World { get; }
    public Player Player { get; } = new() { Ammo = 24 };
    public LevelInfo Level { get; }
    public DifficultySettings Difficulty { get; }
    public Weapon Weapon => Player.CurrentWeapon;
    public GameStatus Status { get; private set; } = GameStatus.Playing;
    public int ElapsedFrames { get; private set; }
    public int ShotFlashFrames { get; private set; }
    public int DamageFlashFrames { get; private set; }
    public int HealFlashFrames { get; private set; }
    public double Shake { get; private set; }
    public int Score { get; private set; }
    public int RequiredKills { get; }
    public string Message { get; private set; } = "Busca la tarjeta roja. M abre el mapa.";
    public int MessageFrames { get; private set; } = 240;

    // Diálogo narrado en subtítulos (sin voz): avisos de CONTROL u otros
    // interlocutores sobre lo que ocurre en la misión.
    public string Subtitle { get; private set; } = "";
    public string SubtitleSpeaker { get; private set; } = "";
    public int SubtitleFrames { get; private set; }

    // Recarga manual (tecla R): fotogramas restantes y duración total del
    // arma en mano, para que la presentación dibuje una barra de progreso.
    public int ReloadFramesLeft { get; private set; }
    public int ReloadDurationFrames => Weapon.ReloadFrames;
    public bool IsReloading => ReloadFramesLeft > 0;

    // Fotogramas de "zona segura" al desplegar en una misión nueva: los
    // enemigos no se alertan todavía, para que el jugador no aparezca en
    // mitad de un tiroteo.
    public int SafeZoneFrames { get; private set; }
    public bool InSafeZone => SafeZoneFrames > 0;

    public IReadOnlyList<GameEvent> LastEvents => events;
    public IReadOnlyList<Projectile> Projectiles => projectiles;
    public IReadOnlyList<Particle> Particles => particles;
    public int DefeatedEnemies => repository.GetDefeatedEnemiesCount();
    // Bajas contadas sobre el propio mundo: es lo que abre la salida.
    public int EnemiesDown => World.Enemies.Count(e => !e.IsAlive);
    public int TotalEnemies => initialEnemies;
    public int WeaponSwitchFrames => weaponSwitchFrames;

    // ¿Queda alguna compuerta del color indicado sin abrir en este nivel?
    private bool DoorPending(string key) => World.Doors.Any(d => d.Door.RequiredKey == key && !d.Door.IsOpen);

    public string Objective
    {
        get
        {
            if (Mission is not null) return Mission.Title;
            foreach (var (key, name) in new[]
            {
                ("Tarjeta Roja", "ROJA"), ("Tarjeta Azul", "AZUL"), ("Tarjeta Amarilla", "AMARILLA")
            })
            {
                if (!DoorPending(key)) continue;
                return Player.Inventory.Contains(key)
                    ? $"ABRE LA COMPUERTA {name} CON E"
                    : $"ENCUENTRA LA TARJETA {name}";
            }
            if (Level.IsBoss && World.Enemies.Any(e => e.IsAlive && e.Kind == EnemyKind.Ciberdemonio))
                return "DESTRUYE AL CIBERDEMONIO";
            if (EnemiesDown < RequiredKills)
                return $"ELIMINA LAS AMENAZAS  {Math.Min(EnemiesDown, RequiredKills)}/{RequiredKills}";
            return "LLEGA A LA SALIDA VERDE";
        }
    }

    public GameService(GameWorld world, IGameStateRepository repository,
        CombatService combat, DoorService doors,
        LevelInfo? level = null, DifficultySettings? difficulty = null, int requiredKills = -1, MissionPlan? mission = null)
    {
        World = world; this.repository = repository; this.combat = combat; this.doors = doors;
        missionService = mission is null ? null : new MissionService(mission);
        Difficulty = difficulty ?? DifficultySettings.For(Domain.Difficulty.Normal);
        initialEnemies = world.Enemies.Count;
        RequiredKills = requiredKills < 0 ? initialEnemies : Math.Min(requiredKills, initialEnemies);
        Level = level ?? new LevelInfo(1, "SECTOR 09", "Estación de superficie",
            new[] { "Recupera las tarjetas y limpia el sector." }, "Sector asegurado.",
            2.5, 2.5, Math.PI / 2);
        if (level is not null)
        {
            Player.SetPosition(level.SpawnX, level.SpawnY);
            Player.Rotate(level.SpawnAngle - Player.Angle);
            Message = level.Briefing.Count > 0 ? level.Briefing[0] : "Avanza.";
            MessageFrames = 300;
            // Al desplegar, el jugador aparece en una zona segura: los
            // demonios cercanos no se alertan hasta que pasa este margen.
            SafeZoneFrames = 150;
            var chatter = level.ComsChatter;
            var opening = chatter is { Count: > 0 } ? chatter[0]
                : new DialogueLine("CONTROL", level.Briefing.Count > 0 ? level.Briefing[0] : "Despliegue confirmado. Zona segura.");
            NotifyStory(opening.Speaker, opening.Text);
        }
        foreach (var enemy in world.Enemies) if (enemy.MaxHealth <= 0) enemy.MaxHealth = enemy.Health;
    }

    public void SetPaused(bool paused)
    {
        if (Status is GameStatus.Playing or GameStatus.Paused)
            Status = paused ? GameStatus.Paused : GameStatus.Playing;
    }

    // Paso fijo: entrada -> movimiento -> interacción -> combate -> mundo -> resultado.
    public void Step(GameInput input)
    {
        events.Clear();
        if (Status != GameStatus.Playing) return;
        ElapsedFrames++;
        if (fireCooldown > 0) fireCooldown--;
        if (ShotFlashFrames > 0) ShotFlashFrames--;
        if (DamageFlashFrames > 0) DamageFlashFrames--;
        if (HealFlashFrames > 0) HealFlashFrames--;
        if (MessageFrames > 0) MessageFrames--;
        if (SubtitleFrames > 0) SubtitleFrames--;
        if (weaponSwitchFrames > 0) weaponSwitchFrames--;
        if (SafeZoneFrames > 0) SafeZoneFrames--;
        if (ReloadFramesLeft > 0) { ReloadFramesLeft--; if (ReloadFramesLeft == 0) FinishReload(); }
        Shake *= 0.88; if (Shake < 0.01) Shake = 0;
        Player.ViewKick *= 0.80;

        Player.Rotate(Math.Clamp(input.Turn, -0.5, 0.5));
        if (input.CenterView) Player.CenterView();
        else Player.Look(Math.Clamp(input.Look, -0.5, 0.5));
        MovePlayer(input);
        bool wasAirborne = Player.IsAirborne;
        Player.UpdateJump(input.Jump);
        if (!wasAirborne && Player.IsAirborne) events.Add(new("jump", ""));
        else if (wasAirborne && !Player.IsAirborne) events.Add(new("land", ""));
        CollectPickups();
        SwitchWeapon(input);
        if (input.Interact) Interact();
        if (input.Reload && ReloadFramesLeft == 0 && weaponSwitchFrames == 0) TryStartReload();
        if (input.Fire && fireCooldown == 0 && weaponSwitchFrames == 0 && ReloadFramesLeft == 0) Fire();
        UpdateProjectiles();
        UpdateEnemies();
        UpdateParticles();
        if (!lowHealthAnnounced && Player.Health > 0 && Player.Health <= 25)
        { lowHealthAnnounced = true; NotifyStory("CONTROL", "Alerta: daño crítico. Busca botiquines."); }
        if (missionService is not null)
        {
            int hazardDamage = missionService.Tick(Player, World);
            if (hazardDamage > 0) HurtPlayer(hazardDamage);
            if (!missionCompleteAnnounced && Mission!.Complete)
            { missionCompleteAnnounced = true; NotifyStory("CONTROL", "Objetivo cumplido. Dirígete a la salida."); }
            if (Mission!.Failed)
            { Status = GameStatus.Lost; Notify("lost", "Tiempo agotado: objetivo fallido."); return; }
        }

        if (!Player.IsAlive)
        { Status = GameStatus.Lost; Notify("lost", "MISIÓN FALLIDA"); return; }
        if (World.Tile((int)Player.X, (int)Player.Y) == 'X')
        {
            if (ExitUnlocked())
            { Status = GameStatus.Won; Notify("won", "SECTOR ASEGURADO"); }
            else if (MessageFrames == 0) Notify("info", Mission is null ? "La salida requiere eliminar las amenazas." : Mission.Detail);
        }
    }

    private bool ExitUnlocked()
    {
        if (Mission is not null)
            return !Mission.Failed && Mission.Complete && (Mission.Plan.Kind != MissionKind.BossShield
                || !World.Enemies.Any(e => e.IsAlive && e.Kind == EnemyKind.Ciberdemonio));
        if (Level.IsBoss && World.Enemies.Any(e => e.IsAlive && e.Kind == EnemyKind.Ciberdemonio)) return false;
        if (RequiredKills >= initialEnemies) return World.Enemies.All(e => !e.IsAlive);
        return EnemiesDown >= RequiredKills;
    }

    // ---------------------------------------------------------------- jugador

    private void MovePlayer(GameInput input)
    {
        double forward = Math.Clamp(input.Forward, -1, 1), strafe = Math.Clamp(input.Strafe, -1, 1);
        double length = Math.Sqrt(forward * forward + strafe * strafe);
        if (length > 1) { forward /= length; strafe /= length; }
        double speed = (input.Sprint ? 3.7 : 2.5) / FramesPerSecond;
        double dx = (Math.Cos(Player.Angle) * forward - Math.Sin(Player.Angle) * strafe) * speed;
        double dy = (Math.Sin(Player.Angle) * forward + Math.Cos(Player.Angle) * strafe) * speed;
        bool Free(double x, double y) => World.CanStand(x, y)
            && World.Enemies.All(e => !e.IsAlive || Distance(x, y, e.X, e.Y) >= 0.46);
        double startX = Player.X, startY = Player.Y;
        if (Free(Player.X + dx, Player.Y)) Player.SetPosition(Player.X + dx, Player.Y);
        if (Free(Player.X, Player.Y + dy)) Player.SetPosition(Player.X, Player.Y + dy);

        // Pisadas: se emiten por distancia recorrida, no por fotograma, para que
        // el ritmo de pasos suene igual caminando o corriendo (más rápido al
        // esprintar porque la distancia por fotograma es mayor), y sin ningún
        // paso al quedar quieto o chocar contra una pared.
        double moved = Distance(Player.X, Player.Y, startX, startY);
        if (moved > 0 && !Player.IsAirborne)
        {
            const double strideLength = 0.62;
            strideDistance += moved;
            if (strideDistance >= strideLength) { strideDistance -= strideLength; events.Add(new("step", "")); }
        }
        else strideDistance = 0;
    }

    private void SwitchWeapon(GameInput input)
    {
        bool changed = false;
        if (input.WeaponSlot > 0) changed = Player.SelectSlot(input.WeaponSlot);
        else if (input.NextWeapon) changed = Player.CycleWeapon(1);
        else if (input.PreviousWeapon) changed = Player.CycleWeapon(-1);
        if (!changed) return;
        weaponSwitchFrames = 14; fireCooldown = Math.Max(fireCooldown, 10);
        ReloadFramesLeft = 0;
        Notify("weapon", Player.CurrentWeapon.Name.ToUpperInvariant());
    }

    private void Interact()
    {
        if (missionService?.Interact(Player, World) is string message)
        { Notify("door", message); return; }
        var hit = World.CastRay(Player.X, Player.Y, Math.Cos(Player.Angle), Math.Sin(Player.Angle), 1.5);
        var door = World.DoorAt(hit.CellX, hit.CellY);
        if (door is null || door.IsOpen || hit.Distance > 1.4)
        { Notify("info", "Acércate a una compuerta y mírala para abrirla."); return; }
        if (doors.TryOpenDoor(door, Player)) Notify("door", door.Name + " abierta.");
        else Notify("info", "Necesitas: " + door.RequiredKey);
    }

    private void Fire()
    {
        var weapon = Player.CurrentWeapon;
        if (weapon.MagazineSize > 0)
        {
            // Solo se puede disparar con las balas ya cargadas: si el cargador
            // no alcanza, el arma no dispara y hace falta recargar a mano (R)
            // para volver a usarla, sin tirar automáticamente de la reserva.
            if (weapon.Loaded < weapon.AmmoPerShot)
            { fireCooldown = 15; Notify("empty", "Cargador vacío. Pulsa R para recargar " + weapon.ShortName + "."); return; }
            weapon.Loaded -= weapon.AmmoPerShot;
        }
        else if (Player.AmmoOf(weapon.Ammo) < weapon.AmmoPerShot)
        { fireCooldown = 15; Notify("empty", "Sin munición para " + weapon.ShortName + ". Cambia de arma."); return; }
        else
        {
            Player.TryConsumeAmmo(weapon.Ammo, weapon.AmmoPerShot);
        }
        fireCooldown = weapon.CooldownFrames;
        ShotFlashFrames = 6;
        Player.ViewKick = weapon.Kick;
        Shake = Math.Max(Shake, weapon.Kick * 0.35);
        events.Add(new(weapon.ShotSound, ""));

        if (weapon.FiresProjectile) { LaunchPlayerProjectile(weapon); return; }

        for (int pellet = 0; pellet < weapon.Pellets; pellet++)
        {
            double angle = Player.Angle;
            if (weapon.Spread > 0) angle += (random.NextDouble() * 2 - 1) * weapon.Spread;
            HitscanPellet(weapon, angle);
        }
    }

    // Un perdigón: rayo contra el muro más cercano y contra el círculo de cada enemigo.
    private void HitscanPellet(Weapon weapon, double angle)
    {
        double dx = Math.Cos(angle), dy = Math.Sin(angle);
        var wall = World.CastRay(Player.X, Player.Y, dx, dy, weapon.Range);
        double slope = Math.Tan(Player.Pitch);
        // El techo/suelo también interceptan los disparos inclinados.
        double planeDistance = Math.Abs(slope) < 1e-9 ? weapon.Range : (slope > 0 ? World.CeilingHeight - 0.5 : 0.5) / Math.Abs(slope);
        double nearest = Math.Min(wall.Distance, planeDistance);
        Enemy? target = null;
        foreach (var enemy in World.Enemies)
        {
            if (!enemy.IsAlive) continue;
            double ex = enemy.X - Player.X, ey = enemy.Y - Player.Y;
            double projection = ex * dx + ey * dy;
            double perpendicularSquared = ex * ex + ey * ey - projection * projection;
            double radius = 0.30 * (enemy.Kind == EnemyKind.Ciberdemonio ? 2.4 : 1);
            if (projection <= 0 || perpendicularSquared > radius * radius) continue;
            double entry = projection - Math.Sqrt(Math.Max(0, radius * radius - perpendicularSquared));
            double hitHeight = 0.5 + slope * projection;
            double feet = enemy.Bob + (enemy.Flying ? 0.42 : 0);
            if (hitHeight < feet || hitHeight > feet + enemy.SpriteScale) continue;
            if (entry < nearest) { nearest = entry; target = enemy; }
        }
        if (target is null)
        {
            if (nearest < weapon.Range) SpawnParticles(ParticleKind.Spark, Player.X + dx * nearest, Player.Y + dy * nearest, 3, 0.5, 0xC8C8A0, Math.Clamp(0.5 + slope * nearest, 0, World.CeilingHeight));
            return;
        }
        ApplyEnemyDamage(target, weapon.Damage);
        SpawnParticles(ParticleKind.Blood, target.X, target.Y, 4, 0.9, BloodColor(target));
        if (!target.IsAlive) RegisterKill(target);
    }

    private void LaunchPlayerProjectile(Weapon weapon)
    {
        double dx = Math.Cos(Player.Angle), dy = Math.Sin(Player.Angle);
        projectiles.Add(new Projectile(Player.X + dx * 0.35, Player.Y + dy * 0.35, dx, dy,
            weapon.ProjectileSpeed, weapon.Damage, true, weapon.Projectile,
            weapon.SplashDamage, weapon.SplashRadius,
            weapon.Projectile == ProjectileKind.Rocket ? 0.22 : 0.14, verticalSlope: Math.Tan(Player.Pitch)));
    }

    private bool ApplyEnemyDamage(Enemy enemy, int damage)
    {
        if (Mission?.ShieldActive == true && enemy.Kind == EnemyKind.Ciberdemonio)
        {
            if (MessageFrames < 90) Notify("info", "Desactiva los tres pilares: el escudo absorbe el impacto.");
            return false;
        }
        return combat.ApplyDamage(enemy, damage);
    }

    private void RegisterKill(Enemy enemy)
    {
        Score += enemy.Score;
        SpawnParticles(ParticleKind.Gib, enemy.X, enemy.Y, 9, 1.6, BloodColor(enemy));
        Notify("kill", Describe(enemy) + " eliminado.");
    }

    private static string Describe(Enemy enemy)
        => string.IsNullOrEmpty(enemy.DisplayName) ? enemy.Type : enemy.DisplayName;

    private static int BloodColor(Enemy enemy) => enemy.Kind switch
    {
        EnemyKind.CaballeroInfernal => 0x3FC24A,
        EnemyKind.Espectro => 0x9AA7C8,
        EnemyKind.AlmaPerdida => 0xFFB347,
        _ => 0xB3222A
    };

    private void CollectPickups()
    {
        foreach (var item in World.Pickups)
        {
            if (item.Collected) continue;
            if (Distance(Player.X, Player.Y, item.X, item.Y) > 0.5) continue;
            string message;
            if (item.IsWeapon)
            {
                var kind = item.AsWeapon;
                var extra = kind switch
                {
                    WeaponKind.Escopeta => (AmmoKind.Shells, 8),
                    WeaponKind.EscopetaDoble => (AmmoKind.Shells, 8),
                    WeaponKind.Ametralladora => (AmmoKind.Bullets, 40),
                    WeaponKind.Plasma => (AmmoKind.Cells, 40),
                    _ => (AmmoKind.Rockets, 5)
                };
                bool isNew = Player.GiveWeapon(kind);
                int added = Player.AddAmmo(extra.Item1, extra.Item2);
                if (!isNew && added == 0) continue;
                message = isNew
                    ? "¡" + Weapon.Create(kind).Name.ToUpperInvariant() + "!  Pulsa " + Weapon.Create(kind).Slot
                    : "Munición recuperada.";
                if (isNew) { weaponSwitchFrames = 16; events.Add(new("weapon", "")); }
            }
            else
            {
                switch (item.Kind)
                {
                    case PickupKind.RedKey: Player.AddItem("Tarjeta Roja"); message = "Tarjeta roja obtenida."; break;
                    case PickupKind.BlueKey: Player.AddItem("Tarjeta Azul"); message = "Tarjeta azul obtenida."; break;
                    case PickupKind.YellowKey: Player.AddItem("Tarjeta Amarilla"); message = "Tarjeta amarilla obtenida."; break;
                    case PickupKind.Health:
                        if (Player.Health >= 100) continue;
                        Player.Heal(30); HealFlashFrames = 8; message = "+30 de salud (máximo 100)."; break;
                    case PickupKind.MegaHealth:
                        if (Player.Health >= 200) continue;
                        Player.HealUpTo(100, 200); HealFlashFrames = 14; message = "MEGAESFERA  ·  salud sobrecargada."; break;
                    case PickupKind.Armor:
                        if (Player.AddArmor(50, 100) == 0) continue;
                        message = "+50 de armadura."; break;
                    case PickupKind.Backpack:
                        Player.BackpackLevel = 1;
                        Player.AddAmmo(AmmoKind.Bullets, 40); Player.AddAmmo(AmmoKind.Shells, 12);
                        message = "Mochila: capacidad de munición duplicada."; break;
                    case PickupKind.Bullets:
                        if (Player.AddAmmo(AmmoKind.Bullets, 30) == 0) continue;
                        message = "+30 balas."; break;
                    case PickupKind.Cells:
                        if (Player.AddAmmo(AmmoKind.Cells, 40) == 0) continue;
                        message = "+40 celdas de energía."; break;
                    case PickupKind.Rockets:
                        if (Player.AddAmmo(AmmoKind.Rockets, 5) == 0) continue;
                        message = "+5 cohetes."; break;
                    default:
                        if (Player.AddAmmo(AmmoKind.Shells, 12) == 0) continue;
                        message = "+12 cartuchos."; break;
                }
            }
            item.Collect(); Notify("pickup", message);
        }
    }

    // ------------------------------------------------------------ proyectiles

    private void UpdateProjectiles()
    {
        for (int i = projectiles.Count - 1; i >= 0; i--)
        {
            var shot = projectiles[i];
            shot.Age1();
            double step = shot.Speed / FramesPerSecond;
            int substeps = Math.Max(1, (int)Math.Ceiling(step / 0.09));
            for (int s = 0; s < substeps && shot.Alive; s++)
            {
                shot.Advance(step / substeps);
                if (shot.Height <= 0 || shot.Height >= World.CeilingHeight || World.IsSolid((int)Math.Floor(shot.X), (int)Math.Floor(shot.Y))) { Detonate(shot, true); break; }
                if (shot.FromPlayer)
                {
                    foreach (var enemy in World.Enemies)
                    {
                        if (!enemy.IsAlive) continue;
                        double reach = shot.Radius + (enemy.Kind == EnemyKind.Ciberdemonio ? 0.75 : 0.30);
                        if (Distance(shot.X, shot.Y, enemy.X, enemy.Y) > reach) continue;
                        double feet = enemy.Bob + (enemy.Flying ? 0.42 : 0);
                        if (shot.Height + shot.Radius < feet || shot.Height - shot.Radius > feet + enemy.SpriteScale) continue;
                        ApplyEnemyDamage(enemy, shot.Damage);
                        SpawnParticles(ParticleKind.Blood, enemy.X, enemy.Y, 5, 1.0, BloodColor(enemy));
                        if (!enemy.IsAlive) RegisterKill(enemy);
                        Detonate(shot, false); break;
                    }
                }
                else if (Distance(shot.X, shot.Y, Player.X, Player.Y) < shot.Radius + 0.28)
                {
                    HurtPlayer(shot.Damage);
                    Detonate(shot, false);
                }
            }
            if (!shot.Alive) projectiles.RemoveAt(i);
        }
    }

    private void Detonate(Projectile shot, bool onWall)
    {
        shot.Kill();
        if (shot.SplashRadius > 0 && shot.SplashDamage > 0)
        {
            events.Add(new("explode", ""));
            Shake = Math.Max(Shake, 5.0);
            SpawnParticles(ParticleKind.Fire, shot.X, shot.Y, 16, 2.4, 0xFFA33B);
            SpawnParticles(ParticleKind.Smoke, shot.X, shot.Y, 8, 1.4, 0x50504E);
            foreach (var enemy in World.Enemies)
            {
                if (!enemy.IsAlive) continue;
                double d = Distance(shot.X, shot.Y, enemy.X, enemy.Y);
                if (d > shot.SplashRadius) continue;
                if (!World.HasLineOfSight(shot.X, shot.Y, enemy.X, enemy.Y)) continue;
                int damage = (int)(shot.SplashDamage * (1 - d / shot.SplashRadius));
                ApplyEnemyDamage(enemy, damage);
                if (!enemy.IsAlive) RegisterKill(enemy);
            }
            if (shot.FromPlayer)
            {
                double d = Distance(shot.X, shot.Y, Player.X, Player.Y);
                if (d < shot.SplashRadius * 0.75)
                    HurtPlayer((int)(shot.SplashDamage * 0.35 * (1 - d / (shot.SplashRadius * 0.75))));
            }
        }
        else
        {
            int color = shot.Kind switch
            {
                ProjectileKind.Plasma => 0x6FD8FF,
                ProjectileKind.GreenBolt => 0x74E24A,
                ProjectileKind.Soul => 0xFFD489,
                _ => 0xFF8A2A
            };
            SpawnParticles(onWall ? ParticleKind.Spark : ParticleKind.Fire, shot.X, shot.Y, 5, 0.9, color);
        }
    }

    private void HurtPlayer(int damage)
    {
        if (damage <= 0) return;
        Player.TakeDamage(damage);
        DamageFlashFrames = Math.Max(DamageFlashFrames, 12);
        Shake = Math.Max(Shake, Math.Min(7, damage * 0.35));
        events.Add(new("hurt", ""));
    }

    // -------------------------------------------------------------- enemigos

    private void UpdateEnemies()
    {
        var snapshot = World.Enemies.ToArray();
        foreach (var enemy in snapshot)
        {
            enemy.Tick();
            if (!enemy.IsAlive) continue;
            double distance = Distance(Player.X, Player.Y, enemy.X, enemy.Y);
            bool sight = World.HasLineOfSight(enemy.X, enemy.Y, Player.X, Player.Y);

            // IA original conservada para el enemigo cuerpo a cuerpo básico.
            if (enemy.Behavior == EnemyBehavior.Chaser && enemy.Attack == AttackKind.Melee)
            { ClassicChaser(enemy, distance, sight); continue; }

            if (!enemy.Alerted)
            {
                if (InSafeZone || distance > enemy.AggroRange || !sight) { enemy.SetState(EnemyState.Idle); continue; }
                enemy.Alerted = true;
                events.Add(new(GrowlSound(enemy.Kind), ""));
            }
            if (enemy.StunFrames > 0) { enemy.SetState(EnemyState.Pain); continue; }

            enemy.FacingX = Player.X - enemy.X; enemy.FacingY = Player.Y - enemy.Y;
            switch (enemy.Behavior)
            {
                case EnemyBehavior.Zigzag: Zigzag(enemy, distance, sight); break;
                case EnemyBehavior.Charger: Charger(enemy, distance, sight); break;
                case EnemyBehavior.Circler: Circler(enemy, distance, sight); break;
                case EnemyBehavior.Kamikaze: Kamikaze(enemy, distance, sight); break;
                case EnemyBehavior.Floater: Floater(enemy, distance, sight); break;
                case EnemyBehavior.Sentinel: Sentinel(enemy, distance, sight); break;
                case EnemyBehavior.Boss: Boss(enemy, distance, sight); break;
                default: ChaserRanged(enemy, distance, sight); break;
            }
        }
    }

    // Comportamiento idéntico al del proyecto original: acercarse y morder.
    private void ClassicChaser(Enemy enemy, double distance, bool sight)
    {
        if (InSafeZone || distance > enemy.AggroRange || !sight) return;
        bool wasAlerted = enemy.Alerted;
        enemy.Alerted = true;
        if (!wasAlerted) events.Add(new(GrowlSound(enemy.Kind), ""));
        if (distance < enemy.AttackRange)
        {
            enemy.SetState(EnemyState.Attack);
            if (enemy.TryAttack(Player)) { DamageFlashFrames = 12; Shake = Math.Max(Shake, 2.2); events.Add(new("hurt", "")); }
            return;
        }
        enemy.SetState(EnemyState.Walk);
        double speed = enemy.Speed / FramesPerSecond;
        StepEnemy(enemy, (Player.X - enemy.X) / distance * speed, (Player.Y - enemy.Y) / distance * speed);
    }

    // Zombi: avanza recto y dispara su fusil desde lejos, con aviso previo.
    private void ChaserRanged(Enemy enemy, double distance, bool sight)
    {
        if (TryRangedAttack(enemy, distance, sight)) return;
        if (distance > enemy.PreferredRange * 0.7)
        {
            enemy.SetState(EnemyState.Walk);
            double speed = enemy.Speed / FramesPerSecond;
            StepEnemy(enemy, (Player.X - enemy.X) / distance * speed, (Player.Y - enemy.Y) / distance * speed);
        }
        else enemy.SetState(EnemyState.Idle);
    }

    // Imp: avanza en diagonal alternada y lanza bolas de fuego a media distancia.
    private void Zigzag(Enemy enemy, double distance, bool sight)
    {
        enemy.Phase += 0.075;
        if (distance < enemy.AttackRange && MeleeIfReady(enemy)) return;
        if (TryRangedAttack(enemy, distance, sight)) return;
        double speed = enemy.Speed / FramesPerSecond;
        double toX = (Player.X - enemy.X) / distance, toY = (Player.Y - enemy.Y) / distance;
        double approach = distance > enemy.PreferredRange ? 1 : -0.35;
        double swing = Math.Sin(enemy.Phase) * 0.85;
        enemy.SetState(EnemyState.Walk);
        StepEnemy(enemy,
            (toX * approach - toY * swing) * speed,
            (toY * approach + toX * swing) * speed);
    }

    // Demonio: se agazapa un instante y luego embiste en línea recta.
    private void Charger(Enemy enemy, double distance, bool sight)
    {
        if (distance < enemy.AttackRange && MeleeIfReady(enemy)) return;
        if (enemy.DashFrames > 0)
        {
            enemy.DashFrames--;
            enemy.SetState(EnemyState.Attack);
            double dash = enemy.Speed * 3.1 / FramesPerSecond;
            if (!StepEnemy(enemy, enemy.DashX * dash, enemy.DashY * dash))
            { enemy.DashFrames = 0; enemy.StunFrames = 22; }
            return;
        }
        if (enemy.WindUp > 0)
        {
            enemy.WindUp--; enemy.SetState(EnemyState.WindUp);
            if (enemy.WindUp == 0)
            {
                enemy.DashX = (Player.X - enemy.X) / distance; enemy.DashY = (Player.Y - enemy.Y) / distance;
                enemy.DashFrames = 46; events.Add(new("charge", ""));
            }
            return;
        }
        if (sight && distance < 9 && enemy.AttackCooldown == 0)
        { enemy.WindUp = enemy.WindUpFrames; enemy.ArmCooldown(enemy.AttackDelayFrames); return; }
        enemy.SetState(EnemyState.Walk);
        double speed = enemy.Speed / FramesPerSecond;
        StepEnemy(enemy, (Player.X - enemy.X) / distance * speed, (Player.Y - enemy.Y) / distance * speed);
    }

    // Espectro: gira alrededor del jugador y se lanza a zarpazos.
    private void Circler(Enemy enemy, double distance, bool sight)
    {
        if (enemy.Phase == 0) enemy.Phase = random.Next(2) == 0 ? 1 : -1;
        if (distance < enemy.AttackRange && MeleeIfReady(enemy)) return;
        double speed = enemy.Speed / FramesPerSecond;
        double toX = (Player.X - enemy.X) / distance, toY = (Player.Y - enemy.Y) / distance;
        double radial = distance > 3.4 ? 1 : distance < 1.6 ? -0.5 : 0.18;
        double tangent = distance > 3.4 ? 0.25 : 1.05 * enemy.Phase;
        enemy.SetState(EnemyState.Walk);
        if (!StepEnemy(enemy,
            (toX * radial - toY * tangent) * speed,
            (toY * radial + toX * tangent) * speed)) enemy.Phase = -enemy.Phase;
    }

    // Alma perdida: flota, apunta y se lanza como un proyectil vivo.
    private void Kamikaze(Enemy enemy, double distance, bool sight)
    {
        enemy.Bob = Math.Sin(enemy.AnimTimer * 0.09) * 0.10;
        if (enemy.DashFrames > 0)
        {
            enemy.DashFrames--; enemy.SetState(EnemyState.Attack);
            double dash = enemy.Speed * 3.6 / FramesPerSecond;
            if (distance < 0.75)
            {
                HurtPlayer(enemy.Damage);
                SpawnParticles(ParticleKind.Fire, enemy.X, enemy.Y, 14, 2.0, 0xFFC061);
                enemy.TakeDamage(enemy.Health); RegisterKill(enemy);
                events.Add(new("explode", ""));
                return;
            }
            if (!StepEnemy(enemy, enemy.DashX * dash, enemy.DashY * dash))
            { enemy.DashFrames = 0; enemy.StunFrames = 40; events.Add(new("thud", "")); }
            return;
        }
        if (enemy.WindUp > 0)
        {
            enemy.WindUp--; enemy.SetState(EnemyState.WindUp);
            if (enemy.WindUp == 0)
            {
                enemy.DashX = (Player.X - enemy.X) / distance; enemy.DashY = (Player.Y - enemy.Y) / distance;
                enemy.DashFrames = 80; events.Add(new("soul", ""));
            }
            return;
        }
        if (sight && enemy.AttackCooldown == 0)
        { enemy.WindUp = enemy.WindUpFrames; enemy.ArmCooldown(enemy.AttackDelayFrames); return; }
        enemy.SetState(EnemyState.Idle);
        double drift = enemy.Speed * 0.4 / FramesPerSecond;
        StepEnemy(enemy, (Player.X - enemy.X) / distance * drift, (Player.Y - enemy.Y) / distance * drift);
    }

    // Cacodemon: levita, mantiene la distancia y escupe plasma.
    private void Floater(Enemy enemy, double distance, bool sight)
    {
        enemy.Bob = Math.Sin(enemy.AnimTimer * 0.045) * 0.16;
        enemy.Phase += 0.02;
        if (TryRangedAttack(enemy, distance, sight)) return;
        double speed = enemy.Speed / FramesPerSecond;
        double toX = (Player.X - enemy.X) / distance, toY = (Player.Y - enemy.Y) / distance;
        double radial = distance > enemy.PreferredRange + 1.2 ? 1 : distance < enemy.PreferredRange - 1.2 ? -0.8 : 0;
        double tangent = Math.Sin(enemy.Phase) * 0.75;
        enemy.SetState(EnemyState.Walk);
        StepEnemy(enemy, (toX * radial - toY * tangent) * speed, (toY * radial + toX * tangent) * speed);
    }

    // Mancubus y caballero infernal: se plantan y sueltan ráfagas.
    private void Sentinel(Enemy enemy, double distance, bool sight)
    {
        if (distance < enemy.AttackRange * 0.35 && MeleeIfReady(enemy)) return;
        if (TryRangedAttack(enemy, distance, sight)) return;
        double speed = enemy.Speed / FramesPerSecond;
        double toX = (Player.X - enemy.X) / distance, toY = (Player.Y - enemy.Y) / distance;
        double radial = distance > enemy.PreferredRange ? 1 : distance < enemy.PreferredRange * 0.55 ? -0.9 : 0.05;
        enemy.Phase += 0.03;
        double tangent = Math.Sin(enemy.Phase) * 0.45;
        enemy.SetState(EnemyState.Walk);
        StepEnemy(enemy, (toX * radial - toY * tangent) * speed, (toY * radial + toX * tangent) * speed);
    }

    // Ciberdemonio: tres fases, andanadas de cohetes, pisotón e invocaciones.
    private void Boss(Enemy enemy, double distance, bool sight)
    {
        double fraction = enemy.HealthFraction;
        enemy.BossPhase = fraction > 0.66 ? 1 : fraction > 0.33 ? 2 : 3;
        if (enemy.BossPhase != lastBossPhase)
        {
            lastBossPhase = enemy.BossPhase;
            NotifyStory("CIBERDEMONIO", enemy.BossPhase switch
            {
                2 => "¡Sistemas de combate al máximo! Invoca refuerzos.",
                3 => "¡Núcleo crítico! Es ahora o nunca.",
                _ => "Detecto una amenaza. Eliminándola."
            });
        }

        if (distance < 2.6 && enemy.AttackCooldown == 0)
        {
            enemy.SetState(EnemyState.Attack);
            enemy.TryAttack(Player);
            DamageFlashFrames = 18; Shake = Math.Max(Shake, 8);
            SpawnParticles(ParticleKind.Smoke, enemy.X, enemy.Y, 10, 1.8, 0x5A5148);
            events.Add(new("stomp", ""));
            return;
        }
        if (enemy.BurstLeft > 0)
        {
            enemy.SetState(EnemyState.Attack);
            if (enemy.AnimTimer % 9 == 0)
            {
                enemy.BurstLeft--;
                double spread = (enemy.BurstLeft - 1) * 0.13;
                FireEnemyProjectile(enemy, spread, ProjectileKind.Rocket, enemy.ProjectileDamage, 55, 2.6);
            }
            return;
        }
        if (enemy.WindUp > 0)
        {
            enemy.WindUp--; enemy.SetState(EnemyState.WindUp);
            if (enemy.WindUp == 0) enemy.BurstLeft = 2 + enemy.BossPhase;
            return;
        }
        if (sight && enemy.AttackCooldown == 0)
        {
            enemy.WindUp = enemy.WindUpFrames;
            enemy.ArmCooldown(Math.Max(28, enemy.AttackDelayFrames - enemy.BossPhase * 14));
            events.Add(new("bosswake", ""));
            return;
        }
        if (enemy.BossPhase >= 2 && enemy.SummonCooldown == 0 && sight)
        {
            enemy.SummonCooldown = 480;
            SummonSouls(enemy, enemy.BossPhase);
            Notify("summon", "¡El Ciberdemonio invoca almas perdidas!");
            return;
        }
        enemy.SetState(EnemyState.Walk);
        double speed = enemy.Speed * (0.8 + enemy.BossPhase * 0.16) / FramesPerSecond;
        double toX = (Player.X - enemy.X) / distance, toY = (Player.Y - enemy.Y) / distance;
        double radial = distance > enemy.PreferredRange ? 1 : -0.35;
        StepEnemy(enemy, toX * radial * speed, toY * radial * speed);
    }

    private void SummonSouls(Enemy boss, int count)
    {
        for (int i = 0; i < count; i++)
        {
            double angle = random.NextDouble() * Math.PI * 2;
            double x = boss.X + Math.Cos(angle) * 1.8, y = boss.Y + Math.Sin(angle) * 1.8;
            if (!World.CanStand(x, y, 0.25)) continue;
            var soul = EnemyCatalog.Create(EnemyKind.AlmaPerdida, Difficulty);
            soul.SetPosition(x, y); soul.Alerted = true; soul.MaxHealth = soul.Health;
            World.AddEnemy(soul);
            SpawnParticles(ParticleKind.Fire, x, y, 8, 1.2, 0xFFCF7A);
        }
    }

    private bool MeleeIfReady(Enemy enemy)
    {
        if (enemy.AttackCooldown > 0) return false;
        enemy.SetState(EnemyState.Attack);
        if (enemy.TryAttack(Player))
        {
            DamageFlashFrames = 14; Shake = Math.Max(Shake, 2.6);
            events.Add(new("hurt", ""));
        }
        return true;
    }

    // Ataque a distancia con aviso previo: primero se telegrafía, luego dispara.
    private bool TryRangedAttack(Enemy enemy, double distance, bool sight)
    {
        if (enemy.Attack is AttackKind.Melee or AttackKind.Explode) return false;
        if (enemy.BurstLeft > 0)
        {
            enemy.SetState(EnemyState.Attack);
            if (enemy.AnimTimer % 7 == 0)
            {
                enemy.BurstLeft--;
                double spread = (enemy.BurstLeft - (enemy.BurstCount - 1) / 2.0) * 0.16;
                if (enemy.Attack == AttackKind.Hitscan) HitscanEnemy(enemy, spread);
                else FireEnemyProjectile(enemy, spread, enemy.Projectile, enemy.ProjectileDamage, 0, 0);
            }
            return true;
        }
        if (enemy.WindUp > 0)
        {
            enemy.WindUp--; enemy.SetState(EnemyState.WindUp);
            if (enemy.WindUp == 0) enemy.BurstLeft = Math.Max(1, enemy.BurstCount);
            return true;
        }
        if (!sight || distance > enemy.PreferredRange + 3.5 || !enemy.TryBeginRangedAttack()) return false;
        enemy.WindUp = Math.Max(4, enemy.WindUpFrames);
        enemy.SetState(EnemyState.WindUp);
        events.Add(new("aim", ""));
        return true;
    }

    private void FireEnemyProjectile(Enemy enemy, double spread, ProjectileKind kind,
        int damage, int splashDamage, double splashRadius)
    {
        double angle = Math.Atan2(Player.Y - enemy.Y, Player.X - enemy.X) + spread;
        double dx = Math.Cos(angle), dy = Math.Sin(angle);
        double offset = enemy.Kind == EnemyKind.Ciberdemonio ? 1.2 : 0.42;
        projectiles.Add(new Projectile(enemy.X + dx * offset, enemy.Y + dy * offset, dx, dy,
            enemy.ProjectileSpeed, Math.Max(1, damage), false, kind, splashDamage, splashRadius,
            kind == ProjectileKind.Rocket ? 0.22 : 0.15));
        events.Add(new(kind == ProjectileKind.Rocket ? "rocket" : "fireball", ""));
    }

    // Disparo instantáneo del zombi: un rayo que solo llega si hay línea de visión.
    private void HitscanEnemy(Enemy enemy, double spread)
    {
        events.Add(new("rifle", ""));
        double angle = Math.Atan2(Player.Y - enemy.Y, Player.X - enemy.X) + spread + (random.NextDouble() - 0.5) * 0.07;
        double dx = Math.Cos(angle), dy = Math.Sin(angle);
        var wall = World.CastRay(enemy.X, enemy.Y, dx, dy, enemy.PreferredRange + 4);
        double ex = Player.X - enemy.X, ey = Player.Y - enemy.Y;
        double projection = ex * dx + ey * dy;
        double perpendicularSquared = ex * ex + ey * ey - projection * projection;
        if (projection > 0 && perpendicularSquared <= 0.32 * 0.32 && projection < wall.Distance)
            HurtPlayer(enemy.ProjectileDamage > 0 ? enemy.ProjectileDamage : enemy.Damage);
        else SpawnParticles(ParticleKind.Spark, wall.X, wall.Y, 3, 0.6, 0xE0D8A0);
    }

    // Mueve al enemigo eje por eje. Devuelve false si quedó completamente bloqueado.
    private bool StepEnemy(Enemy enemy, double dx, double dy)
    {
        bool Free(double x, double y) => World.CanStand(x, y, enemy.Radius)
            && World.Enemies.All(other => ReferenceEquals(enemy, other) || !other.IsAlive
                || Distance(x, y, other.X, other.Y) >= enemy.Radius + other.Radius + 0.02);
        bool moved = false;
        if (Free(enemy.X + dx, enemy.Y)) { enemy.SetPosition(enemy.X + dx, enemy.Y); moved = true; }
        if (Free(enemy.X, enemy.Y + dy)) { enemy.SetPosition(enemy.X, enemy.Y + dy); moved = true; }
        return moved;
    }

    // -------------------------------------------------------------- partículas

    private void SpawnParticles(ParticleKind kind, double x, double y, int count, double force, int color, double? atHeight = null)
    {
        if (particles.Count > 420) return;
        for (int i = 0; i < count; i++)
        {
            double angle = random.NextDouble() * Math.PI * 2;
            double speed = force * (0.25 + random.NextDouble() * 0.75);
            particles.Add(new Particle(kind, x, y,
                Math.Cos(angle) * speed, Math.Sin(angle) * speed,
                (atHeight ?? 0.35) + random.NextDouble() * 0.25, force * 0.5 + random.NextDouble() * 0.4,
                kind == ParticleKind.Smoke ? 42 : 26 + random.Next(14), color,
                kind == ParticleKind.Gib ? 0.10 : 0.055));
        }
    }

    private void UpdateParticles()
    {
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            var p = particles[i];
            p.Life--;
            if (p.Life <= 0) { particles.RemoveAt(i); continue; }
            double dt = 1d / FramesPerSecond;
            p.X += p.VelX * dt; p.Y += p.VelY * dt;
            p.Height += p.VelHeight * dt;
            p.VelHeight -= (p.Kind == ParticleKind.Smoke ? -0.6 : 3.2) * dt;
            p.VelX *= 0.94; p.VelY *= 0.94;
            if (p.Height < 0.03) { p.Height = 0.03; p.VelX *= 0.6; p.VelY *= 0.6; p.VelHeight = 0; }
        }
    }

    private void Notify(string kind, string text)
    { Message = text; MessageFrames = 180; events.Add(new(kind, text)); }

    // Subtítulo narrado (sin voz): distinto del aviso táctico de Notify, se
    // dibuja aparte y dura más para que se pueda leer con calma.
    private void NotifyStory(string speaker, string text)
    { SubtitleSpeaker = speaker; Subtitle = text; SubtitleFrames = 260; events.Add(new("story", text)); }

    // Tecla R durante la partida: inicia una recarga manual si el arma actual
    // tiene cargador, le falta munición y hay reserva disponible.
    private void TryStartReload()
    {
        var weapon = Player.CurrentWeapon;
        if (weapon.MagazineSize <= 0) return;
        if (weapon.Loaded >= weapon.MagazineSize) { Notify("info", "Cargador lleno."); return; }
        if (Player.AmmoOf(weapon.Ammo) <= 0) { Notify("empty", "Sin munición de reserva para recargar."); return; }
        ReloadFramesLeft = Math.Max(1, weapon.ReloadFrames);
        events.Add(new("reload", ""));
        Notify("info", "Recargando " + weapon.ShortName + "...");
    }

    private void FinishReload()
    {
        var weapon = Player.CurrentWeapon;
        int need = weapon.MagazineSize - weapon.Loaded;
        int moved = Math.Min(need, Player.AmmoOf(weapon.Ammo));
        if (moved > 0) { Player.TryConsumeAmmo(weapon.Ammo, moved); weapon.Loaded += moved; }
        events.Add(new("reloaded", ""));
    }

    private static double Distance(double x, double y, double xx, double yy)
        => Math.Sqrt((x - xx) * (x - xx) + (y - yy) * (y - yy));

    // Cada especie avisa con su propio sonido de alerta, para reconocerla de
    // oído antes de verla. El nombre coincide con el efecto en SoundBank.
    private static string GrowlSound(EnemyKind kind) => kind switch
    {
        EnemyKind.Zombi => "growl_zombi",
        EnemyKind.Imp => "growl_imp",
        EnemyKind.Demonio => "growl_demonio",
        EnemyKind.Espectro => "growl_espectro",
        EnemyKind.AlmaPerdida => "growl_almaperdida",
        EnemyKind.Cacodemon => "growl_cacodemon",
        EnemyKind.CaballeroInfernal => "growl_caballeroinfernal",
        EnemyKind.Mancubus => "growl_mancubus",
        EnemyKind.Ciberdemonio => "growl_ciberdemonio",
        _ => "growl"
    };
}
