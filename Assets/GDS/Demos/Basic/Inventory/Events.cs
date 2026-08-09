using GDS.Core;
using GDS.Core.Events;

namespace GDS.Demos.Basic {
    public class CollectAll : Command {
        public Bag Bag;
        public CollectAll(Bag bag) => Bag = bag;
    };

    public class CraftItemSuccess : PickItemSuccess {
        public CraftItemSuccess(Item item) : base(item) { }
    }
}