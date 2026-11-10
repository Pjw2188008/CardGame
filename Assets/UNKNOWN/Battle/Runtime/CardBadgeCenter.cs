using UnityEngine;
using UnityEngine.UI;
namespace Unknown.Battle
{
    // Center the visible digits, independent of the font's ascender/descender padding.
    public sealed class CardBadgeCenter : BaseMeshEffect
    {
        public override void ModifyMesh(VertexHelper vertices)
        {
            if(!IsActive() || vertices.currentVertCount==0)return;
            var min=new Vector2(float.MaxValue,float.MaxValue);var max=new Vector2(float.MinValue,float.MinValue);
            UIVertex vertex=default;bool visible=false;
            for(int i=0;i<vertices.currentVertCount;i++) {
                vertices.PopulateUIVertex(ref vertex,i);if(vertex.color.a==0)continue;
                min=Vector2.Min(min,vertex.position);max=Vector2.Max(max,vertex.position);visible=true;
            }
            if(!visible)return;
            var bounds=graphic.rectTransform.rect;Vector2 center=(min+max)*.5f;Vector2 size=max-min;
            float scale=Mathf.Min(1f,Mathf.Max(0,bounds.width-2)/Mathf.Max(.001f,size.x),Mathf.Max(0,bounds.height-2)/Mathf.Max(.001f,size.y));
            for(int i=0;i<vertices.currentVertCount;i++){vertices.PopulateUIVertex(ref vertex,i);vertex.position=(vertex.position-(Vector3)center)*scale+(Vector3)bounds.center;vertices.SetUIVertex(vertex,i);}
        }
    }
}
