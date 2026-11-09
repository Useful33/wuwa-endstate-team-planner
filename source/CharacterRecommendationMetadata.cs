using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace EndstateMatrix {
 public sealed class CharacterRecommendationInfo {
  public string Id {get;set;} public string Name {get;set;} public string FirstRelease {get;set;}
  public bool LimitedFiveStar {get;set;} public bool MainOutput {get;set;} public bool RapidConcerto {get;set;} public bool Healer {get;set;}
 }
 public sealed class CharacterRecommendationData {public string CheckedAt {get;set;} public List<CharacterRecommendationInfo> Characters {get;set;}}
 public static class CharacterRecommendationMetadata {
  static readonly object gate=new object();static Dictionary<string,CharacterRecommendationInfo> records;
  static Dictionary<string,CharacterRecommendationInfo> Records {get{lock(gate){if(records==null){
   var data=new JavaScriptSerializer().Deserialize<CharacterRecommendationData>(Data.ReadResource("character-recommendation-metadata.json"));
   if(data==null||data.Characters==null||data.Characters.Count!=Data.Catalog.Count)throw new Exception("角色首发与限定类型资料不完整。");
   foreach(var item in data.Characters){DateTime date;var character=Data.Find(item.Id);if(character==null||!DateTime.TryParseExact(item.FirstRelease,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out date)||item.LimitedFiveStar&&(character.Rarity!=5||Data.IsRover(character)))throw new Exception("角色推荐资料不合法。");}
   records=data.Characters.ToDictionary(x=>x.Id);
  }return records;}}}
  public static CharacterRecommendationInfo Get(string id){return Records[id];}
  public static bool IsLimited(string id){return Get(id).LimitedFiveStar;}
  public static CharacterRecommendationInfo Primary(TeamRecipe recipe){
   var members=recipe.Members.Select(Get).ToList();var mains=members.Where(c=>c.MainOutput).ToList();var pure=mains.Where(c=>!c.RapidConcerto).ToList();
   // A dual-role unit such as Phoebe may support a dedicated damage dealer.
   // For genuine dual-carry teams use the earlier of the two primary releases.
   if(pure.Count>0)return pure.OrderBy(c=>c.FirstRelease,StringComparer.Ordinal).ThenBy(c=>c.Id,StringComparer.Ordinal).First();
   if(mains.Count>0)return mains.OrderBy(c=>c.FirstRelease,StringComparer.Ordinal).ThenBy(c=>c.Id,StringComparer.Ordinal).First();
   var owner=members[0];if(!owner.Healer||owner.RapidConcerto)return owner;return members.FirstOrDefault(c=>!c.Healer)??owner;
  }
  public static List<TeamRecipe> Chronological(IEnumerable<TeamRecipe> teams){return teams.OrderBy(r=>Primary(r).FirstRelease,StringComparer.Ordinal).ThenBy(r=>Primary(r).Id,StringComparer.Ordinal).ThenBy(r=>r.Id,StringComparer.Ordinal).ToList();}
  public static string Describe(TeamRecipe recipe){var main=Primary(recipe);return "主力："+main.Name+" · 首次推出："+main.FirstRelease+" · 限定五星 "+recipe.Members.Count(IsLimited)+" 名";}
 }
}
