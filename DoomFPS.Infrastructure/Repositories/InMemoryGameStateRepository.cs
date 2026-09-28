using System.Collections.Generic;
using DoomFPS.Application.Interfaces;

namespace DoomFPS.Infrastructure.Repositories
{
    public class InMemoryGameStateRepository : IGameStateRepository
    {
        private readonly HashSet<string> _openedDoors = new();
        private readonly List<string> _defeatedEnemies = new();

        public void RegisterDoorOpened(string doorName)
        {
            _openedDoors.Add(doorName);
        }

        public bool IsDoorOpened(string doorName)
        {
            return _openedDoors.Contains(doorName);
        }

        public void RegisterEnemyDefeated(string enemyType)
        {
            _defeatedEnemies.Add(enemyType);
        }

        public int GetDefeatedEnemiesCount()
        {
            return _defeatedEnemies.Count;
        }
    }
}
