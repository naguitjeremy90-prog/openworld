using System;
using System.Collections.Generic;
using System.Linq;
using GDS.Core;
using GDS.Core.Events;

namespace GDS.Demos.Basic {

    [Serializable]
    public class Stash : Bag {
        public Observable<int> CurrentIndex = new(0);
        public ListBag Current => Tabs.ElementAtOrDefault(CurrentIndex.Value);

        public List<ListBag> Tabs = new() {
            new ListBag() { Name = "Tab1", Size = 10 },
            new ListBag() { Name = "Tab2", Size = 5 },
        };

        public override Result Add(Item item) {
            return Current.Add(item);
        }

        public override Result CanAdd(Item item) {
            return Current.CanAdd(item);
        }
    }
}
