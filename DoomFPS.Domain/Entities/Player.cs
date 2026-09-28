using System.Collections.Generic;

namespace DoomFPS.Domain.Entities
{
    public class Player
    {
        private readonly int[] pouch = new int[4];
        private readonly List<Weapon> weapons = new() { new Weapon() };
        private int weaponIndex;

        public int Health { get; set; } = 100;
        public int Armor { get; set; }
        public int HealthCap { get; set; } = 100;
        public int ArmorCap { get; set; } = 100;
        public List<string> Inventory { get; set; } = new List<string>();
        public double X { get; private set; } = 2.5;
        public double Y { get; private set; } = 2.5;
        public double Angle { get; private set; } = Math.PI / 2;
        public bool IsAlive => Health > 0;
        // Inclinación vertical, positiva hacia arriba. La posición sigue sobre el suelo.
        public const double MaxPitch = Math.PI * 55 / 180;
        public double Pitch { get; private set; }
        public void Look(double radians)
        {
            if (!double.IsFinite(radians)) throw new ArgumentOutOfRangeException(nameof(radians));
            Pitch = Math.Clamp(Pitch + radians, -MaxPitch, MaxPitch);
        }
        public void CenterView() => Pitch = 0;

        // Retroceso visual y sacudida: la presentación los lee, no cambian reglas.
        public double ViewKick { get; set; }
        public int BackpackLevel { get; set; }

        // Salto: solo desplaza la cámara en un arco (no hay obstáculos con
        // altura en este mundo 2D), pero se siente como un salto de verdad.
        // No se puede volver a saltar hasta aterrizar.
        private double jumpVelocity;
        public double JumpHeight { get; private set; }
        public bool IsAirborne => JumpHeight > 0 || jumpVelocity > 0;

        public void UpdateJump(bool jumpRequested)
        {
            const double impulse = 0.043, gravity = 0.00287;
            if (jumpRequested && !IsAirborne) jumpVelocity = impulse;
            if (jumpVelocity == 0 && JumpHeight == 0) return;
            JumpHeight += jumpVelocity;
            jumpVelocity -= gravity;
            if (JumpHeight <= 0) { JumpHeight = 0; jumpVelocity = 0; }
        }

        public Player() { pouch[(int)AmmoKind.Shells] = 10; }

        // Munición de cartuchos. Nombre heredado del proyecto original.
        public int Ammo
        {
            get => pouch[(int)AmmoKind.Shells];
            set => pouch[(int)AmmoKind.Shells] = Math.Max(0, value);
        }

        public IReadOnlyList<Weapon> Weapons => weapons;
        public Weapon CurrentWeapon => weapons[Math.Clamp(weaponIndex, 0, weapons.Count - 1)];
        public int WeaponIndex => weaponIndex;

        public int AmmoOf(AmmoKind kind) => pouch[(int)kind];

        public int AmmoCap(AmmoKind kind)
        {
            int baseCap = kind switch
            {
                AmmoKind.Shells => 99,
                AmmoKind.Bullets => 220,
                AmmoKind.Cells => 320,
                _ => 50
            };
            return baseCap * (1 + BackpackLevel);
        }

        // Devuelve lo realmente añadido para poder avisar "cargador lleno".
        public int AddAmmo(AmmoKind kind, int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            int cap = AmmoCap(kind), before = pouch[(int)kind];
            pouch[(int)kind] = Math.Min(cap, before + amount);
            return pouch[(int)kind] - before;
        }

        public bool HasWeapon(WeaponKind kind)
        {
            foreach (var w in weapons) if (w.Kind == kind) return true;
            return false;
        }

        // Al recoger un arma nueva se equipa sola, como en los FPS clásicos.
        public bool GiveWeapon(WeaponKind kind)
        {
            if (HasWeapon(kind)) return false;
            weapons.Add(Weapon.Create(kind));
            weapons.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            weaponIndex = weapons.FindIndex(w => w.Kind == kind);
            return true;
        }

        public void ClearWeapons() { weapons.Clear(); weaponIndex = 0; }

        public bool SelectSlot(int slot)
        {
            int index = weapons.FindIndex(w => w.Slot == slot);
            if (index < 0 || index == weaponIndex) return false;
            weaponIndex = index; return true;
        }

        public bool CycleWeapon(int direction)
        {
            if (weapons.Count < 2) return false;
            weaponIndex = ((weaponIndex + direction) % weapons.Count + weapons.Count) % weapons.Count;
            return true;
        }

        public void SetPosition(double x, double y)
        {
            if (!double.IsFinite(x) || !double.IsFinite(y)) throw new ArgumentOutOfRangeException(nameof(x));
            X = x; Y = y;
        }

        public void Rotate(double radians)
        {
            if (!double.IsFinite(radians)) throw new ArgumentOutOfRangeException(nameof(radians));
            Angle = Math.IEEERemainder(Angle + radians, Math.PI * 2);
        }

        // La armadura absorbe un tercio del golpe mientras le queden puntos.
        public void TakeDamage(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (Armor > 0 && amount > 0)
            {
                int absorbed = Math.Min(Armor, Math.Max(1, amount / 3));
                Armor -= absorbed; amount -= absorbed;
            }
            Health = Math.Max(0, Health - amount);
        }

        public void Heal(int amount) => HealUpTo(amount, 100);

        public void HealUpTo(int amount, int cap)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (!IsAlive) return;
            Health = Math.Max(Health, Math.Min(cap, Health + amount));
        }

        public int AddArmor(int amount, int cap)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            int before = Armor;
            Armor = Math.Max(Armor, Math.Min(cap, Armor + amount));
            return Armor - before;
        }

        public bool TryConsumeAmmo() => TryConsumeAmmo(AmmoKind.Shells, 1);

        public bool TryConsumeAmmo(AmmoKind kind, int amount)
        {
            if (!IsAlive || amount <= 0 || pouch[(int)kind] < amount) return false;
            pouch[(int)kind] -= amount; return true;
        }

        public bool Shoot(Enemy target, Weapon weapon)
        {
            if (target.Health > 0 && TryConsumeAmmo())
            {
                target.TakeDamage(weapon.Damage);
                return true;
            }
            return false;
        }

        public void AddItem(string item)
        {
            if (!Inventory.Contains(item))
            {
                Inventory.Add(item);
            }
        }

        // Estado que viaja de una misión a la siguiente.
        public Loadout Capture() => new(
            Health, Armor,
            new Dictionary<AmmoKind, int>
            {
                [AmmoKind.Shells] = pouch[0], [AmmoKind.Bullets] = pouch[1],
                [AmmoKind.Cells] = pouch[2], [AmmoKind.Rockets] = pouch[3]
            },
            weapons.Select(w => w.Kind).ToArray(), CurrentWeapon.Kind, BackpackLevel);

        public void Apply(Loadout loadout)
        {
            Health = Math.Max(1, loadout.Health); Armor = loadout.Armor;
            BackpackLevel = loadout.BackpackLevel;
            for (int i = 0; i < pouch.Length; i++) pouch[i] = 0;
            foreach (var pair in loadout.Ammo) pouch[(int)pair.Key] = Math.Clamp(pair.Value, 0, AmmoCap(pair.Key));
            weapons.Clear();
            foreach (var kind in loadout.Weapons) weapons.Add(Weapon.Create(kind));
            if (weapons.Count == 0) weapons.Add(Weapon.Create(WeaponKind.Pistola));
            weapons.Sort((a, b) => a.Slot.CompareTo(b.Slot));
            weaponIndex = Math.Max(0, weapons.FindIndex(w => w.Kind == loadout.Current));
        }
    }
}
