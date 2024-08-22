using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using System.Diagnostics;
using Windows.UI.Xaml.Controls;

namespace NovaSoftware
{
    public sealed partial class SalesSummaryPage : Page
    {
        private const string SalesFileName = "Sales.xml";

        public SalesSummaryPage()
        {
            InitializeComponent();
            if (SharedState.CurrentSalesFile != null)
            {
                LoadSalesDataAsync(); // Load the sales data if the file is already selected
            }
        }

        // Load sales data from the selected file
        private async void LoadSalesDataAsync()
        {
            if (SharedState.CurrentSalesFile == null)
            {
                await ShowDialogAsync("Error", "No sales file selected. Please select a sales file.");
                return;
            }

            try
            {
                XDocument doc;
                using (Stream fileStream = await SharedState.CurrentSalesFile.OpenStreamForReadAsync())
                {
                    doc = XDocument.Load(fileStream);
                }

                var salesElement = doc.Element("sales");
                if (salesElement == null)
                {
                    await ShowDialogAsync("Error", "The selected XML file does not have a <sales> root element. Please select a valid sales XML file.");
                    return;
                }

                var sales = salesElement.Elements("sale")
                                        .Select(s => new
                                        {
                                            Date = ParseDate(s.Attribute("date")?.Value),
                                            PaymentMethod = s.Attribute("payment_method")?.Value.ToLower(),
                                            Total = s.Element("total")?.Value
                                        })
                                        .ToList();

                // Log the number of sales entries loaded
                Debug.WriteLine($"Number of sales entries loaded: {sales.Count}");

                // Check if sales list is empty
                if (sales.Count == 0)
                {
                    await ShowDialogAsync("Notification", "No sales data found in the selected XML file.");
                }

                // Display sales data in the list view
                SalesListView.ItemsSource = sales.Select(s => new
                {
                    s.Date,
                    s.PaymentMethod,
                    Total = FormatCurrency(s.Total)
                }).ToList();

                var salesUtility = new SalesUtility();

                // Calculate today's sales for cash and credit
                var todaySalesCash = sales.Where(s => salesUtility.IsToday(s.Date) && s.PaymentMethod == "cash")
                                           .Sum(s => double.TryParse(s.Total, NumberStyles.Currency, CultureInfo.InvariantCulture, out double val) ? val : 0);
                var todaySalesCredit = sales.Where(s => salesUtility.IsToday(s.Date) && s.PaymentMethod == "credit")
                                             .Sum(s => double.TryParse(s.Total, NumberStyles.Currency, CultureInfo.InvariantCulture, out double val) ? val : 0);

                TodayTotalSalesCashTextBlock.Text = FormatCurrency(todaySalesCash);
                TodayTotalSalesCreditTextBlock.Text = FormatCurrency(todaySalesCredit);

                // Calculate overall sales for cash and credit
                var overallSalesCash = sales.Where(s => s.PaymentMethod == "cash")
                                            .Sum(s => double.TryParse(s.Total, NumberStyles.Currency, CultureInfo.InvariantCulture, out double val) ? val : 0);
                var overallSalesCredit = sales.Where(s => s.PaymentMethod == "credit")
                                              .Sum(s => double.TryParse(s.Total, NumberStyles.Currency, CultureInfo.InvariantCulture, out double val) ? val : 0);

                OverallTotalSalesCashTextBlock.Text = FormatCurrency(overallSalesCash);
                OverallTotalSalesCreditTextBlock.Text = FormatCurrency(overallSalesCredit);
            }
            catch (FileNotFoundException)
            {
                await ShowDialogAsync("Error", "Sales XML file not found.");
            }
            catch (Exception ex)
            {
                await ShowDialogAsync("Error", $"Failed to load sales data: {ex.Message}");
            }
        }

        // Parse the date from the XML
        private string ParseDate(string date)
        {
            if (string.IsNullOrEmpty(date))
            {
                return "Unknown Date"; // Handle missing date attribute
            }

            if (DateTime.TryParse(date, out DateTime parsedDate))
            {
                return parsedDate.ToString("dd-MM-yyyy - HH:mm");
            }
            return date;
        }

        // Format the amount as currency
        private string FormatCurrency(double amount)
        {
            return string.Format("{0:C}", amount);
        }

        // Format the amount as currency (overload)
        private string FormatCurrency(string amount)
        {
            if (double.TryParse(amount, NumberStyles.Currency, CultureInfo.InvariantCulture, out double result))
            {
                return string.Format("{0:C}", result);
            }
            return amount;
        }

        // Select a new XML file for sales data
        private async void SelectXmlButton_Click(object sender, RoutedEventArgs e)
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
                SharedState.CurrentSalesFile = file;

                // Ensure the selected file has a <sales> root element
                XDocument doc;
                using (Stream fileStream = await SharedState.CurrentSalesFile.OpenStreamForReadAsync())
                {
                    doc = XDocument.Load(fileStream);
                }

                if (doc.Element("sales") == null)
                {
                    SharedState.CurrentSalesFile = null;
                    await ShowDialogAsync("Error", "The selected XML file does not have a <sales> root element. Please select a valid sales XML file.");
                    return;
                }

                await ShowDialogAsync("Notification", "Sales XML file selected and loaded.");
                LoadSalesDataAsync(); // Refresh data
            }
        }

        // Save a sale into the XML file
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
            catch (UnauthorizedAccessException)
            {
                await ShowDialogAsync("Error", "Permission denied. Please ensure the app has write access to the file.");
            }
            catch (Exception ex)
            {
                await ShowDialogAsync("Error", $"Failed to save sale: {ex.Message}");
            }
        }

        // Create a new XML file for sales data
        private async void CreateXmlButton_Click(object sender, RoutedEventArgs e)
        {
            var savePicker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                FileTypeChoices = { { "XML File", new List<string> { ".xml" } } },
                SuggestedFileName = $"Sales-{DateTime.Now:dd-MM-yy-HH-mm-ss}"
            };

            StorageFile salesFile = await savePicker.PickSaveFileAsync();

            if (salesFile != null)
            {
                await InitializeSalesXmlAsync(salesFile);
                SharedState.CurrentSalesFile = salesFile; // Update shared state with the selected file
                await ShowDialogAsync("Notification", $"Sales XML file '{salesFile.Name}' has been created and selected.");
            }
            else
            {
                await ShowDialogAsync("Error", "Operation cancelled. Sales XML file was not created.");
            }
        }

        // Initialize the new XML file with a root element
        private async Task InitializeSalesXmlAsync(StorageFile salesFile)
        {
            XDocument newDoc = new XDocument(new XElement("sales"));

            using (Stream fileStream = await salesFile.OpenStreamForWriteAsync())
            {
                newDoc.Save(fileStream);
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

    public class SalesUtility
    {
        // Check if the given date is today
        public bool IsToday(string date)
        {
            Debug.WriteLine($"Processing Date String: {date}");

            if (string.IsNullOrEmpty(date))
            {
                return false; // Handle missing date
            }

            // Specify the exact format used in the XML file
            string format = "dd-MM-yyyy - HH:mm";

            if (DateTime.TryParseExact(date, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime parsedDate))
            {
                Debug.WriteLine($"Parsed Date: {parsedDate.Date}, Today: {DateTime.Today}");

                // Compare only the date part (ignoring time)
                return parsedDate.Date == DateTime.Today;
            }
            else
            {
                Debug.WriteLine($"Failed to parse date: {date}");
            }

            return false;
        }
    }}
