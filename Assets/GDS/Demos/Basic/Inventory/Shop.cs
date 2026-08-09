using System;
using System.Collections.Generic;
using System.Linq;
using GDS.Core;
using GDS.Core.Events;
using UnityEngine;

namespace GDS.Demos.Basic {
    [Serializable]
    public class Shop : DenseListBag {
        public List<Basic_ItemBase> Catalog = new();
        Observable<int> PlayerGold;

        // PlayerGold should be injected by a Store or a MonoBehavior on init/awake
        public void Init(Observable<int> playerGold) {
            PlayerGold = playerGold;
            Refresh();
        }

        public void Refresh() {
            Clear();
            AddRange(Catalog.Select(b => b.CreateItem()).OrderBy(_ => UnityEngine.Random.Range(0, 100)));
        }

        public override bool AllowStacking() => false;

        public override Result Add(Item item) {
            var result = base.Add(item);
            if (result is Success) PlayerGold.SetValue(PlayerGold.Value + item.Cost());
            return result.MapTo(new SellItemSuccess(item, null));
        }

        public override Result AddAt(Slot slot, Item item) {
            return Add(item);
        }

        public override Result CanRemove(Item item) {
            var itemCost = item.Cost();
            if (PlayerGold.Value < itemCost) { Debug.Log("Not enough gold".Red()); }
            return PlayerGold.Value < itemCost ? Result.Fail : Result.Success;
        }

        public override Result Remove(Item item) {
            var result = CanRemove(item);
            if (result is Success) result = base.Remove(item);
            if (result is Success) PlayerGold.SetValue(PlayerGold.Value - item.Cost());
            return result.MapTo(new BuyItemSuccess(item));
        }
    }

}