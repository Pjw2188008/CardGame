#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;
namespace Unknown.Multiplayer
{
    public sealed partial class RoomTestUI
    {
        [Serializable] sealed class TitleReport {public bool completed;public List<string> passed=new List<string>(),failed=new List<string>(),errors=new List<string>();}
        public void RunTitleChecks(){StartCoroutine(TitleChecks());}
        IEnumerator TitleChecks()
        {
            var report=new TitleReport();Action<bool,string> check=(ok,name)=>{if(ok)report.passed.Add(name);else report.failed.Add(name);};
            Application.LogCallback listener=(m,s,t)=>{if(t==LogType.Error || t==LogType.Exception || t==LogType.Assert)report.errors.Add(m);};
            Application.logMessageReceived+=listener;
            var previous=panelSettings.targetTexture;RenderTexture target=null;
            try {
                ActivatePage(Page.Home,false);
                foreach(int width in new[]{1280,1920}) {
                    target=new RenderTexture(width,width*9/16,0);target.Create();panelSettings.targetTexture=target;
                    yield return null;yield return null;yield return null;
                    var root=document.rootVisualElement;
                    var expected=UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Title/Title.png");
                    check(expected!=null && titleBackdrop?.image==expected,"Supplied title image is loaded at "+width);
                    check(titleBackdrop!=null && titleBackdrop.pickingMode==PickingMode.Ignore && titleBackdrop.scaleMode==ScaleMode.ScaleAndCrop,"Image preserves ratio and ignores pointer input at "+width);
                    check(root.Q<Label>(className:"title").resolvedStyle.display==DisplayStyle.None,"No duplicate UNKNOWN label at "+width);
                    check(menuBackdrop!=null && menuBackdrop.resolvedStyle.display==DisplayStyle.None,"Submenu image is hidden on home at "+width);
                    foreach(var button in actions.FindAll(b=>pages[Page.Home].Contains(b))) {
                        var bounds=button.worldBound;
                        check(Mathf.Abs(bounds.center.x-root.worldBound.center.x)<2 && bounds.yMin>=root.worldBound.height*.60f && bounds.yMax<=root.worldBound.yMax,"Centered home button below artwork logo: "+button.tooltip+" at "+width);
                        var hit=root.panel.Pick(bounds.center);
                        check(hit==button || hit!=null && button.Contains(hit),"Pointer can reach "+button.tooltip+" at "+width);
                    }
                    var active=RenderTexture.active;RenderTexture.active=target;var pixels=new Texture2D(width,width*9/16,TextureFormat.RGB24,false);
                    pixels.ReadPixels(new Rect(0,0,width,width*9/16),0,0);pixels.Apply();Directory.CreateDirectory("Library/TitleVisuals");
                    File.WriteAllBytes("Library/TitleVisuals/Title-"+width+".png",pixels.EncodeToPNG());Destroy(pixels);RenderTexture.active=active;
                    panelSettings.targetTexture=previous;target.Release();Destroy(target);target=null;
                }
                target=new RenderTexture(1280,720,0);target.Create();panelSettings.targetTexture=target;
                foreach(var page in new[]{Page.Online,Page.Join,Page.Lobby,Page.Game,Page.Tutorial,Page.AiSelect}) {
                    ActivatePage(page,false);yield return null;yield return null;yield return null;
                    var expected=UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Title/back.png");
                    check(expected!=null && menuBackdrop?.image==expected && menuBackdrop.resolvedStyle.display==DisplayStyle.Flex,"Submenu uses back.png: "+page);
                    check(titleBackdrop.resolvedStyle.display==DisplayStyle.None && menuBackdrop.pickingMode==PickingMode.Ignore && menuBackdrop.scaleMode==ScaleMode.ScaleAndCrop,"Submenu background preserves ratio and ignores input: "+page);
                    var root=document.rootVisualElement;
                    var button=actions.Find(b=>pages[page].Contains(b) && b.enabledInHierarchy && b.resolvedStyle.display!=DisplayStyle.None);
                    if(button!=null){var hit=root.panel.Pick(button.worldBound.center);check(hit==button || hit!=null && button.Contains(hit),"Submenu buttons remain clickable: "+page);}
                    var active=RenderTexture.active;RenderTexture.active=target;var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
                    pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes("Library/TitleVisuals/Menu-"+page+".png",pixels.EncodeToPNG());Destroy(pixels);RenderTexture.active=active;
                }
                ActivatePage(Page.Home,false);panelSettings.targetTexture=previous;target.Release();Destroy(target);target=null;
                yield return null;
                foreach(var page in new[]{Page.Online,Page.AiSelect,Page.Tutorial}) {
                    string caption=page==Page.Online ? "온라인 대전" : page==Page.AiSelect ? "AI 대전" : "튜토리얼";
                    var button=actions.Find(b=>pages[Page.Home].Contains(b) && b.tooltip==caption);
                    using(var submit=NavigationSubmitEvent.GetPooled()){submit.target=button;button.SendEvent(submit);}
                    yield return new WaitForSecondsRealtime(.25f);
                    check(currentPage==page && pages[page].resolvedStyle.display==DisplayStyle.Flex && titleBackdrop.resolvedStyle.display==DisplayStyle.None,"Home button opens existing page: "+caption);
                    check(menuBackdrop.resolvedStyle.display==DisplayStyle.Flex,"Navigation reveals back.png: "+caption);
                    ShowPage(Page.Home,false);yield return new WaitForSecondsRealtime(.25f);
                    check(titleBackdrop.resolvedStyle.display==DisplayStyle.Flex,"Returning restores illustrated home: "+caption);
                    check(menuBackdrop.resolvedStyle.display==DisplayStyle.None,"Returning hides back.png: "+caption);
                }
                check(room.Session==null && battleUI==null,"Title navigation creates no network session or battle");
                report.completed=true;
            } finally {
                panelSettings.targetTexture=previous;if(target!=null){target.Release();Destroy(target);}
                Application.logMessageReceived-=listener;File.WriteAllText("Library/UNKNOWN-Title-Checks.json",JsonUtility.ToJson(report,true));
                Debug.Log($"[UNKNOWN] Title checks: passed={report.passed.Count}, failed={report.failed.Count}, errors={report.errors.Count}");
            }
        }
    }
}
#endif
