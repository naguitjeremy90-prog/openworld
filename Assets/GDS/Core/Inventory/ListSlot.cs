namespace GDS.Core {

    [System.Serializable]
    public class ListSlot : Slot {
        public int Index;

        public override string ToString() => Item == null
        ? $"[{Index}] <empty>"
        : $"[{Index}] {Item.Name} ({Item.Id})";

    }
}