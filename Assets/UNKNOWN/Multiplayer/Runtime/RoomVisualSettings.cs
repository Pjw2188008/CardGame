using UnityEngine;
namespace Unknown.Multiplayer
{
    [CreateAssetMenu(menuName="UNKNOWN/Room Visual Settings")]
    public sealed class RoomVisualSettings : ScriptableObject
    {
        // Neutral overlays reduce both brightness and colour intensity behind foreground UI.
        internal static readonly Color BattleShade = new Color(.12f,.12f,.12f,.43f);
        internal static readonly Color MenuShade = new Color(.12f,.12f,.12f,.50f);
        internal static readonly Color TitleShade = new Color(.12f,.12f,.12f,.24f);
        public Texture2D titleBackground;
        public Texture2D menuBackground;
        [Header("Button artwork")]
        public Sprite singleButtonNormal,singleButtonPressed;
    }
}
