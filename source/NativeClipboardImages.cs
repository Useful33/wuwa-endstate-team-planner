using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace EndstateMatrix {
 internal static class NativeClipboardImages {
  [DllImport("user32.dll",SetLastError=true)]static extern bool OpenClipboard(IntPtr owner);
  [DllImport("user32.dll")]static extern bool CloseClipboard();
  [DllImport("user32.dll")]static extern IntPtr GetClipboardData(uint format);
  [DllImport("kernel32.dll")]static extern IntPtr GlobalLock(IntPtr handle);
  [DllImport("kernel32.dll")]static extern bool GlobalUnlock(IntPtr handle);
  [DllImport("kernel32.dll")]static extern UIntPtr GlobalSize(IntPtr handle);
  internal static BitmapSource TryRead(){
   if(!OpenClipboard(IntPtr.Zero))return null;
   try{foreach(uint format in new uint[]{17,8}){
    IntPtr handle=GetClipboardData(format);if(handle==IntPtr.Zero)continue;
    ulong length=GlobalSize(handle).ToUInt64();if(length<40||length>250000000)continue;IntPtr address=GlobalLock(handle);if(address==IntPtr.Zero)continue;
    try{var bytes=new byte[(int)length];Marshal.Copy(address,bytes,0,bytes.Length);var image=DecodeDib(bytes);if(image!=null)return image;}catch{}finally{GlobalUnlock(handle);}
   }}finally{CloseClipboard();}return null;
  }
  static uint MaskValue(uint pixel,uint mask){if(mask==0)return 0;int shift=0;while((mask&1)==0){mask>>=1;shift++;}return (uint)(((ulong)((pixel>>shift)&mask)*255+mask/2)/mask);}
  // CF_DIB's fourth byte is commonly reserved (zero), not meaningful alpha.
  // Decode the RGB channels directly instead of passing that byte through WPF.
  internal static BitmapSource DecodeDib(byte[] bytes){
   if(bytes==null||bytes.Length<40)return null;
   try{
    int header=BitConverter.ToInt32(bytes,0),width=BitConverter.ToInt32(bytes,4),signedHeight=BitConverter.ToInt32(bytes,8);if(header<40||header>bytes.Length||width<1||width>16000||signedHeight==int.MinValue)return null;
    int height=Math.Abs(signedHeight),bpp=BitConverter.ToUInt16(bytes,14);uint compression=BitConverter.ToUInt32(bytes,16),palette=BitConverter.ToUInt32(bytes,32);
    if(height<1||height>16000||(long)width*height>60000000||BitConverter.ToUInt16(bytes,12)!=1||(bpp!=24&&bpp!=32)||(compression!=0&&compression!=3&&compression!=6))return null;
    int offset=header;uint red=0x00ff0000,green=0x0000ff00,blue=0x000000ff;
    if(compression==3||compression==6){
     int masks=header>=52?40:header;if(masks+12>bytes.Length)return null;red=BitConverter.ToUInt32(bytes,masks);green=BitConverter.ToUInt32(bytes,masks+4);blue=BitConverter.ToUInt32(bytes,masks+8);if(red==0||green==0||blue==0)return null;
     if(header==40)offset+=compression==6?16:12;
    }
    long pixelOffset=(long)offset+palette*4L,stride=((long)width*bpp+31)/32*4;if(pixelOffset<0||pixelOffset+stride*height>bytes.Length)return null;
    var result=new byte[width*height*4];for(int y=0;y<height;y++){
     int input=(int)(pixelOffset+(signedHeight>0?height-1-y:y)*stride),output=y*width*4;
     for(int x=0;x<width;x++){
      int target=output+x*4;if(bpp==24){int index=input+x*3;result[target]=bytes[index];result[target+1]=bytes[index+1];result[target+2]=bytes[index+2];}
      else {int index=input+x*4;if(red==0x00ff0000&&green==0x0000ff00&&blue==0x000000ff){result[target]=bytes[index];result[target+1]=bytes[index+1];result[target+2]=bytes[index+2];}else {uint pixel=BitConverter.ToUInt32(bytes,index);result[target]=(byte)MaskValue(pixel,blue);result[target+1]=(byte)MaskValue(pixel,green);result[target+2]=(byte)MaskValue(pixel,red);}}result[target+3]=255;
     }
    }
    var image=BitmapSource.Create(width,height,96,96,PixelFormats.Bgr32,null,result,width*4);image.Freeze();return image;
   }catch{return null;}
  }
 }
}
