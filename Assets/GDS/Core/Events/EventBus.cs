using System;
using System.Collections.Generic;
using UnityEngine;

namespace GDS.Core.Events {
    public class EventBus {
        private Dictionary<Type, List<Action<CustomEvent>>> byType = new();
        private Dictionary<Type, List<Action<CustomEvent>>> byAnyType = new();
        private Dictionary<Delegate, Action<CustomEvent>> lookup = new();

        public bool Logging = true;

        public void On<T>(Action<T> handler) where T : CustomEvent {
            var type = typeof(T);
            if (!byType.TryGetValue(type, out var list)) { byType[type] = list = new(); }
            Action<CustomEvent> wrapper = e => handler((T)e);
            lookup[handler] = wrapper;
            list.Add(wrapper);
        }

        public void Off<T>(Action<T> handler) where T : CustomEvent {
            if (!lookup.TryGetValue(handler, out var wrapper)) return;

            lookup.Remove(handler);
            var type = typeof(T);
            if (byType.TryGetValue(type, out var list)) {
                list.Remove(wrapper);
                if (list.Count == 0) byType.Remove(type);
            }
        }

        public void OnAny<T>(Action<T> handler) where T : CustomEvent {
            var type = typeof(T);
            if (!byAnyType.TryGetValue(type, out var list)) { byAnyType[type] = list = new(); }
            Action<CustomEvent> wrapper = e => handler((T)e);
            lookup[handler] = wrapper;
            list.Add(wrapper);
        }

        public void OffAny<T>(Action<T> handler) where T : CustomEvent {
            if (!lookup.TryGetValue(handler, out var wrapper)) return;

            lookup.Remove(handler);
            var type = typeof(T);
            if (byAnyType.TryGetValue(type, out var list)) {
                list.Remove(wrapper);
                if (list.Count == 0) byAnyType.Remove(type);
            }
        }

        public void Publish(CustomEvent Event) {
            if (Logging) LogUtil.Print($"{Event}");

            var type = Event.GetType();
            if (byType.TryGetValue(type, out var subs)) subs.ForEach(sub => sub.Invoke(Event));

            foreach (var entry in byAnyType) {
                if (entry.Key.IsAssignableFrom(type)) {
                    entry.Value.ForEach(sub => sub.Invoke(Event));
                }
            }
        }
    }

}