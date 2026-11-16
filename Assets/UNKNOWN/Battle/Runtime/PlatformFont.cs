using System;
using UnityEngine;
namespace Unknown.Battle
{
    /// <summary>Keep native typography; bundle actual font data for browsers without OS font access.</summary>
    public static class PlatformFont
    {
        /// <summary>Imported web fonts are shared Unity assets; native OS fonts are owned instances.</summary>
        public static void Release(Font font)
        {
#if !UNITY_WEBGL || UNITY_EDITOR
            if(font!=null)UnityEngine.Object.Destroy(font);
#endif
        }
        public static Font Create(string[] nativeNames,int size,string role)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var source=Resources.Load<Font>("WebFonts/"+role);
            if(source==null)throw new InvalidOperationException("Missing bundled web font: WebFonts/"+role);
            // Font clones share the source material/atlas, but UGUI tracks rebuilds by Font identity.
            // Use the original imported Font so UV rebuild notifications reach every Text.
            return source;
#else
            return Font.CreateDynamicFontFromOSFont(nativeNames,size);
#endif
        }
    }
}
