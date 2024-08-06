using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace NovaSoftware
{
    public sealed partial class MainMenuPage : Page
    {
        public MainMenuPage()
        {
            this.InitializeComponent();
        }

        private void ManageStockButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(ManageStockPage));
        }

        private async void PosButton_Click(object sender, RoutedEventArgs e)
        {
            if (SharedState.CurrentSalesFile == null || SharedState.CurrentStockFile == null)
            {
                await ShowFileSelectionDialog();
            }
            else
            {
                Frame.Navigate(typeof(PosPage));
            }
        }

        private void SalesSummaryButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SalesSummaryPage));
        }

        private void LogOutButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage));
        }

        private async Task ShowFileSelectionDialog()
        {
            var dialog = new ContentDialog
            {
                Title = "File Selection Required",
                Content = "Please select both sales and stock files before proceeding.",
                CloseButtonText = "OK"
            };
            await dialog.ShowAsync();

            if (SharedState.CurrentSalesFile == null)
            {
                SharedState.CurrentSalesFile = await PickFileAsync("Select Sales File", "sales");
            }

            if (SharedState.CurrentStockFile == null)
            {
                SharedState.CurrentStockFile = await PickFileAsync("Select Stock File", "stock");
            }
        }

        private async Task<StorageFile> PickFileAsync(string title, string expectedRootElement)
        {
            var openPicker = new Windows.Storage.Pickers.FileOpenPicker
            {
                ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail,
                SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary
            };
            openPicker.FileTypeFilter.Add(".xml");

            var file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                if (await ValidateXmlFile(file, expectedRootElement))
                {
                    return file;
                }
                else
                {
                    var dialog = new ContentDialog
                    {
                        Title = "Invalid File",
                        Content = $"The selected file does not contain the expected <{expectedRootElement}> root element.",
                        CloseButtonText = "OK"
                    };
                    await dialog.ShowAsync();
                }
            }
            else
            {
                var dialog = new ContentDialog
                {
                    Title = "File Selection",
                    Content = $"No file selected for {title}.",
                    CloseButtonText = "OK"
                };
                await dialog.ShowAsync();
            }

            return null;
        }

        private async Task<bool> ValidateXmlFile(StorageFile file, string expectedRootElement)
        {
            try
            {
                using (Stream fileStream = await file.OpenStreamForReadAsync())
                {
                    var doc = XDocument.Load(fileStream);
                    var root = doc.Root;
                    if (root != null && root.Name.LocalName == expectedRootElement)
                    {
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "File Error",
                    Content = $"Failed to read or validate the file: {ex.Message}",
                    CloseButtonText = "OK"
                };
                await dialog.ShowAsync();
            }

            return false;
        }
    }
}