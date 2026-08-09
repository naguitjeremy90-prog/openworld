using System;
using UnityEngine;

namespace GDS.Core {
    public interface IItemContext {
        Bag Bag { get; }
        Slot Slot { get; }
        Item Item { get; }
    }

    public interface IHoveredItemContext {
        Bag Bag { get; }
        Item Item { get; }
        Rect WorldBound { get; }
    }

    public interface IWorldItem {
        Item Item { get; }
        GameObject GameObject { get; }
    }

    public interface IHasPrefab {
        GameObject Prefab { get; }
    }

    public interface ICanReset {
        void Reset();
    }

    public interface ITag { }

    public class Pos {
        public int X, Y;
        public Pos(int x, int y) => (X, Y) = (x, y);
        public void Deconstruct(out int x, out int y) => (x, y) = (X, Y);
        public override string ToString() => $"Pos: {{ x: {X}, y: {Y} }}";
    }

    [Serializable]
    public class Size {
        public int W, H;
        public Size(int w, int h) => (W, H) = (w, h);
        public void Deconstruct(out int w, out int h) => (w, h) = (W, H);
        public override string ToString() => $"Size: {{ w: {W}, h: {H} }}";
    }

    public enum Direction { N = 0, E = 90, S = 180, W = 270 }

    [Serializable]
    public class IntRange {
        public int Min, Max;
        public float Avg => Min + (Max - Min) * .5f;
        public int Roll() => UnityEngine.Random.Range(Min, Max + 1);
        public IntRange Clone() => new() { Min = Min, Max = Max };
        public override string ToString() => $"{Min}-{Max}";
        public static IntRange operator +(IntRange a, IntRange b) => new() { Min = a.Min + b.Min, Max = a.Max + b.Max };
    }

    [Serializable]
    public class FloatRange {
        public float Min, Max;
        public float Avg => Min + (Max - Min) * .5f;
        public float Roll() => UnityEngine.Random.Range(Min, Max);
    }

}