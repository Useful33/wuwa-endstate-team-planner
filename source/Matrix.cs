using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Automation;
using Microsoft.Win32;

[assembly: AssemblyTitle("鸣潮终焉矩阵配队工具")]
[assembly: AssemblyProduct("鸣潮终焉矩阵配队工具")]
[assembly: AssemblyVersion("2.0.0.0")]

namespace EndstateMatrix {
 public class Character {
  public string Id {get;set;} public string Name {get;set;} public string Element {get;set;}
  public string Weapon {get;set;} public int Rarity {get;set;} public string Image {get;set;} public string Group {get;set;}
  public int Limit { get { return Special.Contains(Name) ? 2 : 1; } }
  public static readonly string[] Special={"白芷","维里奈","卜灵","守岸人","莫宁","穗穗"};
 }
 public class Profile { public int Level {get;set;} public int Chain {get;set;} public string Note {get;set;} public Profile(){Level=90;Note="";} }
 public sealed class ExtraUseChoice {public string Id {get;set;}public string Name {get;set;}}
 public class Team {
  public string Name {get;set;} public List<string> Slots {get;set;}
  public string RecommendationId {get;set;}
  public Team(){Name="新队伍";Slots=new List<string>{null,null,null};}
 }
 public class Plan {
  public int Version {get;set;} public List<string> Owned {get;set;}
  public List<Team> Teams {get;set;} public Dictionary<string,Profile> Profiles {get;set;}
  public string RoverGender {get;set;} public string RoverElement {get;set;}
  public string ExtraUseCharacterId {get;set;}
  public Plan(){Version=2;RoverGender="男";RoverElement="衍射";Owned=new List<string>();Teams=new List<Team>();Profiles=new Dictionary<string,Profile>();}
 }
 public static class Data {
  public static JavaScriptSerializer Json=new JavaScriptSerializer {MaxJsonLength=4000000};
  public static List<Character> Catalog;
  public static string ReadResource(string name){using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream(name))using(var r=new StreamReader(s,Encoding.UTF8))return r.ReadToEnd();}
  public static void Load(){Catalog=Json.Deserialize<List<Character>>(ReadResource("characters.json"));}
  public static Character Find(string id){return Catalog.FirstOrDefault(c=>c.Id==id);}
  public static bool IsRover(Character c){return c!=null&&c.Group=="漂泊者";}
  public static int UseLimit(Plan p,Character c){var extra=Find(p.ExtraUseCharacterId);return c.Limit==1&&extra!=null&&extra.Group==c.Group?2:c.Limit;}
  public static Character RoverFor(string element){return Catalog.FirstOrDefault(c=>IsRover(c)&&c.Element==element);}
  public static bool IsSelected(Plan p,Character c){return !IsRover(c)||c.Element==p.RoverElement;}
  public static IEnumerable<Character> ActiveCatalog(Plan p){return Catalog.Where(c=>IsSelected(p,c));}
  public static string PortraitResource(Character c,string gender){return "portrait."+(IsRover(c)?(gender=="女"?"1604":"1501"):c.Id);}
  public const int DefaultTeamCount=8;
  public static void EnsureDefaultTeams(Plan p){while(p.Teams.Count<DefaultTeamCount)p.Teams.Add(new Team{Name="第 "+(p.Teams.Count+1)+" 队"});}
  public static Plan Fresh(){var p=new Plan();EnsureDefaultTeams(p);return p;}
  public static string Serialize(Plan p){return Json.Serialize(p);}
  public static Plan Parse(string text){
   var p=Json.Deserialize<Plan>(text);Validate(p);
   if(p.Version==1){
    // Prefer the Rover already deployed in a team when migrating older plans.
    var rover=p.Teams.SelectMany(t=>t.Slots).Select(Find).FirstOrDefault(IsRover)??p.Owned.Select(Find).FirstOrDefault(IsRover);
    if(rover!=null){p.RoverElement=rover.Element;p.RoverGender=rover.Id=="1604"?"女":"男";}
    else {p.RoverElement="衍射";p.RoverGender="男";}
    p.Owned.RemoveAll(id=>IsRover(Find(id))&&id!=RoverFor(p.RoverElement).Id);p.Version=2;Validate(p);
   }return p;
  }
  public static void Validate(Plan p){
   if(p==null||(p.Version!=1&&p.Version!=2)||p.Owned==null||p.Teams==null||p.Profiles==null)throw new Exception("方案格式不正确或版本不受支持。");
   if(p.Teams.Count<1||p.Teams.Count>60)throw new Exception("队伍数量必须在 1～60 之间。");
   if(p.Owned.Count!=p.Owned.Distinct().Count()||p.Owned.Any(id=>Find(id)==null))throw new Exception("持有角色列表包含重复或未知角色。");
   if(p.ExtraUseCharacterId!=null){var extra=Find(p.ExtraUseCharacterId);if(extra==null||extra.Limit!=1||!IsSelected(p,extra))throw new Exception("特殊角色必须是当前形态中原本限用一次的角色。");}
   if(p.Version==2){
    if((p.RoverGender!="男"&&p.RoverGender!="女")||RoverFor(p.RoverElement)==null)throw new Exception("漂泊者性别或属性设置不合法。");
    if(p.Owned.Any(id=>!IsSelected(p,Find(id))))throw new Exception("持有角色中只能保留选定属性的漂泊者。");
   }
   var counts=new Dictionary<string,int>();
   foreach(var t in p.Teams){
    if(t==null||t.Name==null||t.Name.Length>40||t.Slots==null||t.Slots.Count!=3)throw new Exception("每队必须有三个角色位置，队名最多 40 字。");
    var local=new HashSet<string>();
    foreach(var id in t.Slots){if(id==null)continue;var c=Find(id);if(c==null||!p.Owned.Contains(id))throw new Exception("队伍包含未知或未持有角色。");
     if(!local.Add(c.Group))throw new Exception("同一队伍不能重复放入同一角色。");
     int n=counts.ContainsKey(c.Group)?counts[c.Group]+1:1;counts[c.Group]=n;if(n>UseLimit(p,c))throw new Exception(c.Name+" 超过可用次数。");
    }
   }
   foreach(var pair in p.Profiles){var v=pair.Value;if(Find(pair.Key)==null||v==null||v.Level<1||v.Level>90||v.Chain<0||v.Chain>6||v.Note==null||v.Note.Length>200)throw new Exception("角色资料不合法。");}
  }
 }
 public class Model {
  public Plan State;
  public Model(Plan p){State=p;}
  public int Used(Character c,int exceptTeam=-1,int exceptSlot=-1){int n=0;for(int i=0;i<State.Teams.Count;i++)for(int j=0;j<3;j++){if(i==exceptTeam&&j==exceptSlot)continue;var other=Data.Find(State.Teams[i].Slots[j]);if(other!=null&&other.Group==c.Group)n++;}return n;}
  public string Check(string id,int ti,int si){
   var c=Data.Find(id);if(c==null)return "角色不存在";
   if(!Data.IsSelected(State,c))return "请先在「漂泊者设置」中选择该属性";
   if(ti<0||ti>=State.Teams.Count||si<0||si>2)return "请先选择一个队伍位置";
   if(!State.Owned.Contains(id))return "请先在「管理持有」中勾选该角色";
   for(int j=0;j<3;j++){var other=Data.Find(State.Teams[ti].Slots[j]);if(j!=si&&other!=null&&other.Group==c.Group)return "同一队伍不能重复放入 "+c.Name;}
   int limit=Data.UseLimit(State,c);if(Used(c,ti,si)>=limit)return c.Name+" 已用完 "+limit+" 次出场机会";
   return null;
  }
  public string Assign(string id,int ti,int si){var error=Check(id,ti,si);if(error==null)State.Teams[ti].Slots[si]=id;return error;}
  public void ClearTeams(){foreach(var team in State.Teams)for(int j=0;j<3;j++)team.Slots[j]=null;}
  public int MergeRecognized(IEnumerable<string> ids,string roverGender=null,string roverElement=null){
   var characters=ids.Distinct().Select(Data.Find).ToList();if(characters.Any(c=>c==null))throw new ArgumentException("识别结果中包含未知角色。");
   if(characters.Any(Data.IsRover)&&(roverGender!=null||roverElement!=null))SetRover(roverGender??State.RoverGender,roverElement??State.RoverElement);
   var additions=characters.Select(c=>Data.IsRover(c)?Data.RoverFor(State.RoverElement).Id:c.Id).Distinct().Where(id=>!State.Owned.Contains(id)).ToList();State.Owned.AddRange(additions);return additions.Count;
  }
  public void SetRover(string gender,string element){
   var target=Data.RoverFor(element);if((gender!="男"&&gender!="女")||target==null)throw new ArgumentException("请选择有效的漂泊者性别和属性。");
   var source=Data.RoverFor(State.RoverElement);bool owned=State.Owned.Any(id=>Data.IsRover(Data.Find(id)));
   if(source!=null&&source.Id!=target.Id&&!State.Profiles.ContainsKey(target.Id)&&State.Profiles.ContainsKey(source.Id)){
    var profile=State.Profiles[source.Id];State.Profiles[target.Id]=new Profile{Level=profile.Level,Chain=profile.Chain,Note=profile.Note};
   }
   State.Owned.RemoveAll(id=>Data.IsRover(Data.Find(id)));if(owned)State.Owned.Add(target.Id);
   foreach(var team in State.Teams)for(int j=0;j<3;j++)if(Data.IsRover(Data.Find(team.Slots[j])))team.Slots[j]=target.Id;
   if(Data.IsRover(Data.Find(State.ExtraUseCharacterId)))State.ExtraUseCharacterId=target.Id;
   State.RoverGender=gender;State.RoverElement=element;
  }
  public void SetExtraUse(string id){
   var next=Data.Find(id);if(id!=null&&(next==null||next.Limit!=1||!Data.IsSelected(State,next)))throw new ArgumentException("请选择一名原本限用一次的角色。");
   var old=Data.Find(State.ExtraUseCharacterId);if(old!=null&&(next==null||next.Group!=old.Group)&&Used(old)>1)throw new InvalidOperationException(old.Name+" 已经编入两支队伍，请先移除一处，再切换或取消特殊角色。");
   State.ExtraUseCharacterId=id;
  }
 }
 public class MainWindow:Window {
  static readonly Color Accent=Color.FromRgb(170,239,198);
  static Brush B(string h){return (Brush)new BrushConverter().ConvertFromString(h);}
  Model model; int activeTeam=0,activeSlot=0; bool managing=false;
  string element="全部",query=""; bool availableOnly=false;
  Stack<string> undo=new Stack<string>();
  StackPanel teamList; WrapPanel pool; TextBlock status,summary,poolCount,targetLabel;
  TextBox search; Button manageButton,undoButton,roverButton,clearAllButton,recommendButton,extraUseButton; ScrollViewer teamScroll,poolScroll; Grid content;bool recommending;
  string dataPath; bool storageReady=true; Dictionary<string,BitmapSource> images=new Dictionary<string,BitmapSource>();
  public MainWindow(string testDataDir=null){
   Title="鸣潮 · 终焉矩阵配队工具";FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=13;
   UseLayoutRounding=true;SnapsToDevicePixels=true;Background=B("#0E141B");Foreground=B("#E8EDF2");Width=1420;Height=900;MinWidth=1120;MinHeight=700;
   Width=Math.Min(Width,SystemParameters.WorkArea.Width*0.94);Height=Math.Min(Height,SystemParameters.WorkArea.Height*0.94);WindowStartupLocation=WindowStartupLocation.CenterScreen;
   dataPath=Path.Combine(testDataDir??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WuwaEndstateMatrix"),"plan.json");
   var p=Data.Fresh();string loadMessage=null;
   try{if(File.Exists(dataPath)){p=Data.Parse(File.ReadAllText(dataPath,Encoding.UTF8));Data.EnsureDefaultTeams(p);managing=p.Owned.Count==0;}else managing=true;}
   catch(Exception ex){storageReady=false;loadMessage="旧存档读取失败，已保留原文件。请导入有效方案后继续保存。 "+ex.Message;}
   model=new Model(p);LoadStyles();Build();Refresh();
   if(loadMessage!=null)Say(loadMessage,true);else Say(managing?"先勾选你拥有的角色，再点击「完成管理」开始配队。":"已载入上次方案。选择左侧位置，再点击右侧角色。",false);
   Closing+=(s,e)=>Save();
   PreviewKeyDown+=(s,e)=>{if(e.Key==Key.V&&(Keyboard.Modifiers&ModifierKeys.Control)!=0){BitmapSource image;string message;if(ClipboardScreenshots.TryRead(out image,out message)){ShowScreenshotRecognition(image);e.Handled=true;}}else if(e.Key==Key.Z&&(Keyboard.Modifiers&ModifierKeys.Control)!=0){Undo();e.Handled=true;}else if(e.Key==Key.S&&(Keyboard.Modifiers&ModifierKeys.Control)!=0){if(Save())Say("方案已保存",false);e.Handled=true;}else if(e.Key==Key.Escape){managing=false;Refresh();}};
  }
  void LoadStyles(){
   const string x=@"<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'>
    <Style TargetType='Button'><Setter Property='HorizontalContentAlignment' Value='Center'/><Setter Property='VerticalContentAlignment' Value='Center'/><Setter Property='Foreground' Value='#DDE6EE'/><Setter Property='Background' Value='#202D39'/><Setter Property='BorderBrush' Value='#344454'/><Setter Property='BorderThickness' Value='1'/><Setter Property='Padding' Value='14,9'/><Setter Property='Cursor' Value='Hand'/><Setter Property='FontSize' Value='12'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='Button'><Border x:Name='bd' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='7' Padding='{TemplateBinding Padding}'><ContentPresenter HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}' VerticalAlignment='{TemplateBinding VerticalContentAlignment}'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='bd' Property='BorderBrush' Value='#AAEFC6'/><Setter TargetName='bd' Property='Opacity' Value='0.85'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='bd' Property='Opacity' Value='0.4'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>
    <Style TargetType='TextBox'><Setter Property='Foreground' Value='#E8EDF2'/><Setter Property='Background' Value='#111C26'/><Setter Property='CaretBrush' Value='#AAEFC6'/><Setter Property='BorderBrush' Value='#344454'/><Setter Property='Padding' Value='10,8'/><Setter Property='VerticalContentAlignment' Value='Center'/><Setter Property='SelectionBrush' Value='#3E6959'/></Style>
    <Style TargetType='ComboBox'><Setter Property='Padding' Value='8,6'/><Setter Property='MinWidth' Value='100'/><Setter Property='Foreground' Value='#17232C'/></Style>
    <Style TargetType='ScrollViewer'><Setter Property='PanningMode' Value='VerticalOnly'/></Style>
    <Style TargetType='ToolTip'><Setter Property='Background' Value='#263642'/><Setter Property='Foreground' Value='#EDF5EF'/><Setter Property='Padding' Value='10'/></Style>
   </ResourceDictionary>";
   Resources=(ResourceDictionary)System.Windows.Markup.XamlReader.Parse(x);
  }
  TextBlock Text(string text,double size=13,string color="#E8EDF2"){return new TextBlock{Text=text,FontSize=size,Foreground=B(color),VerticalAlignment=VerticalAlignment.Center};}
  Button Btn(string text,Action action,bool primary=false){var b=new Button{Content=text,Margin=new Thickness(4,0,0,0)};if(primary){b.Background=new SolidColorBrush(Accent);b.Foreground=B("#10231A");b.BorderBrush=new SolidColorBrush(Accent);}b.Click+=(s,e)=>action();return b;}
  Border Box(UIElement child,string bg="#17212B",int radius=12){return new Border{Child=child,Background=B(bg),CornerRadius=new CornerRadius(radius)};}
  void Build(){
   var root=new Grid();root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Content=root;
   var header=new Grid{Margin=new Thickness(28,23,28,20)};header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
   var brand=new StackPanel();brand.Children.Add(Text("WUTHERING WAVES  /  TEAM PLANNER",10,"#8DA79D"));var title=Text("终焉矩阵",30);title.FontWeight=FontWeights.Bold;title.Margin=new Thickness(0,3,0,3);brand.Children.Add(title);brand.Children.Add(Text("让每一次出场，都恰到好处。",12,"#8A9AA9"));header.Children.Add(brand);
   var actions=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};undoButton=Btn("↶  撤销",Undo);actions.Children.Add(undoButton);actions.Children.Add(Btn("粘贴截图识别",ImportScreenshots,true));actions.Children.Add(Btn("导入方案",Import));
   var export=Btn("导出 ▾",()=>{});var menu=new ContextMenu();var json=new MenuItem{Header="导出方案文件（JSON）"};json.Click+=(s,e)=>ExportJson();menu.Items.Add(json);var png=new MenuItem{Header="导出队伍图片（PNG）"};png.Click+=(s,e)=>ExportImage();menu.Items.Add(png);var txt=new MenuItem{Header="复制配队文本"};txt.Click+=(s,e)=>CopyTeams();menu.Items.Add(txt);export.Click+=(s,e)=>{menu.PlacementTarget=export;menu.IsOpen=true;};actions.Children.Add(export);actions.Children.Add(Btn("使用说明",Help));Grid.SetColumn(actions,1);header.Children.Add(actions);root.Children.Add(header);
   content=new Grid{Margin=new Thickness(24,0,24,0)};content.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(458)});content.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(18)});content.ColumnDefinitions.Add(new ColumnDefinition());Grid.SetRow(content,1);root.Children.Add(content);
   var left=new Grid{Margin=new Thickness(18)};left.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});left.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});left.RowDefinitions.Add(new RowDefinition());left.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});var leftBox=Box(left);leftBox.BorderBrush=B("#283541");leftBox.BorderThickness=new Thickness(1);content.Children.Add(leftBox);
   var lh=new DockPanel{Margin=new Thickness(0,0,0,7)};var add=Btn("＋ 新增队伍",AddTeam);DockPanel.SetDock(add,Dock.Right);lh.Children.Add(add);recommendButton=Btn("一键推荐",()=>RecommendTeams(false),true);recommendButton.ToolTip="优先限定五星，按主力首次推出时间从早到晚填充空队；右键可重新规划全部或查看攻略来源";AutomationProperties.SetName(recommendButton,"一键推荐配队");var recommendationMenu=new ContextMenu();var fill=new MenuItem{Header="推荐空队（保留已有配队）"};fill.Click+=(s,e)=>RecommendTeams(false);recommendationMenu.Items.Add(fill);var rebuild=new MenuItem{Header="重新规划全部队伍（可撤销）"};rebuild.Click+=(s,e)=>RecommendTeams(true);recommendationMenu.Items.Add(rebuild);var guideSources=new MenuItem{Header="推荐规则与攻略来源"};guideSources.Click+=(s,e)=>ShowRecommendationSources();recommendationMenu.Items.Add(guideSources);recommendButton.ContextMenu=recommendationMenu;DockPanel.SetDock(recommendButton,Dock.Right);lh.Children.Add(recommendButton);clearAllButton=Btn("清空全部",ClearAllTeams);clearAllButton.ToolTip="一键清空全部队伍角色并返还次数，保留队伍名称和持有信息；Ctrl+Z 撤销";AutomationProperties.SetName(clearAllButton,"清空全部配队");DockPanel.SetDock(clearAllButton,Dock.Right);lh.Children.Add(clearAllButton);var lt=Text("作战编队",20);lt.FontWeight=FontWeights.Bold;lh.Children.Add(lt);left.Children.Add(lh);
   summary=Text("",11,"#91A4B5");summary.Margin=new Thickness(0,0,0,16);Grid.SetRow(summary,1);left.Children.Add(summary);
   teamList=new StackPanel();teamScroll=new ScrollViewer{Content=teamList,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(teamScroll,2);left.Children.Add(teamScroll);
   var rules=new StackPanel{Margin=new Thickness(0,12,0,0)};var rulesHeading=new DockPanel();extraUseButton=Btn("特殊角色设置",EditExtraUse);extraUseButton.Padding=new Thickness(9,5,9,5);extraUseButton.ToolTip="额外指定一名原本限用一次的角色，改为可用两次";DockPanel.SetDock(extraUseButton,Dock.Right);rulesHeading.Children.Add(extraUseButton);rulesHeading.Children.Add(Text("出场规则",11,"#AAEFC6"));rules.Children.Add(rulesHeading);var rule=Text("普通角色 1 次  ·  以下角色各 2 次\n白芷 / 维里奈 / 卜灵 / 守岸人 / 莫宁 / 穗穗",11,"#91A4B5");rule.LineHeight=21;rule.Margin=new Thickness(0,5,0,0);rules.Children.Add(rule);Grid.SetRow(rules,3);left.Children.Add(rules);
   var right=new Grid();right.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});right.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});right.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});right.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});right.RowDefinitions.Add(new RowDefinition());Grid.SetColumn(right,2);content.Children.Add(right);
   var rh=new DockPanel{Margin=new Thickness(2,4,0,16)};manageButton=Btn("管理持有",()=>{managing=!managing;Refresh();Say(managing?"点击角色卡片勾选持有；再次点击可取消。右键角色可编辑等级、共鸣链和备注。":"已切换为配队模式，点击角色加入选中位置。",false);});DockPanel.SetDock(manageButton,Dock.Right);rh.Children.Add(manageButton);roverButton=Btn("漂泊者设置",EditRover);roverButton.ToolTip="设置漂泊者的性别和当前属性";AutomationProperties.SetName(roverButton,"漂泊者设置");DockPanel.SetDock(roverButton,Dock.Right);rh.Children.Add(roverButton);var rt=new StackPanel{Orientation=Orientation.Horizontal};var title2=Text("我的共鸣者",20);title2.FontWeight=FontWeights.Bold;rt.Children.Add(title2);poolCount=Text("",11,"#8A9AA9");poolCount.Margin=new Thickness(12,2,0,0);rt.Children.Add(poolCount);rh.Children.Add(rt);right.Children.Add(rh);
   var searchRow=new Grid{Margin=new Thickness(0,0,0,12)};searchRow.ColumnDefinitions.Add(new ColumnDefinition());searchRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});search=new TextBox{Height=38,ToolTip="搜索角色名、武器或备注"};AutomationProperties.SetName(search,"搜索角色");search.TextChanged+=(s,e)=>{query=search.Text.Trim();RefreshPool();};var searchHolder=new Grid();searchHolder.Children.Add(search);var watermark=Text("搜索角色、武器或备注…",12,"#657C8E");watermark.Margin=new Thickness(12,0,0,0);watermark.IsHitTestVisible=false;searchHolder.Children.Add(watermark);search.TextChanged+=(s,e)=>watermark.Visibility=search.Text.Length==0?Visibility.Visible:Visibility.Collapsed;searchRow.Children.Add(searchHolder);var available=new CheckBox{Content="仅看可用",Foreground=B("#B5C4D0"),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(14,0,0,0)};available.Checked+=(s,e)=>{availableOnly=true;RefreshPool();};available.Unchecked+=(s,e)=>{availableOnly=false;RefreshPool();};Grid.SetColumn(available,1);searchRow.Children.Add(available);Grid.SetRow(searchRow,1);right.Children.Add(searchRow);
   var filters=new WrapPanel{Margin=new Thickness(0,0,0,12)};foreach(var el in new[]{"全部","冷凝","热熔","导电","气动","衍射","湮灭"}){string value=el;var f=Btn(el,()=>{element=value;foreach(Button b in filters.Children){bool on=(string)b.Content==element;b.Background=B(on?"#29463B":"#202D39");b.Foreground=B(on?"#AAEFC6":"#A8BAC8");}RefreshPool();});f.Margin=new Thickness(0,0,6,4);f.Padding=new Thickness(12,7,12,7);if(el=="全部"){f.Background=B("#29463B");f.Foreground=B("#AAEFC6");}filters.Children.Add(f);}Grid.SetRow(filters,2);right.Children.Add(filters);
   var hintRow=new DockPanel{Margin=new Thickness(0,0,0,10)};targetLabel=Text("",11,"#AAEFC6");hintRow.Children.Add(targetLabel);var all=Btn("全选",()=>SetAllOwned(true));all.Padding=new Thickness(9,4,9,4);var none=Btn("全不选",()=>SetAllOwned(false));none.Padding=new Thickness(9,4,9,4);all.Name="SelectAllOwned";none.Name="SelectNoneOwned";DockPanel.SetDock(none,Dock.Right);DockPanel.SetDock(all,Dock.Right);hintRow.Children.Insert(0,none);hintRow.Children.Insert(0,all);Grid.SetRow(hintRow,3);right.Children.Add(hintRow);hintRow.Tag=new Button[]{all,none};
   pool=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Left};poolScroll=new ScrollViewer{Content=pool,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(poolScroll,4);right.Children.Add(poolScroll);
   status=Text("",11,"#8A9AA9");status.TextWrapping=TextWrapping.Wrap;status.Margin=new Thickness(26,11,24,12);Grid.SetRow(status,2);root.Children.Add(status);
  }
  void Say(string message,bool error){status.Text=(error?"!  ":"●  ")+message;status.Foreground=B(error?"#F3B1A6":"#9AAFA6");}
  void Commit(Action change,string message){var old=Data.Serialize(model.State);change();undo.Push(old);if(undo.Count>80)undo=new Stack<string>(undo.Take(80).Reverse());bool saved=Save();Refresh();if(message!=null&&saved)Say(message,false);}
  bool Save(){
   if(!storageReady){Say("原存档读取失败，当前编辑暂未保存；请导出备份或导入有效方案。",true);return false;}
   try{Data.Validate(model.State);var dir=Path.GetDirectoryName(dataPath);Directory.CreateDirectory(dir);var temp=dataPath+".tmp";File.WriteAllText(temp,Data.Serialize(model.State),new UTF8Encoding(false));if(File.Exists(dataPath))File.Replace(temp,dataPath,dataPath+".bak");else File.Move(temp,dataPath);return true;}
   catch(Exception ex){Say("保存失败，请导出方案备份："+ex.Message,true);return false;}
  }
  void Undo(){if(undo.Count==0)return;model.State=Data.Parse(undo.Pop());activeTeam=Math.Min(activeTeam,model.State.Teams.Count-1);Save();Refresh();Say("已撤销上一步操作",false);}
  void Refresh(){
   activeTeam=Math.Max(0,Math.Min(activeTeam,model.State.Teams.Count-1));undoButton.IsEnabled=undo.Count>0;manageButton.Content=managing?"✓ 完成管理":"管理持有";manageButton.Background=B(managing?"#AAEFC6":"#202D39");manageButton.Foreground=B(managing?"#10231A":"#DDE6EE");
   int full=model.State.Teams.Count(t=>t.Slots.All(id=>id!=null));int filled=model.State.Teams.Sum(t=>t.Slots.Count(id=>id!=null));summary.Text=string.Format("{0} 支队伍   /   {1} 队已完成   /   {2} 个出场位",model.State.Teams.Count,full,filled);
   clearAllButton.IsEnabled=filled>0;
   roverButton.Content="漂泊者："+model.State.RoverGender+" · "+model.State.RoverElement+" ⚙";var extra=Data.Find(model.State.ExtraUseCharacterId);extraUseButton.Content=extra==null?"特殊角色：未设置":"特殊："+extra.Name+" ×2";RefreshTeams();RefreshPool();
   var r=(Grid)content.Children[1];var dock=(DockPanel)r.Children[3];foreach(var b in (Button[])dock.Tag)b.Visibility=managing?Visibility.Visible:Visibility.Collapsed;
  }
  string ElColor(string el){switch(el){case "冷凝":return "#83D9F1";case "热熔":return "#FAAA88";case "导电":return "#C2A3F4";case "气动":return "#8FDDC4";case "衍射":return "#EBD998";default:return "#D8A0C1";}}
  BitmapSource Portrait(string id,string gender=null){
   string resource=Data.PortraitResource(Data.Find(id),gender??model.State.RoverGender);
   if(images.ContainsKey(resource))return images[resource];
   try{using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)){if(s==null)return null;var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=s;image.EndInit();image.Freeze();images[resource]=image;return image;}}catch{return null;}
  }
  Grid PortraitPanel(Character c,double height,string gender=null){var g=new Grid{Height=height,ClipToBounds=true,Background=new LinearGradientBrush((Color)ColorConverter.ConvertFromString("#27394A"),(Color)ColorConverter.ConvertFromString("#17212B"),90)};var im=Portrait(c.Id,gender);if(im!=null)g.Children.Add(new Image{Source=im,Stretch=Stretch.UniformToFill,VerticalAlignment=VerticalAlignment.Center});else g.Children.Add(Text(c.Name.Substring(0,1),40,ElColor(c.Element)));return g;}
  void RefreshTeams(){
   double scroll=teamScroll.VerticalOffset;teamList.Children.Clear();
   for(int ti=0;ti<model.State.Teams.Count;ti++){
    int index=ti;var t=model.State.Teams[ti];bool active=ti==activeTeam;var stack=new StackPanel{Margin=new Thickness(12,11,12,12)};
    var head=new DockPanel{Margin=new Thickness(0,0,0,9)};var close=Btn("×",()=>{if(model.State.Teams.Count==1){Say("至少保留一支队伍",true);return;}Commit(()=>model.State.Teams.RemoveAt(index),"已删除队伍，角色次数已返还；可按 Ctrl+Z 撤销。");});close.Padding=new Thickness(7,1,7,1);close.ToolTip="删除队伍（可撤销）";DockPanel.SetDock(close,Dock.Right);head.Children.Add(close);
    var clear=Btn("清空",()=>Commit(()=>{for(int j=0;j<3;j++)t.Slots[j]=null;},"已清空该队，角色次数已返还。"));clear.Padding=new Thickness(8,3,8,3);DockPanel.SetDock(clear,Dock.Right);head.Children.Add(clear);var recipe=TeamRecommendations.FindRecipe(t);if(recipe!=null){var guide=Btn("攻略",ShowRecommendationSources);guide.Padding=new Thickness(7,3,7,3);var source=TeamRecommendations.SourceFor(recipe);guide.ToolTip="参考："+source.Title+" / 作者："+source.Author;DockPanel.SetDock(guide,Dock.Right);head.Children.Add(guide);}
    var n=Text((ti+1).ToString("00"),13,active?"#AAEFC6":"#728696");n.FontFamily=new FontFamily("Consolas");n.Margin=new Thickness(0,0,9,0);head.Children.Add(n);
    var name=new TextBox{Text=t.Name,MaxLength=40,BorderThickness=new Thickness(0),Background=Brushes.Transparent,Padding=new Thickness(3,2,3,2),FontSize=13,MinWidth=110};AutomationProperties.SetName(name,"队伍 "+(ti+1)+" 名称");name.LostKeyboardFocus+=(s,e)=>{var value=name.Text.Trim();if(value.Length==0)value="第 "+(index+1)+" 队";name.Text=value;if(value!=t.Name)Commit(()=>t.Name=value,"队名已保存");};name.KeyDown+=(s,e)=>{if(e.Key==Key.Enter){Keyboard.ClearFocus();e.Handled=true;}};head.Children.Add(name);stack.Children.Add(head);
    var slots=new System.Windows.Controls.Primitives.UniformGrid{Columns=3};for(int si=0;si<3;si++){
     int slot=si;var c=Data.Find(t.Slots[si]);bool selected=active&&si==activeSlot;
     var inside=new Grid();inside.RowDefinitions.Add(new RowDefinition{Height=new GridLength(91)});inside.RowDefinitions.Add(new RowDefinition{Height=new GridLength(29)});
     if(c==null){var plus=new StackPanel{VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};plus.Children.Add(Text("＋",27,selected?"#AAEFC6":"#526776"));plus.Children.Add(Text("选择共鸣者",10,"#7F93A3"));inside.Children.Add(plus);var empty=Text("位置 "+(si+1),10,"#526776");empty.HorizontalAlignment=HorizontalAlignment.Center;Grid.SetRow(empty,1);inside.Children.Add(empty);}
     else {inside.Children.Add(PortraitPanel(c,91));var label=Text(c.Name,11);label.HorizontalAlignment=HorizontalAlignment.Center;Grid.SetRow(label,1);inside.Children.Add(label);}
     var button=new Button{Content=inside,Padding=new Thickness(0),Margin=new Thickness(0,0,6,0),HorizontalContentAlignment=HorizontalAlignment.Stretch,Background=B("#101A24"),BorderBrush=B(selected?"#AAEFC6":"#30404E"),BorderThickness=new Thickness(selected?2:1),ToolTip=c==null?"点击选择位置，再从右侧添加角色":c.Name+" · 点击选择位置 / 右键移除"};AutomationProperties.SetName(button,"第 "+(ti+1)+" 队 位置 "+(si+1)+(c==null?" 空位":" "+c.Name));button.Click+=(s,e)=>{activeTeam=index;activeSlot=slot;if(managing)managing=false;Refresh();Say("已选中「"+model.State.Teams[index].Name+"」位置 "+(slot+1)+"，点击右侧角色加入或替换。",false);};
     var context=new ContextMenu();var remove=new MenuItem{Header="移除此位置角色",IsEnabled=c!=null};remove.Click+=(s,e)=>Commit(()=>t.Slots[slot]=null,"已移除角色，次数已返还。");context.Items.Add(remove);button.ContextMenu=context;
     // The close control sits above the slot button, so removal never selects a new target.
     var holder=new Grid();holder.Children.Add(button);if(c!=null){var rm=Btn("×",()=>Commit(()=>t.Slots[slot]=null,"已移除角色，次数已返还。"));rm.Padding=new Thickness(5,0,5,1);rm.Margin=new Thickness(0,4,10,0);rm.VerticalAlignment=VerticalAlignment.Top;rm.HorizontalAlignment=HorizontalAlignment.Right;rm.Background=B("#C0101922");rm.ToolTip="移除 "+c.Name;holder.Children.Add(rm);}slots.Children.Add(holder);
    }stack.Children.Add(slots);var outer=Box(stack,active?"#1B2C31":"#121D27",10);outer.BorderBrush=B(active?"#446C5C":"#263541");outer.BorderThickness=new Thickness(1);outer.Margin=new Thickness(0,0,4,10);teamList.Children.Add(outer);
   }teamScroll.ScrollToVerticalOffset(scroll);
  }
  Profile GetProfile(string id){Profile p;return model.State.Profiles.TryGetValue(id,out p)?p:new Profile();}
  void RefreshPool(){
   if(pool==null)return;double scroll=poolScroll.VerticalOffset;pool.Children.Clear();poolCount.Text=model.State.Owned.Count+" / "+Data.ActiveCatalog(model.State).Count()+" 已持有";
   targetLabel.Text=managing?"持有管理 · 点击勾选，右键编辑资料":"加入「"+model.State.Teams[activeTeam].Name+"」· 位置 "+(activeSlot+1)+"   /   右键编辑资料";
   var chars=Data.ActiveCatalog(model.State).Where(c=>(managing||model.State.Owned.Contains(c.Id))&&(element=="全部"||element==c.Element));
   if(query.Length>0)chars=chars.Where(c=>c.Name.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0||c.Weapon.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0||GetProfile(c.Id).Note.IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0);
   if(availableOnly&&!managing)chars=chars.Where(c=>model.Check(c.Id,activeTeam,activeSlot)==null);
   chars=chars.OrderByDescending(c=>Data.UseLimit(model.State,c)==2).ThenByDescending(c=>c.Rarity).ThenBy(c=>int.Parse(c.Id));int count=0;
   foreach(var c in chars){count++;var ch=c;bool owned=model.State.Owned.Contains(c.Id);int limit=Data.UseLimit(model.State,c);int remaining=Math.Max(0,limit-model.Used(c));bool enabled=model.Check(c.Id,activeTeam,activeSlot)==null;
    var stack=new StackPanel();var picture=PortraitPanel(c,104);var tint=new Border{Background=new LinearGradientBrush(Colors.Transparent,Color.FromArgb(180,12,21,29),90),VerticalAlignment=VerticalAlignment.Bottom,Height=40};picture.Children.Add(tint);
    var badge=Text(managing?(owned?"✓ 已持有":"＋ 未持有"):(remaining+" / "+limit),10,managing?(owned?"#AAEFC6":"#C8D1D9"):(remaining>0?"#AAEFC6":"#8493A0"));var badgeBox=Box(badge,"#DF12221C",4);badgeBox.Padding=new Thickness(6,3,6,3);badgeBox.HorizontalAlignment=HorizontalAlignment.Right;badgeBox.VerticalAlignment=VerticalAlignment.Top;badgeBox.Margin=new Thickness(0,5,5,0);picture.Children.Add(badgeBox);
    var el=Text(c.Element,9,ElColor(c.Element));el.VerticalAlignment=VerticalAlignment.Bottom;el.Margin=new Thickness(7,0,0,6);picture.Children.Add(el);if(limit==2){var special=Text(c.Limit==1?"特殊 2 次":"可用 2 次",9,"#E9D6A3");special.VerticalAlignment=VerticalAlignment.Bottom;special.HorizontalAlignment=HorizontalAlignment.Right;special.Margin=new Thickness(0,0,6,6);picture.Children.Add(special);}stack.Children.Add(picture);
    var name=Text(c.Name,12);name.TextTrimming=TextTrimming.CharacterEllipsis;name.Margin=new Thickness(8,7,4,2);stack.Children.Add(name);var profile=GetProfile(c.Id);var info=Text("Lv."+profile.Level+"  ·  "+profile.Chain+" 链  ·  "+c.Weapon,9,"#8DA1B1");info.Margin=new Thickness(8,0,3,7);stack.Children.Add(info);
    var card=new Button{Content=stack,HorizontalContentAlignment=HorizontalAlignment.Stretch,Width=112,Padding=new Thickness(0),Margin=new Thickness(0,0,9,10),Background=B("#18242F"),BorderBrush=B(managing&&owned?"#779F89":c.Rarity==5?"#605442":"#484362"),Opacity=managing?(owned?1:0.50):(enabled?1:0.43),ToolTip=c.Name+" · "+c.Element+" / "+c.Weapon+"\n剩余 "+remaining+" / "+limit+" 次\n"+(managing?"点击切换持有状态":"点击加入选中位置")+"；右键编辑角色资料"+(profile.Note.Length>0?"\n备注："+profile.Note:"")};AutomationProperties.SetName(card,c.Name+(managing?(owned?" 已持有":" 未持有"):" 剩余 "+remaining+" 次"));card.Click+=(s,e)=>{if(managing)ToggleOwned(ch);else AddCharacter(ch);};
    var ctx=new ContextMenu();var edit=new MenuItem{Header="编辑等级 / 共鸣链 / 备注"};edit.Click+=(s,e)=>EditProfile(ch);ctx.Items.Add(edit);var own=new MenuItem{Header=owned?"设为未持有（同时移出队伍）":"设为已持有"};own.Click+=(s,e)=>ToggleOwned(ch);ctx.Items.Add(own);card.ContextMenu=ctx;pool.Children.Add(card);
   }
   if(count==0){var empty=new StackPanel{Margin=new Thickness(16,60,0,0)};empty.Children.Add(Text(model.State.Owned.Count==0&&!managing?"还没有添加持有角色":"没有符合条件的角色",19,"#C1D3DF"));var desc=Text(model.State.Owned.Count==0&&!managing?"点击右上角「管理持有」，勾选你的共鸣者。":"试试其他属性，或清空搜索内容。",12,"#8DA1B1");desc.Margin=new Thickness(0,10,0,0);empty.Children.Add(desc);pool.Children.Add(empty);}poolScroll.ScrollToVerticalOffset(scroll);
  }
  void AddCharacter(Character c){var err=model.Check(c.Id,activeTeam,activeSlot);if(err!=null){Say(err,true);return;}if(model.State.Teams[activeTeam].Slots[activeSlot]==c.Id){Say("该角色已在选中位置",false);return;}
   string teamName=model.State.Teams[activeTeam].Name;Commit(()=>{model.Assign(c.Id,activeTeam,activeSlot);for(int k=1;k<=3;k++){int j=(activeSlot+k)%3;if(model.State.Teams[activeTeam].Slots[j]==null){activeSlot=j;break;}}},"已将 "+c.Name+" 加入「"+teamName+"」 · 自动保存");
  }
  void ToggleOwned(Character c){Commit(()=>{if(model.State.Owned.Contains(c.Id)){model.State.Owned.Remove(c.Id);foreach(var t in model.State.Teams)for(int j=0;j<3;j++)if(t.Slots[j]==c.Id)t.Slots[j]=null;}else model.State.Owned.Add(c.Id);},"已更新 "+c.Name+" 的持有状态；可按 Ctrl+Z 撤销。");}
  void SetAllOwned(bool value){Commit(()=>{if(value)model.State.Owned=Data.ActiveCatalog(model.State).Select(c=>c.Id).ToList();else {model.State.Owned.Clear();foreach(var t in model.State.Teams)for(int j=0;j<3;j++)t.Slots[j]=null;}},value?"已勾选全部角色（漂泊者仅保留所选属性）":"已取消全部持有并清空编队；可按 Ctrl+Z 撤销。");}
  void EditRover(){
   var dialog=new Window{Title="漂泊者设置",Width=490,Height=530,ResizeMode=ResizeMode.NoResize,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=B("#17212B"),Foreground=B("#E8EDF2"),FontFamily=FontFamily,Resources=Resources};
   var body=new StackPanel{Margin=new Thickness(26)};body.Children.Add(Text("漂泊者设置",22));var explanation=Text("选择你在游戏中使用的性别和当前属性。",12,"#9EB1C0");explanation.Margin=new Thickness(0,8,0,20);body.Children.Add(explanation);
   var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(145)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(24)});row.ColumnDefinitions.Add(new ColumnDefinition());
   var preview=new Border{CornerRadius=new CornerRadius(10),ClipToBounds=true};row.Children.Add(preview);
   var fields=new StackPanel();fields.Children.Add(Text("性别",12,"#9EB1C0"));var gender=new ComboBox{ItemsSource=new[]{"男","女"},SelectedItem=model.State.RoverGender,Margin=new Thickness(0,7,0,14)};AutomationProperties.SetName(gender,"漂泊者性别");fields.Children.Add(gender);
   fields.Children.Add(Text("当前属性",12,"#9EB1C0"));var attribute=new ComboBox{ItemsSource=Data.Catalog.Where(Data.IsRover).Select(c=>c.Element).ToArray(),SelectedItem=model.State.RoverElement,Margin=new Thickness(0,7,0,0)};AutomationProperties.SetName(attribute,"漂泊者属性");fields.Children.Add(attribute);Grid.SetColumn(fields,2);row.Children.Add(fields);body.Children.Add(row);
   Action refreshPreview=()=>{if(gender.SelectedItem!=null&&attribute.SelectedItem!=null)preview.Child=PortraitPanel(Data.RoverFor((string)attribute.SelectedItem),150,(string)gender.SelectedItem);};gender.SelectionChanged+=(s,e)=>refreshPreview();attribute.SelectionChanged+=(s,e)=>refreshPreview();refreshPreview();
   var note=Text("角色池只保留所选形态。切换属性会同步替换队伍中的\n漂泊者，保留所在位置与持有状态；总出场次数按当前规则计算（默认 1 次）。",12,"#9EB1C0");note.LineHeight=22;note.Margin=new Thickness(0,18,0,16);body.Children.Add(note);
   var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};actions.Children.Add(Btn("取消",()=>dialog.Close()));var apply=Btn("保存设置",()=>{
    string sex=(string)gender.SelectedItem,el=(string)attribute.SelectedItem;if(sex==model.State.RoverGender&&el==model.State.RoverElement){dialog.Close();return;}
    Commit(()=>model.SetRover(sex,el),"漂泊者已设为「"+sex+" · "+el+"」，队伍与头像已同步；可按 Ctrl+Z 撤销。");dialog.Close();
   },true);actions.Children.Add(apply);body.Children.Add(actions);dialog.Content=body;dialog.ShowDialog();
  }
  void EditExtraUse(){
   var dialog=new Window{Title="特殊角色设置",Width=490,Height=460,Owner=this,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=B("#17212B"),Foreground=B("#E8EDF2"),FontFamily=FontFamily,Resources=Resources};var body=new StackPanel{Margin=new Thickness(25)};body.Children.Add(Text("额外二次出场角色",21));var description=Text("在固定六名二次角色之外，再指定一名原本限用一次的角色。",12,"#9EB1C0");description.TextWrapping=TextWrapping.Wrap;description.Margin=new Thickness(0,12,0,18);body.Children.Add(description);
   var choices=new List<ExtraUseChoice>{new ExtraUseChoice{Name="不额外设置角色",Id=null}};choices.AddRange(Data.ActiveCatalog(model.State).Where(c=>c.Limit==1).OrderByDescending(c=>model.State.Owned.Contains(c.Id)).ThenBy(c=>c.Name).Select(c=>new ExtraUseChoice{Id=c.Id,Name=c.Name+(model.State.Owned.Contains(c.Id)?"":"（未持有）")}));var choice=new ComboBox{ItemsSource=choices,DisplayMemberPath="Name",SelectedItem=choices.First(x=>x.Id==model.State.ExtraUseCharacterId),MaxDropDownHeight=330,IsTextSearchEnabled=true};AutomationProperties.SetName(choice,"额外二次出场角色");body.Children.Add(choice);
   var note=Text("选择后，该角色可跨队使用 2 次，同队仍禁止重复。\n不会改变角色持有状态。切换或取消时，若旧角色已用两次，请先移除一处。",12,"#9EB1C0");note.TextWrapping=TextWrapping.Wrap;note.LineHeight=22;note.Margin=new Thickness(0,18,0,20);body.Children.Add(note);var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};actions.Children.Add(Btn("取消",()=>dialog.Close()));actions.Children.Add(Btn("保存设置",()=>{var selected=(ExtraUseChoice)choice.SelectedItem;if(selected.Id==model.State.ExtraUseCharacterId){dialog.Close();return;}try{Commit(()=>model.SetExtraUse(selected.Id),selected.Id==null?"已取消额外二次出场角色；可按 Ctrl+Z 撤销。":"已将 "+Data.Find(selected.Id).Name+" 设为特殊角色，可使用 2 次；一键推荐同步生效。");dialog.Close();}catch(Exception ex){MessageBox.Show(dialog,ex.Message,"无法更改特殊角色",MessageBoxButton.OK,MessageBoxImage.Information);}},true));body.Children.Add(actions);dialog.Content=body;dialog.ShowDialog();
  }
  void ImportScreenshots(){
   BitmapSource image;string message;ClipboardScreenshots.TryRead(out image,out message);ShowScreenshotRecognition(image,image==null?message:null);
  }
  void ShowScreenshotRecognition(BitmapSource image,string message=null){
   var dialog=new ScreenshotImportWindow(this,Data.Parse(Data.Serialize(model.State)),new string[0],MergeScreenshotResults,image);dialog.InputMessage(message);dialog.ShowDialog();
  }
  internal void MergeScreenshotResults(ScreenshotSelection selection){
   var before=Data.Serialize(model.State);var next=new Model(Data.Parse(before));int added=next.MergeRecognized(selection.Ids,selection.RoverGender,selection.RoverElement);Data.Validate(next.State);
   if(Data.Serialize(next.State)==before){Say("已勾选的角色均已持有，漂泊者设置也一致，无需重复修改。",false);return;}
   bool changed=next.State.RoverGender!=model.State.RoverGender||next.State.RoverElement!=model.State.RoverElement;Commit(()=>{model.State=next.State;},"截图结果已合并：新增 "+added+" 名持有角色"+(changed?"；漂泊者已按截图改为 "+next.State.RoverGender+" · "+next.State.RoverElement:"，已持有角色自动去重")+"；Ctrl+Z 可一次撤销。");
  }
  void ClearAllTeams(){if(!model.State.Teams.Any(t=>t.Slots.Any(id=>id!=null)))return;Commit(()=>{model.ClearTeams();activeSlot=0;},"已清空全部配队，所有角色次数已返还；按 Ctrl+Z 可一次恢复。");}
  async void RecommendTeams(bool replace){
   if(recommending)return;if(model.State.Owned.Count<3){Say("请先勾选或粘贴识别至少 3 名持有角色，再推荐配队。",true);return;}
   recommending=true;recommendButton.IsEnabled=false;string before=Data.Serialize(model.State);Say("正在参考官方攻略站收录搭配，为持有角色安排队伍…",false);
   try{
    var snapshot=Data.Parse(before);var result=await System.Threading.Tasks.Task.Run(()=>TeamRecommendations.Generate(snapshot,replace));
    if(Data.Serialize(model.State)!=before){Say("角色或队伍在计算期间已改变，请重新点击推荐。",true);return;}
    if(result.EmptyTeams==0){Say("当前没有空队伍。可清空部分队伍，或右键「一键推荐」选择重新规划全部。",false);return;}
    if(result.Teams.Count==0){Say("现有持有角色与剩余次数暂时无法组成攻略库中的完整搭配。可右键查看攻略来源，或继续手动配队。",false);return;}
    var next=TeamRecommendations.Apply(snapshot,result,replace);Commit(()=>{model.State=next;managing=false;},"已按限定五星优先和主力首发时间推荐 "+result.Teams.Count+" 队"+(replace?"（重新规划全部）":"（填充空队，保留已有配队）")+"；队伍中的「攻略」可查看来源，Ctrl+Z 撤销。"+(result.Teams.Count<result.EmptyTeams?"其余队伍暂留空。":""));
   }catch(Exception ex){Say("推荐未完成："+ex.Message,true);}finally{recommending=false;recommendButton.IsEnabled=true;}
  }
  void ShowRecommendationSources(){new RecommendationSourcesWindow(this,Data.Parse(Data.Serialize(model.State))).ShowDialog();}
  void AddTeam(){if(model.State.Teams.Count>=60){Say("最多支持 60 支队伍",true);return;}Commit(()=>{model.State.Teams.Add(new Team{Name="第 "+(model.State.Teams.Count+1)+" 队"});activeTeam=model.State.Teams.Count-1;activeSlot=0;},"已新增队伍");teamScroll.ScrollToEnd();}
  void EditProfile(Character c){
   var p=GetProfile(c.Id);var dialog=new Window{Title=c.Name+" · 角色资料",Width=400,Height=390,ResizeMode=ResizeMode.NoResize,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=B("#17212B"),Foreground=B("#E8EDF2"),FontFamily=FontFamily,Resources=Resources};var body=new StackPanel{Margin=new Thickness(26)};body.Children.Add(Text(c.Name+"  /  "+c.Element+"  /  "+c.Weapon,17));var label=Text("等级（1～90）",12,"#9EB1C0");label.Margin=new Thickness(0,18,0,7);body.Children.Add(label);var level=new TextBox{Text=p.Level.ToString()};body.Children.Add(level);label=Text("共鸣链（0～6）",12,"#9EB1C0");label.Margin=new Thickness(0,12,0,7);body.Children.Add(label);var chain=new TextBox{Text=p.Chain.ToString()};body.Children.Add(chain);label=Text("备注（最多 200 字）",12,"#9EB1C0");label.Margin=new Thickness(0,12,0,7);body.Children.Add(label);var note=new TextBox{Text=p.Note,MaxLength=200};body.Children.Add(note);var save=Btn("保存资料",()=>{int lv,ch;if(!int.TryParse(level.Text,out lv)||lv<1||lv>90||!int.TryParse(chain.Text,out ch)||ch<0||ch>6){MessageBox.Show(dialog,"等级需要是 1～90 的整数，共鸣链需要是 0～6 的整数。","请检查输入");return;}Commit(()=>model.State.Profiles[c.Id]=new Profile{Level=lv,Chain=ch,Note=note.Text},"角色资料已保存");dialog.Close();},true);save.Margin=new Thickness(0,18,0,0);body.Children.Add(save);dialog.Content=body;dialog.ShowDialog();
  }
  void Import(){var d=new OpenFileDialog{Title="导入配队方案",Filter="配队方案 (*.json)|*.json",CheckFileExists=true};if(d.ShowDialog(this)!=true)return;try{if(new FileInfo(d.FileName).Length>4000000)throw new Exception("方案文件过大。");var plan=Data.Parse(File.ReadAllText(d.FileName,Encoding.UTF8));Commit(()=>{model.State=plan;activeTeam=0;activeSlot=0;managing=false;storageReady=true;},"方案已导入并保存；可按 Ctrl+Z 返回导入前。");}catch(Exception ex){MessageBox.Show(this,"导入失败："+ex.Message+"\n当前方案保持不变。","无法导入",MessageBoxButton.OK,MessageBoxImage.Warning);}}
  void ExportJson(){var d=new SaveFileDialog{Title="导出配队方案",Filter="配队方案 (*.json)|*.json",FileName="终焉矩阵配队_"+DateTime.Now.ToString("yyyyMMdd")+".json"};if(d.ShowDialog(this)!=true)return;try{File.WriteAllText(d.FileName,Data.Serialize(model.State),new UTF8Encoding(false));Say("方案已导出："+d.FileName,false);}catch(Exception ex){Say("导出失败："+ex.Message,true);}}
  void CopyTeams(){try{var b=new StringBuilder("鸣潮 · 终焉矩阵\n");foreach(var t in model.State.Teams)b.AppendLine(t.Name+"："+string.Join(" / ",t.Slots.Select(id=>Data.Find(id)==null?"空位":Data.Find(id).Name)));Clipboard.SetText(b.ToString());Say("配队文本已复制",false);}catch(Exception ex){Say("复制失败："+ex.Message,true);}}
  void ExportImage(){var d=new SaveFileDialog{Title="导出队伍图片",Filter="PNG 图片 (*.png)|*.png",FileName="终焉矩阵配队_"+DateTime.Now.ToString("yyyyMMdd")+".png"};if(d.ShowDialog(this)!=true)return;try{
   var page=new StackPanel{Width=720,Background=B("#0E141B")};var banner=new StackPanel{Margin=new Thickness(32,24,32,16)};banner.Children.Add(Text("终焉矩阵 / 作战编队",28));banner.Children.Add(Text("WUTHERING WAVES   ·   "+DateTime.Now.ToString("yyyy.MM.dd")+"   /   漂泊者："+model.State.RoverGender+" · "+model.State.RoverElement+(model.State.ExtraUseCharacterId==null?"":"   /   额外二次："+Data.Find(model.State.ExtraUseCharacterId).Name),11,"#9CB4A5"));page.Children.Add(banner);
   int n=0;foreach(var t in model.State.Teams){n++;var row=new StackPanel{Margin=new Thickness(32,6,32,12)};row.Children.Add(Text(n.ToString("00")+"  "+t.Name,17));var slots=new System.Windows.Controls.Primitives.UniformGrid{Columns=3,Margin=new Thickness(0,10,0,0)};foreach(var id in t.Slots){var c=Data.Find(id);var col=new StackPanel{Margin=new Thickness(0,0,10,0)};if(c!=null){col.Children.Add(PortraitPanel(c,136));col.Children.Add(Text(c.Name+" · "+c.Element,13));}else {var z=Text("空位",16,"#647987");z.Height=136;col.Children.Add(z);}slots.Children.Add(col);}row.Children.Add(slots);page.Children.Add(row);}
   var foot=Text("角色图源：encore.moe  ·  非官方配队工具",10,"#799080");foot.Margin=new Thickness(32,6,32,24);page.Children.Add(foot);page.Measure(new Size(720,double.PositiveInfinity));page.Arrange(new Rect(page.DesiredSize));page.UpdateLayout();var bmp=new RenderTargetBitmap(720,(int)Math.Ceiling(page.ActualHeight),96,96,PixelFormats.Pbgra32);bmp.Render(page);var enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(bmp));using(var s=File.Create(d.FileName))enc.Save(s);Say("队伍图片已导出："+d.FileName,false);
   }catch(Exception ex){Say("图片导出失败："+ex.Message,true);}}
  void Help(){MessageBox.Show(this,"1. 在「管理持有」中勾选拥有的角色，点击「完成管理」。\n2. 点击左侧队伍位置，再点击右侧角色加入或替换。\n3. 位置右上角 × 可移除角色；队伍可新增、改名、清空、删除；左侧顶部「清空全部」可一键移除全部队伍角色并返还次数，Ctrl+Z 一次恢复。\n4. 右键角色卡片，编辑等级、共鸣链和备注。\n5. 普通角色全局限 1 次（可在「特殊角色设置」额外指定一名角色使用 2 次）；白芷、维里奈、卜灵、守岸人、莫宁、穗穗限 2 次。同队禁止重复，漂泊者限用所选性别和属性的唯一形态，默认 1 次；设为特殊角色后可用 2 次。\n6. 每次修改自动保存；Ctrl+Z 撤销，Ctrl+S 保存。支持导入/导出 JSON、导出 PNG 和复制文本。\n7. 复制角色截图后按 Ctrl+V，或点击「粘贴截图识别」，会自动识别；可连续粘贴多张。核对结果后合并持有，必要时调整区域和列/行。\n\n数据与头像： https://encore.moe/character\n内置名单为制作时站点公开名单；点击「漂泊者设置」选择性别与属性，角色池仅保留所选形态；切换时同步更新队伍和头像。角色默认等级仅作记录。\n一键推荐参考《鸣潮》官方攻略站收录的玩家攻略：默认仅填空队，右键可重新规划全部。推荐不是伤害计算或官方强度排名，队伍「攻略」按钮可查看原文。\n\n存档位置：\n"+dataPath,"使用说明",MessageBoxButton.OK,MessageBoxImage.Information);}
 }
 public static class Program {
  [STAThread] public static int Main(string[] args){
   try{Data.Load();if(args.Length==2&&args[0]=="--recommend-test"){try{var plan=Data.Fresh();plan.Owned=Data.ActiveCatalog(plan).Select(c=>c.Id).ToList();var watch=System.Diagnostics.Stopwatch.StartNew();var result=TeamRecommendations.Generate(plan,false);var applied=TeamRecommendations.Apply(plan,result,false);File.WriteAllText(args[1],Data.Json.Serialize(new {ElapsedMs=watch.ElapsedMilliseconds,Count=result.Teams.Count,result.AvailableRecipes,result.SearchLimited,Sources=TeamRecommendations.Database.Sources.Count,Recipes=TeamRecommendations.Database.Recipes.Count,Teams=applied.Teams.Select(t=>new {t.Name,Members=t.Slots.Select(id=>Data.Find(id)==null?null:Data.Find(id).Name).ToArray(),t.RecommendationId})}),Encoding.UTF8);return 0;}catch(Exception ex){File.WriteAllText(args[1],"FAILED\n"+ex,Encoding.UTF8);return 1;}}if(args.Length==2&&args[0]=="--clipboard-probe"){try{BitmapSource image;string message;bool ok=ClipboardScreenshots.TryRead(out image,out message);var native=NativeClipboardImages.TryRead();var data=Clipboard.GetDataObject();var raw=data==null?null:data.GetData(DataFormats.Bitmap) as BitmapSource;File.WriteAllText(args[1],Data.Json.Serialize(new {ReadSucceeded=ok,Width=image==null?0:image.PixelWidth,Height=image==null?0:image.PixelHeight,Format=image==null?null:image.Format.ToString(),Visible=image!=null&&ClipboardScreenshots.HasColorInformation(image),NativeAvailable=native!=null,NativeWidth=native==null?0:native.PixelWidth,NativeHeight=native==null?0:native.PixelHeight,ManagedFormat=raw==null?null:raw.Format.ToString(),ManagedAlpha=ClipboardBridgeTests.AlphaSummary(raw),Formats=data==null?new string[0]:data.GetFormats(false)}),Encoding.UTF8);return 0;}catch(Exception ex){File.WriteAllText(args[1],"FAILED: "+ex.GetType().Name,Encoding.UTF8);return 1;}}if(args.Length==3&&args[0]=="--clipboard-bridge-test"){try{ClipboardBridgeTests.Run(args[1],args[2]);return 0;}catch(Exception ex){File.WriteAllText(args[2],"FAILED\n"+ex,Encoding.UTF8);return 1;}}if(args.Length>=3&&args[0]=="--recognize-test"){using(var image=new System.Drawing.Bitmap(args[1])){var hits=new PortraitRecognizer(Data.Fresh()).Recognize(args[1],new System.Drawing.Rectangle(0,0,image.Width,image.Height),args.Length>=5?int.Parse(args[3]):3,args.Length>=5?int.Parse(args[4]):4);File.WriteAllText(args[2],Data.Json.Serialize(hits.Select(h=>new {h.Row,h.Column,h.Similarity,h.Margin,h.Reliable,h.DetectedElement,h.ElementSimilarity,h.ElementMargin,Gender=h.Candidates[0].Gender,Name=Data.Find(h.Candidates[0].Id).Name,Alternatives=h.Candidates.Select(c=>new {Name=Data.Find(c.Id).Name,c.Error}),h.Bounds})),Encoding.UTF8);}return 0;}if(args.Length>0&&args[0]=="--self-test"){SelfTest(args.Length>1?args[1]:"self-test.txt");return 0;}var app=new Application();if(args.Length==3&&args[0]=="--screenshot-session-test"){app.ShutdownMode=ShutdownMode.OnExplicitShutdown;var main=new MainWindow(Path.Combine(Path.GetDirectoryName(args[2]),"session-test-data"));main.ShowInTaskbar=false;main.Loaded+=async (sender,eventArgs)=>{main.Hide();try{await ScreenshotSessionTests.Run(main,args[1],args[2]);}catch(Exception ex){File.WriteAllText(args[2],"FAILED\n"+ex,Encoding.UTF8);}finally{main.Close();app.Shutdown();}};app.Run(main);return 0;}if(args.Length>=3&&args[0]=="--paste-ui-test"){BitmapSource image;using(var stream=File.OpenRead(args[1]))image=ClipboardScreenshots.Decode(stream);if(args.Contains("--zero-alpha")){var opaque=ClipboardScreenshots.Opaque(image);var bytes=new byte[opaque.PixelWidth*opaque.PixelHeight*4];opaque.CopyPixels(bytes,opaque.PixelWidth*4,0);for(int i=3;i<bytes.Length;i+=4)bytes[i]=0;image=BitmapSource.Create(opaque.PixelWidth,opaque.PixelHeight,96,96,PixelFormats.Bgra32,null,bytes,opaque.PixelWidth*4);image.Freeze();}var main=new MainWindow(args[2]);main.Loaded+=(sender,eventArgs)=>{var dialog=new ScreenshotImportWindow(main,Data.Fresh(),new string[0],main.MergeScreenshotResults,image);if(args.Length>3&&args[3]=="--queue")dialog.Loaded+=(ss,ee)=>dialog.AddPastedImage(image);dialog.ShowDialog();};app.Run(main);return 0;}if(args.Length==3&&args[0]=="--screenshot-ui-test"){var main=new MainWindow(args[2]);main.Loaded+=(sender,eventArgs)=>new ScreenshotImportWindow(main,Data.Fresh(),new[]{args[1]},main.MergeScreenshotResults).ShowDialog();app.Run(main);return 0;}app.DispatcherUnhandledException+=(s,e)=>{MessageBox.Show("操作未完成："+e.Exception.Message,"鸣潮配队工具");e.Handled=true;};app.Run(new MainWindow(args.Length==2&&args[0]=="--data-dir"?args[1]:null));return 0;}
   catch(Exception ex){if(args.Length>0&&args[0]=="--self-test"){File.WriteAllText(args.Length>1?args[1]:"self-test.txt","FAILED\n"+ex,Encoding.UTF8);return 1;}MessageBox.Show("程序启动失败："+ex.Message,"鸣潮配队工具");return 1;}
  }
  static void SelfTest(string output){
   int count=0;Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);count++;};
   foreach(var name in Character.Special){var c=Data.Catalog.Single(x=>x.Name==name);var p=Data.Fresh();p.Owned.Add(c.Id);var m=new Model(p);check(m.Assign(c.Id,0,0)==null,name+" first");check(m.Assign(c.Id,0,1)!=null,name+" same team denied");check(m.Assign(c.Id,1,0)==null,name+" second");check(m.Assign(c.Id,2,0)!=null,name+" third denied");p.Teams[0].Slots[0]=null;check(m.Assign(c.Id,2,0)==null,name+" removal returns use");check(m.Assign(c.Id,1,0)==null,name+" same-slot replacement");Data.Validate(p);}
   var normal=Data.Catalog.First(c=>c.Limit==1&&!c.Name.StartsWith("漂泊者"));var state=Data.Fresh();var model=new Model(state);check(model.Assign(normal.Id,0,0)!=null,"unowned denied");state.Owned.Add(normal.Id);check(model.Assign(normal.Id,0,0)==null,"normal first");check(model.Assign(normal.Id,1,0)!=null,"normal second denied");check(model.Assign(normal.Id,0,0)==null,"normal same slot allowed");state.Teams[0].Slots[0]=null;check(model.Assign(normal.Id,1,0)==null,"normal removal releases");
   var rovers=Data.Catalog.Where(Data.IsRover).ToList();state=Data.Fresh();var initial=Data.RoverFor(state.RoverElement);state.Owned.Add(initial.Id);model=new Model(state);
   check(Data.ActiveCatalog(state).Count(Data.IsRover)==1,"one Rover in initial roster");check(model.Assign(initial.Id,0,0)==null,"rover first");check(model.Assign(initial.Id,1,0)!=null,"rover shared cap");
   state.Profiles[initial.Id]=new Profile{Level=85,Chain=3,Note="漂泊者资料"};string beforeSwitch=Data.Serialize(state);
   foreach(var sex in new[]{"男","女"})foreach(var rover in rovers){
    model.SetRover(sex,rover.Element);Data.Validate(state);
    check(state.RoverGender==sex&&state.RoverElement==rover.Element,"Rover setting applied "+sex+rover.Element);
    check(state.Teams[0].Slots[0]==rover.Id,"deployed Rover replaced in same slot");
    check(state.Teams.Skip(1).All(t=>t.Slots.All(id=>id==null)),"switch never adds another deployment");
    check(state.Owned.Count(id=>Data.IsRover(Data.Find(id)))==1&&state.Owned.Contains(rover.Id),"Rover ownership migrated");
    check(Data.ActiveCatalog(state).Count(Data.IsRover)==1,"one selectable Rover after switch");
    check(state.Profiles[rover.Id].Level==85&&state.Profiles[rover.Id].Note=="漂泊者资料","Rover profile retained");
    check(model.Assign(rover.Id,1,0)!=null,"Rover switch preserves spent use");
    check(model.Check(rovers.First(x=>x.Id!=rover.Id).Id,0,0)!=null,"nonselected attribute denied");
    var restored=Data.Parse(Data.Serialize(state));check(restored.RoverGender==sex&&restored.RoverElement==rover.Element&&restored.Teams[0].Slots[0]==rover.Id,"Rover settings JSON roundtrip");
    check(Data.PortraitResource(rover,sex)==(sex=="男"?"portrait.1501":"portrait.1604"),"correct gender portrait resource");
   }
   var undoPlan=Data.Parse(beforeSwitch);check(undoPlan.RoverGender=="男"&&undoPlan.RoverElement=="衍射"&&undoPlan.Teams[0].Slots[0]==initial.Id,"undo restores Rover settings and team");
   var prior=Data.Serialize(state);bool rejected=false;try{model.SetRover("无效","气动");}catch{rejected=true;}check(rejected&&Data.Serialize(state)==prior,"invalid gender leaves plan untouched");
   rejected=false;try{model.SetRover("女","无效");}catch{rejected=true;}check(rejected&&Data.Serialize(state)==prior,"invalid attribute leaves plan untouched");
   model.SetRover("女","气动");var air=Data.RoverFor("气动");state.Teams[0].Slots[0]=null;check(model.Assign(air.Id,1,0)==null,"removing switched Rover releases use");
   state.Teams[0].Slots[0]=air.Id;rejected=false;try{Data.Parse(Data.Serialize(state));}catch{rejected=true;}check(rejected,"invalid import duplicate rejected");
   state=Data.Fresh();state.Owned=rovers.Select(c=>c.Id).ToList();state.Version=1;state.Teams[2].Slots[1]=Data.RoverFor("湮灭").Id;state.Profiles[Data.RoverFor("湮灭").Id]=new Profile{Level=77,Chain=2,Note="旧存档"};
   var legacyMap=Data.Json.Deserialize<Dictionary<string,object>>(Data.Serialize(state));legacyMap.Remove("RoverGender");legacyMap.Remove("RoverElement");var migrated=Data.Parse(Data.Json.Serialize(legacyMap));
   check(migrated.Version==2&&migrated.RoverGender=="女"&&migrated.RoverElement=="湮灭","legacy migration chooses deployed Rover and prior portrait gender");
   check(migrated.Owned.Count==1&&migrated.Teams[2].Slots[1]==Data.RoverFor("湮灭").Id,"legacy migration preserves slot and removes extra Rover forms");check(migrated.Profiles[Data.RoverFor("湮灭").Id].Level==77,"legacy profile preserved");
   state.Teams[2].Slots[1]=null;var ownedMigration=Data.Parse(Data.Serialize(state));check(ownedMigration.RoverElement==rovers[0].Element,"legacy migration falls back to first owned form");
   state.Owned.Clear();var emptyMigration=Data.Parse(Data.Serialize(state));check(emptyMigration.RoverGender=="男"&&emptyMigration.RoverElement=="衍射"&&emptyMigration.Owned.Count==0,"empty legacy plan migrated");
   state=Data.Fresh();state.Owned=Data.ActiveCatalog(state).Select(c=>c.Id).ToList();check(state.Owned.Count==57&&state.Owned.Count(id=>Data.IsRover(Data.Find(id)))==1,"select all includes one Rover");Data.Validate(state);
   state.Owned.Add(rovers.First(c=>c.Element!=state.RoverElement).Id);rejected=false;try{Data.Parse(Data.Serialize(state));}catch{rejected=true;}check(rejected,"v2 import with unselected Rover rejected");
   state=Data.Fresh();model=new Model(state);model.SetRover("女","气动");check(state.Owned.Count==0&&state.Teams.All(t=>t.Slots.All(id=>id==null)),"setting unowned Rover does not add ownership or deployment");
   state=Data.Fresh();state.Owned.Add(normal.Id);state.Profiles[normal.Id]=new Profile{Level=80,Chain=2,Note="中文备注与存档测试"};check(Data.Parse(Data.Serialize(state)).Profiles[normal.Id].Note=="中文备注与存档测试","Chinese profile persistence");
   state=Data.Fresh();state.Owned=Data.ActiveCatalog(state).Select(c=>c.Id).ToList();state.RoverGender="女";state.Teams[0].Name="自定义队名";state.Profiles[normal.Id]=new Profile{Level=80,Chain=2,Note="保留资料"};model=new Model(state);
   model.Assign(normal.Id,0,0);var special=Data.Catalog.Single(c=>c.Name=="穗穗");model.Assign(special.Id,0,1);model.Assign(special.Id,2,0);model.Assign(Data.RoverFor(state.RoverElement).Id,3,2);var beforeClear=Data.Serialize(state);var preservedOwned=state.Owned.ToArray();var names=state.Teams.Select(t=>t.Name).ToArray();
   model.ClearTeams();Data.Validate(state);check(state.Teams.All(t=>t.Slots.All(id=>id==null)),"clear all empties every team slot including offscreen teams");
   check(state.Teams.Select(t=>t.Name).SequenceEqual(names),"clear all keeps team count and names");check(state.Owned.SequenceEqual(preservedOwned),"clear all keeps ownership");
   check(state.Profiles[normal.Id].Note=="保留资料"&&state.RoverGender=="女"&&state.RoverElement=="衍射","clear all keeps profiles and Rover settings");
   check(model.Used(normal)==0&&model.Used(special)==0&&model.Used(Data.RoverFor(state.RoverElement))==0,"clear all releases ordinary special and Rover usage");
   check(Data.Parse(Data.Serialize(state)).Teams.All(t=>t.Slots.All(id=>id==null)),"cleared plan survives serialization");model.State=Data.Parse(beforeClear);check(Data.Serialize(model.State)==beforeClear,"single undo snapshot restores full cleared plan");
   model.ClearTeams();var once=Data.Serialize(model.State);model.ClearTeams();check(Data.Serialize(model.State)==once,"clearing an empty plan is idempotent");
   state=Data.Fresh();state.Owned.Add(normal.Id);state.Profiles[normal.Id]=new Profile{Level=66,Chain=4,Note="截图合并不改资料"};state.Teams[0].Slots[0]=normal.Id;state.RoverGender="女";state.RoverElement="气动";model=new Model(state);var originalTeam=Data.Json.Serialize(state.Teams);var originalProfiles=Data.Json.Serialize(state.Profiles);var mergeUndo=Data.Serialize(state);
   int newlyAdded=model.MergeRecognized(new[]{normal.Id,special.Id,special.Id,Data.RoverFor("衍射").Id});check(newlyAdded==2,"screenshot merge adds only new distinct characters");
   check(state.Owned.Contains(Data.RoverFor("气动").Id)&&!state.Owned.Contains(Data.RoverFor("衍射").Id)&&state.RoverGender=="女","screenshot Rover follows player settings");
   check(Data.Json.Serialize(state.Teams)==originalTeam&&Data.Json.Serialize(state.Profiles)==originalProfiles,"screenshot merge preserves teams and profiles");
   check(model.MergeRecognized(new[]{special.Id,special.Id})==0,"repeated screenshots do not duplicate ownership");var beforeInvalid=Data.Serialize(state);rejected=false;try{model.MergeRecognized(new[]{"not-a-character",Data.Catalog.Last().Id});}catch{rejected=true;}check(rejected&&Data.Serialize(state)==beforeInvalid,"invalid screenshot import is atomic");Data.Validate(state);
   check(Data.Parse(mergeUndo).Owned.SequenceEqual(new[]{normal.Id}),"screenshot merge can be undone in one snapshot");
   var pixels=new byte[24*32*4];for(int i=0;i<pixels.Length;i+=4){pixels[i]=42;pixels[i+1]=100;pixels[i+2]=180;pixels[i+3]=255;}var clipboardBitmap=BitmapSource.Create(24,32,96,96,PixelFormats.Bgra32,null,pixels,24*4);clipboardBitmap.Freeze();var png=ClipboardScreenshots.Encode(clipboardBitmap);BitmapSource pasted;string clipboardMessage;
   var clipboardData=new DataObject();clipboardData.SetData("PNG",new MemoryStream(png));check(ClipboardScreenshots.TryRead(clipboardData,out pasted,out clipboardMessage)&&pasted.PixelWidth==24&&pasted.PixelHeight==32&&pasted.IsFrozen,"clipboard PNG stream decoded entirely in memory");
   clipboardData=new DataObject();clipboardData.SetData("PNG",png);check(ClipboardScreenshots.TryRead(clipboardData,out pasted,out clipboardMessage)&&pasted.PixelWidth==24,"clipboard PNG bytes supported");
   clipboardData=new DataObject();clipboardData.SetImage(clipboardBitmap);check(ClipboardScreenshots.TryRead(clipboardData,out pasted,out clipboardMessage)&&pasted.PixelHeight==32,"standard clipboard Bitmap supported");
   clipboardData=new DataObject();clipboardData.SetData("PNG",new byte[]{0,1,2});clipboardData.SetImage(clipboardBitmap);check(ClipboardScreenshots.TryRead(clipboardData,out pasted,out clipboardMessage),"invalid clipboard PNG falls back to Bitmap");
   clipboardData=new DataObject();clipboardData.SetText("普通文本");check(!ClipboardScreenshots.TryRead(clipboardData,out pasted,out clipboardMessage)&&pasted==null&&clipboardMessage.Contains("没有图片"),"text clipboard reports no image and preserves native text paste");
   check(!ClipboardScreenshots.TryRead((IDataObject)null,out pasted,out clipboardMessage),"empty clipboard handled");
   TeamRecommendationTests.Run(check);
   RecommendationRuleTests.Run(check);
   RoverAndExtraUseTests.Run(check);
   ClipboardBridgeTests.Unit(check);
   foreach(var variant in PortraitRecognizer.Variants()){check(Data.Find(variant.Id)!=null,"skin template maps to existing character");using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(variant.Resource)){check(stream!=null,"skin template embedded");var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();check(bitmap.PixelWidth>0,"skin template decodes");}}
   foreach(var c in Data.Catalog){using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("portrait."+c.Id)){check(s!=null,c.Name+" image bundled");if(s!=null){var bm=new BitmapImage();bm.BeginInit();bm.CacheOption=BitmapCacheOption.OnLoad;bm.StreamSource=s;bm.EndInit();check(bm.PixelWidth>0,c.Name+" image decodes");}}}
   File.WriteAllText(output,"PASS: "+count+" assertions\nCharacters: "+Data.Catalog.Count+"\nSix special limits, normal cap, same-team duplicates, ownership, slot replacement, released uses, single Rover form, both gender portraits, every attribute switch, team slot and ownership migration, profile retention, undo, legacy plan migration, JSON roundtrip, invalid import rejection, profile persistence, clear all teams, retained ownership and profiles, released uses, bulk-clear undo, screenshot merge deduplication and preservation, in-memory clipboard PNG and Bitmap decoding, invalid clipboard fallback, text and empty clipboard handling, every bundled portrait decoded.\n",Encoding.UTF8);
  }
 }
}
