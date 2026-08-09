namespace GDS.Demos.Basic {
    [System.Serializable]
    public class PlayerInventory {
        public Equipment Equipment = new();
        public Inventory Inventory = new();
    }
}