using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unknown.Battle;
namespace Unknown.Multiplayer.Editor
{
    public static class CardArtSetup
    {
        public static string ImagePath(CardDefinition definition)
        {
            if(definition.CardType==CardType.Unknown)return "Assets/CARD/UNKNOWN/unknown-"+definition.Rank.ToString().ToLowerInvariant()+".png";
            if(definition.CardType==CardType.Joker)return "Assets/CARD/Joker/joker-"+(definition.CardID=="Joker_1" ? "red" : "purple")+".png";
            string folder=definition.Suit==Suit.Diamond ? "DIA" : definition.Suit==Suit.Club ? "CLOVE" : definition.Suit.ToString().ToUpperInvariant();
            string prefix=definition.Suit==Suit.Diamond ? "diamond" : definition.Suit.ToString().ToLowerInvariant();
            string rank=definition.Rank==Rank.Ace ? "a" : definition.Rank>=Rank.Jack ? definition.Rank.ToString().ToLowerInvariant() : ((int)definition.Rank).ToString();
            return "Assets/CARD/"+folder+"/"+prefix+"-"+rank+".png";
        }
        [MenuItem("UNKNOWN/Cards/Connect Supplied Card Images")]
        public static void Connect()
        {
            var entries=CardDatabase.All.Select(d=>new CardArtSettings.Entry{cardID=d.CardID,texture=AssetDatabase.LoadAssetAtPath<Texture2D>(ImagePath(d)) ?? throw new InvalidOperationException("Missing card image: "+ImagePath(d))}).ToArray();
            const string path="Assets/UNKNOWN/Battle/Resources/CardArtSettings.asset";
            var asset=AssetDatabase.LoadAssetAtPath<CardArtSettings>(path);
            if(asset==null){asset=ScriptableObject.CreateInstance<CardArtSettings>();AssetDatabase.CreateAsset(asset,path);}
            asset.cards=entries;EditorUtility.SetDirty(asset);AssetDatabase.SaveAssets();
        }
        [MenuItem("UNKNOWN/Cards/Connect Map Card Backs")]
        public static void ConnectCardBacks()
        {
            var settings=AssetDatabase.LoadAssetAtPath<BattleMapSettings>("Assets/UNKNOWN/Battle/Resources/BattleMapSettings.asset");
            if(settings==null)throw new InvalidOperationException("BattleMapSettings asset is missing.");
            Texture2D Load(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/CARD/CardBack/"+name+"-back.png")
                ?? throw new InvalidOperationException("Missing deck back: "+name);
            settings.normalCardBack=Load("normal");settings.jokerCardBack=Load("joker");
            settings.diamondCardBack=Load("diamond");settings.spadeCardBack=Load("spade");
            settings.heartCardBack=Load("heart");settings.clubCardBack=Load("club");
            EditorUtility.SetDirty(settings);AssetDatabase.SaveAssets();
        }
        public static void RunBatch(){Connect();ConnectCardBacks();AttackCheckMenu.RunSuiteBatch();}
    }
}
