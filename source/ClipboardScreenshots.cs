using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EndstateMatrix {
 public static class ClipboardScreenshots {
  public static BitmapSource Decode(Stream input){var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=input;bitmap.EndInit();return Opaque(bitmap);}
  public static BitmapSource Opaque(BitmapSource image){
   CheckSize(image);if(image.Format==PixelFormats.Bgr32&&image.IsFrozen)return image;bool premultiplied=image.Format==PixelFormats.Pbgra32;
   if(image.Format!=PixelFormats.Bgra32&&image.Format!=PixelFormats.Bgr32&&!premultiplied)image=new FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0);
   int stride=image.PixelWidth*4;var pixels=new byte[stride*image.PixelHeight];image.CopyPixels(pixels,stride,0);
   for(int i=0;i<pixels.Length;i+=4){int alpha=pixels[i+3];if(premultiplied&&alpha>0&&alpha<255)for(int channel=0;channel<3;channel++)pixels[i+channel]=(byte)Math.Min(255,(pixels[i+channel]*255+alpha/2)/alpha);pixels[i+3]=255;}
   var result=BitmapSource.Create(image.PixelWidth,image.PixelHeight,96,96,PixelFormats.Bgr32,null,pixels,stride);result.Freeze();return result;
  }
  internal static bool HasColorInformation(BitmapSource image){
   var pixels=new byte[image.PixelWidth*image.PixelHeight*4];image.CopyPixels(pixels,image.PixelWidth*4,0);int min=255,max=0;
   for(int i=0;i<pixels.Length;i+=Math.Max(4,(pixels.Length/16000/4)*4)){for(int channel=0;channel<3;channel++){int value=pixels[i+channel];min=Math.Min(min,value);max=Math.Max(max,value);}}
   return max-min>=4;
  }
  public static void CheckSize(BitmapSource image){if(image==null||image.PixelWidth<1||image.PixelHeight<1||image.PixelWidth>16000||image.PixelHeight>16000||(long)image.PixelWidth*image.PixelHeight>60000000)throw new ArgumentException("截图尺寸过大或无效，请只截取角色列表区域。");}
  public static byte[] Encode(BitmapSource image){image=Opaque(image);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var stream=new MemoryStream()){encoder.Save(stream);return stream.ToArray();}}
  public static bool TryRead(out BitmapSource image,out string message){
   try{
    bool managed=TryRead(Clipboard.GetDataObject(),out image,out message);if(managed&&HasColorInformation(image))return true;
    var native=NativeClipboardImages.TryRead();if(native!=null&&HasColorInformation(native)){image=native;message=null;return true;}
    if(managed){image=null;message="剪贴板图片没有可见的颜色内容，请在截图软件中重新复制原图后粘贴。";return false;}return false;
   }catch(Exception ex){image=null;message="暂时无法读取剪贴板："+ex.Message+" 请重新复制截图后重试。";return false;}
  }
  public static bool TryRead(IDataObject data,out BitmapSource image,out string message){
   image=null;message="剪贴板中没有图片。请先截图或复制图片，再按 Ctrl+V。";
   try{
    if(data==null)return false;
    // Browsers may publish PNG without the standard Windows Bitmap format.
    foreach(string pngFormat in new[]{"PNG","image/png","image/x-png","image/bmp"})if(data.GetDataPresent(pngFormat,false)){
     object value=data.GetData(pngFormat,false);var stream=value as Stream;
     if(stream!=null){try{if(stream.CanSeek)stream.Position=0;image=Decode(stream);return true;}catch{}}
     var bytes=value as byte[];if(bytes!=null){try{using(var png=new MemoryStream(bytes))image=Decode(png);return true;}catch{}}
    }
    foreach(string format in new[]{"DeviceIndependentBitmapV5",DataFormats.Dib})if(data.GetDataPresent(format,false)){
     var raw=data.GetData(format,false);var bytes=raw as byte[];var stream=raw as Stream;
     if(bytes==null&&stream!=null){if(stream.CanSeek)stream.Position=0;using(var copy=new MemoryStream()){stream.CopyTo(copy);bytes=copy.ToArray();}}
     var dib=NativeClipboardImages.DecodeDib(bytes);if(dib!=null){image=dib;return true;}
    }
    if(data.GetDataPresent(DataFormats.Bitmap)){
     var value=data.GetData(DataFormats.Bitmap);var bitmap=value as BitmapSource;
     if(bitmap!=null){image=Opaque(bitmap);return true;}
     var drawing=value as System.Drawing.Bitmap;if(drawing!=null){using(var stream=new MemoryStream()){drawing.Save(stream,System.Drawing.Imaging.ImageFormat.Png);stream.Position=0;image=Decode(stream);}return true;}
    }
   }catch(Exception ex){message="暂时无法读取剪贴板："+ex.Message+" 请重新复制截图后重试。";}
   return false;
  }
 }
}
