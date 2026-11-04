using UnityEngine;
using UnityEngine.UI;
namespace Unknown.Battle
{
    /// <summary>Local UI-only curved arrow, in the drag layer's coordinate system.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AttackArrowGraphic : MaskableGraphic
    {
        Vector2 start,end;
        float pixelsToUnits=1;
        public void SetPoints(Vector2 from,Vector2 to,Color tint,float scaleFactor=1) { start=from; end=to; pixelsToUnits=1/Mathf.Max(0.01f,scaleFactor); color=tint; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); Vector2 delta=end-start;
            if(delta.sqrMagnitude<4)return;
            var normal=new Vector2(-delta.y,delta.x).normalized;
            Vector2 control=(start+end)/2+normal*Mathf.Min(60,delta.magnitude*0.12f);
            Vector2 previous=start;
            for(int i=1;i<=24;i++)
            {
                float t=i/24f; Vector2 point=(1-t)*(1-t)*start+2*(1-t)*t*control+t*t*end;
                var direction=(point-previous).normalized; var side=new Vector2(-direction.y,direction.x)*3.5f*pixelsToUnits;
                int index=vh.currentVertCount;
                vh.AddVert(previous-side,color,Vector2.zero); vh.AddVert(previous+side,color,Vector2.zero);
                vh.AddVert(point+side,color,Vector2.zero); vh.AddVert(point-side,color,Vector2.zero);
                vh.AddTriangle(index,index+1,index+2); vh.AddTriangle(index,index+2,index+3); previous=point;
            }
            var tipDirection=(end-control).normalized; var tipSide=new Vector2(-tipDirection.y,tipDirection.x)*11*pixelsToUnits;
            int tip=vh.currentVertCount;
            vh.AddVert(end,color,Vector2.zero); vh.AddVert(end-tipDirection*23*pixelsToUnits+tipSide,color,Vector2.zero); vh.AddVert(end-tipDirection*23*pixelsToUnits-tipSide,color,Vector2.zero);
            vh.AddTriangle(tip,tip+2,tip+1);
        }
    }
}
