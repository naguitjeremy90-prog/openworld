using GDS.Core;
using GDS.Core.Events;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Demos.Basic {

    public class CharacterSheetWindow : WindowView {

        public CharacterSheetWindow(CharacterSheet characterSheet, Basic_Store store) {
            Init("Character Sheet", () => store.Bus.Publish(new CloseWindow(characterSheet)));
            AddToClassList("character-sheet-window");

            var vta = Resources.Load<VisualTreeAsset>("CharacterSheetView");
            Container.Add(vta.Instantiate());

            var AttackDamage = this.Q<Label>("AttackDamage");
            var AttackSpeed = this.Q<Label>("AttackSpeed");
            var Dps = this.Q<Label>("Dps");
            var Defense = this.Q<Label>("Defense");
            var Weight = this.Q<Label>("Weight");

            this.Observe(store.CharacterSheet.Stats, value => {
                AttackDamage.text = value.AttackDamage.ToString();
                AttackSpeed.text = value.AttackSpeed.ToString();
                Dps.text = ColorizedValue(value.Dps);
                Defense.text = ColorizedValue(value.Defense);
                Weight.text = value.Weight.ToString();
            });

            string ColorizedValue(object value) => value switch {
                0 => value.ToString().Red(),
                0f => value.ToString().Red(),
                _ => value.ToString()
            };

        }

    }

}