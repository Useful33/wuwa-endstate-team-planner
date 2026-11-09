using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace EndstateMatrix {
 internal static class ScreenshotSessionTests {
  static async Task Idle(ScreenshotImportWindow dialog){var until=DateTime.UtcNow.AddSeconds(20);while(dialog.IsRecognizing||dialog.PendingCount>0){if(DateTime.UtcNow>until)throw new Exception("Recognition did not finish");await Task.Delay(10);}await Task.Delay(40);}
  internal static async Task Run(Window owner,string imagePath,string output){
   BitmapSource image;using(var stream=File.OpenRead(imagePath))image=ClipboardScreenshots.Decode(stream);
   int checks=0,merged=0;Action<bool,string> check=(value,name)=>{if(!value)throw new Exception(name);checks++;};var plan=Data.Fresh();plan.Owned.Add("1503");string planBefore=Data.Serialize(plan);
   var dialog=new ScreenshotImportWindow(owner,plan,new string[0],ids=>merged++);
   try{
    dialog.AddPastedImage(image);dialog.AddPastedImage(image);
    check(dialog.IsRecognizing&&dialog.ScreenshotCount==1&&dialog.PendingCount==1,"second paste queued while first runs");
    dialog.ClearScreenshots();check(dialog.ScreenshotCount==0&&dialog.ResultCount==0&&dialog.PendingCount==0&&!dialog.HasPreview,"clear removes all screenshot state immediately");
    await Idle(dialog);check(dialog.ScreenshotCount==0&&dialog.ResultCount==0&&dialog.PendingCount==0&&!dialog.HasPreview,"cancelled work never restores cleared images or results");
    check(merged==0&&Data.Serialize(plan)==planBefore,"clear never changes ownership or invokes merge");
    dialog.AddPastedImage(image);await Idle(dialog);check(dialog.ResultCount==11&&dialog.ScreenshotCount==1&&dialog.HasPreview,"new paste works after clearing cancelled work and ignores blank cell");
    dialog.ClearScreenshots();check(dialog.ScreenshotCount==0&&dialog.ResultCount==0&&!dialog.HasPreview&&!dialog.IsRecognizing,"idle clear resets preview and results");
    dialog.ClearScreenshots();check(dialog.ScreenshotCount==0&&dialog.ResultCount==0,"repeated clear is safe");
    dialog.AddPastedImage(image);dialog.AddPastedImage(image);await Idle(dialog);check(dialog.ScreenshotCount==2&&dialog.ResultCount==22&&dialog.PendingCount==0,"consecutive pastes still complete after repeated clear");
    dialog.ClearScreenshots();check(dialog.ScreenshotCount==0&&dialog.ResultCount==0&&!dialog.HasPreview,"all images and results cleared after batch recognition");
    File.WriteAllText(output,"PASS: "+checks+" screenshot session assertions\nActive cancellation, cleared queue, stale result suppression, ownership preservation, repeated clear, paste again and batch recognition verified.\n",Encoding.UTF8);
   }finally{dialog.ClearScreenshots();if(!dialog.IsRecognizing)dialog.Close();}
  }
 }
}
