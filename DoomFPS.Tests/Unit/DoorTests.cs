using Xunit;
using DoomFPS.Domain.Entities;

namespace DoomFPS.Tests.Unit
{
    public class DoorTests
    {
        [Fact]
        public void Escenario_PuertaRoja_SinLlave_NoAbre()
        {
            // Arrange
            var player = new Player(); 
            var door = new Door("Puerta Norte", "Tarjeta Roja");

            // Act
            bool result = door.TryOpen(player);

            // Assert
            Assert.False(result);
            Assert.False(door.IsOpen);
        }

        [Fact]
        public void Escenario_PuertaRoja_ConLlave_Abre()
        {
            // Arrange
            var player = new Player();
            player.AddItem("Tarjeta Roja");
            var door = new Door("Puerta Norte", "Tarjeta Roja");

            // Act
            bool result = door.TryOpen(player);

            // Assert
            Assert.True(result);
            Assert.True(door.IsOpen);
        }
    }
}