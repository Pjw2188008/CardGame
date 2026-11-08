using UnityEngine;
namespace Unknown.Battle
{
    /// <summary>Route an existing source without changing its clip or creating another audio manager.</summary>
    [DisallowMultipleComponent,RequireComponent(typeof(AudioSource))]
    public sealed class GameAudioChannel : MonoBehaviour
    {
        public AudioChannel channel=AudioChannel.SFX;
        void OnEnable() {GamePreferences.Instance.Register(GetComponent<AudioSource>(),channel);}
    }
}
