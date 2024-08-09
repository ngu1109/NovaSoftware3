using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;

namespace NovaSoftware
{
    public sealed partial class PosPage : Page
    {
        private List<Product> cart = new List<Product>();
        private List<ProductAddition> additionHistory = new List<ProductAddition>();
        private double total = 0.0;

        public PosPage()
        {
            InitializeComponent();
            _ = EnsureSalesFileExists();
            BarcodeTextBox.KeyDown += BarcodeTextBox_KeyDown; // Handle barcode scanner inputs
        }

        // This class is like, each item we're selling
        public class Product
        {
            public string Name { get; set; }
            public int Qty { get; set; }
            public double Price { get; set; }
            public double TotalPrice => Qty * Price; // Calculate total price

            public Product(string name, int qty, double price)
            {
                Name = name;
                Qty = qty;
                Price = price;
            }
        }

        // This class keeps track of each time we add something to the cart
        public class ProductAddition
        {
            public string Name { get; set; }
            public int Qty { get; set; }
            public double Price { get; set; }

            public ProductAddition(string name, int qty, double price)
            {
                Name = name;
                Qty = qty;
                Price = price;
            }
        }

        // This class helps us convert prices to a nice format with dollar signs
        public class CurrencyConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, string language)
            {
                return value is double doubleValue ? string.Format("{0:C2}", doubleValue) : value.ToString();
            }

