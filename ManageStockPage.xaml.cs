using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace NovaSoftware
{
    public sealed partial class ManageStockPage : Page
    {
        private const string StockFileName = "Stock.xml";

        public ManageStockPage()
        {
            this.InitializeComponent();
            EnsureStockFileExists();
        }

        private async Task EnsureStockFileExists()
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            StorageFile stockFile = await localFolder.CreateFileAsync(StockFileName, CreationCollisionOption.OpenIfExists);
            if (new FileInfo(stockFile.Path).Length == 0)
            {
                XDocument doc = new XDocument(new XElement("stock"));
                using (Stream fileStream = await stockFile.OpenStreamForWriteAsync())
                {
                    doc.Save(fileStream);
                }
            }
        }

        private async void AddItemButton_Click(object sender, RoutedEventArgs e)
        {
            var itemName = ItemNameTextBox.Text;
            var barcode = BarcodeTextBox.Text;
            var price = PriceTextBox.Text;

            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            StorageFile stockFile = await localFolder.GetFileAsync(StockFileName);

            XDocument doc;
            using (Stream fileStream = await stockFile.OpenStreamForReadAsync())
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

            using (Stream fileStream = await stockFile.OpenStreamForWriteAsync())
            {
                doc.Save(fileStream);
            }

            var dialog = new ContentDialog
            {
                Title = "Success",
                Content = "Item Added",
                CloseButtonText = "OK"
            };
            await dialog.ShowAsync();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainMenuPage));
        }
    }
}
