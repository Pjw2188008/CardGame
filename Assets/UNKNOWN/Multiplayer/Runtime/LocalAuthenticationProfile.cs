using System;
using System.Threading;
using UnityEngine;
namespace Unknown.Multiplayer
{
    /// <summary>Reserves a persistent authentication cache namespace for this running process.
    /// Slot one keeps the existing account; extra simultaneous instances keep their own reusable accounts.</summary>
    internal static class LocalAuthenticationProfile
    {
        static Mutex reservation;
        static string selected;
        public static string Acquire(string configured)
        {
            if(selected!=null)return selected;
            string requested=configured;
            var args=Environment.GetCommandLineArgs();
            int at=Array.IndexOf(args,"-unknownProfile");
            if(at>=0)
            {
                if(at+1>=args.Length || string.IsNullOrWhiteSpace(args[at+1]) || args[at+1].StartsWith("-"))
                    throw new ArgumentException("-unknownProfile 뒤에 인증 프로필 이름을 입력하세요.");
                requested=args[at+1];
            }
            bool explicitProfile=!string.IsNullOrWhiteSpace(requested);
            requested=explicitProfile ? requested.Trim() : "default";
            // Match Authentication 3.x SetProfile/SwitchProfile validation without altering any saved data.
            if(requested.Length>30 || !System.Text.RegularExpressions.Regex.IsMatch(requested,@"^[a-zA-Z0-9_-]+$"))
                throw new ArgumentException("인증 프로필은 영문·숫자·_·-로 1~30자여야 합니다.");
            for(int slot=1;slot<=32;slot++)
            {
                string candidate=slot==1 ? requested : "unknown_local_"+slot;
                var mutex=new Mutex(false,"UNKNOWN_Auth_"+Application.cloudProjectId+"_"+candidate.ToLowerInvariant());
                bool acquired;
                try { acquired=mutex.WaitOne(0); }
                catch(AbandonedMutexException) { acquired=true; }
                if(acquired)
                {
                    reservation=mutex; selected=candidate; Application.quitting+=Release;
                    return selected;
                }
                mutex.Dispose();
                if(explicitProfile)throw new InvalidOperationException($"인증 프로필 '{requested}'을 다른 실행 중인 UNKNOWN이 사용하고 있습니다. 다른 -unknownProfile을 지정하거나 옵션 없이 실행하세요.");
            }
            throw new InvalidOperationException("동시에 실행 중인 UNKNOWN 인스턴스가 너무 많습니다.");
        }
        static void Release()
        {
            Application.quitting-=Release;
            if(reservation!=null) { reservation.ReleaseMutex(); reservation.Dispose(); reservation=null; }
            selected=null;
        }
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        static void ReleaseOnEditorReload() { UnityEditor.AssemblyReloadEvents.beforeAssemblyReload+=Release; }
#endif
    }
}
