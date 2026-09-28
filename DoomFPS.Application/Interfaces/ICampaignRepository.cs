using DoomFPS.Domain;

namespace DoomFPS.Application.Interfaces
{
    // Progreso de la campaña. Como en el proyecto original, la implementación
    // entregada guarda en memoria: al cerrar el juego no queda nada en disco.
    public interface ICampaignRepository
    {
        int HighestLevelReached { get; }
        void SaveCheckpoint(int levelIndex, Loadout loadout);
        Loadout? LoadCheckpoint(int levelIndex);
        void RegisterLevelResult(LevelResult result);
        IReadOnlyList<LevelResult> Results { get; }
        void Reset();
    }
}
