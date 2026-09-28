using DoomFPS.Application.Interfaces;
using DoomFPS.Application.Services;
using DoomFPS.Domain.Entities;
using Moq;
using Xunit;

namespace DoomFPS.Tests.Unit;

public class ServiceRepositoryTests
{
    [Fact] public void EnemigoSeRegistraUnaSolaVezAunqueSeVuelvaADisparar()
    {
        var repo = new Mock<IGameStateRepository>(); var service = new CombatService(repo.Object);
        var p = new Player(); var enemy = new Enemy { Health = 30 };
        service.ProcessAttack(p, enemy, new Weapon()); service.ProcessAttack(p, enemy, new Weapon());
        repo.Verify(r => r.RegisterEnemyDefeated(enemy.Type), Times.Once);
    }

    [Fact] public void GolpeNoLetalNoRegistraDerrota()
    {
        var repo = new Mock<IGameStateRepository>();
        new CombatService(repo.Object).ProcessAttack(new Player(), new Enemy(), new Weapon());
        repo.Verify(r => r.RegisterEnemyDefeated(It.IsAny<string>()), Times.Never);
    }

    [Fact] public void DosEnemigosDelMismoTipoSeCuentanPorSeparado()
    {
        var repo = new Mock<IGameStateRepository>(); var service = new CombatService(repo.Object);
        var p = new Player(); var w = new Weapon { Damage = 100 };
        service.ProcessAttack(p, new Enemy { Type = "Imp" }, w);
        service.ProcessAttack(p, new Enemy { Type = "Imp" }, w);
        repo.Verify(r => r.RegisterEnemyDefeated("Imp"), Times.Exactly(2));
    }

    [Fact] public void AbrirConLlaveCorrectaRegistraNombre()
    {
        var repo = new Mock<IGameStateRepository>(); var p = new Player(); p.AddItem("Tarjeta Azul");
        Assert.True(new DoorService(repo.Object).TryOpenDoor(new Door("Azul", "Tarjeta Azul"), p));
        repo.Verify(r => r.RegisterDoorOpened("Azul"), Times.Once);
    }

    [Fact] public void LlaveIncorrectaNoEscribeEnRepositorio()
    {
        var repo = new Mock<IGameStateRepository>(); var p = new Player(); p.AddItem("Tarjeta Roja");
        Assert.False(new DoorService(repo.Object).TryOpenDoor(new Door("Azul", "Tarjeta Azul"), p));
        repo.Verify(r => r.RegisterDoorOpened(It.IsAny<string>()), Times.Never);
    }
}
