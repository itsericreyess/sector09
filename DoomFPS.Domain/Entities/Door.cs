namespace DoomFPS.Domain.Entities
{
    public class Door
    {
        public string Name { get; set; }
        public string RequiredKey { get; set; }
        public bool IsOpen { get; private set; } = false;

        public Door(string name, string requiredKey)
        {
            Name = name;
            RequiredKey = requiredKey;
        }

        // Sin llave declarada la compuerta es de servicio: se abre con solo pulsar E.
        public bool NeedsKey => !string.IsNullOrEmpty(RequiredKey);

        public bool TryOpen(Player player)
        {
            if (!NeedsKey || player.Inventory.Contains(RequiredKey))
            {
                IsOpen = true;
                return true;
            }
            return false;
        }
    }
}