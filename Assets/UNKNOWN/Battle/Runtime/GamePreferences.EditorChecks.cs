#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace Unknown.Battle
{
    public sealed partial class GamePreferences
    {
        internal void VerifyForEditor(Action<bool,string> verify)
        {
            var bgmObject=new GameObject("BGM check",typeof(AudioSource));bgmObject.transform.SetParent(transform);var bgmSource=bgmObject.GetComponent<AudioSource>();bgmSource.volume=0.5f;Register(bgmSource,AudioChannel.BGM);
            var sfxObject=new GameObject("SFX check",typeof(AudioSource));sfxObject.transform.SetParent(transform);var sfxSource=sfxObject.GetComponent<AudioSource>();sfxSource.volume=0.75f;Register(sfxSource,AudioChannel.SFX);
            SetAudio(0.5f,0.2f,0.8f);verify(Mathf.Approximately(AudioListener.volume,0.5f) && Mathf.Approximately(bgmSource.volume,0.1f) && Mathf.Approximately(sfxSource.volume,0.6f),"Master/BGM/SFX route independently with original source gain");
            Register(bgmSource,AudioChannel.BGM);verify(Mathf.Approximately(bgmSource.volume,0.1f),"Repeated source registration never multiplies volume again");
            SetAudio(0,0,0);verify(AudioListener.volume==0 && bgmSource.volume==0 && sfxSource.volume==0,"Zero percent is exact silence on every channel");
            SetAudio(0.37f,0.26f,0.81f);Preview(AudioChannel.BGM);Preview(AudioChannel.SFX);verify(musicPreview.clip!=null && effectPreview.clip!=null && musicPreview.volume==0.26f && effectPreview.volume==0.81f,"BGM/SFX preview uses real separate AudioSources");
            verify(Resolutions.Length>0 && Resolutions.Distinct().Count()==Resolutions.Length,"Resolution list is nonempty and deduplicated");
            Open();draft.mode=(Current.mode+1)%3;draft.vsync=1-Current.vsync;var original=Current.Copy();Close();verify(Current.mode==original.mode && Current.vsync==original.vsync,"Cancel discards unapplied screen changes");
            for(int mode=0;mode<3;mode++){var value=Current.Copy();value.mode=mode;value.vsync=mode%2;ApplyScreen(value);verify(Current.mode==mode && QualitySettings.vSyncCount==value.vsync,"Screen mode and VSync apply/save: "+mode);}
            Open();draft=Defaults();RefreshScreen();verify(draft.master==0.8f && draft.mode==1 && draft.vsync==1 && Resolutions.Any(r=>r.x==draft.width && r.y==draft.height),"Defaults select a supported resolution and recommended values");Close();
            var loaded=JsonUtility.FromJson<LocalOptions>(PlayerPrefs.GetString(Key));verify(Mathf.Approximately(loaded.master,Current.master) && Mathf.Approximately(loaded.bgm,Current.bgm) && Mathf.Approximately(loaded.sfx,Current.sfx) && loaded.mode==Current.mode && loaded.width==Current.width && loaded.height==Current.height && loaded.vsync==Current.vsync,"All seven preferences fields are persisted to PlayerPrefs");
        }
        internal bool CaptureOptionsFrame(string path)
        {
            var cameraObject=new GameObject("Settings render check",typeof(Camera));var camera=cameraObject.GetComponent<Camera>();camera.orthographic=true;camera.orthographicSize=360;camera.transform.position=new Vector3(0,0,-10);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;
            var texture=new RenderTexture(1920,1080,24);texture.Create();camera.targetTexture=texture;var previous=RenderTexture.active;var target=panel.GetComponent<Canvas>();
            try {target.renderMode=RenderMode.ScreenSpaceCamera;target.worldCamera=camera;target.planeDistance=1;var texts=target.GetComponentsInChildren<Text>();foreach(var t in texts)t.font.RequestCharactersInTexture(t.text,t.fontSize,t.fontStyle);foreach(var t in texts)t.SetVerticesDirty();Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=texture;var pixels=new Texture2D(1920,1080,TextureFormat.RGB24,false);pixels.ReadPixels(new UnityEngine.Rect(0,0,1920,1080),0,0);pixels.Apply();Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,pixels.EncodeToPNG());Destroy(pixels);return File.Exists(path);}
            finally{target.renderMode=RenderMode.ScreenSpaceOverlay;target.worldCamera=null;RenderTexture.active=previous;texture.Release();Destroy(texture);Destroy(cameraObject);}
        }
    }
}
#endif
