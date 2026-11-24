using UnityEngine;
using UnityEngine.UI;
namespace Unknown.Battle
{
    // Presentation state is copied with animation ghosts; combat HP/protection stay in the engine.
    public sealed class CardProtectionVisual : MonoBehaviour
    {
        [HideInInspector] public Text health;
        [HideInInspector] public RectTransform border,weaknessBorder;
        [HideInInspector] public int currentHP,protection;
        [HideInInspector] public Color ink;
        [HideInInspector] public bool showHealth;
        static readonly Color Blue=new Color(.32f,.8f,1);
        public void SetState(Text label,int hp,int shield,Color baseInk,bool visible)
        {
            health=label;currentHP=hp;ink=baseInk;showHealth=visible;SetProtection(shield);
        }
        public void SetWeakness(bool active)
        {
            if(active && weaknessBorder==null){
                var go=new GameObject("Weakening border",typeof(RectTransform));weaknessBorder=go.GetComponent<RectTransform>();
                var settings=Resources.Load<CardArtSettings>("CardArtSettings");
                if(settings!=null && settings.weakeningBorder!=null){
                    var image=go.AddComponent<RawImage>();image.texture=settings.weakeningBorder;image.color=Color.white;image.raycastTarget=false;
                }else{
                    var graphic=go.AddComponent<AttackGlowGraphic>();graphic.raycastTarget=false;graphic.color=new Color(.73f,.4f,1);
                }
            }
            if(weaknessBorder==null)return;
            var art=transform.Find("Card Artwork");weaknessBorder.SetParent(art!=null?art:transform,false);
            // Inset from the protection frame so both states remain readable together.
            weaknessBorder.anchorMin=Vector2.zero;weaknessBorder.anchorMax=Vector2.one;
            weaknessBorder.offsetMin=Vector2.one*6;weaknessBorder.offsetMax=Vector2.one*-6;
            weaknessBorder.gameObject.SetActive(active);
        }
        public void SetProtection(int amount)
        {
            protection=Mathf.Max(0,amount);
            if(health!=null){health.text=showHealth?((long)currentHP+protection).ToString():"";health.color=protection>0?Blue:ink;}
            if(protection>0 && border==null){
                var go=new GameObject("Protection border",typeof(RectTransform));border=go.GetComponent<RectTransform>();
                var settings=Resources.Load<CardArtSettings>("CardArtSettings");
                if(settings!=null && settings.protectionBorder!=null){
                    var image=go.AddComponent<RawImage>();image.texture=settings.protectionBorder;image.color=Color.white;image.raycastTarget=false;
                }else{
                    var graphic=go.AddComponent<AttackGlowGraphic>();graphic.raycastTarget=false;graphic.color=Blue;
                }
            }
            if(border==null)return;
            var art=transform.Find("Card Artwork");border.SetParent(art!=null?art:transform,false);
            border.anchorMin=Vector2.zero;border.anchorMax=Vector2.one;border.offsetMin=Vector2.one*2;border.offsetMax=Vector2.one*-2;
            border.gameObject.SetActive(protection>0);
        }
    }
}
