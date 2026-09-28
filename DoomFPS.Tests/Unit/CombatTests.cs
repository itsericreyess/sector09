using Xunit;
using DoomFPS.Domain.Entities;

namespace DoomFPS.Tests.Unit
{
    public class CombatTests
    {
        [Fact]
        public void Escenario_JugadorAciertaDisparo_ReduceVidaYMunicion()
        {
            // Arrange (Dado)
            var player = new Player { Ammo = 10 };
            var weapon = new Weapon { Name = "Escopeta", Damage = 30 };
            var enemy = new Enemy { Type = "Cacodemon", Health = 100 };

            // Act (Cuando)
            bool hitSuccess = player.Shoot(enemy, weapon);

            // Assert (Entonces)
            Assert.True(hitSuccess);
            Assert.Equal(70, enemy.Health);
            Assert.Equal(9, player.Ammo);
        }
    }
}