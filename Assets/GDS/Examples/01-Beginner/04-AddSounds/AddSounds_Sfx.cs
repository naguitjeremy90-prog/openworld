using GDS.Core;
using GDS.Core.Events;
using UnityEngine;

namespace GDS.Examples {

    [RequireComponent(typeof(AudioSource))]
    public class AddSounds_Sfx : MonoBehaviour {
        [System.Serializable]
        public class SoundList {
            public AudioClip Fail;
            public AudioClip Pick;
            public AudioClip Place;
        }

        [Required]
        public Store store;
        [Space(12)]
        public SoundList Sounds;

        AudioSource audioSource;

        void Awake() {
            audioSource = GetComponent<AudioSource>();
        }
        void OnEnable() {
            store.Bus.OnAny<Result>(PlaySound);
        }
        void OnDisable() {
            store.Bus.OffAny<Result>(PlaySound);
        }

        void PlaySound(Result result) {
            var clip = GetClip(result);
            if (clip == null) return;
            audioSource.PlayOneShot(clip);
        }

        AudioClip GetClip(Result result) => result switch {
            Fail => Sounds.Fail,
            PickItemSuccess => Sounds.Pick,
            PlaceItemSuccess => Sounds.Place,
            _ => null
        };

    }

}