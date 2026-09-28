using DoomFPS.Application.Interfaces;
using DoomFPS.Domain;

namespace DoomFPS.Infrastructure.Repositories
{
    // Progreso de la campaña guardado solo durante la partida, igual que el
    // repositorio original: no hay base de datos ni archivo en disco.
    public class InMemoryCampaignRepository : ICampaignRepository
    {
        private readonly Dictionary<int, Loadout> _checkpoints = new();
        private readonly List<LevelResult> _results = new();

        public int HighestLevelReached { get; private set; }

        public void SaveCheckpoint(int levelIndex, Loadout loadout)
        {
            _checkpoints[levelIndex] = loadout;
            if (levelIndex > HighestLevelReached) HighestLevelReached = levelIndex;
        }

        public Loadout? LoadCheckpoint(int levelIndex)
            => _checkpoints.TryGetValue(levelIndex, out var loadout) ? loadout : null;

        public void RegisterLevelResult(LevelResult result)
        {
            _results.RemoveAll(r => r.LevelIndex == result.LevelIndex);
            _results.Add(result);
            _results.Sort((a, b) => a.LevelIndex.CompareTo(b.LevelIndex));
        }

        public IReadOnlyList<LevelResult> Results => _results;

        public void Reset()
        {
            _checkpoints.Clear();
            _results.Clear();
            HighestLevelReached = 0;
        }
    }
}
