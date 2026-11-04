using UnityEngine;
using UnityEngine.UI;

namespace Unknown.Battle
{
    /// <summary>Hollow, fading UI glow. Never paints over the card face.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AttackGlowGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r=rectTransform.rect;
            for(int i=0;i<6;i++)
            {
                float outer=i+1,inner=i; var tint=color; tint.a*=1-i/6f;
                Quad(vh,new Rect(r.xMin-outer,r.yMin-outer,r.width+2*outer,1),tint);
                Quad(vh,new Rect(r.xMin-outer,r.yMax+inner,r.width+2*outer,1),tint);
                Quad(vh,new Rect(r.xMin-outer,r.yMin-inner,1,r.height+2*inner),tint);
                Quad(vh,new Rect(r.xMax+inner,r.yMin-inner,1,r.height+2*inner),tint);
            }
        }
        static void Quad(VertexHelper vh,Rect r,Color tint)
        {
            int n=vh.currentVertCount;
            vh.AddVert(new Vector2(r.xMin,r.yMin),tint,Vector2.zero); vh.AddVert(new Vector2(r.xMin,r.yMax),tint,Vector2.zero);
            vh.AddVert(new Vector2(r.xMax,r.yMax),tint,Vector2.zero); vh.AddVert(new Vector2(r.xMax,r.yMin),tint,Vector2.zero);
            vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+3);
        }
    }
}
