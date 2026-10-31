using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using DeadSurvivor;
public static class InteractionCheck {
 public static string Queue(){return QueueTo("MigrationVerification/interaction.txt");}
 public static string QueueTo(string outputPath){
  var g=UnityEngine.Object.FindAnyObjectByType<DSGame>();
  if(g==null)throw new Exception("DSGame missing");
  if(System.IO.File.Exists(outputPath))throw new Exception("Output path must be new");
  g.StartCoroutine(Flow(g,outputPath));return "QUEUED";
 }
 static System.Collections.IEnumerator Flow(DSGame g,string outputPath){
  string result=null;
  try{g.Selected=new System.Collections.Generic.List<int>{0,1,2,5};g.StartRun();g.FinishWave(true);g.Stash.Add(g.MakeItem("amulet"));g.UI.Prep();}
  catch(Exception ex){result="FAILED: setup: "+ex;}
  if(result==null){float prepBefore=g.RealPrep;yield return null;yield return null;try{if(g.RealPrep<=prepBefore)throw new Exception("Prep real time did not advance");result=Run();}catch(Exception ex){result="FAILED: "+ex;}}
  if(result!=null&&result.StartsWith("PASS")){
   yield return null;yield return null;
   try {
    Canvas.ForceUpdateCanvases();
    var held=g.Equipment.Actions(g.Team[0]).First();var blocker=g.Stash.First(i=>i.type=="amulet");
    var source=UnityEngine.Object.FindObjectsByType<DSPointer>().First(p=>p.item==held.id&&p.gameObject.activeInHierarchy);
    var target=UnityEngine.Object.FindObjectsByType<DSPointer>().First(p=>p.item==blocker.id&&p.gameObject.activeInHierarchy);
    var rect=(RectTransform)target.transform;var e=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center))};
    source.OnPointerDown(e);source.OnBeginDrag(e);source.OnDrag(e);source.OnEndDrag(e);
    if(!g.Stash.Contains(held)||!g.Stash.Contains(blocker)||g.Team[0].items.Contains(held))throw new Exception("Occupied stash raycast drag unequip failed");
   }catch(Exception ex){result="FAILED: "+ex;}
   try {
    var dead=g.Team[0];dead.hp=0;g.UI.Prep();Canvas.ForceUpdateCanvases();
    var button=UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>().First(b=>b.gameObject.activeInHierarchy&&b.name=="부활하기");
    var cover=button.transform.parent;
    if(cover.name!="ReviveOverlay"||cover.GetComponent<UnityEngine.UI.Image>().color!=Color.black||cover.GetComponentsInChildren<DSPointer>().Length!=0)throw new Exception("Death overlay not black or exposes equipment input");
    if(g.Unequip(dead.items.First().id))throw new Exception("Dead equipment was not locked");
    UnityEngine.EventSystems.ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},UnityEngine.EventSystems.ExecuteEvents.pointerClickHandler);
    if(dead.hp!=dead.maxhp||dead.hp<=0)throw new Exception("Revive central button failed to restore HP");
    if(!g.Unequip(dead.items.First().id))throw new Exception("Revive failed to unlock equipment");
    result+="; revive central button restores HP and unlocks equipment";
   }catch(Exception ex){result="FAILED: "+ex;}
   g.StartRun();g.Paused=true;float pauseBefore=g.RealPaused,battleBefore=g.RealBattle;
   yield return null;yield return null;
   if(g.RealPaused<=pauseBefore||g.RealBattle!=battleBefore)result="FAILED: real pause/battle time partition";
   else result+="; real prep/pause time partition";
  }
  try{g.MoveInput=Vector2.zero;g.ReturnLobby();}catch(Exception ex){result+=" FAILED cleanup: "+ex;}
  System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(outputPath)));
  System.IO.File.WriteAllText(outputPath,result);
 }

 public static string Run(){var g=UnityEngine.Object.FindAnyObjectByType<DSGame>();var i=g.Stash.Last(i=>i.type=="amulet");var source=UnityEngine.Object.FindObjectsByType<DSPointer>().First(p=>p.item==i.id&&p.gameObject.activeInHierarchy);var target=UnityEngine.Object.FindObjectsByType<DSPointer>().First(p=>p.character==0&&p.x==4&&p.y==4&&p.gameObject.activeInHierarchy);var sr=(RectTransform)source.transform;var tr=(RectTransform)target.transform;var from=RectTransformUtility.WorldToScreenPoint(null,sr.TransformPoint(sr.rect.center));var to=RectTransformUtility.WorldToScreenPoint(null,tr.TransformPoint(tr.rect.center));var e=new PointerEventData(EventSystem.current){position=from};source.OnBeginDrag(e);e.position=to;source.OnDrag(e);source.OnEndDrag(e);if(!g.Team[0].items.Contains(i)||i.x!=4||i.y!=4)throw new Exception("Drag/drop through EventSystem raycast failed");
 g.NextWave();Canvas.ForceUpdateCanvases();var joy=UnityEngine.Object.FindObjectsByType<DSPointer>().First(p=>p.joystick&&p.gameObject.activeInHierarchy);e.position=new Vector2(300,300);joy.OnPointerDown(e);e.position+=new Vector2(100,0);joy.OnDrag(e);if(!g.UI.JoystickActive||g.UI.Joystick.x<.9f)throw new Exception("Joystick drag failed");joy.OnPointerUp(e);if(g.UI.JoystickActive||g.UI.Joystick!=Vector2.zero)throw new Exception("Joystick release failed");g.FinishWave(true);g.Stash.Clear();var selected=g.MakeItem("amulet");g.Stash.Add(selected);g.UI.Prep();Canvas.ForceUpdateCanvases();
 var source2=UnityEngine.Object.FindObjectsByType<DSPointer>().First(p=>p.item==selected.id&&p.gameObject.activeInHierarchy);g.UI.ClickTarget(source2);g.UI.RotateHeld();if(selected.rotation!=0)throw new Exception("Preview rotation mutated original");
 var target2=UnityEngine.Object.FindObjectsByType<DSPointer>().First(p=>p.character==0&&p.x==4&&p.y==3&&p.gameObject.activeInHierarchy);g.UI.HoverTarget(target2);target2.OnPointerExit(e);
 if(!UnityEngine.Object.FindObjectsByType<TMPro.TextMeshProUGUI>().Any(t=>t.gameObject.activeInHierarchy&&t.text.Contains("공유")))throw new Exception("Missing unchanged party share explanation");
 var details=UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>().FirstOrDefault(b=>b.name=="상세 보기"&&b.gameObject.activeInHierarchy);if(details==null)throw new Exception("Details button vanished on pointer exit");details.onClick.Invoke();target2.OnPointerEnter(e);target2.OnPointerDown(e);target2.OnPointerUp(e);target2.OnPointerClick(e);if(g.Team[0].items.Contains(selected)||selected.rotation!=0)throw new Exception("First tap committed instead of previewing");target2.OnPointerEnter(e);target2.OnPointerDown(e);target2.OnPointerUp(e);target2.OnPointerClick(e);if(!g.Team[0].items.Contains(selected)||selected.rotation!=1)throw new Exception("Tap preview/rotation commit failed");
 g.StartRun();g.FinishWave(true);g.Stash.Clear();var stone=g.MakeItem("whetstone");stone.x=1;stone.y=2;g.Team[0].items.Add(stone);g.UI.Prep();Canvas.ForceUpdateCanvases();
 var weapon=g.Equipment.Actions(g.Team[0]).First();var weaponPointer=UnityEngine.Object.FindObjectsByType<DSPointer>().First(p=>p.item==weapon.id&&p.gameObject.activeInHierarchy);var weaponTarget=UnityEngine.Object.FindObjectsByType<DSPointer>().First(p=>p.character==0&&p.x==1&&p.y==1&&p.gameObject.activeInHierarchy);g.UI.ClickTarget(weaponPointer);g.UI.HoverTarget(weaponTarget);
 if(!UnityEngine.Object.FindObjectsByType<DSPointer>().Any(p=>p.item==stone.id&&p.gameObject.activeInHierarchy&&p.GetComponent<UnityEngine.UI.Outline>()?.enabled==true))throw new Exception("Weapon preview failed to highlight adjacent auxiliary");
 g.UI.Prep();g.Stash.Add(g.MakeItem("whetstone"));g.UI.Prep();var picked=g.Stash.Last();var pickedPointer=UnityEngine.Object.FindObjectsByType<DSPointer>().First(p=>p.item==picked.id&&p.gameObject.activeInHierarchy);g.UI.ClickTarget(pickedPointer);var hover=UnityEngine.Object.FindObjectsByType<DSPointer>().First(p=>p.character==0&&p.x==4&&p.y==4&&p.gameObject.activeInHierarchy);g.UI.HoverTarget(hover);
 if(!UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Outline>().Any(o=>o.enabled&&o.transform.Find("Recommendation")!=null))throw new Exception("Recommendation outline lost during preview");
 g.UI.Prep();Canvas.ForceUpdateCanvases();weapon=g.Equipment.Actions(g.Team[0]).First();weaponPointer=UnityEngine.Object.FindObjectsByType<DSPointer>().First(p=>p.item==weapon.id&&p.gameObject.activeInHierarchy);g.UI.ClickTarget(weaponPointer);
 var stashTarget=UnityEngine.Object.FindObjectsByType<DSPointer>().First(p=>p.stash&&p.gameObject.activeInHierarchy);g.UI.HoverTarget(stashTarget);
 if(!UnityEngine.Object.FindObjectsByType<TMPro.TextMeshProUGUI>().Any(t=>t.gameObject.activeInHierarchy&&t.text.Contains("장비 해제 미리보기")&&!t.text.Contains("출발 불가")))throw new Exception("Missing primary unequip preview/warning");
 UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>().First(b=>b.name=="상세 보기"&&b.gameObject.activeInHierarchy).onClick.Invoke();
 if(!UnityEngine.Object.FindObjectsByType<TMPro.TextMeshProUGUI>().Any(t=>t.gameObject.activeInHierarchy&&t.text.Contains("피해/회복")&&t.text.Contains("→ 없음")))throw new Exception("Missing weapon loss stats in detailed unequip preview");
 stashTarget.OnPointerClick(e);if(!g.Team[0].items.Contains(weapon)||g.Stash.Contains(weapon))throw new Exception("First stash tap committed instead of previewing");
 stashTarget.OnPointerClick(e);if(g.Team[0].items.Contains(weapon)||!g.Stash.Contains(weapon)||g.Equipment.Actions(g.Team[0]).Any())throw new Exception("Second stash tap failed to unequip");
 g.UI.Prep();Canvas.ForceUpdateCanvases();weaponPointer=UnityEngine.Object.FindObjectsByType<DSPointer>().First(p=>p.item==weapon.id&&p.gameObject.activeInHierarchy);g.UI.ClickTarget(weaponPointer);
 weaponTarget=UnityEngine.Object.FindObjectsByType<DSPointer>().First(p=>p.character==0&&p.x==1&&p.y==1&&p.gameObject.activeInHierarchy);g.UI.HoverTarget(weaponTarget);
 UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>().First(b=>b.name=="상세 보기"&&b.gameObject.activeInHierarchy).onClick.Invoke();
 if(!UnityEngine.Object.FindObjectsByType<TMPro.TextMeshProUGUI>().Any(t=>t.gameObject.activeInHierarchy&&t.text.Contains("피해/회복 없음 →")))throw new Exception("Missing weapon gain stats in detailed equip preview");
 g.UI.Prep();g.Stash.Clear();var mergeA=g.MakeItem("amulet");var mergeB=g.MakeItem("amulet");g.Stash.Add(mergeA);g.Stash.Add(mergeB);g.UI.Prep();Canvas.ForceUpdateCanvases();
 var mergeFrom=UnityEngine.Object.FindObjectsByType<DSPointer>().First(p=>p.item==mergeA.id&&p.gameObject.activeInHierarchy);var mergeTo=UnityEngine.Object.FindObjectsByType<DSPointer>().First(p=>p.item==mergeB.id&&p.gameObject.activeInHierarchy);g.UI.ClickTarget(mergeFrom);mergeTo.OnPointerClick(e);
 if(mergeB.grade!=0||g.Find(mergeA.id)==null||!UnityEngine.Object.FindObjectsByType<TMPro.TextMeshProUGUI>().Any(t=>t.gameObject.activeInHierarchy&&t.text.Contains("보관함 합성 미리보기")))throw new Exception("Stash merge first tap preview failed");
 mergeTo.OnPointerClick(e);if(mergeB.grade!=1||g.Find(mergeA.id)!=null)throw new Exception("Stash merge second tap commit failed");
 g.StartRun();g.FinishWave(true);g.Team[0].items.Clear();g.Stash.Clear();var piercing=g.MakeItem("pierce");piercing.x=1;piercing.y=2;g.Team[0].items.Add(piercing);var addedBow=g.MakeItem("shortbow");g.Stash.Add(addedBow);g.UI.Prep();Canvas.ForceUpdateCanvases();
 var bowSource=UnityEngine.Object.FindObjectsByType<DSPointer>().First(p=>p.item==addedBow.id&&p.gameObject.activeInHierarchy);var bowTarget=UnityEngine.Object.FindObjectsByType<DSPointer>().First(p=>p.character==0&&p.x==1&&p.y==1&&p.gameObject.activeInHierarchy);g.UI.ClickTarget(bowSource);bowTarget.OnPointerClick(e);
 UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>().First(b=>b.name=="상세 보기"&&b.gameObject.activeInHierarchy).onClick.Invoke();
 if(!UnityEngine.Object.FindObjectsByType<TMPro.TextMeshProUGUI>().Any(t=>t.gameObject.activeInHierarchy&&t.text.Contains("#"+addedBow.id)&&t.text.Contains("관통")&&t.text.Contains("1명")))throw new Exception("New action preview omitted scoped special effect or item identity");
 bowTarget.OnPointerClick(e);if(!g.Team[0].items.Contains(addedBow)||g.Equipment.Stats(g.Team[0],addedBow).pierce!=1)throw new Exception("New action preview disagrees with actual scoped effect");
 var otherBow=g.MakeItem("shortbow");g.Stash.Add(otherBow);g.UI.Prep();Canvas.ForceUpdateCanvases();var otherSource=UnityEngine.Object.FindObjectsByType<DSPointer>().First(p=>p.item==otherBow.id&&p.gameObject.activeInHierarchy);var otherTarget=UnityEngine.Object.FindObjectsByType<DSPointer>().First(p=>p.character==0&&p.x==1&&p.y==4&&p.gameObject.activeInHierarchy);g.UI.ClickTarget(otherSource);otherTarget.OnPointerClick(e);otherTarget.OnPointerClick(e);
 if(g.Equipment.Actions(g.Team[0]).Count()!=2||!g.Team[0].items.Contains(addedBow)||!g.Team[0].items.Contains(otherBow))throw new Exception("Multiple actions did not coexist through actual tap placement");

 g.Stash.Clear();var blocker=g.MakeItem("amulet");g.Stash.Add(blocker);g.UI.Prep();Canvas.ForceUpdateCanvases();
 var outSource=UnityEngine.Object.FindObjectsByType<DSPointer>().First(p=>p.item==addedBow.id&&p.gameObject.activeInHierarchy);
 var outTarget=UnityEngine.Object.FindObjectsByType<DSPointer>().First(p=>p.item==blocker.id&&p.gameObject.activeInHierarchy);
 g.UI.ClickTarget(outSource);outTarget.OnPointerClick(e);
 if(!g.Team[0].items.Contains(addedBow)||!UnityEngine.Object.FindObjectsByType<TMPro.TextMeshProUGUI>().Any(t=>t.gameObject.activeInHierarchy&&t.text.Contains("장비 해제 미리보기")))throw new Exception("Occupied stash unequip preview failed");
 outTarget.OnPointerClick(e);if(!g.Stash.Contains(addedBow)||!g.Stash.Contains(blocker))throw new Exception("Occupied stash tap unequip failed");

 return "PASS occupied stash tap/drag unequip, drag/drop (4,4), joystick, rotation preview isolation, details button, tap preview/commit, unequip preview/commit, stash merge preview/commit, parallel action placement and scoped preview";
 }
}
