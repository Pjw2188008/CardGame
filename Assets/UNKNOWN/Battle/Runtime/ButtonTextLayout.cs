using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Unknown.Battle
{
    // Fit button captions, leaving card faces and non-button text alone.
    public sealed class ButtonTextLayout : UIBehaviour
    {
        [SerializeField] Text label;
        [SerializeField] float minimumHeight;
        [SerializeField] int minimumFont=14,maximumFont=18;
        [SerializeField] bool framed;
        [SerializeField] float uiScale=1f;
        readonly TextGenerator measure=new TextGenerator();
        Vector2 previousSize;string previousText;Font previousFont;bool refreshing;
        float addedHeight,lastHeightDelta;bool trackedHeight;
        public static void Configure(Button button,Text caption,float height,int maxFont,bool artwork,float uiScale=1f)
        {
            var fit=button.GetComponent<ButtonTextLayout>();if(fit==null)fit=button.gameObject.AddComponent<ButtonTextLayout>();
            fit.label=caption;fit.minimumHeight=height;fit.maximumFont=maxFont;fit.framed=artwork;fit.uiScale=uiScale;
            fit.minimumFont=Mathf.Min(maxFont,Mathf.CeilToInt(14*uiScale));
            if(artwork)caption.color=SingleButtonSkin.CaptionColor;
            fit.RefreshFit(true);
        }
        protected override void OnEnable(){base.OnEnable();RefreshFit(true);}
        protected override void OnRectTransformDimensionsChange(){base.OnRectTransformDimensionsChange();RefreshFit(true);}
        void LateUpdate(){RefreshFit(false);}
        void RefreshFit(bool force)
        {
            if(refreshing || label==null || label.font==null || !label.gameObject.activeInHierarchy)return;
            var rect=(RectTransform)transform;var parent=rect.parent as RectTransform;
            if(parent==null || parent.rect.height<=0 || rect.rect.width<=0 || rect.rect.height<=0)return;
            if(!force && previousSize==rect.rect.size && previousText==label.text && previousFont==label.font)return;
            refreshing=true;
            try {
                // Respect explicit list-row sizes, while undoing our own extra height after a resize/text change.
                if(trackedHeight && !Mathf.Approximately(lastHeightDelta,rect.sizeDelta.y))addedHeight=0;
                float naturalHeight=Mathf.Max(0,rect.rect.height-addedHeight);
                float verticalPadding=6*uiScale;
                float horizontal=framed?Mathf.Min(40*uiScale,rect.rect.width*.2f):Mathf.Min(8,rect.rect.width*.12f);
                var area=label.rectTransform;area.anchorMin=Vector2.zero;area.anchorMax=Vector2.one;
                area.offsetMin=new Vector2(horizontal,verticalPadding);area.offsetMax=new Vector2(-horizontal,-verticalPadding);
                label.alignment=TextAnchor.MiddleCenter;label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;
                label.resizeTextForBestFit=true;label.resizeTextMinSize=minimumFont;label.resizeTextMaxSize=maximumFont;label.fontSize=maximumFont;
                var settings=label.GetGenerationSettings(new Vector2(Mathf.Max(1,rect.rect.width-2*horizontal),0));
                settings.fontSize=minimumFont;settings.resizeTextForBestFit=false;settings.verticalOverflow=VerticalWrapMode.Overflow;
                float needed=measure.GetPreferredHeight(label.text,settings)/label.pixelsPerUnit+2*verticalPadding;
                float targetHeight=Mathf.Ceil(Mathf.Max(framed ? minimumHeight : Mathf.Max(naturalHeight,minimumHeight),needed));
                addedHeight=targetHeight-naturalHeight;
                if(!Mathf.Approximately(rect.rect.height,targetHeight))rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,targetHeight);
                lastHeightDelta=rect.sizeDelta.y;trackedHeight=true;
                if(framed){var image=GetComponent<Image>();if(image!=null)image.pixelsPerUnitMultiplier=(minimumHeight<SingleButtonSkin.StandardHeight*uiScale?3:2)/uiScale;}
                previousSize=rect.rect.size;previousText=label.text;previousFont=label.font;
            }finally{refreshing=false;}
        }
#if UNITY_EDITOR
        internal static void VerifyVisible(GameObject root,System.Action<bool,string> verify,string screen)
        {
            Canvas.ForceUpdateCanvases();
            foreach(var fit in root.GetComponentsInChildren<ButtonTextLayout>())
                if(fit.label!=null && fit.label.gameObject.activeInHierarchy)
                    verify(fit.CaptionFits,"Full button caption fits at readable font: "+screen+" / "+fit.name);
        }
        internal bool CaptionFits
        {
            get {
                RefreshFit(true);if(label==null || label.font==null)return false;
                var settings=label.GetGenerationSettings(new Vector2(label.rectTransform.rect.width,0));
                settings.fontSize=minimumFont;settings.resizeTextForBestFit=false;settings.verticalOverflow=VerticalWrapMode.Overflow;
                return label.rectTransform.rect.height+1>=measure.GetPreferredHeight(label.text,settings)/label.pixelsPerUnit;
            }
        }
#endif
    }
}
