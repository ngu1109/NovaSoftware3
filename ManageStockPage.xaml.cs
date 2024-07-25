using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace NovaSoftware
{
    public sealed partial class ManageStockPage : Page
    {
        private const string StockFileName = "Stock.xml";
        private StorageFile currentStockFile;

        public ManageStockPage()
        {
            this.InitializeComponent();
        }

        private async void SelectXmlFileButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add(".xml");

            StorageFile file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                currentStockFile = file;
                SharedState.CurrentStockFile = file; // Set the shared stock file
                await LoadStockItems();
                var dialog = new ContentDialog
                {
                    Title = "File Selected",
                    Content = $"Selected file: {file.Name}",
                    CloseButtonText = "OK"
                };
                await dialog.ShowAsync();
            }
        }

        private async void CreateNewXmlFileButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = "NewStock"
            };
            picker.FileTypeChoices.Add("XML", new List<string>() { ".xml" });

            StorageFile file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                XDocument doc = new XDocument(new XElement("stock"));
                using (Stream fileStream = await file.OpenStreamForWriteAsync())
                {
                    doc.Save(fileStream);
                }
                currentStockFile = file;
                SharedState.CurrentStockFile = file; // Set the shared stock file
                await LoadStockItems();
                var dialog = new ContentDialog
                {
                    Title = "File Created",
                    Content = $"Created and selected file: {file.Name}",
                    CloseButtonText = "OK"
                };
                await dialog.ShowAsync();
            }
        }

        private async void AddItemButton_Click(object sender, RoutedEventArgs e)
        {
            // Check if an XML file is selected or created
            if (currentStockFile == null)
            {
                var dialog = new ContentDialog
                {
                    Title = "Error",
                    Content = "No XML file selected or created.",
                    CloseButtonText = "OK"
                };
                await dialog.ShowAsync();
                return;
            }

            // Validate text boxes
            var itemName = ItemNameTextBox.Text.Trim();
            var barcode = BarcodeTextBox.Text.Trim();
            var price = PriceTextBox.Text.Trim();

            if (string.IsNullOrEmpty(itemName) || string.IsNullOrEmpty(barcode) || string.IsNullOrEmpty(price))
            {
                var errorDialog = new ContentDialog
                {
                    Title = "Error",
                    Content = "All fields must be filled out.",
                    CloseButtonText = "OK"
                };
                await errorDialog.ShowAsync();
                return;
            }

            // Check for duplicate items
            if (await IsDuplicateItem(itemName, barcode))
            {
                var duplicateDialog = new ContentDialog
                {
                    Title = "Error",
                    Content = "An item with this name or barcode already exists.",
                    CloseButtonText = "OK"
                };
                await duplicateDialog.ShowAsync();
                return;
            }

            // Add new item to the XML file
            XDocument doc;
            using (Stream fileStream = await currentStockFile.OpenStreamForReadAsync())
            {
                doc = XDocument.Load(fileStream);
            }

            var root = doc.Element("stock");

            var newItem = new XElement("item",
                new XElement("name", itemName),
                new XElement("barcode", barcode),
                new XElement("price", price)
            );

            root.Add(newItem);

            using (Stream fileStream = await currentStockFile.OpenStreamForWriteAsync())
            {
                doc.Save(fileStream);
            }

            await LoadStockItems();

            var successDialog = new ContentDialog
            {
                Title = "Success",
                Content = "Item Added",
                CloseButtonText = "OK"
            };
            await successDialog.ShowAsync();
        }

        private async Task<bool> IsDuplicateItem(string name, string barcode)
        {
            if (currentStockFile == null)
                return false;

            XDocument doc;
            using (Stream fileStream = await currentStockFile.OpenStreamForReadAsync())
            {
                doc = XDocument.Load(fileStream);
            }

            var existingItems = doc.Element("stock")
                .Elements("item")
                .Any(item =>
                    item.Element("name")?.Value == name ||
                    item.Element("barcode")?.Value == barcode
                );

            return existingItems;
        }

        private async Task LoadStockItems()
        {
            if (currentStockFile == null) return;

            XDocument doc;
            using (Stream fileStream = await currentStockFile.OpenStreamForReadAsync())
            {
                doc = XDocument.Load(fileStream);
            }

            var items = doc.Element("stock")
                .Elements("item")
                .Select(item => new
                {
                    Name = item.Element("name")?.Value,
                    Barcode = item.Element("barcode")?.Value,
                    Price = item.Element("price")?.Value
                })
                .ToList();

            StockListView.ItemsSource = items;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainMenuPage));
        }
    }
}