            public object ConvertBack(object value, Type targetType, object parameter, string language)
            {
                throw new NotImplementedException();
            }
        }

        // Make sure the sales file exists so we can save stuff
        private async Task EnsureSalesFileExists()
        {
            if (SharedState.CurrentSalesFile == null)
            {
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                SharedState.CurrentSalesFile = await localFolder.CreateFileAsync("Sales.xml", CreationCollisionOption.OpenIfExists);
            }

            // Initialize sales file if it's empty
            if (SharedState.CurrentSalesFile != null && new FileInfo(SharedState.CurrentSalesFile.Path).Length == 0)
            {
                XDocument doc = new XDocument(new XElement("sales"));
                using (Stream fileStream = await SharedState.CurrentSalesFile.OpenStreamForWriteAsync())
                {
                    doc.Save(fileStream);
                }
            }
        }

        // Save the sale details into the sales file
        private async Task SaveSaleAsync(string date, string paymentMethod, double total)
        {
            if (SharedState.CurrentSalesFile == null)
            {
                await ShowDialogAsync("Error", "No sales file selected. Please select or create a sales file.");
                return;
            }

            try
            {
                XDocument doc;
                using (Stream fileStream = await SharedState.CurrentSalesFile.OpenStreamForReadAsync())
                {
                    doc = XDocument.Load(fileStream);
                }

                var root = doc.Element("sales") ?? new XElement("sales");
                if (root.Parent == null) doc.Add(root);

                var saleElement = new XElement("sale",
                    new XAttribute("date", date),
                    new XAttribute("payment_method", paymentMethod),
                    new XElement("total", total.ToString("F2", CultureInfo.InvariantCulture))
                );

                // Add each product in the cart to the sale
                foreach (var product in cart)
                {
                    var saleItem = new XElement("item",
                        new XAttribute("name", product.Name),
                        new XAttribute("price", product.Price.ToString("F2", CultureInfo.InvariantCulture)),
                        new XAttribute("qty", product.Qty)
                    );
                    saleElement.Add(saleItem);
                }

                root.Add(saleElement);

                using (Stream fileStream = await SharedState.CurrentSalesFile.OpenStreamForWriteAsync())
                {
                    doc.Save(fileStream);
                }

                await ShowDialogAsync("Success", "Sale saved successfully.");
            }
            catch (Exception ex)
            {
                await ShowDialogAsync("Error", $"Failed to save sale: {ex.Message}");
            }
        }

        // This button adds stuff to the cart
        private async void AddToCartButton_Click(object sender, RoutedEventArgs e)
        {
            var barcode = BarcodeTextBox.Text.Trim();

            // Automatically set quantity to 1 if it's not provided or invalid
            if (!int.TryParse(QtyTextBox.Text.Trim(), out int qty) || qty <= 0)
            {
                qty = 1;
            }

            if (SharedState.CurrentStockFile == null)
            {
                await ShowDialogAsync("Error", "No stock file selected or created.");
                return;
            }

            try
            {
                XDocument doc;
                using (Stream fileStream = await SharedState.CurrentStockFile.OpenStreamForReadAsync())
                {
                    doc = XDocument.Load(fileStream);
                }

                var item = doc.Element("stock")?.Elements("item").FirstOrDefault(x => x.Element("barcode")?.Value == barcode);

                if (item != null)
                {
                    var name = item.Element("name")?.Value ?? "Unknown";
                    var priceElement = item.Element("price")?.Value;
                    if (string.IsNullOrEmpty(priceElement) || !double.TryParse(priceElement, out double price))
                    {
                        await ShowDialogAsync("Error", "Invalid price in stock file");
                        return;
                    }

                    var existingProduct = cart.FirstOrDefault(p => p.Name == name);
                    if (existingProduct != null)
                    {
                        existingProduct.Qty += qty;
                    }
                    else
                    {
                        cart.Add(new Product(name, qty, price));
                    }

                    // Add to history
                    additionHistory.Add(new ProductAddition(name, qty, price));
                    total += price * qty;
                    UpdateCart();
                }
                else
                {
                    await ShowDialogAsync("Error", "Item not found");
                }
            }
            catch (Exception ex)
            {
                await ShowDialogAsync("Error", $"Failed to read stock file: {ex.Message}");
            }
            finally
            {
                // Clear the BarcodeTextBox and QtyTextBox for the next input
                BarcodeTextBox.Text = string.Empty;
                QtyTextBox.Text = string.Empty;

                // Set focus back to the BarcodeTextBox
                BarcodeTextBox.Focus(FocusState.Programmatic);
            }
        }

        // Add an item to the cart using the barcode and quantity
        private async Task AddToCart()
        {
            var barcode = BarcodeTextBox.Text.Trim();
            if (!int.TryParse(QtyTextBox.Text.Trim(), out int qty) || qty <= 0)
            {
                await ShowDialogAsync("Error", "Invalid quantity");
                return;
            }

            if (SharedState.CurrentStockFile == null)
            {
                await ShowDialogAsync("Error", "No stock file selected or created.");
                return;
            }

            try
            {
                XDocument doc;
                using (Stream fileStream = await SharedState.CurrentStockFile.OpenStreamForReadAsync())
                {
                    doc = XDocument.Load(fileStream);
                }

                var item = doc.Element("stock")?.Elements("item").FirstOrDefault(x => x.Element("barcode")?.Value == barcode);

                if (item != null)
                {
                    var name = item.Element("name")?.Value ?? "Unknown";
                    var priceElement = item.Element("price")?.Value;
                    if (string.IsNullOrEmpty(priceElement) || !double.TryParse(priceElement, out double price))
                    {
                        await ShowDialogAsync("Error", "Invalid price in stock file");
                        return;
                    }

                    var existingProduct = cart.FirstOrDefault(p => p.Name == name);
                    if (existingProduct != null)
                    {
                        existingProduct.Qty += qty;
                    }
                    else
                    {
                        cart.Add(new Product(name, qty, price));
                    }

                    // Add to history
                    additionHistory.Add(new ProductAddition(name, qty, price));
                    total += price * qty;
                    UpdateCart();
                }
                else
                {
                    await ShowDialogAsync("Error", "Item not found");
                }
            }
            catch (Exception ex)
            {
                await ShowDialogAsync("Error", $"Failed to read stock file: {ex.Message}");
            }
        }

        // Show a dialog box for errors or info
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

        // Update the cart display and total price
        private void UpdateCart()
        {
            var cartDisplayList = cart.Select(item => new
            {
                item.Name,
                Qty = item.Qty.ToString(),
                Price = item.Price.ToString("C2"),
                TotalPrice = item.TotalPrice.ToString("C2")
            }).ToList();

            CartListView.ItemsSource = cartDisplayList;
            TotalTextBlock.Text = $"Total: {total:C2}";
        }

        // Apply a discount to the total price
        private void ApplyDiscountButton_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(InputTextBox.Text, out double discount))
            {
                if (discount >= 0 && discount <= 100)
                {
                    total -= total * (discount / 100);
                    UpdateCart();
                }
                else
                {
                    _ = ShowDialogAsync("Error", "Discount should be between 0% and 100%");
                }
            }
            else
            {
                _ = ShowDialogAsync("Error", "Invalid discount value");
            }
        }

        // Apply a deduction to the total price
        private void ApplyDeductionButton_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(InputTextBox.Text, out double deduction))
            {
                if (deduction >= 0 && deduction <= total)
                {
                    total -= deduction;
                    UpdateCart();
                }
                else
                {
                    _ = ShowDialogAsync("Error", "Deduction exceeds total amount or is negative");
                }
            }
            else
            {
                _ = ShowDialogAsync("Error", "Invalid deduction value");
            }
        }

        // Handle payment with cash
        private async void PayWithCashButton_Click(object sender, RoutedEventArgs e)
        {
            await Checkout("cash");
        }

        // Handle payment with credit
        private async void PayWithCreditButton_Click(object sender, RoutedEventArgs e)
        {
            await Checkout("credit");
        }

        // Complete the checkout process and save the sale
        private async Task Checkout(string paymentMethod)
        {
            try
            {
                if (SharedState.CurrentSalesFile == null)
                {
                    await ShowDialogAsync("Error", "No sales file selected.");
                    return;
                }

                XDocument doc;
                using (Stream fileStream = await SharedState.CurrentSalesFile.OpenStreamForReadAsync())
                {
                    doc = XDocument.Load(fileStream);
                }

                var root = doc.Element("sales");

                var sale = new XElement("sale",
                    new XAttribute("date", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")),
                    new XAttribute("payment_method", paymentMethod),
                    new XElement("total", total.ToString("F2", CultureInfo.InvariantCulture))
                );

                // Add each product in the cart to the sale
                foreach (var product in cart)
                {
                    var saleItem = new XElement("item",
                        new XAttribute("name", product.Name),
                        new XAttribute("price", product.Price.ToString("F2", CultureInfo.InvariantCulture)),
                        new XAttribute("qty", product.Qty)
                    );
                    sale.Add(saleItem);
                }

                root.Add(sale);

                using (Stream fileStream = await SharedState.CurrentSalesFile.OpenStreamForWriteAsync())
                {
                    doc.Save(fileStream);
                }

                await ShowDialogAsync("Success", "Transaction Completed");

                cart.Clear();
                additionHistory.Clear();
                total = 0.0;
                UpdateCart();

                BarcodeTextBox.Text = string.Empty;
                QtyTextBox.Text = string.Empty;
                InputTextBox.Text = string.Empty;
            }
            catch (Exception ex)
            {
                await ShowDialogAsync("Error", $"Failed to complete transaction: {ex.Message}");
            }
        }

        // Go back to the main menu
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainMenuPage));
        }

        // Add number pad input to the InputTextBox
        private void NumberPadButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                InputTextBox.Text += button.Content.ToString();
            }
        }

        // Clear the text of the InputTextBox
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            InputTextBox.Text = string.Empty;
        }

        // Add a decimal point to the InputTextBox
        private void DecimalButton_Click(object sender, RoutedEventArgs e)
        {
            if (!InputTextBox.Text.Contains("."))
            {
                InputTextBox.Text += ".";
            }
        }

        // Remove the last added item or quantity from the cart
        private void RemoveItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (additionHistory.Any())
            {
                var lastAddition = additionHistory.Last();
                additionHistory.Remove(lastAddition);

                var existingProduct = cart.FirstOrDefault(p => p.Name == lastAddition.Name);
                if (existingProduct != null)
                {
                    existingProduct.Qty -= lastAddition.Qty;
                    total -= lastAddition.Price * lastAddition.Qty;

                    if (existingProduct.Qty <= 0)
                    {
                        cart.Remove(existingProduct);
                    }

                    UpdateCart();
                }
            }
            else
            {
                _ = ShowDialogAsync("Error", "No items in the cart to remove.");
            }
        }

        // Handle enter key press on the barcode text box
        private async void BarcodeTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                await AddToCart();
                BarcodeTextBox.Text = string.Empty;  // Clear the BarcodeTextBox after processing
                QtyTextBox.Text = "1";  // Reset quantity to 1 for the next item
            }
        }
    }
}
