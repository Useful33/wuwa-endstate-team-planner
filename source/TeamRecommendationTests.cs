using System;
using System.Linq;
using System.Collections.Generic;

namespace EndstateMatrix {
 internal static class TeamRecommendationTests {
  static Plan All(){var plan=Data.Fresh();plan.Owned=Data.ActiveCatalog(plan).Select(c=>c.Id).ToList();return plan;}
  static GuideDatabase Fixture(params TeamRecipe[] recipes){return new GuideDatabase{Sources=recipes.Select(r=>new GuideSource{Id=r.SourceId,RoleId=r.Members[0],Author="test",Title="test",Url="https://wuwaguide.kurogames.com/zh-Hans/"}).ToList(),Recipes=recipes.ToList()};}
  static TeamRecipe Recipe(string name,int alternatives,params string[] members){return new TeamRecipe{Id=name,SourceId=name,Members=members.ToList(),Alternatives=alternatives};}
  internal static void Run(Action<bool,string> check){
   var db=TeamRecommendations.Database;TeamRecommendations.ValidateDatabase(db);check(db.Sources.Count>=100&&db.Recipes.Count>=500,"official guide corpus loaded with provenance");
   var plan=All();string original=Data.Serialize(plan);var generated=TeamRecommendations.Generate(plan,false);check(generated.Teams.Count==8&&!generated.SearchLimited,"all-owned account produces eight complete guide teams");check(Data.Serialize(plan)==original,"recommendation search does not mutate player state");var applied=TeamRecommendations.Apply(plan,generated,false);Data.Validate(applied);
   check(applied.Teams.Count==8&&applied.Teams.All(t=>t.Slots.All(id=>id!=null)),"recommendation fills all eight empty teams");
   check(applied.Teams.All(t=>TeamRecommendations.FindRecipe(t)!=null),"every generated team has a matching guide source");
   var roundtrip=Data.Parse(Data.Serialize(applied));check(roundtrip.Teams.All(t=>TeamRecommendations.FindRecipe(t)!=null),"guide sources survive plan export and import");
   check(TeamRecommendations.Generate(applied,false).Teams.Count==0,"occupied teams left alone by default");
   var model=new Model(applied);foreach(var character in Data.ActiveCatalog(applied))check(model.Used(character)<=character.Limit,"generated usage cap for "+character.Name);
   var changed=Data.Parse(Data.Serialize(applied));var unused=Data.ActiveCatalog(changed).First(c=>!changed.Teams.SelectMany(t=>t.Slots).Contains(c.Id));changed.Teams[0].Slots[0]=unused.Id;check(TeamRecommendations.FindRecipe(changed.Teams[0])==null,"manual changes hide stale guide attribution");
   plan=All();plan.Teams[0].Name="保留手动队";plan.Teams[0].Slots=new List<string>{"1503","1505","1110"};plan.Teams[1].Slots=new List<string>{"1503","1505","1110"};plan.Teams[2].Slots[0]="1302";plan.Profiles["1302"]=new Profile{Level=80,Chain=2,Note="保留资料"};original=Data.Serialize(plan);
   generated=TeamRecommendations.Generate(plan,false);check(generated.Teams.Count>0&&generated.Teams.Count<=5,"recommendation uses only fully empty teams");check(generated.Teams.All(r=>r.Members.All(id=>id!="1503"&&id!="1505"&&id!="1110"&&id!="1302")),"recommendation respects spent special uses and partial team reservations");
   applied=TeamRecommendations.Apply(plan,generated,false);check(applied.Teams[0].Name=="保留手动队"&&applied.Teams[0].Slots.SequenceEqual(plan.Teams[0].Slots)&&applied.Teams[2].Slots.SequenceEqual(plan.Teams[2].Slots),"manual complete and partial teams preserved");check(applied.Profiles["1302"].Note=="保留资料"&&applied.Owned.SequenceEqual(plan.Owned),"recommendation keeps inventory and character profiles");check(Data.Serialize(plan)==original,"applying recommendations leaves input immutable for undo");
   generated=TeamRecommendations.Generate(plan,true);applied=TeamRecommendations.Apply(plan,generated,true);check(generated.Teams.Count==8&&applied.Teams[0].Name=="保留手动队","rebuild replaces formations while keeping names");check(Data.Serialize(Data.Parse(original))==original,"one undo snapshot restores previous formations");
   plan=Data.Fresh();generated=TeamRecommendations.Generate(plan,true);check(generated.Teams.Count==0&&Data.Serialize(TeamRecommendations.Apply(plan,generated,true))==Data.Serialize(plan),"no owned roles yields no fabricated recommendation or destructive replacement");
   plan=Data.Fresh();plan.Owned=new List<string>{"1212","1410","1209"};generated=TeamRecommendations.Generate(plan,false);check(generated.Teams.Count==1&&generated.Teams[0].Members.OrderBy(x=>x).SequenceEqual(plan.Owned.OrderBy(x=>x)),"exact official three-role inventory produces one legal team");
   foreach(var rover in Data.Catalog.Where(Data.IsRover)){plan=Data.Fresh();plan.RoverElement=rover.Element;plan.RoverGender="女";plan.Owned=Data.ActiveCatalog(plan).Select(c=>c.Id).ToList();generated=TeamRecommendations.Generate(plan,false);applied=TeamRecommendations.Apply(plan,generated,false);check(generated.Teams.Count==8&&applied.RoverElement==rover.Element&&applied.RoverGender=="女"&&applied.Teams.SelectMany(t=>t.Slots).Where(id=>id!=null).All(id=>Data.IsSelected(applied,Data.Find(id))),"selected Rover form respected for "+rover.Element);}
   var ids=Data.Catalog.Where(c=>c.Limit==1&&!Data.IsRover(c)).Take(6).Select(c=>c.Id).ToArray();plan=Data.Fresh();plan.Teams=plan.Teams.Take(2).ToList();plan.Owned=ids.ToList();
   var trap=Fixture(Recipe("a",0,ids[0],ids[1],ids[2]),Recipe("b",1,ids[0],ids[3],ids[4]),Recipe("c",1,ids[1],ids[2],ids[5]));generated=TeamRecommendations.Generate(plan,false,trap);check(generated.Teams.Count==2&&generated.Teams.All(r=>r.Id!="a"),"multi-team search avoids greedy overlap trap");
   plan=Data.Fresh();plan.Owned=ids.Concat(new[]{"1503"}).ToList();var shared=Fixture(Recipe("s1",0,ids[0],ids[1],"1503"),Recipe("s2",0,ids[2],ids[3],"1503"),Recipe("s3",0,ids[4],ids[5],"1503"));generated=TeamRecommendations.Generate(plan,false,shared);check(generated.Teams.Count==2,"shared healer used twice rather than three times");
   plan=Data.Fresh();plan.Teams=plan.Teams.Take(1).ToList();plan.Owned=ids.ToList();var choices=Fixture(Recipe("primary",0,ids[0],ids[1],ids[2]),Recipe("alternative",1,ids[3],ids[4],ids[5]));generated=TeamRecommendations.Generate(plan,false,choices);check(generated.Teams.Single().Id=="primary","guide primary members preferred to alternatives");
   choices.Recipes[1].Alternatives=0;choices.Sources[0].UpdatedAt=1704067200;choices.Sources[1].UpdatedAt=1792636819;generated=TeamRecommendations.Generate(plan,false,choices);check(generated.Teams.Single().Id=="alternative","more recently updated guide breaks equivalent ties");
   original=Data.Serialize(plan);bool rejected=false;try{TeamRecommendations.Apply(Data.Fresh(),generated,true);}catch{rejected=true;}check(rejected&&Data.Serialize(plan)==original,"stale unowned recipe cannot be applied atomically");
  }
 }
}
