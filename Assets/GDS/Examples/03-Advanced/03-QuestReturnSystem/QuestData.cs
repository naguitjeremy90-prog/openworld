using System.Collections.Generic;
using GDS.Core;
using UnityEngine;

namespace GDS.Examples {

    [CreateAssetMenu(menuName = "SO/Examples/QuestReturn/QuestData")]
    public class QuestData : ScriptableObject {
        public List<Requirement> Requirements;
        [SerializeReference]
        public List<Item> Rewards;
    }

    [System.Serializable]
    public class Requirement {
        public ItemBase ItemBase;
        [Min(1)]
        public int Quantity = 1;
    }


}
