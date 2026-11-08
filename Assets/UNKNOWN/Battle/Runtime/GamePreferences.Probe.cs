using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
namespace Unknown.Battle
{
    public sealed partial class GamePreferences
    {
        [Serializable] sealed class PreferencesProbeReport {public bool completed,persisted,audioMatches;public string error;public LocalOptions saved;public List<string> modes=new List<string>();public List<bool> modeMatches=new List<bool>();}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartPreferencesProbe()
        {
            if(!Debug.isDebugBuild)return;var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-unknownPreferencesProbe");
            if(at>=0 && at+2<args.Length)Instance.StartCoroutine(Instance.ProbePreferences(args[at+1],args[at+2]));
        }
        IEnumerator ProbePreferences(string operation,string path)
        {
            var report=new PreferencesProbeReport();yield return null;
            if(operation=="write") {
                SetAudio(0.42f,0.35f,0.77f);
                var music=gameObject.AddComponent<AudioSource>();music.volume=0.5f;Register(music,AudioChannel.BGM);var effect=gameObject.AddComponent<AudioSource>();effect.volume=0.75f;Register(effect,AudioChannel.SFX);
                report.audioMatches=Mathf.Approximately(AudioListener.volume,0.42f) && Mathf.Approximately(music.volume,0.175f) && Mathf.Approximately(effect.volume,0.5775f);
                var native=Defaults();var smaller=Resolutions.Where(r=>r.x>=1280 && r.y>=720).OrderBy(r=>r.x*r.y).FirstOrDefault();if(smaller.x==0)smaller=Resolutions[0];
                foreach(int mode in new[]{2,1,0}) {
                    var option=Current.Copy();option.mode=mode;option.vsync=mode%2;option.width=mode==0 ? native.width : smaller.x;option.height=mode==0 ? native.height : smaller.y;ApplyScreen(option);yield return new WaitForSecondsRealtime(1.5f);
                    report.modes.Add($"requested={Mode(mode)} {option.width}x{option.height} vsync={option.vsync}; actual={Screen.fullScreenMode} {Screen.width}x{Screen.height} vsync={QualitySettings.vSyncCount}");
                    report.modeMatches.Add(!Application.isBatchMode && Screen.fullScreenMode==Mode(mode) && QualitySettings.vSyncCount==option.vsync);
                }
                var final=Current.Copy();final.mode=2;final.width=smaller.x;final.height=smaller.y;final.vsync=0;ApplyScreen(final);yield return new WaitForSecondsRealtime(0.8f);report.saved=Current.Copy();report.persisted=PlayerPrefs.HasKey(Key);report.completed=true;
            }else {
                yield return new WaitForSecondsRealtime(1);var previous=JsonUtility.FromJson<PreferencesProbeReport>(File.ReadAllText(path+".write.json"));report.saved=Current.Copy();report.persisted=JsonUtility.ToJson(previous.saved)==JsonUtility.ToJson(Current);report.audioMatches=Mathf.Approximately(AudioListener.volume,Current.master);report.modes.Add($"startup={Screen.fullScreenMode} {Screen.width}x{Screen.height} vsync={QualitySettings.vSyncCount}");report.modeMatches.Add(Screen.fullScreenMode==Mode(Current.mode) && QualitySettings.vSyncCount==Current.vsync);report.completed=true;
            }
            File.WriteAllText(path+"."+operation+".json",JsonUtility.ToJson(report,true));Application.Quit(report.completed && report.persisted && report.audioMatches && report.modeMatches.All(m=>m) ? 0 : 1);
        }
    }
}
