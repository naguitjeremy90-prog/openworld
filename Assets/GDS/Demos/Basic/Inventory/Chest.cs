using GDS.Core;

namespace GDS.Demos.Basic {
    [System.Serializable]
    public class Chest : DenseListBag {
        public override bool Accepts(Item item) => false;
    }
}
