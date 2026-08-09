using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GDS.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace GDS.Examples {

#if UNITY_6000_0_OR_NEWER
    [UxmlElement]
#endif

    public partial class QuestView : VisualElement {

#if !UNITY_6000_0_OR_NEWER
        public new class UxmlFactory : UxmlFactory<QuestView> { }
#endif

        public QuestView() {
            this.style.width = 240;

            Add(PreviewQuestItem());
        }

        ListBag PlayerInventory;
        List<QuestData> Quests;
        HashSet<QuestData> Done = new();


        public void Init(ListBag playerInventory, List<QuestData> quests) {
            (PlayerInventory, Quests) = (playerInventory, quests);
            Render();
        }

        void FulfillQuest(QuestData quest) {
            PlayerInventory.ReturnQuest(quest);
            Done.Add(quest);
            Render();
        }

        void Render() {
            if (Quests.Count == 0) { Debug.LogWarning("no quests!"); return; }
            Clear();

            var notDone = Quests.Where(q => !Done.Contains(q));
            VisualElement QuestContainer = new();

            this.Add(notDone.Select(q => Dom.Button(q.name, () => {
                QuestContainer.Clear();
                QuestContainer.Add(QuestItem(q, PlayerInventory.CanFulfill(q)));
            })).ToArray());
            this.Add(QuestContainer);
        }

        VisualElement QuestItem(QuestData quest, bool valid) {
            var el = Dom.Div(
                Dom.Label("title", quest.name),
                Dom.Label("mt-20", "Requirements:"),
                Dom.Div(quest.Requirements.Select(RequirementLine).ToArray()),
                Dom.Label("mt-20", "Rewards"),
                Dom.Div(quest.Rewards.Select(RewardLine).ToArray())
            );

            if (valid) {
                el.Add(Dom.Button("mt-20", "Return Quest", () => FulfillQuest(quest)));
            } else {
                el.Add(Dom.Label("Cannot fulfill quest".Red()));
            }

            return el;
        }

        VisualElement RequirementLine(Requirement r) => Dom.Div("row align-items-center",
            new Image { sprite = r.ItemBase.Icon }.SetSize(32),
            new Label { text = $"{r.ItemBase.Name} x{r.Quantity}" }
        );

        VisualElement RewardLine(Item i) => Dom.Div("row align-items-center",
            new Image { sprite = i.Icon }.SetSize(32),
            new Label { text = $"{i.Name} {(i.Stackable ? $"x{i.StackSize}" : "")}" }
        );



        VisualElement PreviewQuestItem() {
            var el = Dom.Div(
                Dom.Label("title", "Quest Name"),
                Dom.Label("mt-20", "Requirements: "),
                Dom.Div("row", Dom.Div("preview-image-apple").SetSize(32), Dom.Label("Apple (1)")),
                Dom.Label("mt-20", "Rewards: "),
                Dom.Label("Axe"),
                Dom.Button("mt-20", "Return Quest", () => { })
            );

            return el;
        }

    }

}