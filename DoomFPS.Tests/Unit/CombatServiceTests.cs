using Xunit;
using Moq;
using DoomFPS.Domain.Entities;
using DoomFPS.Application.Services;
using DoomFPS.Application.Interfaces;

namespace DoomFPS.Tests.Unit
{
    public class CombatServiceTests
    {
        [Fact]
        public void Escenario_EnemigoDerrotado_RegistraEnRepositorio()
        {
            // Arrange (Dado)
            var mockRepo = new Mock<IGameStateRepository>();
            var service = new CombatService(mockRepo.Object);
            var player = new Player { Ammo = 5 };
            var weapon = new Weapon { Name = "Escopeta", Damage = 100 };
            var enemy = new Enemy { Type = "Cacodemon", Health = 100 };

            // Act (Cuando)
            bool result = service.ProcessAttack(player, enemy, weapon);

            // Assert (Entonces)
            Assert.True(result);
            Assert.Equal(0, enemy.Health);
            mockRepo.Verify(r => r.RegisterEnemyDefeated("Cacodemon"), Times.Once);
        }

        [Fact]
        public void Escenario_SinMunicion_NoRegistraDerrota()
        {
            // Arrange (Dado)
            var mockRepo = new Mock<IGameStateRepository>();
            var service = new CombatService(mockRepo.Object);
            var player = new Player { Ammo = 0 };
            var weapon = new Weapon { Name = "Escopeta", Damage = 30 };
            var enemy = new Enemy { Type = "Imp", Health = 50 };

            // Act (Cuando)
            bool result = service.ProcessAttack(player, enemy, weapon);

            // Assert (Entonces)
            Assert.False(result);
            Assert.Equal(50, enemy.Health);
            mockRepo.Verify(r => r.RegisterEnemyDefeated(It.IsAny<string>()), Times.Never);
        }
    }
}
