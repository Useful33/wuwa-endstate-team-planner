using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using DrawingRectangle=System.Drawing.Rectangle;

namespace EndstateMatrix {
 public sealed class ScreenshotImportWindow:Window {
  sealed class ReviewItem {public string File;public RecognitionHit Hit;public string Id,Gender;public bool Selected;}
  readonly Plan plan;readonly List<string> files=new List<string>();readonly List<ReviewItem> reviews=new List<ReviewItem>();
  readonly Action<ScreenshotSelection> merge;PortraitRecognizer recognizer;
  readonly Dictionary<string,BitmapSource> memoryImages=new Dictionary<string,BitmapSource>();readonly Dictionary<string,string> names=new Dictionary<string,string>();readonly Queue<BitmapSource> pendingPastes=new Queue<BitmapSource>();int pasteCount,sessionRevision;CancellationTokenSource recognitionJob;
  Image preview;Canvas overlay;Grid imageHost;ComboBox fileChoice,columns,rows;TextBlock status,regionText,total;StackPanel resultList;
  Button recognize,apply,addFiles,selectAll,paste,clearScreenshots;BitmapSource source;DrawingRectangle region;Point start;bool dragging,busy,loading;string current;
  internal int ScreenshotCount {get{return files.Count;}}
  internal int ResultCount {get{return reviews.Count;}}
  internal int PendingCount {get{return pendingPastes.Count;}}
  internal bool IsRecognizing {get{return busy;}}
  internal bool HasPreview {get{return preview.Source!=null;}}
  public ScreenshotImportWindow(Window owner,Plan snapshot,string[] paths,Action<ScreenshotSelection> onMerge,BitmapSource initialClipboard=null){
   Owner=owner;plan=snapshot;merge=onMerge;Title="截图识别持有角色";Width=Math.Min(1180,SystemParameters.WorkArea.Width*0.95);Height=Math.Min(850,SystemParameters.WorkArea.Height*0.94);MinWidth=960;MinHeight=650;WindowStartupLocation=WindowStartupLocation.CenterOwner;
   FontFamily=owner.FontFamily;Resources=owner.Resources;Background=Brush("#101923");Foreground=Brush("#E8EDF2");UseLayoutRounding=true;
   Build();AddPaths(paths);if(initialClipboard!=null)Loaded+=(s,e)=>AddPastedImage(initialClipboard);PreviewKeyDown+=(s,e)=>{if(e.Key==Key.V&&(Keyboard.Modifiers&ModifierKeys.Control)!=0){if(Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase){BitmapSource image;string message;if(ClipboardScreenshots.TryRead(out image,out message)){AddPastedImage(image);e.Handled=true;}}else {PasteClipboard();e.Handled=true;}}};Closing+=(s,e)=>{if(busy||pendingPastes.Count>0){e.Cancel=true;status.Text="识别进行中，请稍候再关闭。";}};
  }
  static Brush Brush(string hex){return (Brush)new BrushConverter().ConvertFromString(hex);}
  TextBlock Label(string value,double size=12,string color="#A8BBCB"){return new TextBlock{Text=value,FontSize=size,Foreground=Brush(color),TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};}
  Button Button(string text,Action action){var button=new Button{Content=text,Margin=new Thickness(0,0,7,0)};button.Click+=(s,e)=>action();return button;}
  void Build(){
   var root=new Grid{Margin=new Thickness(22)};root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Content=root;
   var heading=new StackPanel{Margin=new Thickness(0,0,0,16)};var headingRow=new DockPanel();clearScreenshots=Button("清空所有截图",ClearScreenshots);clearScreenshots.ToolTip="清空所有截图、识别结果和待识别队列；已持有角色和配队不变";DockPanel.SetDock(clearScreenshots,Dock.Right);headingRow.Children.Add(clearScreenshots);headingRow.Children.Add(Label("截图识别持有角色",23,"#E8EDF2"));heading.Children.Add(headingRow);heading.Children.Add(Label("复制截图后按 Ctrl+V 即自动识别。可连续粘贴多张，核对后统一合并。"));root.Children.Add(heading);
   var body=new Grid();body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1.1,GridUnitType.Star)});body.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(20)});body.ColumnDefinitions.Add(new ColumnDefinition());Grid.SetRow(body,1);root.Children.Add(body);
   var left=new Grid();left.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});left.RowDefinitions.Add(new RowDefinition());left.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});body.Children.Add(left);
   var fileRow=new DockPanel{Margin=new Thickness(0,0,0,10)};paste=Button("粘贴截图",PasteClipboard);paste.ToolTip="Ctrl+V，粘贴后自动识别";DockPanel.SetDock(paste,Dock.Right);fileRow.Children.Add(paste);addFiles=Button("打开图片",ChooseMore);addFiles.Padding=new Thickness(8,9,8,9);DockPanel.SetDock(addFiles,Dock.Right);fileRow.Children.Add(addFiles);fileChoice=new ComboBox{DisplayMemberPath="Value",SelectedValuePath="Key",Margin=new Thickness(0,0,9,0)};fileChoice.SelectionChanged+=(s,e)=>{if(fileChoice.SelectedItem is KeyValuePair<string,string>)LoadImage(((KeyValuePair<string,string>)fileChoice.SelectedItem).Key);};fileRow.Children.Add(fileChoice);left.Children.Add(fileRow);
   imageHost=new Grid{Background=Brush("#0A1017"),ClipToBounds=true,MinHeight=250};preview=new Image{Stretch=Stretch.Uniform};imageHost.Children.Add(preview);overlay=new Canvas{Background=Brushes.Transparent,Cursor=Cursors.Cross};imageHost.Children.Add(overlay);Grid.SetRow(imageHost,1);left.Children.Add(imageHost);imageHost.SizeChanged+=(s,e)=>DrawRegion();
   overlay.MouseLeftButtonDown+=(s,e)=>{if(busy||source==null)return;start=ImagePoint(e.GetPosition(overlay));dragging=true;overlay.CaptureMouse();e.Handled=true;};
   overlay.MouseMove+=(s,e)=>{if(!dragging)return;var p=ImagePoint(e.GetPosition(overlay));region=new DrawingRectangle((int)Math.Min(start.X,p.X),(int)Math.Min(start.Y,p.Y),Math.Max(1,(int)Math.Abs(p.X-start.X)),Math.Max(1,(int)Math.Abs(p.Y-start.Y)));DrawRegion();};
   overlay.MouseLeftButtonUp+=(s,e)=>{if(!dragging)return;dragging=false;overlay.ReleaseMouseCapture();if(region.Width<20||region.Height<20)region=new DrawingRectangle(0,0,source.PixelWidth,source.PixelHeight);DrawRegion();};
   var selection=new StackPanel{Margin=new Thickness(0,10,0,0)};regionText=Label("");selection.Children.Add(regionText);selection.Children.Add(Button("重置为整张图片",()=>{if(source==null||busy)return;region=new DrawingRectangle(0,0,source.PixelWidth,source.PixelHeight);DrawRegion();}));Grid.SetRow(selection,2);left.Children.Add(selection);
   var right=new Grid();right.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});right.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});right.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});right.RowDefinitions.Add(new RowDefinition());Grid.SetColumn(right,2);body.Children.Add(right);
   var settings=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,12)};settings.Children.Add(Label("列数 "));columns=new ComboBox{ItemsSource=Enumerable.Range(1,8).ToArray(),SelectedItem=3,MinWidth=55,Margin=new Thickness(0,0,12,0)};settings.Children.Add(columns);settings.Children.Add(Label("行数 "));rows=new ComboBox{ItemsSource=Enumerable.Range(1,8).ToArray(),SelectedItem=4,MinWidth=55,Margin=new Thickness(0,0,12,0)};settings.Children.Add(rows);columns.SelectionChanged+=(s,e)=>DrawRegion();rows.SelectionChanged+=(s,e)=>DrawRegion();recognize=Button("识别本张",()=>RunRecognition());recognize.Background=Brush("#AAEFC6");recognize.Foreground=Brush("#11281C");settings.Children.Add(recognize);right.Children.Add(settings);
   status=Label("复制截图后按 Ctrl+V，或点击「粘贴截图」；粘贴后自动开始识别。");status.Margin=new Thickness(0,0,0,10);Grid.SetRow(status,1);right.Children.Add(status);
   var resultHeader=new DockPanel{Margin=new Thickness(0,0,0,8)};selectAll=Button("勾选全部结果",()=>{foreach(var item in reviews)item.Selected=true;RefreshResults();});DockPanel.SetDock(selectAll,Dock.Right);resultHeader.Children.Add(selectAll);total=Label("尚未识别",13,"#AAEFC6");resultHeader.Children.Add(total);Grid.SetRow(resultHeader,2);right.Children.Add(resultHeader);
   resultList=new StackPanel();var scroll=new ScrollViewer{Content=resultList,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(scroll,3);right.Children.Add(scroll);
   var footer=new Grid{Margin=new Thickness(0,16,0,0)};footer.ColumnDefinitions.Add(new ColumnDefinition());footer.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});var note=Label("低相似度结果默认不勾选，可手动改名后勾选。多张截图中的重复角色自动去重。\n合并时漂泊者性别/属性会覆盖手动设置；多图冲突按加入顺序取最后一张勾选结果。等级和共鸣链不变。",11);footer.Children.Add(note);apply=Button("合并到已持有",Apply);apply.IsEnabled=false;Grid.SetColumn(apply,1);footer.Children.Add(apply);Grid.SetRow(footer,2);root.Children.Add(footer);
  }
  void ChooseMore(){if(busy)return;var dialog=FileDialog();if(dialog.ShowDialog(this)==true)AddPaths(dialog.FileNames);}
  public static OpenFileDialog FileDialog(){return new OpenFileDialog{Title="选择游戏内角色列表截图（可多选）",Filter="角色截图 (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp",Multiselect=true,CheckFileExists=true};}
  string DisplayName(string key){string name;return names.TryGetValue(key,out name)?name:System.IO.Path.GetFileName(key);}
  void RefreshFiles(string selected){fileChoice.ItemsSource=files.Select((key,index)=>new KeyValuePair<string,string>(key,(index+1)+"/"+files.Count+"  "+DisplayName(key))).ToArray();fileChoice.SelectedValue=selected;if(selected!=null)LoadImage(selected);}
  void AddPaths(IEnumerable<string> paths){foreach(var path in paths)if(!files.Contains(path))files.Add(path);RefreshFiles(current??files.FirstOrDefault());}
  public void PasteClipboard(){BitmapSource image;string message;if(ClipboardScreenshots.TryRead(out image,out message))AddPastedImage(image);else status.Text=message;}
  internal void InputMessage(string message){if(!string.IsNullOrEmpty(message))status.Text=message;}
  public void AddPastedImage(BitmapSource image){
   try{image=ClipboardScreenshots.Opaque(image);
    if(busy){pendingPastes.Enqueue(image);status.Text="已加入待识别队列："+pendingPastes.Count+" 张。";return;}
    string key="clipboard:"+Guid.NewGuid().ToString("N");memoryImages[key]=image;names[key]="粘贴截图 "+(++pasteCount)+"（"+DateTime.Now.ToString("HH:mm:ss")+"）";files.Add(key);RefreshFiles(key);RunRecognition();
   }catch(Exception ex){status.Text="粘贴未完成："+ex.Message;}
  }
  void LoadImage(string path){
   if(busy||loading)return;loading=true;
   try{BitmapSource bitmap;if(!memoryImages.TryGetValue(path,out bitmap)){
     var file=new FileInfo(path);if(file.Length>40000000)throw new Exception("图片文件超过 40 MB，请裁剪后导入。");
     using(var stream=File.OpenRead(path))bitmap=ClipboardScreenshots.Decode(stream);memoryImages[path]=bitmap;
    }
    source=bitmap;preview.Source=source;current=path;region=new DrawingRectangle(0,0,source.PixelWidth,source.PixelHeight);columns.SelectedItem=source.PixelWidth>source.PixelHeight?4:3;rows.SelectedItem=source.PixelWidth>source.PixelHeight?3:4;DrawRegion();
    status.Text="需要调整时拖动框选角色区域，设置列数/行数后点击「识别本张」。";
   }catch(Exception ex){status.Text="图片读取失败："+ex.Message;source=null;current=null;preview.Source=null;overlay.Children.Clear();}finally{loading=false;}
  }
  public void ClearScreenshots(){
   sessionRevision++;if(recognitionJob!=null)recognitionJob.Cancel();pendingPastes.Clear();files.Clear();reviews.Clear();memoryImages.Clear();names.Clear();pasteCount=0;
   current=null;source=null;preview.Source=null;region=DrawingRectangle.Empty;dragging=false;if(overlay.IsMouseCaptured)overlay.ReleaseMouseCapture();overlay.Children.Clear();fileChoice.ItemsSource=null;resultList.Children.Clear();regionText.Text="尚未添加截图";UpdateTotal();SetBusy(busy);
   status.Text="已清空所有截图、识别结果和待识别队列。可继续 Ctrl+V 粘贴新截图。";
  }
  double DisplayScale(){return source==null?1:Math.Min(imageHost.ActualWidth/source.PixelWidth,imageHost.ActualHeight/source.PixelHeight);}
  Point ImagePoint(Point p){double scale=DisplayScale();if(scale<=0)return new Point();double ox=(imageHost.ActualWidth-source.PixelWidth*scale)/2,oy=(imageHost.ActualHeight-source.PixelHeight*scale)/2;return new Point(Math.Max(0,Math.Min(source.PixelWidth,(p.X-ox)/scale)),Math.Max(0,Math.Min(source.PixelHeight,(p.Y-oy)/scale)));}
  void DrawRegion(){
   if(overlay==null||source==null||columns==null||rows==null||columns.SelectedItem==null||rows.SelectedItem==null)return;overlay.Children.Clear();double scale=DisplayScale();if(scale<=0)return;double ox=(imageHost.ActualWidth-source.PixelWidth*scale)/2,oy=(imageHost.ActualHeight-source.PixelHeight*scale)/2,x=ox+region.X*scale,y=oy+region.Y*scale,w=region.Width*scale,h=region.Height*scale;
   var rect=new Rectangle{Width=w,Height=h,Stroke=Brush("#AAEFC6"),StrokeThickness=2,Fill=Brush("#0FAAEECC"),IsHitTestVisible=false};Canvas.SetLeft(rect,x);Canvas.SetTop(rect,y);overlay.Children.Add(rect);int cols=(int)columns.SelectedItem,rws=(int)rows.SelectedItem;
   for(int i=1;i<cols;i++)overlay.Children.Add(new Line{X1=x+w*i/cols,X2=x+w*i/cols,Y1=y,Y2=y+h,Stroke=Brush("#66AAEFC6"),StrokeThickness=1,IsHitTestVisible=false});for(int i=1;i<rws;i++)overlay.Children.Add(new Line{X1=x,X2=x+w,Y1=y+h*i/rws,Y2=y+h*i/rws,Stroke=Brush("#66AAEFC6"),StrokeThickness=1,IsHitTestVisible=false});
   regionText.Text="角色区域："+region.Width+" × "+region.Height+" 像素   ·   "+cols+" 列 × "+rws+" 行";
  }
  async void RunRecognition(){
   if(busy||source==null||current==null)return;busy=true;SetBusy(true);string path=current;var snapshot=source;var crop=region;int cols=(int)columns.SelectedItem,rws=(int)rows.SelectedItem;int revision=sessionRevision;var job=new CancellationTokenSource();recognitionJob=job;
   try{
    status.Text="正在识别，请稍候…";
    var hits=await Task.Run(()=>{if(recognizer==null)recognizer=new PortraitRecognizer(plan);using(var stream=new MemoryStream(ClipboardScreenshots.Encode(snapshot)))using(var bitmap=new System.Drawing.Bitmap(stream))return recognizer.Recognize(bitmap,crop,cols,rws,p=>Dispatcher.BeginInvoke(new Action(()=>{if(revision==sessionRevision&&!job.IsCancellationRequested)status.Text="正在识别："+p+"%"+(pendingPastes.Count>0?" · 待识别 "+pendingPastes.Count+" 张":"");})),job.Token);});
    if(revision!=sessionRevision||job.IsCancellationRequested)return;
    reviews.RemoveAll(item=>item.File==path);foreach(var hit in hits)reviews.Add(new ReviewItem{File=path,Hit=hit,Id=hit.Candidates[0].Id,Gender=hit.Candidates[0].Gender,Selected=hit.Reliable});RefreshResults();
    status.Text=hits.Count==0?"未检测到可见角色头像。请检查左侧预览，确认复制的是图片；也可重新框选角色区域后再识别。":"本张识别 "+hits.Count+" 个角色位置（已跳过空位），"+hits.Count(h=>!h.Reliable)+" 个待核对。可继续 Ctrl+V 粘贴下一张，再统一合并。";
   }catch(OperationCanceledException){if(revision==sessionRevision)status.Text="识别已取消。";}catch(Exception ex){if(revision==sessionRevision)status.Text="识别未完成："+ex.Message;}finally{if(ReferenceEquals(recognitionJob,job))recognitionJob=null;job.Dispose();busy=false;SetBusy(false);UpdateTotal();if(pendingPastes.Count>0)SchedulePaste(pendingPastes.Dequeue());}
  }
  void SchedulePaste(BitmapSource next){int revision=sessionRevision;Dispatcher.BeginInvoke(new Action(()=>{if(revision==sessionRevision)AddPastedImage(next);}));}
  void SetBusy(bool value){recognize.IsEnabled=!value;fileChoice.IsEnabled=!value;columns.IsEnabled=!value;rows.IsEnabled=!value;addFiles.IsEnabled=!value;selectAll.IsEnabled=!value;apply.IsEnabled=!value&&reviews.Any(x=>x.Selected);}
  void RefreshResults(){
   resultList.Children.Clear();var choices=Data.Catalog.OrderBy(c=>c.Name).ToList();
   foreach(var item in reviews){var result=item;var row=new Grid{Margin=new Thickness(0,0,0,8),Background=Brush("#1B2936")};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(28)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(60)});row.ColumnDefinitions.Add(new ColumnDefinition());var check=new CheckBox{IsChecked=item.Selected,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};check.Checked+=(s,e)=>{result.Selected=true;UpdateTotal();};check.Unchecked+=(s,e)=>{result.Selected=false;UpdateTotal();};row.Children.Add(check);
    try{var bitmap=memoryImages[item.File];var bounds=item.Hit.Bounds;var thumb=new Image{Source=new CroppedBitmap(bitmap,new Int32Rect(bounds.X,bounds.Y,bounds.Width,bounds.Height)),Width=54,Height=60,Stretch=Stretch.UniformToFill,Margin=new Thickness(3)};Grid.SetColumn(thumb,1);row.Children.Add(thumb);}catch{}
    var info=new StackPanel{Margin=new Thickness(8,5,7,5)};var name=new ComboBox{ItemsSource=choices,DisplayMemberPath="Name",SelectedItem=choices.First(c=>c.Id==item.Id),IsTextSearchEnabled=true,MaxDropDownHeight=330};info.Children.Add(name);
    if(item.Gender!="男"&&item.Gender!="女")item.Gender=plan.RoverGender;var genderRow=new StackPanel{Orientation=Orientation.Horizontal,Visibility=Data.IsRover(Data.Find(item.Id))?Visibility.Visible:Visibility.Collapsed,Margin=new Thickness(0,5,0,3)};genderRow.Children.Add(Label("漂泊者性别：",11));var sex=new ComboBox{ItemsSource=new[]{"男","女"},SelectedItem=item.Gender,MinWidth=65,Margin=new Thickness(6,0,0,0)};sex.SelectionChanged+=(s,e)=>{if(sex.SelectedItem!=null){result.Gender=(string)sex.SelectedItem;check.IsChecked=true;}};genderRow.Children.Add(sex);genderRow.Children.Add(Label(string.IsNullOrEmpty(item.Hit.Candidates[0].Element)?" 属性请核对":" 属性图标已识别",10,"#E8C39C"));info.Children.Add(genderRow);
    name.SelectionChanged+=(s,e)=>{var selected=name.SelectedItem as Character;if(selected!=null){result.Id=selected.Id;genderRow.Visibility=Data.IsRover(selected)?Visibility.Visible:Visibility.Collapsed;check.IsChecked=true;}};
    var note=Label((item.Hit.Reliable?"匹配较好":"待核对")+" · 相似度 "+(item.Hit.Similarity*100).ToString("F0")+"% · 第 "+(item.Hit.Row+1)+" 行 "+(item.Hit.Column+1)+" 列",10,item.Hit.Reliable?"#AAEFC6":"#E8C39C");note.ToolTip=DisplayName(item.File)+"\n候选："+string.Join(" / ",item.Hit.Candidates.Select(c=>Data.Find(c.Id).Name))+"\n相似度是图像匹配指标，不代表识别准确率。";info.Children.Add(note);Grid.SetColumn(info,2);row.Children.Add(info);resultList.Children.Add(row);
   }UpdateTotal();
  }
  void UpdateTotal(){int unique=reviews.Where(x=>x.Selected).Select(x=>Data.IsRover(Data.Find(x.Id))?"Rover":x.Id).Distinct().Count();total.Text=files.Count==0?"尚未添加截图":reviews.Count+" 个结果 · 已选 "+unique+" 名";apply.IsEnabled=!busy&&unique>0;}
  void Apply(){if(busy)return;var selected=reviews.Where(x=>x.Selected).ToList();if(selected.Count==0)return;var rover=selected.LastOrDefault(x=>Data.IsRover(Data.Find(x.Id)));merge(new ScreenshotSelection{Ids=selected.Select(x=>x.Id).Distinct().ToList(),RoverGender=rover==null?null:rover.Gender,RoverElement=rover==null?null:Data.Find(rover.Id).Element});DialogResult=true;}
 }
}
