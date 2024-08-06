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

namespace NovaSoftware
{
    public sealed partial class ManageStockPage : Page
    {
        private const string StockFileName = "Stock.xml";
        private StorageFile currentStockFile;

        public ManageStockPage()
        {
            InitializeComponent();
            // Load stock items if a stock file is already selected
            if (SharedState.CurrentStockFile != null)
            {
                currentStockFile = SharedState.CurrentStockFile;
                _ = LoadStockItemsAsync();
            }
        }

        // This button lets us pick an XML file
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
                SharedState.CurrentStockFile = file; // Update shared state
                await LoadStockItemsAsync();
                await ShowDialogAsync("File Selected", $"Selected file: {file.Name}");
            }
        }

        // This button creates a new XML file
        private async void CreateNewXmlFileButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = "NewStock"
            };
            picker.FileTypeChoices.Add("XML", new List<string> { ".xml" });

            StorageFile file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                XDocument doc = new XDocument(new XElement("stock"));
                using (Stream fileStream = await file.OpenStreamForWriteAsync())
                {
                    doc.Save(fileStream);
                }
                currentStockFile = file;
                SharedState.CurrentStockFile = file; // Update shared state
                await LoadStockItemsAsync();
                await ShowDialogAsync("File Created", $"Created and selected file: {file.Name}");
            }
            else
            {
                await ShowDialogAsync("Error", "Operation cancelled. No file was created.");
            }
        }

        // This button adds a new item to the stock
        private async void AddItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentStockFile == null)
            {
                await ShowDialogAsync("Error", "No XML file selected or created.");
                return;
            }

            var itemName = ItemNameTextBox.Text.Trim();
            var barcode = BarcodeTextBox.Text.Trim();
            var priceText = PriceTextBox.Text.Trim();

            if (string.IsNullOrEmpty(itemName) || string.IsNullOrEmpty(barcode) || string.IsNullOrEmpty(priceText))
            {
                await ShowDialogAsync("Error", "All fields must be filled out.");
                return;
            }

            if (!double.TryParse(priceText, out double price))
            {
                await ShowDialogAsync("Error", "Invalid price. Please enter a valid number.");
                return;
            }

            if (price < 0)
            {
                await ShowDialogAsync("Error", "Price cannot be negative.");
                return;
            }

            if (await IsDuplicateItemAsync(itemName, barcode))
            {
                await ShowDialogAsync("Error", "An item with this name or barcode already exists.");
                return;
            }

            try
            {
                XDocument doc;
                using (Stream fileStream = await currentStockFile.OpenStreamForReadAsync())
                {
                    doc = XDocument.Load(fileStream);
                }

                var root = doc.Element("stock");
                var newItem = new XElement("item",
                    new XElement("name", itemName),
                    new XElement("barcode", barcode),
                    new XElement("price", price.ToString())
                );
                root.Add(newItem);

                using (Stream fileStream = await currentStockFile.OpenStreamForWriteAsync())
                {
                    doc.Save(fileStream);
                }

                await LoadStockItemsAsync();
                await ShowDialogAsync("Success", "Item Added");
            }
            catch (Exception ex)
            {
                await ShowDialogAsync("Error", $"Failed to add item: {ex.Message}");
            }
        }

        // Check if the item is already in the stock
        private async Task<bool> IsDuplicateItemAsync(string name, string barcode)
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

        // Load the stock items from the XML file
        private async Task LoadStockItemsAsync()
        {
            if (currentStockFile == null) return;

            try
            {
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
            catch (Exception ex)
            {
                await ShowDialogAsync("Error", $"Failed to load stock items: {ex.Message}");
            }
        }

        // Go back to the main menu
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainMenuPage));
        }

        // Show a dialog box for errors or notifications
        private async Task ShowDialogAsync(string title, string content)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "OK"
            };
            await dialog.ShowAsync();
        }
    }
}