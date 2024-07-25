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
    public static class SharedState
    {
        public static StorageFile CurrentStockFile { get; set; }
    }

    public sealed partial class PosPage : Page
    {
        private List<(string Name, double Price, int Qty)> cart = new List<(string Name, double Price, int Qty)>();
        private double total = 0.0;
        private const string StockFileName = "Stock.xml";
        private const string SalesFileName = "Sales.xml";

        public PosPage()
        {
            this.InitializeComponent();
            EnsureSalesFileExists();
        }

        private async Task EnsureSalesFileExists()
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            StorageFile salesFile = await localFolder.CreateFileAsync(SalesFileName, CreationCollisionOption.OpenIfExists);
            if (new FileInfo(salesFile.Path).Length == 0)
            {
                XDocument doc = new XDocument(new XElement("sales"));
                using (Stream fileStream = await salesFile.OpenStreamForWriteAsync())
                {
                    doc.Save(fileStream);
                }
            }
        }

        private async void AddToCartButton_Click(object sender, RoutedEventArgs e)
        {
            var barcode = BarcodeTextBox.Text.Trim();
            if (!int.TryParse(QtyTextBox.Text.Trim(), out int qty))
            {
                await ShowDialog("Error", "Invalid quantity");
                return;
            }

            if (SharedState.CurrentStockFile == null)
            {
                await ShowDialog("Error", "No stock file selected or created.");
                return;
            }

            XDocument doc;
            try
            {
                using (Stream fileStream = await SharedState.CurrentStockFile.OpenStreamForReadAsync())
                {
                    doc = XDocument.Load(fileStream);
                }
            }
            catch (Exception ex)
            {
                await ShowDialog("Error", $"Failed to read stock file: {ex.Message}");
                return;
            }

            var item = doc.Element("stock")
                          .Elements("item")
                          .FirstOrDefault(x => x.Element("barcode")?.Value == barcode);

            if (item != null)
            {
                var name = item.Element("name")?.Value;
                var price = double.Parse(item.Element("price")?.Value ?? "0");
                var totalItemPrice = price * qty;

                cart.Add((name, totalItemPrice, qty));
                total += totalItemPrice;
                UpdateCart();
            }
            else
            {
                await ShowDialog("Error", "Item not found");
            }
        }

        private async Task ShowDialog(string title, string content)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "OK"
            };
            await dialog.ShowAsync();
        }

        private void UpdateCart()
        {
            CartListView.ItemsSource = null;
            CartListView.ItemsSource = cart.Select(item => new
            {
                item.Name,
                Qty = item.Qty.ToString("D4"),
                Price = item.Price.ToString("C2")
            }).ToList();
            TotalTextBlock.Text = $"Total: {total:C2}";
        }


        private void ApplyDiscountButton_Click(object sender, RoutedEventArgs e)
        {
            var discount = double.Parse(InputTextBox.Text);
            total -= total * (discount / 100);
            UpdateCart();
        }

        private void ApplyDeductionButton_Click(object sender, RoutedEventArgs e)
        {
            var deduction = double.Parse(InputTextBox.Text);
            total -= deduction;
            UpdateCart();
        }

        private async void PayWithCashButton_Click(object sender, RoutedEventArgs e)
        {
            await Checkout("cash");
        }

        private async void PayWithCreditButton_Click(object sender, RoutedEventArgs e)
        {
            await Checkout("credit");
        }

        private async Task Checkout(string paymentMethod)
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            StorageFile salesFile = await localFolder.GetFileAsync(SalesFileName);

            XDocument doc;
            using (Stream fileStream = await salesFile.OpenStreamForReadAsync())
            {
                doc = XDocument.Load(fileStream);
            }

            var root = doc.Element("sales");

            var sale = new XElement("sale",
                new XAttribute("date", DateTime.Now),
                new XAttribute("payment_method", paymentMethod),
                new XElement("total", total)
            );

            foreach (var item in cart)
            {
                var saleItem = new XElement("item",
                    new XAttribute("name", item.Name),
                    new XAttribute("price", item.Price),
                    new XAttribute("qty", item.Qty)
                );
                sale.Add(saleItem);
            }

            root.Add(sale);

            using (Stream fileStream = await salesFile.OpenStreamForWriteAsync())
            {
                doc.Save(fileStream);
            }

            var dialog = new ContentDialog
            {
                Title = "Success",
                Content = "Transaction Completed",
                CloseButtonText = "OK"
            };
            await dialog.ShowAsync();

            cart.Clear();
            total = 0.0;
            UpdateCart();
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainMenuPage));
        }

        private void NumberPadButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                InputTextBox.Text += button.Content.ToString();
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            InputTextBox.Text = string.Empty;
        }
    }
}
