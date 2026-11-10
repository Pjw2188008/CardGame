using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Unknown.Battle
{
    // Keep the original input/drag rectangle; fit artwork and data labels inside it.
    public sealed class CardArtLayout : UIBehaviour
    {
        public RawImage artwork;
        public RectTransform mana,description,attack,health;
        public bool joker;
        bool layoutPending;


        protected override void OnRectTransformDimensionsChange(){base.OnRectTransformDimensionsChange();layoutPending=true;RefreshLayout();}
        protected override void OnEnable(){base.OnEnable();layoutPending=true;RefreshLayout();}
        void LateUpdate(){if(layoutPending){RefreshLayout();layoutPending=false;}}
        public void RefreshLayout()
        {
            if(artwork==null || artwork.texture==null)return;
            var rect=((RectTransform)transform).rect;if(rect.width<=0 || rect.height<=0)return;
            float aspect=(float)artwork.texture.width/artwork.texture.height;
            Vector2 span=rect.width/rect.height>aspect ? new Vector2(rect.height*aspect/rect.width,1) : new Vector2(1,rect.width/aspect/rect.height);
            Vector2 origin=(Vector2.one-span)*.5f;
            void Place(RectTransform target,Vector2 min,Vector2 max) {if(target==null)return;target.anchorMin=origin+Vector2.Scale(min,span);target.anchorMax=origin+Vector2.Scale(max,span);target.offsetMin=target.offsetMax=Vector2.zero;}
            Place(artwork.rectTransform,Vector2.zero,Vector2.one);
            Place(mana,joker ? new Vector2(.13f,.815f) : new Vector2(.085f,.785f),joker ? new Vector2(.31f,.965f) : new Vector2(.265f,.935f));
            Place(description,new Vector2(.15f,joker ? .22f : .24f),new Vector2(.87f,.40f));
            Place(attack,joker ? new Vector2(.30f,.115f) : new Vector2(.115f,.095f),joker ? new Vector2(.70f,.205f) : new Vector2(.275f,.235f));
            Place(health,new Vector2(.745f,.105f),new Vector2(.905f,.245f));
            float scale=Mathf.Clamp(rect.height*span.y/244f,.5f,3f);
            void Fit(Text text,int maximum,int minimum) {if(text==null)return;text.resizeTextForBestFit=true;text.resizeTextMinSize=minimum;text.resizeTextMaxSize=Mathf.Max(minimum,Mathf.RoundToInt(maximum*scale));text.fontSize=text.resizeTextMaxSize;}
            Fit(mana==null ? null : mana.GetComponentInChildren<Text>(),18,9);
            Fit(description==null ? null : description.GetComponent<Text>(),10,7);
            Fit(attack==null ? null : attack.GetComponent<Text>(),joker ? 12 : 18,joker ? 7 : 10);
            Fit(health==null ? null : health.GetComponent<Text>(),18,9);
        }
    }
}
