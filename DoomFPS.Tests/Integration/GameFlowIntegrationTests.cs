using Xunit;
using DoomFPS.Domain.Entities;
using DoomFPS.Application.Services;
using DoomFPS.Infrastructure.Repositories;

namespace DoomFPS.Tests.Integration
{
    public class GameFlowIntegrationTests
    {
        [Fact]
        public void Escenario_FlujoCompleto_AbrirPuertaYDerrotarEnemigo()
        {
            // Arrange (Dado) - servicios reales + repositorio real, sin mocks
            var repository = new InMemoryGameStateRepository();
            var doorService = new DoorService(repository);
            var combatService = new CombatService(repository);

            var player = new Player { Ammo = 10 };
            player.AddItem("Tarjeta Roja");
            var door = new Door("Puerta Norte", "Tarjeta Roja");
            var weapon = new Weapon { Name = "Escopeta", Damage = 100 };
            var enemy = new Enemy { Type = "Cacodemon", Health = 100 };

            // Act (Cuando) - se ejecuta el flujo completo a través de las capas
            bool doorOpened = doorService.TryOpenDoor(door, player);
            bool enemyDefeated = combatService.ProcessAttack(player, enemy, weapon);

            // Assert (Entonces) - se verifica el efecto persistido en el repositorio
            Assert.True(doorOpened);
            Assert.True(repository.IsDoorOpened("Puerta Norte"));
            Assert.True(enemyDefeated);
            Assert.Equal(1, repository.GetDefeatedEnemiesCount());
        }

        [Fact]
        public void Escenario_LlaveIncorrecta_PuertaNoQuedaRegistradaComoAbierta()
        {
            // Arrange (Dado)
            var repository = new InMemoryGameStateRepository();
            var doorService = new DoorService(repository);
            var player = new Player();
            var door = new Door("Puerta Sur", "Tarjeta Azul");

            // Act (Cuando)
            bool doorOpened = doorService.TryOpenDoor(door, player);

            // Assert (Entonces)
            Assert.False(doorOpened);
            Assert.False(repository.IsDoorOpened("Puerta Sur"));
        }
    }
}
