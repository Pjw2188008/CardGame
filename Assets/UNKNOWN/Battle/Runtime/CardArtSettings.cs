using System;
using System.Collections.Generic;
using UnityEngine;
namespace Unknown.Battle
{
    [CreateAssetMenu(menuName="UNKNOWN/Card Art Settings")]
    public sealed class CardArtSettings : ScriptableObject
    {
        [Serializable] public sealed class Entry {public string cardID;public Texture2D texture;}
        public Entry[] cards=new Entry[0];
        [Header("Card combat VFX sheets (6 columns)")]
        public Texture2D attackVfxSheet,healingVfxSheet,weakenVfxSheet,buffVfxSheet,protectionVfxSheet;
        public Texture2D unknownVfxSheet,jokerRedVfxSheet,jokerPurpleVfxSheet,protectionBorder,protectionBreakVfxSheet;
        public Texture2D weakeningBorder,weakeningReleaseVfxSheet;
        Dictionary<string,Texture2D> lookup;
        static CardArtSettings cached;
        void OnEnable(){lookup=null;}
        void OnValidate(){lookup=null;}
        public Texture2D For(string id)
        {
            if(lookup==null){lookup=new Dictionary<string,Texture2D>(StringComparer.Ordinal);foreach(var entry in cards)if(entry!=null && !string.IsNullOrEmpty(entry.cardID))lookup[entry.cardID]=entry.texture;}
            return lookup.TryGetValue(id,out var texture) ? texture : null;
        }
        public static Texture2D TextureFor(string id)
        {if(cached==null)cached=Resources.Load<CardArtSettings>("CardArtSettings");return cached==null ? null : cached.For(id);}
    }
}
