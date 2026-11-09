using System;
using System.Linq;
using System.Collections.Generic;

namespace EndstateMatrix {
 internal static class RoverAndExtraUseTests {
  static TeamRecipe Recipe(string id,params string[] members){return new TeamRecipe{Id=id,SourceId=id,Members=members.ToList(),Alternatives=0};}
  internal static void Run(Action<bool,string> check){
   var plan=Data.Fresh();plan.Owned=new List<string>{"1404","1204","1407","1503"};var model=new Model(plan);var jiyan=Data.Find("1404");
   check(Data.UseLimit(plan,jiyan)==1,"ordinary character starts with one use");model.SetExtraUse(jiyan.Id);check(Data.UseLimit(plan,jiyan)==2,"selected extra character has two uses");
   check(model.Assign(jiyan.Id,0,0)==null&&model.Assign(jiyan.Id,1,0)==null,"extra character may appear in two different teams");check(model.Assign(jiyan.Id,2,0)!=null,"extra character third use rejected");check(model.Assign(jiyan.Id,0,1)!=null,"extra character duplicate within one team rejected");Data.Validate(plan);
   string before=Data.Serialize(plan);bool rejected=false;try{model.SetExtraUse("1204");}catch{rejected=true;}check(rejected&&Data.Serialize(plan)==before,"changing a twice-used extra character does not invalidate or alter teams");rejected=false;try{model.SetExtraUse(null);}catch{rejected=true;}check(rejected&&Data.Serialize(plan)==before,"cannot silently revoke an in-use extra allowance");
   plan.Teams[1].Slots[0]=null;model.SetExtraUse("1204");check(Data.UseLimit(plan,jiyan)==1&&Data.UseLimit(plan,Data.Find("1204"))==2,"changing selection restores old one-use limit");model.SetExtraUse(null);check(plan.ExtraUseCharacterId==null&&Data.UseLimit(plan,Data.Find("1204"))==1,"extra allowance may be cancelled after releasing use");
   foreach(var name in Character.Special){rejected=false;try{model.SetExtraUse(Data.Catalog.Single(c=>c.Name==name).Id);}catch{rejected=true;}check(rejected,"built-in two-use character cannot receive third use "+name);}
   model.SetExtraUse("1204");var saved=Data.Parse(Data.Serialize(plan));check(saved.ExtraUseCharacterId=="1204"&&Data.UseLimit(saved,Data.Find("1204"))==2,"extra character persists in plan JSON");
   var legacy=Data.Json.Deserialize<Dictionary<string,object>>(Data.Serialize(plan));legacy.Remove("ExtraUseCharacterId");check(Data.Parse(Data.Json.Serialize(legacy)).ExtraUseCharacterId==null,"old plan without extra setting still opens");
   model.SetExtraUse("1302");check(!plan.Owned.Contains("1302")&&Data.UseLimit(plan,Data.Find("1302"))==2,"extra setting does not invent ownership");model.SetExtraUse(null);
   var a=Recipe("j1","1404","1204","1503");var b=Recipe("j2","1404","1407","1503");var db=new GuideDatabase{Recipes=new List<TeamRecipe>{a,b},Sources=new List<GuideSource>{new GuideSource{Id="j1",RoleId="1404"},new GuideSource{Id="j2",RoleId="1404"}}};plan=Data.Fresh();plan.Owned=new List<string>{"1404","1204","1407","1503"};
   check(TeamRecommendations.Generate(plan,false,db).Teams.Count==1,"normal recommendation respects single primary use");model=new Model(plan);model.SetExtraUse("1404");var recommended=TeamRecommendations.Generate(plan,false,db);check(recommended.Teams.Count==2,"recommendation can use selected primary twice");var applied=TeamRecommendations.Apply(plan,recommended,false);check(new Model(applied).Used(jiyan)==2&&applied.ExtraUseCharacterId==jiyan.Id,"recommended doubled character stays legal and keeps setting");
   plan=Data.Fresh();model=new Model(plan);model.SetExtraUse("1501");plan.Owned.Add("1501");model.Assign("1501",0,0);model.Assign("1501",1,0);model.SetRover("女","气动");check(plan.ExtraUseCharacterId=="1406"&&plan.Teams[0].Slots[0]=="1406"&&plan.Teams[1].Slots[0]=="1406"&&Data.UseLimit(plan,Data.Find("1406"))==2,"Rover extra allowance follows attribute change");Data.Validate(plan);
   before=Data.Serialize(plan);int added=model.MergeRecognized(new[]{"1604"},"男","湮灭");check(added==0&&plan.RoverGender=="男"&&plan.RoverElement=="湮灭","recognized Rover overrides manual gender and attribute even when already owned");check(plan.Owned.SequenceEqual(new[]{"1604"})&&plan.Teams.Take(2).All(t=>t.Slots[0]=="1604")&&plan.ExtraUseCharacterId=="1604","Rover screenshot override migrates ownership teams and extra allowance");Data.Validate(plan);
   var undo=Data.Parse(before);check(undo.RoverGender=="女"&&undo.RoverElement=="气动"&&undo.ExtraUseCharacterId=="1406","single undo snapshot restores Rover and extra allowance");
   foreach(var gender in new[]{"男","女"})foreach(var rover in Data.Catalog.Where(Data.IsRover)){model.MergeRecognized(new[]{rover.Id},gender,rover.Element);check(plan.RoverGender==gender&&plan.RoverElement==rover.Element&&plan.Owned.Count==1&&plan.Owned[0]==rover.Id&&plan.Teams.Take(2).All(t=>t.Slots[0]==rover.Id),"recognized gender and attribute override "+gender+rover.Element);Data.Validate(plan);}
   before=Data.Serialize(plan);rejected=false;try{model.MergeRecognized(new[]{"1501"},"未知","衍射");}catch{rejected=true;}check(rejected&&Data.Serialize(plan)==before,"invalid recognized gender leaves state untouched");rejected=false;try{model.MergeRecognized(new[]{"1501"},"女","冷凝");}catch{rejected=true;}check(rejected&&Data.Serialize(plan)==before,"unsupported Rover element leaves state untouched");
   model.MergeRecognized(new[]{"1204"},"女","气动");check(plan.RoverGender=="女"&&plan.RoverElement=="导电","unselected Rover evidence never changes settings");
   model.MergeRecognized(new[]{"1501","1604","1406","1309"},"男","气动");check(plan.RoverGender=="男"&&plan.RoverElement=="气动"&&plan.Owned.Count(id=>Data.IsRover(Data.Find(id)))==1,"conflicting selected forms merge into the final chosen Rover form");Data.Validate(plan);
   foreach(var spec in Data.Json.Deserialize<List<ElementRecognitionSpec>>(Data.ReadResource("element-recognition.json"))){using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream(spec.Resource)){check(stream!=null,"element icon embedded "+spec.Name);}}
  }
 }
}
