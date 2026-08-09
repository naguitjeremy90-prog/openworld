using UnityEngine.UIElements;
using GDS.Core;

namespace GDS.Demos.Basic {

    public class BasicTooltipView : BaseTooltipView {

        VisualElement Root, WeaponGroup, ArmorGroup;
        Label Attack, Speed, Dps, Defense, Weight, Cost;

        public BasicTooltipView(VisualTreeAsset uxml) {
            uxml.CloneTree(this);
            Root = this.Q<VisualElement>("Root");
            ItemName = this.Q<Label>("Name");

            WeaponGroup = this.Q<VisualElement>(nameof(WeaponGroup));
            ArmorGroup = this.Q<VisualElement>(nameof(ArmorGroup));
            Attack = this.Q<Label>(nameof(Attack));
            Speed = this.Q<Label>(nameof(Speed));
            Dps = this.Q<Label>(nameof(Dps));
            Defense = this.Q<Label>(nameof(Defense));
            Weight = this.Q<Label>(nameof(Weight));
            Cost = this.Q<Label>(nameof(Cost));
        }

        public override void Render(IHoveredItemContext context) {
            var item = context.Item;
            ItemName.text = NameWithStack(item);

            var rarity = item.Rarity().ToString();
            Root.ClearClassList();
            Root.AddToClassList("tooltip");
            Root.AddToClassList(rarity);
            WeaponGroup.Hide();
            ArmorGroup.Hide();

            if (item.Base is not Basic_ItemBase b) return;
            if (item is Basic_Weapon w) {
                Attack.text = $"Damage: {w.AttackDamage}";
                Speed.text = $"Attack speed: {w.AttackSpeed}";
                Dps.text = $"Dps: {w.Dps}";
                WeaponGroup.Show();
            }
            if (item is Basic_Armor a) {
                Defense.text = $"Defense: {a.Defense}";
                ArmorGroup.Show();
            }

            Weight.text = $"Weight: {b.Weight}";
            var cost = item.Stackable ? b.Cost * item.StackSize : b.Cost;
            Cost.text = $"Cost: {cost}";
        }

        string NameWithStack(Item item) => item.Stackable ? $"{item.Name} ({item.StackSize})" : item.Name;

    }

}