using System;

namespace GDS.Core {

    /// <summary>
    /// A class that wraps a value and allows subscribers to watch for value changes.
    /// </summary>
    public class Observable<T> {
        public Observable(T initialValue) {
            this.initialValue = initialValue;
            Value = initialValue;
        }

        T initialValue;
        public T Value { get; private set; }
        public event Action<T> OnChange = (_) => { };
        public void Notify() => OnChange(Value);
        public void SetValue(T value) {
            if (Value == null && value == null) return;
            Value = value;
            Notify();
        }
        public void Reset() => SetValue(initialValue);
    }

    public static class ObservableExt {
        public static void Toggle(this Observable<bool> obs) => obs.SetValue(!obs.Value);
    }
}