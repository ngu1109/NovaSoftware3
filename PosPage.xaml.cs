﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace NovaSoftware
{
    public sealed partial class PosPage : Page
    {
        private List<(string Name, double Price, int Qty)> cart = new List<(string Name, double Price, int Qty)>();
        private double total = 0.0;

        public PosPage()
        {
            this.InitializeComponent();
            _ = EnsureSalesFileExists();
        }

        private async Task EnsureSalesFileExists()
        {
            if (SharedState.CurrentSalesFile == null)
            {
                StorageFolder localFolder = ApplicationData.Current.LocalFolder;
                SharedState.CurrentSalesFile = await localFolder.CreateFileAsync("Sales.xml", CreationCollisionOption.OpenIfExists);
            }

            if (SharedState.CurrentSalesFile != null && new FileInfo(SharedState.CurrentSalesFile.Path).Length == 0)
            {
                XDocument doc = new XDocument(new XElement("sales"));
                using (Stream fileStream = await SharedState.CurrentSalesFile.OpenStreamForWriteAsync())
                {
                    doc.Save(fileStream);
                }
            }
        }

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

                var root = doc.Element("sales");
                if (root == null)
                {
                    root = new XElement("sales");
                    doc.Add(root);
                }

                var saleElement = new XElement("sale",
                    new XAttribute("date", date),
                    new XAttribute("payment_method", paymentMethod),
                    new XElement("total", total.ToString("F2", CultureInfo.InvariantCulture))
                );

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

        private async void AddToCartButton_Click(object sender, RoutedEventArgs e)
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

                var item = doc.Element("stock")
                              .Elements("item")
                              .FirstOrDefault(x => x.Element("barcode")?.Value == barcode);

                if (item != null)
                {
                    var name = item.Element("name")?.Value ?? "Unknown";
                    var priceElement = item.Element("price")?.Value;
                    if (!double.TryParse(priceElement, out double price))
                    {
                        await ShowDialogAsync("Error", "Invalid price in stock file");
                        return;
                    }

                    var totalItemPrice = price * qty;

                    cart.Add((name, price, qty));
                    total += totalItemPrice;
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
                    new XAttribute("date", DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")),  // Use ISO format
                    new XAttribute("payment_method", paymentMethod),
                    new XElement("total", total.ToString("F2", CultureInfo.InvariantCulture))
                );

                foreach (var item in cart)
                {
                    var saleItem = new XElement("item",
                        new XAttribute("name", item.Name),
                        new XAttribute("price", item.Price.ToString("F2", CultureInfo.InvariantCulture)),
                        new XAttribute("qty", item.Qty)
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
                total = 0.0;
                UpdateCart();

                // Clear textboxes after checkout
                BarcodeTextBox.Text = string.Empty;
                QtyTextBox.Text = string.Empty;
                InputTextBox.Text = string.Empty;
            }
            catch (Exception ex)
            {
                await ShowDialogAsync("Error", $"Failed to complete transaction: {ex.Message}");
            }
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

        private void DecimalButton_Click(object sender, RoutedEventArgs e)
        {
            // Prevent multiple decimals in the input
            if (!InputTextBox.Text.Contains("."))
            {
                InputTextBox.Text += ".";
            }
        }

        private void RemoveItemButton_Click(object sender, RoutedEventArgs e)
        {
            // Implement item removal logic if needed
        }
    }
}
