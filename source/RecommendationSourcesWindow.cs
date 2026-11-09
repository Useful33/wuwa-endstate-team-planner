using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EndstateMatrix {
 public sealed class RecommendationSourcesWindow:Window {
  readonly Plan plan;readonly GuideDatabase db;ComboBox mode;TextBox search;StackPanel rows;
  static Brush Brush(string value){return (Brush)new BrushConverter().ConvertFromString(value);}
  TextBlock Text(string value,double size=12,string color="#A4B8C8"){return new TextBlock{Text=value,FontSize=size,Foreground=Brush(color),TextWrapping=TextWrapping.Wrap};}
  public RecommendationSourcesWindow(Window owner,Plan snapshot){
   Owner=owner;plan=snapshot;db=TeamRecommendations.Database;Title="推荐依据与攻略来源";Width=850;Height=Math.Min(720,SystemParameters.WorkArea.Height*0.92);MinWidth=720;MinHeight=500;WindowStartupLocation=WindowStartupLocation.CenterOwner;Background=Brush("#14202A");Foreground=Brush("#E8EDF2");FontFamily=owner.FontFamily;Resources=owner.Resources;
   var root=new Grid{Margin=new Thickness(24)};root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());Content=root;
   var heading=new StackPanel();heading.Children.Add(Text("推荐依据与攻略来源",22,"#E8EDF2"));var subtitle=Text(db.Site+"收录 · 玩家作者攻略 · 核对日期 "+db.CheckedAt);subtitle.Margin=new Thickness(0,8,0,10);heading.Children.Add(subtitle);var explanation=Text("队伍来自攻略的“队友推荐”栏目。工具尽量凑齐完整队伍，优先覆盖更多限定五星，再参考攻略首选、练度和更新日期。新生成队伍按主力首次推出时间从早到晚排列；双主力取较早一位。未计算当前关卡抗性、赛季增益、武器声骸或实际伤害。",12);explanation.Margin=new Thickness(0,0,0,15);heading.Children.Add(explanation);root.Children.Add(heading);
   var filters=new DockPanel{Margin=new Thickness(0,0,0,14)};mode=new ComboBox{ItemsSource=new[]{"当前配队依据","全部攻略（"+db.Sources.Count+"）"},SelectedIndex=plan.Teams.Any(t=>TeamRecommendations.FindRecipe(t)!=null)?0:1,Width=190,Margin=new Thickness(0,0,12,0)};mode.SelectionChanged+=(s,e)=>Refresh();filters.Children.Add(mode);search=new TextBox{ToolTip="按角色、攻略标题或作者搜索"};search.TextChanged+=(s,e)=>Refresh();filters.Children.Add(search);Grid.SetRow(filters,1);root.Children.Add(filters);
   rows=new StackPanel();var scroll=new ScrollViewer{Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(scroll,2);root.Children.Add(scroll);Refresh();
  }
  void Refresh(){
   if(rows==null)return;rows.Children.Clear();string query=search.Text.Trim();int count=0;
   if(mode.SelectedIndex==0){foreach(var team in plan.Teams){var recipe=TeamRecommendations.FindRecipe(team);if(recipe==null)continue;var source=TeamRecommendations.SourceFor(recipe);string members=string.Join(" / ",team.Slots.Select(id=>Data.Find(id).Name));if(!Matches(query,members,source.Title,source.Author))continue;Add(team.Name+"  ·  "+members,source,CharacterRecommendationMetadata.Describe(recipe)+" / "+(recipe.Alternatives==0?"攻略首选成员组合":"含 "+recipe.Alternatives+" 个攻略列明的备选成员"));count++;}}
   else {foreach(var source in db.Sources.OrderBy(x=>Data.Find(x.RoleId).Name).ThenBy(x=>x.Id)){string character=Data.Find(source.RoleId).Name;if(!Matches(query,character,source.Title,source.Author))continue;Add(character,source,"该攻略已收录 "+db.Recipes.Count(x=>x.SourceId==source.Id)+" 个首选/备选组合");count++;}}
   if(count==0)rows.Children.Add(Text(mode.SelectedIndex==0?"当前没有仍与推荐模板一致的队伍。手动更改成员后，原攻略标签会自动隐藏。":"没有找到匹配的攻略。",13));
  }
  static bool Matches(string query,params string[] values){return query.Length==0||values.Any(value=>(value??"").IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0);}
  void Add(string title,GuideSource source,string note){
   var content=new StackPanel{Margin=new Thickness(14)};content.Children.Add(Text(title,15,"#AAEFC6"));var name=Text(source.Title,13,"#E8EDF2");name.Margin=new Thickness(0,8,0,5);content.Children.Add(name);content.Children.Add(Text("作者："+source.Author+"  /  原站更新："+source.UpdatedOn+"  /  "+note,11));var open=new Button{Content="查看原攻略 ↗",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,10,0,0)};open.Click+=(s,e)=>{try{Uri uri;if(!Uri.TryCreate(source.Url,UriKind.Absolute,out uri)||uri.Scheme!="https"||uri.Host!="wuwaguide.kurogames.com")throw new Exception("来源链接不合法");Process.Start(new ProcessStartInfo(source.Url){UseShellExecute=true});}catch(Exception ex){MessageBox.Show(this,"无法打开攻略："+ex.Message,"攻略来源");}};content.Children.Add(open);rows.Children.Add(new Border{Child=content,Background=Brush("#1B2C39"),CornerRadius=new CornerRadius(9),Margin=new Thickness(0,0,6,10)});
  }
 }
}
