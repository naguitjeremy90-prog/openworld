using GDS.Core;
using GDS.Core.Events;

namespace GDS.Common.Events {
    public class PickWorldItem : Command {
        public IWorldItem WorldItem;
        public PickWorldItem(IWorldItem worldItem) => WorldItem = worldItem;
    }

    public class PickWorldItemSuccess : Success {
        public IWorldItem WorldItem;
        public PickWorldItemSuccess(IWorldItem worldItem) => WorldItem = worldItem;
    }

    public class DropWorldItem : Command {
        public Item Item;
        public DropWorldItem(Item item) => Item = item;
    }

    public class DropWorldItemSuccess : Success {
        public Item Item;
        public DropWorldItemSuccess(Item item) => Item = item;
    }


}