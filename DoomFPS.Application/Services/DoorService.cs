using DoomFPS.Domain.Entities;
using DoomFPS.Application.Interfaces;

namespace DoomFPS.Application.Services
{
    public class DoorService
    {
        private readonly IGameStateRepository _repository;

        public DoorService(IGameStateRepository repository)
        {
            _repository = repository;
        }

        public bool TryOpenDoor(Door door, Player player)
        {
            bool result = door.TryOpen(player);

            if (result)
            {
                _repository.RegisterDoorOpened(door.Name);
            }

            return result;
        }
    }
}
