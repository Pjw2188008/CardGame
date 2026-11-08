#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace Unknown.Battle
{
    public sealed partial class BattleUIController
    {
        [Serializable] sealed class AttackReport { public bool completed; public List<string> passed=new List<string>(),failed=new List<string>(),errors=new List<string>(); }
        public void RunAttackChecks() { Invoke(nameof(BeginAttackChecks),0.15f); }
        void BeginAttackChecks() { StopAllCoroutines(); StartCoroutine(AttackChecks()); }
        void AttackBegin(CardInstance card)
        { var view=cardViews[card.InstanceID]; ExecuteEvents.Execute(view.gameObject,DragData(view),ExecuteEvents.beginDragHandler); }
        IEnumerator AttackChecks()
        {
            var report=new AttackReport();
            Action<bool,string> verify=(ok,name)=> { if(ok)report.passed.Add(name);else report.failed.Add(name); };
            Application.LogCallback listener=(m,s,t)=> { if(t==LogType.Error || t==LogType.Exception || t==LogType.Assert)report.errors.Add(m); };
            Application.logMessageReceived+=listener;
            try
            {
                checkRecipient=0; CheckFixture(); var source=CheckUnit("Heart_K",0,1); var enemy=CheckUnit("Heart_K",1,3); var hand=CheckHand("Club_2"); Refresh(); yield return null;
                var sourceView=cardViews[source.InstanceID]; Vector2 position=sourceView.anchoredPosition;
                verify(sourceView.GetComponent<Outline>().effectColor==ready && sourceView.Find("Attack Status").GetComponent<Text>().text=="⚔","Ready own unit has green border and sword");
                verify(cardViews[enemy.InstanceID].GetComponent<UnitAttackDrag>()==null,"Enemy cannot start an attack drag");
                var rightClick=DragData(sourceView); rightClick.button=PointerEventData.InputButton.Right;
                verify(!BeginAttackDrag(source.InstanceID,rightClick) && !AttackDragging,"Only the left mouse button can start attacks");
                AttackBegin(source);
                verify(AttackState==AttackDragState.Dragging && sourceView.localScale==Vector3.one*1.08f && sourceView.GetComponent<Outline>().effectColor==attackYellow && attackHintText.text=="공격 대상 선택","Begin immediately enlarges source 1.08x with yellow border and cursor hint");
                verify(sourceView.Find("Attack Ready Glow").GetComponent<AttackGlowGraphic>()!=null,"Source glow uses a hollow perimeter and never covers card content");
                verify(AttackDragging && attackArrow.gameObject.activeSelf && !attackArrow.raycastTarget,"Unity BeginDrag shows local non-raycast arrow");
                verify(sourceView.anchoredPosition==position && !source.HasAttacked && enemy.CurrentHP==12,"Dragging fixes source in slot and does not commit combat");
                verify(!BeginCardDrag(hand.InstanceID,cardViews[hand.InstanceID],DragData(cardViews[hand.InstanceID])),"Hand drag cannot start during attack drag");
                var enemyZone=UnitDrop(1,3); var move=DragData(cardViews[enemy.InstanceID]); move.pointerCurrentRaycast=new RaycastResult{gameObject=enemyZone.gameObject};
                MoveAttackDrag(move);
                verify(AttackState==AttackDragState.ValidTarget && attackHintText.text=="놓으면 공격" && Mathf.Abs(attackTargetView.localScale.x-1.05f)<0.001f,"Valid unit has exact 1.05 scale and release hint");
                verify(attackVisualLayer.GetComponent<Canvas>().overrideSorting && attackVisualLayer.GetComponent<Canvas>().sortingOrder>canvas.sortingOrder && attackVisualLayer.GetComponentsInChildren<Graphic>(true).All(g=>!g.raycastTarget),"Feedback sorts above battle and never intercepts raycasts");
                simulateAttackPointer=true;
                foreach(int width in new[]{1920,1600,1280})
                {
                    string path=CaptureAttackFrame(width,cardViews[enemy.InstanceID]);
                    verify(File.Exists(path) && new FileInfo(path).Length>10000,"Rendered camera-space attack UI at "+width+"x"+(width*9/16));
                }
                MoveAttackDrag(move);
                verify(targetAttackOutline.effectColor==attackRed && attackTargetView.localScale.x>1,"Enemy target gets red outline and slight enlargement");
                RectTransformUtility.ScreenPointToLocalPointInRectangle(dragLayer,move.position,null,out var expectedPointer);
                verify(Vector2.Distance(attackPointer,expectedPointer)<0.1f,"Arrow endpoint uses canvas pointer coordinates");
                EndAttackDrag(); RefreshAttackIndicators();
                verify(AttackState==AttackDragState.Cancelled && sourceView.localScale==Vector3.one && sourceView.GetComponent<Outline>().effectColor==ready && !attackHint.gameObject.activeSelf,"Cancel restores source scale, green border and hides hint immediately");
                AttackBegin(source); checkRecipient=-1; engine.State.ActivePlayer=-1; EndAttackDrag();
                verify(!AttackDragging && !attackArrow.gameObject.activeSelf && BoardCard(source.InstanceID)==null,"Room teardown safely cancels after local player index is released");
                engine.State.ActivePlayer=0; checkRecipient=0; RefreshAttackIndicators();
                AttackBegin(source); HighlightAttack(UnitDrop(0,1));
                verify(AttackState==AttackDragState.InvalidTarget && attackArrow.color==attackInvalid && attackHintText.text=="공격 불가" && attackTargetView==null,"Own unit shows gray arrow and invalid hint without target enlargement");
                verify(File.Exists(CaptureAttackFrame(1280,cardViews[hand.InstanceID],"Invalid")),"Rendered invalid gray arrow and cursor hint"); EndAttackDrag();
                verify(!AttackDragging && !attackArrow.gameObject.activeSelf && Mathf.Abs(cardViews[enemy.InstanceID].localScale.x-1)<0.001f && !source.HasAttacked,"Cancel removes arrow and restores target without spending attack");
                foreach(var zone in new[]{UnitDrop(0,1),UnitDrop(1,0),heroAttackArea.GetComponent<CardDropZone>()})
                { AttackBegin(source); verify(!DropAttack(zone) && !source.HasAttacked,"Invalid target cancels: "+zone.Kind+" owner="+zone.Owner+" slot="+zone.Slot); }
                AttackBegin(source); verify(!DropAttack(null) && !source.HasAttacked,"Empty space or non-target button cancels attack");
                AttackBegin(source); DragDrop(enemyZone); verify(source.HasAttacked && !AttackDragging,"Drop commits once and clears context before dispatch");
                verify(AttackState==AttackDragState.AttackConfirmed,"Confirmed state follows accepted engine attack event");
                verify(!DropAttack(enemyZone),"Duplicate Drop cannot submit a second attack");
                yield return DragWait();
                verify(source.CurrentHP==4 && enemy.CurrentHP==4,"Existing simultaneous damage and counterattack remain unchanged");
                AttackBegin(source); verify(!AttackDragging && cardViews[source.InstanceID].Find("Attack Status").GetComponent<Text>().text=="공격 완료","Spent unit cannot drag again and shows attack complete");
                verify(AttackReason(source).Contains("이미 공격"),"Spent unit tooltip explains reason");
                source.HasAttacked=false; source.SummonedTurn=engine.State.TurnNumber; Refresh(); AttackBegin(source);
                verify(!AttackDragging && cardViews[source.InstanceID].Find("Attack Status").GetComponent<Text>().text=="◷" && AttackReason(source).Contains("다음 턴"),"Summoning sickness shows clock and tooltip");
                source.SummonedTurn=0; engine.State.ActivePlayer=1; Refresh(); AttackBegin(source);
                verify(!AttackDragging && !heroAttackGlow.enabled && cardViews[source.InstanceID].Find("Attack Status").GetComponent<Text>().text=="" && AttackReason(source).Contains("상대방"),"Opponent turn removes own attack readiness");
                engine.State.ActivePlayer=0; actionBusy=true; RefreshAttackIndicators();
                verify(!ReadyToAttack(source) && AttackReason(source).Contains("다른 행동"),"Presentation busy blocks new attacks"); actionBusy=false;
                Refresh(); AttackBegin(source); engine.State.TurnExpired=true; UpdateInteractionUI();
                verify(!AttackDragging && !source.HasAttacked,"Timeout cancels pending arrow without attack expense");
                CheckFixture(); source=CheckUnit("Heart_K",0); Refresh(); yield return null;
                verify(heroAttackGlow.enabled,"Empty enemy field exposes direct attack area");
                AttackBegin(source); var heroZone=heroAttackArea.GetComponent<CardDropZone>(); HighlightAttack(heroZone);
                verify(heroAttackGlow.effectColor==attackRed,"Valid hero target gets stronger red emphasis");
                verify(!heroAttackGlow.useGraphicAlpha && heroAttackIcon.gameObject.activeSelf && attackHintText.text=="놓으면 직접 공격","Direct attack has opaque outline, sword icon and release hint");
                verify(File.Exists(CaptureAttackFrame(1920,heroAttackArea,"Hero")),"Rendered direct attack outline, icon and cursor hint");
                DragDrop(heroZone); yield return DragWait();
                verify(engine.State.Players[1].HP==22 && source.HasAttacked && !heroAttackGlow.enabled,"Direct attack updates HP once and removes readiness");
                CheckFixture(); source=CheckUnit("Heart_K",0); engine.State.Players[1].HP=1; Refresh(); AttackBegin(source); DragDrop(heroZone); yield return DragWait();
                verify(engine.State.Winner==0 && resultOverlay.activeSelf && !AttackDragging,"Lethal direct attack uses existing HP victory and clears arrow");
                CheckFixture(); var summon=CheckHand("Club_2"); Refresh(); DragBegin(summon); DragDrop(UnitDrop(0,2)); yield return DragWait();
                verify(engine.State.Players[0].Board[2]?.InstanceID==summon.InstanceID && engine.State.Players[0].Hand.Count==0,"Normal hand drag summons through existing command");
                verify(!AttackDragging && dragged==null,"Hand and attack contexts clean up independently");
                CheckFixture(); source=CheckUnit("Heart_2",0); enemy=CheckUnit("Heart_K",1); Refresh();
                sourceView=cardViews[source.InstanceID]; AttackBegin(source); DragDrop(UnitDrop(1,0));
                verify(engine.State.Players[0].Board[0]==null && sourceView.GetComponent<Outline>().effectColor!=ready && !sourceView.Find("Attack Ready Glow").gameObject.activeSelf,"Destroyed attacker loses readiness before death presentation finishes");
                yield return DragWait();
                report.completed=true;
            }
            finally
            {
                Application.logMessageReceived-=listener; checkRecipient=-1; simulateAttackPointer=false;
                File.WriteAllText("Library/UNKNOWN-AttackUI-Checks.json",JsonUtility.ToJson(report,true));
                Debug.Log($"[UNKNOWN] Attack UI checks: passed={report.passed.Count}, failed={report.failed.Count}, errors={report.errors.Count}"); Destroy(gameObject);
            }
        }
        string CaptureAttackFrame(int width,RectTransform target,string label="Unit",bool arrowRequired=true)
        {
            var cameraGO=new GameObject("Attack UI Render Check",typeof(Camera)); var camera=cameraGO.GetComponent<Camera>();
            camera.orthographic=true; camera.orthographicSize=540; camera.transform.position=new Vector3(0,0,-10); camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
            var texture=new RenderTexture(width,width*9/16,24); texture.Create(); camera.targetTexture=texture;
            var previous=RenderTexture.active;
            try
            {
                canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=1; Canvas.ForceUpdateCanvases();
                if(tutorial!=null)RefreshTutorialRings();
                var screen=RectTransformUtility.WorldToScreenPoint(camera,target.position);
                var data=new PointerEventData(EventSystem.current){position=screen,pointerCurrentRaycast=new RaycastResult{gameObject=target.gameObject}};
                MoveAttackDrag(data); Canvas.ForceUpdateCanvases();
                if(arrowRequired) {
                var mesh=attackArrow.canvasRenderer.GetMesh();
                if(mesh==null || mesh.vertexCount<90)throw new Exception("Attack arrow did not produce a visible mesh");
                var vertices=mesh.vertices;
                var edge0=RectTransformUtility.WorldToScreenPoint(camera,attackArrow.rectTransform.TransformPoint(vertices[0]));
                var edge1=RectTransformUtility.WorldToScreenPoint(camera,attackArrow.rectTransform.TransformPoint(vertices[1]));
                if(Mathf.Abs(Vector2.Distance(edge0,edge1)-7)>0.5f)throw new Exception("Attack shaft is not seven screen pixels wide");
                var tip=RectTransformUtility.WorldToScreenPoint(camera,attackArrow.rectTransform.TransformPoint(vertices[vertices.Length-3]));
                if(Vector2.Distance(tip,screen)>0.5f)throw new Exception("Attack tip does not meet the cursor");
                }
                // Prepare every active text before generating meshes: an offscreen resize can repack the shared font atlas.
                var renderedText=canvas.GetComponentsInChildren<Text>();
                foreach(var text in renderedText)if(text.font!=null)text.font.RequestCharactersInTexture(text.text,text.fontSize,text.fontStyle);
                foreach(var text in renderedText)text.SetVerticesDirty();
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=texture;
                var pixels=new Texture2D(width,width*9/16,TextureFormat.RGB24,false); pixels.ReadPixels(new Rect(0,0,width,width*9/16),0,0); pixels.Apply();
                Directory.CreateDirectory("Library/AttackVisuals"); string path="Library/AttackVisuals/Attack-"+label+"-"+width+".png";
                File.WriteAllBytes(path,pixels.EncodeToPNG()); Destroy(pixels); return path;
            }
            finally { RenderTexture.active=previous; camera.targetTexture=null; canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.worldCamera=null; texture.Release(); Destroy(texture); Destroy(cameraGO); Canvas.ForceUpdateCanvases(); }
        }
    }
}
#endif
