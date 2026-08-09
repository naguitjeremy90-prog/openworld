using UnityEngine;
using GDS.Core;
using GDS.Core.Events;
using GDS.Common.Scripts;
using GDS.Common.Events;

namespace GDS.Common {
    /// <summary>
    /// Spawns an item prefab on the ground when it is dropped.
    /// Requires player position (Defaults to world center).
    /// Despawns (destroys) the game object when it is clicked.
    /// </summary>
    public class SpawnDespawnItemPrefab : MonoBehaviour {

        [Required]
        public Store Store;
        public Transform SpawnPoint;

        [Tooltip("Wraps the item prefab. It should come with a WorldItem component. WorldItem is used to pick the item back from the world.")]
        public GameObject WrapperPrefab;
        [Tooltip("Used if item does not come with a prefab")]
        public GameObject DefaultPrefab;
        [Range(0, 5)]
        public float DropRadius = 2;
        public Vector3 DropOffset = new Vector3(0, 0.5f, 0);
        public ParticleSystem DespawnVFX;

        private void OnEnable() {
            Store.Bus.On<DropWorldItemSuccess>(SpawnItem);
            Store.Bus.On<PickWorldItemSuccess>(DespawnItem);
        }

        private void OnDisable() {
            Store.Bus.Off<DropWorldItemSuccess>(SpawnItem);
            Store.Bus.Off<PickWorldItemSuccess>(DespawnItem);
        }

        void SpawnItem(CustomEvent e) {
            if (e is not DropWorldItemSuccess evt) return;

            var pos = SpawnPoint == null ? new Vector3() : new Vector3(SpawnPoint.position.x, 0, SpawnPoint.position.z);
            pos = pos + RandomPointOnCircle(DropRadius) + DropOffset;
            var instance = Instantiate(WrapperPrefab, pos, Quaternion.identity);

            WorldItem worldItem = instance.GetComponent<WorldItem>();
            if (worldItem == null) return;

            worldItem.Item = evt.Item;
            worldItem.OnClick += OnPickWorldItem;

            GameObject prefab = DefaultPrefab;
            if (evt.Item is Item item && item.Base is IHasPrefab itemBase && itemBase.Prefab != null)
                prefab = itemBase.Prefab;

            worldItem.AddItemPrefab(prefab);
        }

        void DespawnItem(CustomEvent evt) {
            if (evt is not PickWorldItemSuccess e) return;
            Destroy(e.WorldItem.GameObject);
            if (DespawnVFX == null) return;
            Instantiate(DespawnVFX, e.WorldItem.GameObject.transform.position, Quaternion.identity);
        }

        void OnPickWorldItem(IWorldItem worldItem) {
            Store.Bus.Publish(new PickWorldItem(worldItem));
        }

        Vector3 RandomPointOnCircle(float radius) {
            float angle = Random.Range(0, 360);
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            return new Vector3(x, 0, z);
        }
    }

}