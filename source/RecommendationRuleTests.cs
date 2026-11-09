using System;
using System.Linq;
using System.Collections.Generic;

namespace EndstateMatrix {
 internal static class RecommendationRuleTests {
  static TeamRecipe Recipe(string id,int alternatives,params string[] members){return new TeamRecipe{Id=id,SourceId=id,Members=members.ToList(),Alternatives=alternatives};}
  static GuideDatabase Fixture(params TeamRecipe[] recipes){return new GuideDatabase{Recipes=recipes.ToList(),Sources=recipes.Select(r=>new GuideSource{Id=r.SourceId,RoleId=r.Members[0],Author="test",Title="test",Url="https://wuwaguide.kurogames.com/zh-Hans/"}).ToList()};}
  internal static void Run(Action<bool,string> check){
   foreach(string id in new[]{"1503","1203","1301","1104","1405","1501","1604","1406","1309","1103"})check(!CharacterRecommendationMetadata.IsLimited(id),"standard five stars Rover and four stars excluded from limited priority "+id);
   check(CharacterRecommendationMetadata.IsLimited("1305")&&CharacterRecommendationMetadata.IsLimited("1308"),"free acquisition does not change limited character classification");
   check(CharacterRecommendationMetadata.Primary(Recipe("new",0,"1505","1311","1312")).Id=="1311","older healer does not determine release order");
   check(CharacterRecommendationMetadata.Primary(Recipe("zani",0,"1505","1507","1506")).Id=="1507","dual-role Phoebe does not replace Zani as primary");
   check(CharacterRecommendationMetadata.Primary(Recipe("changli",0,"1206","1207","1205")).Id=="1205","support guide owner is not mistaken for primary damage dealer");
   check(CharacterRecommendationMetadata.Primary(Recipe("dual",0,"1205","1304","1503")).Id=="1304","genuine dual carry uses earlier primary release");
   var db=TeamRecommendations.Database;var late=db.Recipes.First(r=>r.Id=="10228-1312-1505");var middle=db.Recipes.First(r=>r.Id=="10075-1507-1506");var early=db.Recipes.First(r=>r.Id=="10153-1207-1205");
   var sorted=CharacterRecommendationMetadata.Chronological(new[]{late,early,middle});check(sorted[0]==early&&sorted[1]==middle&&sorted[2]==late,"teams sorted by carry release instead of source order or helper age");
   var plan=Data.Fresh();plan.Teams=plan.Teams.Take(3).ToList();plan.Owned=sorted.SelectMany(r=>r.Members).Distinct().ToList();var applied=TeamRecommendations.Apply(plan,new RecommendationResult{Teams=new List<TeamRecipe>{late,middle,early}},false);check(applied.Teams.Select(t=>t.RecommendationId).SequenceEqual(new[]{early.Id,middle.Id,late.Id}),"apply always writes recommended teams in chronological order");
   plan=Data.Fresh();plan.Teams=plan.Teams.Take(1).ToList();plan.Owned=new List<string>{"1301","1405","1503","1404","1204"};plan.Profiles["1404"]=new Profile{Level=1};
   var standard=Recipe("standard",0,"1301","1405","1503");var limited=Recipe("limited",2,"1404","1204","1503");var result=TeamRecommendations.Generate(plan,false,Fixture(standard,limited));check(result.Teams.Single().Id=="limited","limited five star takes priority over standard primary recipe and readiness score");
   plan.Owned.Remove("1404");result=TeamRecommendations.Generate(plan,false,Fixture(standard,limited));check(result.Teams.Single().Id=="standard","standard roster remains usable when limited five star is unowned");
   var normals=new[]{"1402","1202","1102","1601"};plan=Data.Fresh();plan.Teams=plan.Teams.Take(2).ToList();plan.Owned=normals.Concat(new[]{"1505","1209"}).ToList();
   var one=Recipe("one",0,normals[0],normals[1],"1505");var repeat=Recipe("repeat",0,normals[2],normals[3],"1505");var distinct=Recipe("distinct",1,normals[2],normals[3],"1209");result=TeamRecommendations.Generate(plan,false,Fixture(one,repeat,distinct));check(result.Teams.Count==2&&result.Teams.SelectMany(r=>r.Members).Where(CharacterRecommendationMetadata.IsLimited).Distinct().Count()==2,"distinct limited coverage preferred to repeatedly counting the same support");
   plan=Data.Fresh();plan.Owned=Data.ActiveCatalog(plan).Select(c=>c.Id).ToList();result=TeamRecommendations.Generate(plan,false);check(result.Teams.Count==8&&result.Teams.All(r=>r.Members.All(CharacterRecommendationMetadata.IsLimited)),"all-owned fixture fills eight guide teams entirely with limited five stars");
   var dates=result.Teams.Select(r=>CharacterRecommendationMetadata.Primary(r).FirstRelease).ToArray();check(dates.SequenceEqual(dates.OrderBy(d=>d,StringComparer.Ordinal)),"real guide recommendations have ascending carry release dates");
   applied=TeamRecommendations.Apply(plan,result,false);check(applied.Teams.All(t=>t.Slots[0]==CharacterRecommendationMetadata.Primary(TeamRecommendations.FindRecipe(t)).Id),"generated teams display the primary damage dealer in the first slot");
   plan.Teams[1].Name="保留手动位置";plan.Teams[1].Slots=new List<string>{"1503",null,null};result=TeamRecommendations.Generate(plan,false);applied=TeamRecommendations.Apply(plan,result,false);check(applied.Teams[1].Name=="保留手动位置"&&applied.Teams[1].Slots.SequenceEqual(plan.Teams[1].Slots),"chronological placement preserves a manual team position");
   var newTeams=applied.Teams.Where(t=>t.RecommendationId!=null).Select(TeamRecommendations.FindRecipe).ToList();check(newTeams.All(r=>r!=null)&&newTeams.Select(r=>CharacterRecommendationMetadata.Primary(r).FirstRelease).SequenceEqual(newTeams.Select(r=>CharacterRecommendationMetadata.Primary(r).FirstRelease).OrderBy(d=>d,StringComparer.Ordinal)),"only generated empty slots are ordered around manual teams");Data.Validate(applied);
  }
 }
}
