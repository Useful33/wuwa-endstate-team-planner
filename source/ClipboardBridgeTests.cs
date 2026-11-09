using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EndstateMatrix {
 internal static class ClipboardBridgeTests {
  internal static byte[] Dib(BitmapSource image,bool topDown,int bpp,bool v5){
   image=ClipboardScreenshots.Opaque(image);int width=image.PixelWidth,height=image.PixelHeight,header=v5?124:40,stride=((width*bpp+31)/32)*4;var source=new byte[width*height*4];image.CopyPixels(source,width*4,0);var bytes=new byte[header+stride*height];
   using(var writer=new BinaryWriter(new MemoryStream(bytes))){writer.Write(header);writer.Write(width);writer.Write(topDown?-height:height);writer.Write((ushort)1);writer.Write((ushort)bpp);writer.Write(v5?3:0);writer.Write(stride*height);if(v5){writer.BaseStream.Position=40;writer.Write(0x00ff0000);writer.Write(0x0000ff00);writer.Write(0x000000ff);writer.Write(unchecked((int)0xff000000));}}
   for(int y=0;y<height;y++)for(int x=0;x<width;x++){int input=(y*width+x)*4,output=header+(topDown?y:height-1-y)*stride+x*(bpp/8);bytes[output]=source[input];bytes[output+1]=source[input+1];bytes[output+2]=source[input+2];if(bpp==32)bytes[output+3]=0;}
   return bytes;
  }
  static byte[] Pixels(BitmapSource source){var result=new byte[source.PixelWidth*source.PixelHeight*4];source.CopyPixels(result,source.PixelWidth*4,0);return result;}
  internal static string AlphaSummary(BitmapSource image){if(image==null)return "none";if(image.Format!=PixelFormats.Bgra32&&image.Format!=PixelFormats.Pbgra32)return "opaque format";var bytes=Pixels(image);int low=255,high=0;for(int i=3;i<bytes.Length;i+=4){low=Math.Min(low,bytes[i]);high=Math.Max(high,bytes[i]);}return low+".."+high;}
  internal static void Unit(Action<bool,string> check){
   var pixels=new byte[]{10,30,80,0,90,130,200,0,170,110,20,0,22,44,66,0,180,90,40,0,11,33,55,0};var transparent=BitmapSource.Create(3,2,96,96,PixelFormats.Bgra32,null,pixels,12);transparent.Freeze();var opaque=ClipboardScreenshots.Opaque(transparent);var fixedPixels=Pixels(opaque);
   check(opaque.Format==PixelFormats.Bgr32&&fixedPixels[0]==10&&fixedPixels[2]==80&&fixedPixels[3]==255,"zero-alpha bitmap RGB survives and becomes visibly opaque");
   var mime=new DataObject();mime.SetData("image/png",new MemoryStream(ClipboardScreenshots.Encode(transparent)));BitmapSource mimeRead;string mimeMessage;check(ClipboardScreenshots.TryRead(mime,out mimeRead,out mimeMessage)&&Pixels(mimeRead).SequenceEqual(fixedPixels),"WeChat MIME image/png clipboard supported");
   var data=new DataObject();data.SetImage(transparent);BitmapSource read;string message;check(ClipboardScreenshots.TryRead(data,out read,out message)&&Pixels(read).SequenceEqual(fixedPixels),"managed clipboard zero-alpha image recovered");
   foreach(bool topDown in new[]{false,true})foreach(int bpp in new[]{24,32})foreach(bool v5 in new[]{false,true}){
    if(v5&&bpp!=32)continue;var dib=Dib(opaque,topDown,bpp,v5);var decoded=NativeClipboardImages.DecodeDib(dib);check(decoded!=null&&Pixels(decoded).SequenceEqual(fixedPixels),"native DIB RGB recovery "+topDown+" / "+bpp+" / "+v5);
    data=new DataObject();data.SetData(v5?"DeviceIndependentBitmapV5":DataFormats.Dib,new MemoryStream(dib));data.SetImage(BitmapSource.Create(3,2,96,96,PixelFormats.Pbgra32,null,new byte[24],12));check(ClipboardScreenshots.TryRead(data,out read,out message)&&Pixels(read).SequenceEqual(fixedPixels),"DIB preferred over lost RGB managed bitmap");
   }
   var premultiplied=BitmapSource.Create(1,1,96,96,PixelFormats.Pbgra32,null,new byte[]{10,25,50,128},4);var straight=Pixels(ClipboardScreenshots.Opaque(premultiplied));check(straight[0]==20&&straight[1]==50&&straight[2]==100,"premultiplied clipboard RGB restored");
   using(var stream=new MemoryStream(ClipboardScreenshots.Encode(transparent))){check(Pixels(ClipboardScreenshots.Decode(stream)).SequenceEqual(fixedPixels),"PNG encoding never discards zero-alpha screenshot colors");}
   check(NativeClipboardImages.DecodeDib(new byte[40])==null,"invalid DIB rejected");var truncated=Dib(opaque,false,32,false).Take(42).ToArray();check(NativeClipboardImages.DecodeDib(truncated)==null,"truncated DIB rejected");
  }
  internal static void Run(string path,string output){
   BitmapSource image;using(var stream=File.OpenRead(path))image=ClipboardScreenshots.Decode(stream);var expected=new[]{"1504","1303","1602","1102","1204","1601","1403","1103","1402","1202","1106"};int checks=0;Action<bool,string> check=(ok,name)=>{if(!ok)throw new Exception(name);checks++;};Unit(check);
   var recognizer=new PortraitRecognizer(Data.Fresh());foreach(bool v5 in new[]{false,true}){
    var data=new DataObject();data.SetData(v5?"DeviceIndependentBitmapV5":DataFormats.Dib,new MemoryStream(Dib(image,v5,32,v5)));data.SetImage(BitmapSource.Create(image.PixelWidth,image.PixelHeight,96,96,PixelFormats.Pbgra32,null,new byte[image.PixelWidth*image.PixelHeight*4],image.PixelWidth*4));
    BitmapSource recovered;string message;check(ClipboardScreenshots.TryRead(data,out recovered,out message),"clipboard recovered");check(ClipboardScreenshots.HasColorInformation(recovered),"recovered image visible");
    using(var stream=new MemoryStream(ClipboardScreenshots.Encode(recovered)))using(var bitmap=new System.Drawing.Bitmap(stream)){var hits=recognizer.Recognize(bitmap,new System.Drawing.Rectangle(0,0,bitmap.Width,bitmap.Height),3,4);check(hits.Select(h=>h.Candidates[0].Id).SequenceEqual(expected),"zero-alpha Windows DIB still recognizes all eleven roles");}
   }
   File.WriteAllText(output,"PASS: "+checks+" clipboard bridge assertions\nZero-alpha BGRA, premultiplied BGRA, bottom-up/top-down 24-bit and 32-bit DIB, DIBV5 bitfields, transparent managed fallback and PNG color preservation tested. Both zero-alpha screenshot DIB variants recognized 11/11 roles.\n",Encoding.UTF8);
  }
 }
}
