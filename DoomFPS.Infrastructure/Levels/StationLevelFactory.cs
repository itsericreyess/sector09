using DoomFPS.Domain;
using DoomFPS.Domain.Entities;

namespace DoomFPS.Infrastructure.Levels;

// Datos concretos del nivel. Se pueden sustituir por un lector JSON sin cambiar Domain.
public static class StationLevelFactory
{
    public static GameWorld Create()
    {
        string[] map =
        {
            "###################",
            "#.....#.....#.....#",
            "#.....#.....#...X.#",
            "#.....#.....#.....#",
            "#.....#.....#.....#",
            "#..#..#..#..#..#..#",
            "#.....#.....#.....#",
            "#.....#.....#.....#",
            "#.....#.....#.....#",
            "#.....R.....B.....#",
            "#.....#.....#.....#",
            "#.....#.....#.....#",
            "#..#..#..#..#..#..#",
            "#.....#.....#.....#",
            "#.....#.....#.....#",
            "#.....#.....#.....#",
            "#.....#.....#.....#",
            "#.....#.....#.....#",
            "###################"
        };
        var doors = new[]
        {
            new DoorPlacement(6, 9, new Door("Compuerta Roja", "Tarjeta Roja")),
            new DoorPlacement(12, 9, new Door("Compuerta Azul", "Tarjeta Azul"))
        };
        var enemies = new List<Enemy>();
        void Spawn(string type, double x, double y, int health, double speed)
        {
            var enemy = new Enemy { Type = type, Health = health, Speed = speed };
            enemy.SetPosition(x, y); enemies.Add(enemy);
        }
        Spawn("Imp", 4.5, 7.5, 60, 0.75);
        Spawn("Imp", 2.5, 13.5, 60, 0.8);
        Spawn("Cacodemon", 9.5, 7.5, 90, 0.65);
        Spawn("Imp", 10.5, 15.5, 60, 0.8);
        Spawn("Cacodemon", 15.5, 7.5, 90, 0.7);
        Spawn("Imp", 16.5, 14.5, 60, 0.85);
        return new GameWorld(map, doors, enemies, new[]
        {
            new Pickup(PickupKind.RedKey, 3.5, 16.5),
            new Pickup(PickupKind.BlueKey, 9.5, 2.5),
            new Pickup(PickupKind.Ammo, 2.5, 4.5),
            new Pickup(PickupKind.Ammo, 4.5, 15.5),
            new Pickup(PickupKind.Ammo, 8.5, 9.5),
            new Pickup(PickupKind.Ammo, 14.5, 10.5),
            new Pickup(PickupKind.Health, 1.5, 10.5),
            new Pickup(PickupKind.Health, 10.5, 4.5),
            new Pickup(PickupKind.Health, 14.5, 15.5)
        });
    }
}
