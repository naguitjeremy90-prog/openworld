using System;
using GDS.Core;

namespace GDS.Demos.Basic {

    [Serializable]
    public class Inventory : ListBag {
        public Inventory() {
            Name = "Inventory";
            Size = 40;
        }
    }

}