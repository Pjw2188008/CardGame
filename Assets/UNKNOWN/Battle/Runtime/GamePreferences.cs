using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace Unknown.Battle
{
    [Serializable] public sealed class LocalOptions
    {
        public float master=0.8f,bgm=0.6f,sfx=0.9f;
        public int mode=1,width,height,vsync=1;
        public LocalOptions Copy() => (LocalOptions)MemberwiseClone();
    }
    public enum AudioChannel { BGM,SFX }
    public sealed partial class GamePreferences : MonoBehaviour
    {
        static string Key => Debug.isDebugBuild && Environment.GetCommandLineArgs().Contains("-unknownPreferencesProbe") ? "UNKNOWN.Options.Probe.v1" : "UNKNOWN.Options.v1";
        static GamePreferences instance;
        public static GamePreferences Instance { get {if(instance==null){var go=new GameObject("UNKNOWN Local Preferences");instance=go.AddComponent<GamePreferences>();DontDestroyOnLoad(go);}return instance;} }
        public static bool IsOpen => instance!=null && instance.panel!=null;
        public static int ConsumedEscapeFrame { get; private set; }=-1;
        public LocalOptions Current { get; private set; }
        public Vector2Int[] Resolutions { get; private set; }
        readonly Dictionary<AudioSource,float> sourceLevels=new Dictionary<AudioSource,float>();
        readonly Dictionary<AudioSource,AudioChannel> channels=new Dictionary<AudioSource,AudioChannel>();
        AudioSource musicPreview,effectPreview;
        AudioClip previewClip;
        float nextScan;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)] static void Boot() { _=Instance; }
        void Awake()
        {
            if(instance!=null && instance!=this){Destroy(gameObject);return;}instance=this;
            Resolutions=Screen.resolutions.Select(r=>new Vector2Int(r.width,r.height)).Distinct().OrderBy(r=>r.x).ThenBy(r=>r.y).ToArray();
            if(Resolutions.Length==0)Resolutions=new[]{new Vector2Int(Mathf.Max(1,Screen.width),Mathf.Max(1,Screen.height))};
            Current=Defaults();
            try {if(PlayerPrefs.HasKey(Key))Current=JsonUtility.FromJson<LocalOptions>(PlayerPrefs.GetString(Key)) ?? Defaults();}catch(ArgumentException){Current=Defaults();}
            Sanitize(Current);ApplyAudio();
            // Batch tests must never resize the user's display; ordinary players restore their saved screen settings.
            if(!Application.isBatchMode && !Application.isEditor)ApplyDisplay(Current);
        }
        public LocalOptions Defaults() {var native=new Vector2Int(Screen.currentResolution.width,Screen.currentResolution.height);var r=Resolutions.Contains(native) ? native : Resolutions[Resolutions.Length-1];return new LocalOptions{width=r.x,height=r.y};}
        void Sanitize(LocalOptions v)
        {
            v.master=SafeVolume(v.master);v.bgm=SafeVolume(v.bgm);v.sfx=SafeVolume(v.sfx);v.mode=Mathf.Clamp(v.mode,0,2);v.vsync=v.vsync==0 ? 0 : 1;
            if(!Resolutions.Any(r=>r.x==v.width && r.y==v.height)){var r=Resolutions.OrderBy(r=>Math.Abs((long)r.x*r.y-(long)v.width*v.height)).First();v.width=r.x;v.height=r.y;}
        }
        static float SafeVolume(float v) => float.IsNaN(v)||float.IsInfinity(v) ? 1 : Mathf.Clamp01(v);
        void Save() {PlayerPrefs.SetString(Key,JsonUtility.ToJson(Current));PlayerPrefs.Save();}
        public void SetAudio(float master,float bgm,float sfx) {Current.master=SafeVolume(master);Current.bgm=SafeVolume(bgm);Current.sfx=SafeVolume(sfx);ApplyAudio();Save();}
        public void Register(AudioSource source,AudioChannel channel)
        {if(source==null)return;if(!sourceLevels.ContainsKey(source))sourceLevels[source]=source.volume;channels[source]=channel;ApplyAudio();}
        void ApplyAudio()
        {
            AudioListener.volume=Current.master;
            foreach(var source in sourceLevels.Keys.ToArray())if(source==null){sourceLevels.Remove(source);channels.Remove(source);}else source.volume=sourceLevels[source]*(channels[source]==AudioChannel.BGM ? Current.bgm : Current.sfx);
        }
        public void ApplyScreen(LocalOptions draft) {var copy=draft.Copy();Sanitize(copy);Current.mode=copy.mode;Current.width=copy.width;Current.height=copy.height;Current.vsync=copy.vsync;ApplyDisplay(Current);Save();}
        static FullScreenMode Mode(int mode) => mode==0 ? FullScreenMode.ExclusiveFullScreen : mode==1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
        void ApplyDisplay(LocalOptions v) {QualitySettings.vSyncCount=v.vsync;if(!Application.isBatchMode)Screen.SetResolution(v.width,v.height,Mode(v.mode));}
        public void Preview(AudioChannel channel)
        {
            if(previewClip==null){const int rate=44100;var data=new float[rate/3];for(int i=0;i<data.Length;i++)data[i]=0.12f*Mathf.Sin(2*Mathf.PI*440*i/rate)*Mathf.Sin(Mathf.PI*i/data.Length);previewClip=AudioClip.Create("Settings preview",data.Length,1,rate,false);previewClip.SetData(data,0);}
            var source=channel==AudioChannel.BGM ? musicPreview : effectPreview;
            if(source==null){source=gameObject.AddComponent<AudioSource>();source.playOnAwake=false;source.volume=1;Register(source,channel);if(channel==AudioChannel.BGM)musicPreview=source;else effectPreview=source;}
            source.clip=previewClip;source.Play();
        }
        void Update()
        {
            if(Time.unscaledTime>=nextScan){nextScan=Time.unscaledTime+1;foreach(var source in FindObjectsByType<AudioSource>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(!sourceLevels.ContainsKey(source))Register(source,source.GetComponent<GameAudioChannel>()?.channel ?? AudioChannel.SFX);}
            if(IsOpen && UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame==true){ConsumedEscapeFrame=Time.frameCount;Close();}
        }
        void OnDestroy() {if(instance==this)instance=null;if(previewClip!=null)Destroy(previewClip);if(optionsFont!=null)Destroy(optionsFont);}
    }
}
