using System.Linq;
using GDS.Core;
using GDS.Core.Events;

namespace GDS.Examples {
    public static class Extensions {
        public static int GetTotalQuantity(this Bag bag, ItemBase itemBase) {
            var items = bag.Items.Where(i => i.Base == itemBase);
            int q = items.Sum(i => i.StackSize);
            return q;
        }

        public static bool CanFulfillRequirement(this Bag bag, Requirement r) {
            return bag.GetTotalQuantity(r.ItemBase) >= r.Quantity;
        }

        public static bool CanFulfill(this Bag bag, QuestData quest) {
            var x = quest.Requirements.Where(bag.CanFulfillRequirement);
            return x.Count() == quest.Requirements.Count;
        }

        public static Result ReturnQuest(this Bag bag, QuestData quest) {
            foreach (var r in quest.Requirements) {
                var remaining = r.Quantity;
                foreach (var item in bag.Items) {
                    if (remaining == 0) continue;
                    if (item.Base == r.ItemBase) {
                        if (item.StackSize > remaining) {
                            item.StackSize -= remaining;
                            remaining = 0;
                        } else {
                            remaining -= item.StackSize;
                            bag.Remove(item);
                        }
                    }
                }
            }

            bag.AddRange(quest.Rewards);
            return Result.Success;
        }
    }

}