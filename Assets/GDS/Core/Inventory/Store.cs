using System;
using GDS.Core.Events;
using UnityEngine;

namespace GDS.Core {
    [Serializable]
    public abstract class Store : ScriptableObject, ICanReset {

        [NonSerialized] public EventBus Bus = new();
        [NonSerialized] public Observable<Item> Ghost = new(null);

        const string emptyString = "<empty>";
        public string ghostItem = emptyString;

        virtual public void Reset() {
            Ghost.Reset();
            ghostItem = emptyString;
        }

        protected void UpdateGhost(Result result) {
            if (result is PlaceItemSuccess r) {
                Ghost.SetValue(r.Replaced);
            } else if (result is PickItemSuccess r1) {
                Ghost.SetValue(r1.Item);
            }
            ghostItem = Ghost.Value == null ? emptyString : Ghost.Value.ToString();
        }

    }

}