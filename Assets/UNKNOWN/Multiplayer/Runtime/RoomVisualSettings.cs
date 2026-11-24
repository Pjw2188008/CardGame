using UnityEngine;
namespace Unknown.Multiplayer
{
    [CreateAssetMenu(menuName="UNKNOWN/Room Visual Settings")]
    public sealed class RoomVisualSettings : ScriptableObject
    {
        public Texture2D titleBackground;
        public Texture2D menuBackground;
        [Header("Button artwork")]
        public Sprite singleButtonNormal,singleButtonPressed;
    }
}
