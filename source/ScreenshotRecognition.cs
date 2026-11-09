using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;

namespace EndstateMatrix {
 public sealed class MatchCandidate {
  public string Id; public string Gender; public string Element;public double Error;
 }
 public sealed class ScreenshotSelection {public List<string> Ids=new List<string>();public string RoverGender;public string RoverElement;}
 public sealed class RecognitionVariant {public string Id {get;set;} public string Name {get;set;} public string File {get;set;} public string Resource {get;set;}public string Gender {get;set;}}
 public sealed class ElementRecognitionSpec {public string Name {get;set;}public string Resource {get;set;}}
 public sealed class RecognitionHit {
  public int Row,Column,SourceWidth,SourceHeight; public Rectangle Bounds; public List<MatchCandidate> Candidates;public string DetectedElement;public double ElementSimilarity,ElementMargin;
  public double Similarity {get{return Math.Max(0,1-Math.Sqrt(Candidates[0].Error));}}
  public double Margin {get{return Candidates.Count>1?Math.Sqrt(Candidates[1].Error)-Math.Sqrt(Candidates[0].Error):0;}}
  public bool Reliable {get{return Similarity>=0.83&&Margin>=0.025&&(!Data.IsRover(Data.Find(Candidates[0].Id))||(!string.IsNullOrEmpty(Candidates[0].Element)&&(Candidates[0].Gender=="男"||Candidates[0].Gender=="女")));}}
 }
 // All recognition runs locally against the bundled portraits, without uploading screenshots.
 public sealed class PortraitRecognizer {
  sealed class Pixels {
   public int Width,Height;public byte[] Bytes;
   Pixels(int width,int height,byte[] bytes){Width=width;Height=height;Bytes=bytes;}
   public Pixels(Bitmap source){
    Width=source.Width;Height=source.Height;
    using(var bitmap=new Bitmap(Width,Height,PixelFormat.Format32bppArgb)){
     using(var g=Graphics.FromImage(bitmap)){g.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;g.DrawImage(source,new Rectangle(0,0,Width,Height));}
     var data=bitmap.LockBits(new Rectangle(0,0,Width,Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
     try{Bytes=new byte[Width*Height*4];for(int y=0;y<Height;y++)Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),Bytes,y*Width*4,Width*4);}finally{bitmap.UnlockBits(data);}
    }
   }
   public Pixels Blurred(int radius){
    int stride=Width+1,length=stride*(Height+1);var sums=new[]{new int[length],new int[length],new int[length]};
    for(int channel=0;channel<3;channel++)for(int y=0;y<Height;y++){int line=0;for(int x=0;x<Width;x++){line+=Bytes[(y*Width+x)*4+channel];sums[channel][(y+1)*stride+x+1]=sums[channel][y*stride+x+1]+line;}}
    var result=new byte[Bytes.Length];for(int y=0;y<Height;y++)for(int x=0;x<Width;x++){
     int x0=Math.Max(0,x-radius),x1=Math.Min(Width,x+radius+1),y0=Math.Max(0,y-radius),y1=Math.Min(Height,y+radius+1),count=(x1-x0)*(y1-y0),index=(y*Width+x)*4;
     for(int channel=0;channel<3;channel++){var sum=sums[channel];result[index+channel]=(byte)((sum[y1*stride+x1]-sum[y0*stride+x1]-sum[y1*stride+x0]+sum[y0*stride+x0])/count);}result[index+3]=255;
    }return new Pixels(Width,Height,result);
   }
  }
  struct PointSample {public double X,Y;public int R,G,B;}
  sealed class Template {public string Id,Gender;public PointSample[] Coarse,Fine;}
  sealed class Positioned {public Template Template;public double Error=double.MaxValue,X,Y,Size;}
  sealed class ElementTemplate {public string Name;public PointSample[] Samples;}
  sealed class ElementMatch {public string Name;public double Error=double.MaxValue;}
  readonly List<Template> templates=new List<Template>();
  readonly List<ElementTemplate> elements=new List<ElementTemplate>();
  public static List<RecognitionVariant> Variants(){return Data.Json.Deserialize<List<RecognitionVariant>>(Data.ReadResource("recognition-variants.json"));}
  public PortraitRecognizer(Plan plan){
   foreach(var character in Data.ActiveCatalog(plan)){
    foreach(var gender in Data.IsRover(character)?new[]{"男","女"}:new[]{""}){
     string resource=Data.PortraitResource(character,gender);
     using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))using(var bitmap=new Bitmap(stream)){
      var pixels=new Pixels(bitmap);templates.Add(new Template{Id=character.Id,Gender=gender,Coarse=Samples(pixels,10),Fine=Samples(pixels,20)});
     }
    }
   }
   foreach(var variant in Variants()){
    var character=Data.Find(variant.Id);if(character==null)continue;string id=Data.IsRover(character)?Data.RoverFor(plan.RoverElement).Id:character.Id;
    using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(variant.Resource))using(var bitmap=new Bitmap(stream)){
     var pixels=new Pixels(bitmap);templates.Add(new Template{Id=id,Gender=Data.IsRover(character)?variant.Gender:"",Coarse=Samples(pixels,10),Fine=Samples(pixels,20)});
    }
   }
   foreach(var spec in Data.Json.Deserialize<List<ElementRecognitionSpec>>(Data.ReadResource("element-recognition.json"))){
    using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(spec.Resource))using(var bitmap=new Bitmap(stream)){
     var pixels=new Pixels(bitmap);var points=new List<PointSample>();var tint=ElementTint(spec.Name);for(int y=0;y<18;y++)for(int x=0;x<18;x++){double nx=(x+0.5)/18,ny=(y+0.5)/18;int index=((int)(ny*pixels.Height)*pixels.Width+(int)(nx*pixels.Width))*4;int alpha=pixels.Bytes[index+3];bool glyph=alpha>=200;bool background=alpha<=20&&(nx-0.5)*(nx-0.5)+(ny-0.5)*(ny-0.5)<0.20;if(!glyph&&!background)continue;points.Add(new PointSample{X=nx,Y=ny,B=glyph?tint[2]:49,G=glyph?tint[1]:44,R=glyph?tint[0]:40});}elements.Add(new ElementTemplate{Name=spec.Name,Samples=points.ToArray()});
    }
   }
  }
  static int[] ElementTint(string name){switch(name){case "冷凝":return new[]{105,170,215};case "热熔":return new[]{214,115,88};case "导电":return new[]{150,109,205};case "气动":return new[]{107,216,174};case "衍射":return new[]{221,219,127};default:return new[]{196,84,144};}}
  List<ElementMatch> MatchElement(Pixels pixels,double left,double top,double portraitSize){
   var matches=new List<ElementMatch>();double step=Math.Max(1,portraitSize*0.012);
   foreach(var element in elements){var match=new ElementMatch{Name=element.Name};
    for(double size=portraitSize*0.23;size<=portraitSize*0.38;size+=portraitSize*0.025)
     for(double x=left-portraitSize*0.04;x<=left+portraitSize*0.11;x+=step)
      for(double y=top-portraitSize*0.04;y<=top+portraitSize*0.13;y+=step){double error=Error(pixels,element.Samples,x,y,size,match.Error);if(error<match.Error)match.Error=error;}
    matches.Add(match);
   }return matches.OrderBy(x=>x.Error).ToList();
  }
  static PointSample[] Samples(Pixels pixels,int grid){
   var result=new List<PointSample>();
   for(int y=0;y<grid;y++)for(int x=0;x<grid;x++){
    double nx=0.12+0.76*(x+0.5)/grid,ny=0.12+0.76*(y+0.5)/grid;
    // Exclude the corner overlays used for element and lock icons in the game.
    if(ny<0.31&&(nx<0.30||nx>0.76))continue;
    int px=Math.Min(pixels.Width-1,(int)(nx*pixels.Width)),py=Math.Min(pixels.Height-1,(int)(ny*pixels.Height));int index=(py*pixels.Width+px)*4;
    if(pixels.Bytes[index+3]<235)continue;
    int b=pixels.Bytes[index],g=pixels.Bytes[index+1],r=pixels.Bytes[index+2];
    if(grid==10){int count=0,sb=0,sg=0,sr=0;for(int yy=Math.Max(0,py-6);yy<=Math.Min(pixels.Height-1,py+6);yy++)for(int xx=Math.Max(0,px-6);xx<=Math.Min(pixels.Width-1,px+6);xx++){int j=(yy*pixels.Width+xx)*4;if(pixels.Bytes[j+3]<220)continue;sb+=pixels.Bytes[j];sg+=pixels.Bytes[j+1];sr+=pixels.Bytes[j+2];count++;}if(count>0){b=sb/count;g=sg/count;r=sr/count;}}
    result.Add(new PointSample{X=nx,Y=ny,B=b,G=g,R=r});
   }return result.ToArray();
  }
  static double Error(Pixels pixels,PointSample[] samples,double left,double top,double size,double stop){
   if(samples.Length<12)return 1;double sum=0,limit=stop*samples.Length*195075.0;
   for(int n=0;n<samples.Length;n++){
    var sample=samples[n];int x=(int)(left+sample.X*size),y=(int)(top+sample.Y*size);
    if(x<0||y<0||x>=pixels.Width||y>=pixels.Height)return 1;
    int index=(y*pixels.Width+x)*4;double b=pixels.Bytes[index]-sample.B,g=pixels.Bytes[index+1]-sample.G,r=pixels.Bytes[index+2]-sample.R;
    sum+=r*r+g*g+b*b;if(sum>limit)return 1;
   }return sum/(samples.Length*195075.0);
  }
  static bool HasPortrait(Pixels pixels,double left,double top,double cw,double ch){
   int x0=Math.Max(1,(int)(left+cw*0.20)),x1=Math.Min(pixels.Width-2,(int)(left+cw*0.93));
   int y0=Math.Max(1,(int)(top+ch*0.12)),y1=Math.Min(pixels.Height-2,(int)(top+ch*0.77));
   double total=0;int strong=0,count=0;
   for(int y=y0;y<y1;y+=2)for(int x=x0;x<x1;x+=2){
    int i=(y*pixels.Width+x)*4,ix=i+8,iy=i+pixels.Width*8;double gradient=0;
    for(int c=0;c<3;c++)gradient+=Math.Abs(pixels.Bytes[i+c]-pixels.Bytes[ix+c])+Math.Abs(pixels.Bytes[i+c]-pixels.Bytes[iy+c]);
    gradient/=6;total+=gradient;if(gradient>18)strong++;count++;
   }return count>0&&total/count>=3.5&&strong/(double)count>=0.028;
  }
  public List<RecognitionHit> Recognize(string path,Rectangle region,int columns,int rows,Action<int> progress=null,CancellationToken cancellation=default(CancellationToken)){
   using(var original=new Bitmap(path))return Recognize(original,region,columns,rows,progress,cancellation);
  }
  public List<RecognitionHit> Recognize(Bitmap original,Rectangle region,int columns,int rows,Action<int> progress=null,CancellationToken cancellation=default(CancellationToken)){
   if(columns<1||columns>8||rows<1||rows>8||columns*rows>48)throw new ArgumentException("排列需要为 1～8 列/行，每张最多 48 个位置。");
   {
    if(original.Width>16000||original.Height>16000||(long)original.Width*original.Height>60000000)throw new ArgumentException("图片过大，请先裁剪到角色列表区域。");
    region=Rectangle.Intersect(region,new Rectangle(0,0,original.Width,original.Height));if(region.Width<columns*28||region.Height<rows*28)throw new ArgumentException("角色区域太小，请重新框选。");
    double scale=Math.Min(1.0,1400.0/Math.Max(region.Width,region.Height));int width=Math.Max(1,(int)(region.Width*scale)),height=Math.Max(1,(int)(region.Height*scale));
    using(var reduced=new Bitmap(width,height,PixelFormat.Format32bppArgb)){
     using(var g=Graphics.FromImage(reduced)){g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;g.DrawImage(original,new Rectangle(0,0,width,height),region,GraphicsUnit.Pixel);}
     var pixels=new Pixels(reduced);var hits=new List<RecognitionHit>();double cw=width/(double)columns,ch=height/(double)rows;var coarsePixels=pixels.Blurred(Math.Max(1,(int)(cw*0.018)));
     for(int row=0;row<rows;row++)for(int column=0;column<columns;column++){
      cancellation.ThrowIfCancellationRequested();double left=column*cw,top=row*ch;var bests=new List<Positioned>();
      if(!HasPortrait(pixels,left,top,cw,ch)){if(progress!=null)progress((row*columns+column+1)*100/(rows*columns));continue;}
      foreach(var template in templates){
       cancellation.ThrowIfCancellationRequested();
       var best=new Positioned{Template=template};double step=Math.Max(3,cw*0.045);
       for(double size=cw*0.60;size<=Math.Min(cw*1.01,ch*0.94);size+=cw*0.065){
        for(double x=left-cw*0.015;x<=left+cw-size+cw*0.035;x+=step)
         for(double y=top-ch*0.045;y<=top+Math.Min(ch-size,ch*0.36);y+=step){double error=Error(coarsePixels,template.Coarse,x,y,size,best.Error);if(error<best.Error){best.Error=error;best.X=x;best.Y=y;best.Size=size;}}
       }bests.Add(best);
      }
      var shortlist=bests.GroupBy(x=>x.Template.Id).OrderBy(g=>g.Min(x=>x.Error)).Take(6).SelectMany(g=>Data.IsRover(Data.Find(g.Key))?g.GroupBy(x=>x.Template.Gender??"").Select(gg=>gg.OrderBy(x=>x.Error).First()):g.OrderBy(x=>x.Error).Take(2)).ToList();
      foreach(var best in shortlist){
       cancellation.ThrowIfCancellationRequested();
       double x0=best.X,y0=best.Y,s0=best.Size;best.Error=double.MaxValue;double step=Math.Max(1,cw*0.012);
       for(double size=s0-cw*0.055;size<=s0+cw*0.055;size+=cw*0.018)
        for(double x=x0-cw*0.045;x<=x0+cw*0.045;x+=step)
         for(double y=y0-cw*0.045;y<=y0+cw*0.045;y+=step){double error=Error(pixels,best.Template.Fine,x,y,size,best.Error);if(error<best.Error){best.Error=error;best.X=x;best.Y=y;best.Size=size;}}
      }
      shortlist=shortlist.GroupBy(x=>x.Template.Id).Select(g=>g.OrderBy(x=>x.Error).First()).OrderBy(x=>x.Error).ToList();var winner=shortlist[0];var bounds=new Rectangle(region.X+(int)(winner.X/scale),region.Y+(int)(winner.Y/scale),Math.Max(1,(int)(winner.Size/scale)),Math.Max(1,(int)(winner.Size/scale)));bounds=Rectangle.Intersect(bounds,new Rectangle(0,0,original.Width,original.Height));
      var elementMatches=MatchElement(pixels,winner.X,winner.Y,winner.Size);double elementSimilarity=1-Math.Sqrt(elementMatches[0].Error),elementMargin=Math.Sqrt(elementMatches[1].Error)-Math.Sqrt(elementMatches[0].Error);string detected=elementSimilarity>=0.84&&elementMargin>=0.012?elementMatches[0].Name:null;
      var candidates=shortlist.Take(3).Select(x=>new MatchCandidate{Id=x.Template.Id,Gender=x.Template.Gender,Error=x.Error}).ToList();var rover=Data.IsRover(Data.Find(candidates[0].Id));if(rover&&Data.RoverFor(detected)!=null){candidates[0].Element=detected;candidates[0].Id=Data.RoverFor(detected).Id;}
      hits.Add(new RecognitionHit{Row=row,Column=column,SourceWidth=original.Width,SourceHeight=original.Height,Bounds=bounds,DetectedElement=detected,ElementSimilarity=elementSimilarity,ElementMargin=elementMargin,Candidates=candidates});
      if(progress!=null)progress((row*columns+column+1)*100/(rows*columns));
     }return hits;
    }
   }
  }
 }
}
