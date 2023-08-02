using Autodesk.Revit.UI;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Demo_Ceiling
{
    public class ExternalApplication : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {

            string assembLyPath = Assembly
                .GetExecutingAssembly().Location;



            application.CreateRibbonTab("Test Tab");

            RibbonPanel ribbonPanel =
                application.
                CreateRibbonPanel(
                    "Test Tab",
                    "Test Panel");
            PushButtonData pushButtonData =
                  new PushButtonData("btnDemo1",
                  "Test",
                  assembLyPath,
                  "Demo_Ceiling.Ceiling"
                  );
            PushButton pushButton =
                  ribbonPanel.AddItem(pushButtonData) as PushButton;

            pushButton.LargeImage = BmpImageSource("Demo_Ceiling.Image.test.bmp");




            return Result.Succeeded;
        }
        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
        private ImageSource BmpImageSource(string embeddedPath)
        {
            Stream stream = this.GetType().Assembly.GetManifestResourceStream(embeddedPath);
            var decoder = new System.Windows.Media.Imaging.BmpBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);

            return decoder.Frames[0];
        }
    }
}

