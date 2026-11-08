using System;
using UnityEngine;
namespace Unknown.Battle
{
    [Serializable]
    public sealed class TutorialCheckpoint
    {
        public int lastCompleted;
        public int nextStep=1;
        public bool completed;
    }
    public static class TutorialProgress
    {
        public const string Key="UNKNOWN.Tutorial.v1";
        public static TutorialCheckpoint Load()
        {
            TutorialCheckpoint p=null;
            try {p=JsonUtility.FromJson<TutorialCheckpoint>(PlayerPrefs.GetString(Key,""));}catch(ArgumentException){}
            p=p ?? new TutorialCheckpoint();p.lastCompleted=Mathf.Clamp(p.lastCompleted,0,7);
            p.completed=p.lastCompleted==7;p.nextStep=Mathf.Min(7,p.lastCompleted+1);return p;
        }
        public static void SaveCompleted(int step)
        {
            var p=Load();p.lastCompleted=Mathf.Max(p.lastCompleted,Mathf.Clamp(step,1,7));p.nextStep=Mathf.Min(7,p.lastCompleted+1);p.completed=p.lastCompleted==7;
            PlayerPrefs.SetString(Key,JsonUtility.ToJson(p));PlayerPrefs.Save();
        }
        public static void Reset() {PlayerPrefs.DeleteKey(Key);PlayerPrefs.Save();}
    }
}
