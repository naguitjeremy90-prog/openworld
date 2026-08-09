using GDS.Core;

namespace GDS.Demos.Basic {

    public class CharacterStats {
        public int AttackDamage;
        public float AttackSpeed;
        public float Dps;
        public int Defense;
        public int Weight;

        public override string ToString() => $"dps: {Dps}, defesne: {Defense}, weight: {Weight}";
    }

    public class CharacterSheet {

        public Observable<CharacterStats> Stats = new(new());
        Equipment Equipment;


        public void Init(Equipment equipment) {
            if (Equipment != null) Equipment.CollectionChanged -= Update;
            Equipment = equipment;
            Equipment.CollectionChanged += Update;
            Update();
        }

        void Update() {
            int defense = 0, damage = 0, weight = 0;
            float dps = 0f, aps = 0f;

            foreach (var slot in Equipment.Slots) {
                if (slot.Item is not Basic_Item item) continue;
                if (item is Basic_Armor a) defense += a.Defense;
                if (item is Basic_Weapon b) { damage = b.AttackDamage; aps = b.AttackSpeed; dps = b.Dps; }
                weight += item.Weight();
            }

            var stats = Stats.Value;
            stats.AttackDamage = damage;
            stats.AttackSpeed = aps;
            stats.Dps = dps;
            stats.Defense = defense;
            stats.Weight = weight;

            Stats.SetValue(stats);
        }

    }
}