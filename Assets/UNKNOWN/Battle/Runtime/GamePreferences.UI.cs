using System;
using UnityEngine;
using UnityEngine.UI;
namespace Unknown.Battle
{
    public sealed partial class GamePreferences
    {
        GameObject panel;
        Font optionsFont;
        LocalOptions draft;
        Text screenMode,resolution,vsync;
        public void Open()
        {
            if(IsOpen)return;draft=Current.Copy();
            if(optionsFont==null)optionsFont=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},20);
            panel=new GameObject("UNKNOWN Settings",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));panel.transform.SetParent(transform,false);
            panel.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;panel.GetComponent<Canvas>().sortingOrder=3000;
            var scaler=panel.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=0.5f;
            var shade=Rect(panel.transform,"Modal backdrop",Vector2.zero,Vector2.one,new Color(0.01f,0.015f,0.03f,0.96f));
            var box=Rect(shade,"Settings",new Vector2(0.18f,0.04f),new Vector2(0.82f,0.96f),new Color(0.045f,0.055f,0.085f));
            TextAt(box,"설정",0.91f,0.99f,30);
            VolumeRow(box,"전체 음량",Current.master,0.79f,value=>SetAudio(value,Current.bgm,Current.sfx));
            VolumeRow(box,"배경음악",Current.bgm,0.68f,value=>SetAudio(Current.master,value,Current.sfx));
            VolumeRow(box,"효과음",Current.sfx,0.57f,value=>SetAudio(Current.master,Current.bgm,value));
            ButtonAt(box,"BGM 미리듣기",new Vector2(0.15f,0.51f),new Vector2(0.47f,0.56f),()=>Preview(AudioChannel.BGM));
            ButtonAt(box,"SFX 미리듣기",new Vector2(0.53f,0.51f),new Vector2(0.85f,0.56f),()=>Preview(AudioChannel.SFX));
            screenMode=CycleRow(box,0.41f,()=>{draft.mode=(draft.mode+2)%3;RefreshScreen();},()=>{draft.mode=(draft.mode+1)%3;RefreshScreen();});
            resolution=CycleRow(box,0.31f,()=>CycleResolution(-1),()=>CycleResolution(1));
            vsync=CycleRow(box,0.21f,()=>{draft.vsync=1-draft.vsync;RefreshScreen();},()=>{draft.vsync=1-draft.vsync;RefreshScreen();});RefreshScreen();
            ButtonAt(box,"적용",new Vector2(0.04f,0.11f),new Vector2(0.31f,0.18f),()=>{ApplyScreen(draft);Close();});
            ButtonAt(box,"취소",new Vector2(0.365f,0.11f),new Vector2(0.635f,0.18f),Close);
            ButtonAt(box,"기본값 복원",new Vector2(0.69f,0.11f),new Vector2(0.96f,0.18f),()=>{draft=Defaults();SetAudio(draft.master,draft.bgm,draft.sfx);Close();Open();draft=Defaults();RefreshScreen();});
            TextAt(box,"음량은 즉시 저장 · 화면 변경은 적용 시 저장",0.02f,0.08f,15);
        }
        public void Close() {if(panel!=null){panel.SetActive(false);Destroy(panel);panel=null;}draft=null;}
        void CycleResolution(int step) {int index=Array.FindIndex(Resolutions,r=>r.x==draft.width && r.y==draft.height);var next=Resolutions[(index+step+Resolutions.Length)%Resolutions.Length];draft.width=next.x;draft.height=next.y;RefreshScreen();}
        void RefreshScreen() {screenMode.text="화면 모드: "+new[]{"전체 화면","테두리 없는 창","창 모드"}[draft.mode];resolution.text=$"해상도: {draft.width} × {draft.height}";vsync.text="수직 동기화: "+(draft.vsync==1 ? "ON" : "OFF");}
        RectTransform Rect(Transform parent,string name,Vector2 min,Vector2 max,Color color)
        {var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;go.GetComponent<Image>().color=color;return r;}
        Text TextAt(Transform parent,string value,float bottom,float top,int size=20)
        {var go=new GameObject(value,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=new Vector2(0.12f,bottom);r.anchorMax=new Vector2(0.88f,top);r.offsetMin=r.offsetMax=Vector2.zero;var text=go.GetComponent<Text>();text.text=value;text.font=optionsFont;text.fontSize=size;text.color=new Color(0.91f,0.87f,0.75f);text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;return text;}
        void ButtonAt(Transform parent,string label,Vector2 min,Vector2 max,Action action)
        {var rect=Rect(parent,label,min,max,new Color(0.15f,0.19f,0.24f));var b=rect.gameObject.AddComponent<Button>();b.targetGraphic=rect.GetComponent<Image>();b.onClick.AddListener(()=>action());TextAt(rect,label,0,1,18);}
        Text CycleRow(Transform parent,float y,Action previous,Action next)
        {var r=Rect(parent,"Screen option",new Vector2(0.08f,y),new Vector2(0.92f,y+0.08f),Color.clear);ButtonAt(r,"◀",Vector2.zero,new Vector2(0.1f,1),previous);ButtonAt(r,"▶",new Vector2(0.9f,0),Vector2.one,next);return TextAt(r,"",0,1);}
        void VolumeRow(Transform parent,string label,float initial,float y,Action<float> change)
        {
            var row=Rect(parent,label,new Vector2(0.1f,y),new Vector2(0.9f,y+0.105f),Color.clear);var text=TextAt(row,label+" "+Mathf.RoundToInt(initial*100)+"%",0.5f,1,19);
            var track=Rect(row,"Volume slider",new Vector2(0.04f,0.12f),new Vector2(0.96f,0.38f),new Color(0.16f,0.2f,0.27f));
            var fill=Rect(track,"Fill",Vector2.zero,Vector2.one,new Color(0.6f,0.5f,0.3f));var handle=Rect(track,"Handle",Vector2.zero,Vector2.one,new Color(0.92f,0.84f,0.6f));handle.sizeDelta=new Vector2(14,12);
            var slider=track.gameObject.AddComponent<Slider>();slider.fillRect=fill;slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<Image>();slider.minValue=0;slider.maxValue=1;slider.value=initial;slider.onValueChanged.AddListener(value=>{text.text=label+" "+Mathf.RoundToInt(value*100)+"%";change(value);});
        }
    }
}
