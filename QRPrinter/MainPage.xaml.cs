using QRCoder;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Printing;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Printing;

// Die Elementvorlage "Leere Seite" wird unter https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x407 dokumentiert.

namespace QRPrinter
{
    /// <summary>
    /// Eine leere Seite, die eigenständig verwendet oder zu der innerhalb eines Rahmens navigiert werden kann.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private PrintManager printMan;
        private PrintDocument printDoc;
        private IPrintDocumentSource printDocSource;
        private int min = 1, max = 10, count = 10;

        public MainPage()
        {
            this.InitializeComponent();
        }


        #region Register for printing

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            // Register for PrintTaskRequested event
            printMan = PrintManager.GetForCurrentView();
            printMan.PrintTaskRequested += PrintTaskRequested;

            // Build a PrintDocument and register for callbacks
            printDoc = new PrintDocument();
            printDocSource = printDoc.DocumentSource;
            printDoc.Paginate += Paginate;
            printDoc.GetPreviewPage += GetPreviewPage;
            printDoc.AddPages += AddPages;
        }

        #endregion

        #region Showing the print dialog

        private async void PrintButtonClick(object sender, RoutedEventArgs e)
        {
            if (PrintManager.IsSupported())
            {
                try
                {
                    // Show print UI
                    await PrintManager.ShowPrintUIAsync();
                }
                catch
                {
                    // Printing cannot proceed at this time
                    ContentDialog noPrintingDialog = new ContentDialog()
                    {
                        Title = "Printing error",
                        Content = "\nSorry, printing can' t proceed at this time.",
                        PrimaryButtonText = "OK"
                    };
                    await noPrintingDialog.ShowAsync();
                }
            }
            else
            {
                // Printing is not supported on this device
                ContentDialog noPrintingDialog = new ContentDialog()
                {
                    Title = "Printing not supported",
                    Content = "\nSorry, printing is not supported on this device.",
                    PrimaryButtonText = "OK"
                };
                await noPrintingDialog.ShowAsync();
            }
        }

        private void PrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs args)
        {
            // Create the PrintTask.
            // Defines the title and delegate for PrintTaskSourceRequested
            var printTask = args.Request.CreatePrintTask("Print", PrintTaskSourceRequrested);

            // Handle PrintTask.Completed to catch failed print jobs
            printTask.Completed += PrintTaskCompleted;
        }

        private void PrintTaskSourceRequrested(PrintTaskSourceRequestedArgs args)
        {
            // Set the document source.
            args.SetSource(printDocSource);
        }

        #endregion

        #region Print preview

        private void Paginate(object sender, PaginateEventArgs e)
        {
            // As I only want to print one Rectangle, so I set the count to 1
            printDoc.SetPreviewPageCount(count, PreviewPageCountType.Final);
        }

        private async void GetPreviewPage(object sender, GetPreviewPageEventArgs e)
        {
            await CreateQR(min + e.PageNumber - 1);
            // Provide a UIElement as the print preview.
            printDoc.SetPreviewPage(e.PageNumber, this.RectangleToPrint);
        }

        #endregion

        #region Add pages to send to the printer

        private async void AddPages(object sender, AddPagesEventArgs e)
        {
            for (int i = min; i <= max; i++)
            {
                await CreateQR(i);
                printDoc.AddPage(this.RectangleToPrint);
            }

            // Indicate that all of the print pages have been provided
            printDoc.AddPagesComplete();
        }

        #endregion

        #region Print task completed

        private async void PrintTaskCompleted(PrintTask sender, PrintTaskCompletedEventArgs args)
        {
            // Notify the user when the print operation fails.
            if (args.Completion == PrintTaskCompletion.Failed)
            {
                await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    ContentDialog noPrintingDialog = new ContentDialog()
                    {
                        Title = "Printing error",
                        Content = "\nSorry, failed to print.",
                        PrimaryButtonText = "OK"
                    };
                    await noPrintingDialog.ShowAsync();
                });
            }
        }

        #endregion

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            await CreateQR(min);
        }

        private async Task CreateQR(int index)
        {
            this.tb2.Text = "PDU-ID: " + index;

            var url         = string.Format(UrlTextBox.Text, index);
            var qrGenerator = new QRCodeGenerator();
            var payload     = new PayloadGenerator.Url(url);
            var dark        = new[] { byte.MinValue, byte.MinValue, byte.MinValue };
            var light       = new[] { byte.MaxValue, byte.MaxValue, byte.MaxValue };
            
            var qrCodeData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.Q);
            var qrCode = new BitmapByteQRCode(qrCodeData);
            var qrCodeImage = qrCode.GetGraphic(20, dark, light);

            using (var stream = new InMemoryRandomAccessStream())
            {
                using (var writer = new DataWriter(stream.GetOutputStreamAt(0)))
                {
                    writer.WriteBytes(qrCodeImage);
                    await writer.StoreAsync();
                }
                var image = new BitmapImage();
                await image.SetSourceAsync(stream);

                QRImage.Source = image;
            }

            
        }

        #region min .. max
        private void MinTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateRange();

        private void MaxTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateRange();

        private void UpdateRange()
        {
            if (int.TryParse(MinTextBox.Text, out var a) && int.TryParse(MaxTextBox.Text, out var b) && a >=0 && b >= 0)
            {
                min = Math.Min(a, b);
                max = Math.Max(a, b);
                count = max - min + 1;
            }
        }
    #endregion
}
}
