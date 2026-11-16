using System;
using UnityEngine;
namespace Unknown.Battle
{
    /// <summary>Keep native typography; bundle actual font data for browsers without OS font access.</summary>
    public static class PlatformFont
    {
        public static Font Create(string[] nativeNames,int size,string role)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var source=Resources.Load<Font>("WebFonts/"+role);
            if(source==null)throw new InvalidOperationException("Missing bundled web font: WebFonts/"+role);
            return UnityEngine.Object.Instantiate(source);
#else
            return Font.CreateDynamicFontFromOSFont(nativeNames,size);
#endif
        }
    }
}
