namespace DoomFPS.Application.Interfaces
{
    public interface IGameStateRepository
    {
        void RegisterDoorOpened(string doorName);
        bool IsDoorOpened(string doorName);
        void RegisterEnemyDefeated(string enemyType);
        int GetDefeatedEnemiesCount();
    }
}
