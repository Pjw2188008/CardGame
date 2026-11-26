using UnityEngine;
using UnityEngine.UI;
namespace Unknown.Battle
{
    // Keep Unity Button input/navigation and disabled behaviour; swap only its artwork.
    public sealed class SingleButtonSkin : Button
    {
        internal const float StandardHeight=54f;
        internal const float CompactHeight=42f;
        internal const float MenuScale=2f/3f;
        internal static readonly Color CaptionColor=new Color(.97f,.91f,.79f);
        [SerializeField] Sprite normalArtwork,pressedArtwork;
        public bool ApplyArtwork()
        {
            var settings=Resources.Load<Unknown.Multiplayer.RoomVisualSettings>("RoomVisualSettings");
            var background=targetGraphic as Image;
            if(settings==null || settings.singleButtonNormal==null || background==null)return false;
            normalArtwork=settings.singleButtonNormal;pressedArtwork=settings.singleButtonPressed!=null?settings.singleButtonPressed:normalArtwork;
            background.sprite=normalArtwork;background.color=Color.white;background.type=Image.Type.Sliced;background.pixelsPerUnitMultiplier=2;
            transition=Transition.ColorTint;
            var tint=colors;tint.highlightedColor=tint.selectedColor=Color.white;
            tint.normalColor=new Color(.9f,.88f,.84f);tint.pressedColor=Color.white;
            tint.disabledColor=new Color(.5f,.5f,.5f,.65f);colors=tint;
            var outline=GetComponent<Outline>();if(outline!=null)outline.enabled=false;
            DoStateTransition(currentSelectionState,true);return true;
        }
        protected override void DoStateTransition(SelectionState state,bool instant)
        {
            base.DoStateTransition(state,instant);
            var background=targetGraphic as Image;
            if(background!=null && normalArtwork!=null)background.overrideSprite=state==SelectionState.Pressed?pressedArtwork:normalArtwork;
        }
    }
}
