using UnityEngine;
using UnityEngine.UI;
namespace Unknown.Battle
{
    [RequireComponent(typeof(Text))]
    public sealed class CardDescriptionFit : BaseMeshEffect
    {
        readonly TextGenerator measure=new TextGenerator();
        Text label;
        string previous;
        Vector2 previousSize;
        Font previousFont;
        int maximum=12,previousMaximum,chosen=6;
        float previousPixels;
        public void SetMaximum(int size){maximum=Mathf.Max(1,size);RefreshFit();}
        protected override void OnEnable(){base.OnEnable();RefreshFit();}
        protected override void OnRectTransformDimensionsChange(){base.OnRectTransformDimensionsChange();RefreshFit();}
        void LateUpdate(){RefreshFit();}
        void RefreshFit()
        {
            if(label==null)label=GetComponent<Text>();
            if(label==null || label.font==null)return;
            label.resizeTextForBestFit=false;
            label.horizontalOverflow=HorizontalWrapMode.Wrap;
            // Generate every line; the measured font and mesh bounds keep it inside the card.
            label.verticalOverflow=VerticalWrapMode.Overflow;
            label.alignment=TextAnchor.MiddleCenter;label.lineSpacing=.9f;
            var size=label.rectTransform.rect.size;
            if(size.x<=4 || size.y<=4)return;
            if(previous==label.text && previousSize==size && previousFont==label.font && previousMaximum==maximum && Mathf.Approximately(previousPixels,label.pixelsPerUnit)){
                label.fontSize=chosen;return;
            }
            previous=label.text;previousSize=size;previousFont=label.font;previousMaximum=maximum;previousPixels=label.pixelsPerUnit;
            chosen=1;
            for(int fontSize=maximum;fontSize>=1;fontSize--){
                var settings=label.GetGenerationSettings(size-new Vector2(4,4));
                settings.fontSize=fontSize;settings.resizeTextForBestFit=false;settings.generateOutOfBounds=true;
                settings.verticalOverflow=VerticalWrapMode.Overflow;
                measure.Populate(label.text,settings);
                var vertices=measure.verts;int count=Mathf.Max(0,vertices.Count-4);
                if(count==0){chosen=fontSize;break;}
                Vector2 min=new Vector2(float.PositiveInfinity,float.PositiveInfinity),max=-min;
                for(int i=0;i<count;i++){Vector2 p=vertices[i].position/label.pixelsPerUnit;min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
                if(max.x-min.x<=size.x-4 && max.y-min.y<=size.y-4){chosen=fontSize;break;}
            }
            label.fontSize=chosen;label.SetVerticesDirty();
        }
        public override void ModifyMesh(VertexHelper vertices)
        {
            if(!IsActive() || vertices.currentVertCount==0)return;
            var rect=((RectTransform)transform).rect;
            float width=Mathf.Max(0,rect.width-4),height=Mathf.Max(0,rect.height-4);
            if(width<=0 || height<=0){vertices.Clear();return;}
            Vector2 min=new Vector2(float.PositiveInfinity,float.PositiveInfinity),max=-min;
            var vertex=new UIVertex();
            for(int i=0;i<vertices.currentVertCount;i++){vertices.PopulateUIVertex(ref vertex,i);Vector2 p=vertex.position;min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
            Vector2 span=max-min,center=(min+max)*.5f;
            float scale=Mathf.Min(1,width/Mathf.Max(.001f,span.x),height/Mathf.Max(.001f,span.y));
            for(int i=0;i<vertices.currentVertCount;i++){
                vertices.PopulateUIVertex(ref vertex,i);Vector2 p=((Vector2)vertex.position-center)*scale+rect.center;
                vertex.position=new Vector3(p.x,p.y,vertex.position.z);vertices.SetUIVertex(vertex,i);
            }
        }
    }
}
