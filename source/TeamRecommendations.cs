using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Web.Script.Serialization;

namespace EndstateMatrix {
 public sealed class GuideSource {
  public string Id {get;set;} public string RoleId {get;set;} public string Title {get;set;}
  public string Author {get;set;} public string Url {get;set;} public long UpdatedAt {get;set;} public string UpdatedOn {get;set;}
 }
 public sealed class TeamRecipe {
  public string Id {get;set;} public string SourceId {get;set;} public List<string> Members {get;set;} public int Alternatives {get;set;}
  public bool Matches(Team team){return team.Slots.All(x=>x!=null)&&team.Slots.OrderBy(x=>x).SequenceEqual(Members.OrderBy(x=>x));}
 }
 public sealed class GuideDatabase {
  public string CheckedAt {get;set;} public string Site {get;set;} public string Authorship {get;set;}
  public List<GuideSource> Sources {get;set;} public List<TeamRecipe> Recipes {get;set;}
 }
 public sealed class RecommendationResult {
  public List<TeamRecipe> Teams=new List<TeamRecipe>();public int AvailableRecipes;public int EmptyTeams;public bool SearchLimited;
 }
 public static class TeamRecommendations {
  static readonly object gate=new object();static GuideDatabase database;
  public static GuideDatabase Database {get{lock(gate){if(database==null){database=new JavaScriptSerializer{MaxJsonLength=8000000}.Deserialize<GuideDatabase>(Data.ReadResource("official-recommendations.json"));ValidateDatabase(database);}return database;}}}
  public static void ValidateDatabase(GuideDatabase db){
   if(db==null||db.Sources==null||db.Recipes==null||db.Sources.Select(x=>x.Id).Distinct().Count()!=db.Sources.Count||db.Recipes.Select(x=>x.Id).Distinct().Count()!=db.Recipes.Count)throw new Exception("攻略数据格式不正确。");
   foreach(var source in db.Sources){Uri url;if(Data.Find(source.RoleId)==null||string.IsNullOrWhiteSpace(source.Author)||!Uri.TryCreate(source.Url,UriKind.Absolute,out url)||url.Scheme!="https"||url.Host!="wuwaguide.kurogames.com")throw new Exception("攻略来源不合法。");}
   foreach(var recipe in db.Recipes){
    var source=db.Sources.FirstOrDefault(x=>x.Id==recipe.SourceId);
    if(source==null||recipe.Members==null||recipe.Members.Count!=3||recipe.Members.Any(x=>Data.Find(x)==null)||recipe.Members.Select(x=>Data.Find(x).Group).Distinct().Count()!=3||recipe.Members[0]!=source.RoleId||recipe.Alternatives<0||recipe.Alternatives>2)throw new Exception("攻略配队条目不合法。");
   }
  }
  public static TeamRecipe FindRecipe(Team team){return string.IsNullOrEmpty(team.RecommendationId)?null:Database.Recipes.FirstOrDefault(x=>x.Id==team.RecommendationId&&x.Matches(team));}
  public static GuideSource SourceFor(TeamRecipe recipe){return Database.Sources.First(x=>x.Id==recipe.SourceId);}
  struct Usage:IEquatable<Usage> {
   public ulong Once,Twice;public bool Equals(Usage other){return Once==other.Once&&Twice==other.Twice;}
   public override bool Equals(object value){return value is Usage&&Equals((Usage)value);}
   public override int GetHashCode(){return Once.GetHashCode()*397^Twice.GetHashCode();}
  }
  sealed class Candidate {public TeamRecipe Recipe;public ulong Mask;public int Score,LimitedCount;}
  sealed class Node {public Usage Usage;public long Score;public int LimitedUnique,LimitedAppearances;public Node Parent;public Candidate Candidate;}
  static int CountBits(ulong value){int count=0;while(value!=0){value&=value-1;count++;}return count;}
  static IOrderedEnumerable<Node> Rank(IEnumerable<Node> nodes){return nodes.OrderByDescending(x=>x.LimitedUnique).ThenByDescending(x=>x.LimitedAppearances).ThenByDescending(x=>x.Score).ThenBy(x=>x.Usage.Once).ThenBy(x=>x.Usage.Twice);}
  // Bounded beam search considers complete combinations together, so shared support
  // characters are allocated across teams instead of greedily consumed by the first team.
  public static RecommendationResult Generate(Plan plan,bool replace,GuideDatabase custom=null){
   Data.Validate(plan);var db=custom??Database;var result=new RecommendationResult();result.EmptyTeams=replace?plan.Teams.Count:plan.Teams.Count(t=>t.Slots.All(id=>id==null));if(result.EmptyTeams==0)return result;
   var catalog=Data.ActiveCatalog(plan).ToList();if(catalog.Count>64)throw new Exception("角色数量超出当前推荐器支持范围。");var index=catalog.Select((c,i)=>new{c.Id,Index=i}).ToDictionary(x=>x.Id,x=>x.Index);var owned=new HashSet<string>(plan.Owned);var model=new Model(plan);Usage initial=new Usage();ulong singletons=0;int remaining=0;
   ulong limitedMask=0;foreach(var c in catalog){int limit=Data.UseLimit(plan,c);ulong bit=1UL<<index[c.Id];if(CharacterRecommendationMetadata.IsLimited(c.Id))limitedMask|=bit;if(limit==1)singletons|=bit;int used=replace?0:model.Used(c);if(used>=1)initial.Once|=bit;if(used>=2)initial.Twice|=bit;if(owned.Contains(c.Id))remaining+=limit-used;}
   var sources=db.Sources.ToDictionary(x=>x.Id);var candidates=new List<Candidate>();foreach(var recipe in db.Recipes){
    if(recipe.Members.Any(id=>!owned.Contains(id)||!index.ContainsKey(id)))continue;
    ulong mask=0;int readiness=0;foreach(string id in recipe.Members){mask|=1UL<<index[id];Profile profile;readiness+=plan.Profiles.TryGetValue(id,out profile)?profile.Level+profile.Chain:90;}
    if((mask&initial.Twice)!=0||(mask&initial.Once&singletons)!=0)continue;
    int freshness=(int)Math.Min(5000,Math.Max(0,(sources[recipe.SourceId].UpdatedAt-1704067200L)/86400));candidates.Add(new Candidate{Recipe=recipe,Mask=mask,LimitedCount=CountBits(mask&limitedMask),Score=100000000-recipe.Alternatives*10000000+readiness*10000+freshness});
   }
   candidates=candidates.GroupBy(x=>x.Mask).Select(g=>g.OrderByDescending(x=>x.Score).ThenBy(x=>x.Recipe.Id,StringComparer.Ordinal).First()).OrderByDescending(x=>x.LimitedCount).ThenByDescending(x=>x.Score).ThenBy(x=>x.Recipe.Id,StringComparer.Ordinal).ToList();result.AvailableRecipes=candidates.Count;
   int target=Math.Min(result.EmptyTeams,remaining/3);var best=new Node{Usage=initial};var beam=new List<Node>{best};var watch=Stopwatch.StartNew();
   for(int depth=0;depth<target;depth++){
    var next=new Dictionary<Usage,Node>();bool stop=false;
    foreach(var node in beam){
     foreach(var candidate in candidates){
      ulong mask=candidate.Mask;if((mask&node.Usage.Twice)!=0||(mask&node.Usage.Once&singletons)!=0)continue;
      var usage=new Usage{Once=node.Usage.Once|mask,Twice=node.Usage.Twice|(node.Usage.Once&mask)};long score=node.Score+candidate.Score;Node old;
      if(!next.TryGetValue(usage,out old)||score>old.Score)next[usage]=new Node{Usage=usage,Score=score,LimitedUnique=CountBits(usage.Once&limitedMask),LimitedAppearances=CountBits(usage.Once&limitedMask)+CountBits(usage.Twice&limitedMask),Parent=node,Candidate=candidate};
     }
     if(next.Count>12000)next=Rank(next.Values).Take(2048).ToDictionary(x=>x.Usage,x=>x);
     if(watch.ElapsedMilliseconds>5000){stop=true;result.SearchLimited=true;break;}
    }
    if(next.Count==0)break;beam=Rank(next.Values).Take(256).ToList();best=beam[0];if(stop)break;
   }
   for(var node=best;node.Candidate!=null;node=node.Parent)result.Teams.Add(node.Candidate.Recipe);result.Teams=CharacterRecommendationMetadata.Chronological(result.Teams);return result;
  }
  public static Plan Apply(Plan original,RecommendationResult recommendations,bool replace){
   var result=Data.Parse(Data.Serialize(original));if(recommendations.Teams.Count==0)return result;if(replace)foreach(var team in result.Teams){team.Slots=new List<string>{null,null,null};team.RecommendationId=null;}
   var targets=result.Teams.Where(t=>t.Slots.All(id=>id==null)).ToList();if(recommendations.Teams.Count>targets.Count)throw new Exception("空队伍数量不足。");
   var ordered=CharacterRecommendationMetadata.Chronological(recommendations.Teams);for(int i=0;i<ordered.Count;i++){string primary=CharacterRecommendationMetadata.Primary(ordered[i]).Id;targets[i].Slots=ordered[i].Members.OrderBy(id=>id==primary?0:1).ToList();targets[i].RecommendationId=ordered[i].Id;}
   Data.Validate(result);return result;
  }
 }
}
