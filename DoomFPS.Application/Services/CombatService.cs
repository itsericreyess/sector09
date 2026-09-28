using DoomFPS.Domain;
using DoomFPS.Domain.Entities;
using DoomFPS.Application.Interfaces;

namespace DoomFPS.Application.Services
{
    public class CombatService
    {
        private readonly IGameStateRepository _repository;
        private readonly HashSet<Enemy> _registeredDefeats = new();

        public CombatService(IGameStateRepository repository)
        {
            _repository = repository;
        }

        // Disparo simple: gasta un cartucho y aplica el daño del arma.
        public bool ProcessAttack(Player player, Enemy enemy, Weapon weapon)
        {
            bool success = player.Shoot(enemy, weapon);

            if (success && enemy.Health == 0 && _registeredDefeats.Add(enemy))
            {
                _repository.RegisterEnemyDefeated(enemy.Type);
            }

            return success;
        }

        // Impacto suelto: las armas de perdigones y las explosiones ya descontaron
        // la munición del cargador, así que aquí solo se aplica el daño.
        public bool ApplyDamage(Enemy enemy, int damage)
        {
            if (!enemy.IsAlive || damage <= 0) return false;
            enemy.TakeDamage(damage);

            if (enemy.Health == 0 && _registeredDefeats.Add(enemy))
            {
                _repository.RegisterEnemyDefeated(enemy.Type);
            }

            return true;
        }
    }
}
