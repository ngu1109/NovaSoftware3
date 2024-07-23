using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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
    public sealed partial class SalesSummaryPage : Page
    {
        private const string SalesFileName = "Sales.xml";
        private const string StockFileName = "Stock.xml";

        public SalesSummaryPage()
        {
            this.InitializeComponent();
            LoadSalesData();
            LoadStockData();
        }

        private async void LoadSalesData()
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            StorageFile salesFile = await localFolder.GetFileAsync(SalesFileName);

            XDocument doc;
            using (Stream fileStream = await salesFile.OpenStreamForReadAsync())
            {
                doc = XDocument.Load(fileStream);
            }

            var sales = doc.Element("sales")
                           .Elements("sale")
                           .Select(s => new
                           {
                               Date = s.Attribute("date")?.Value,
                               PaymentMethod = s.Attribute("payment_method")?.Value,
                               Total = s.Element("total")?.Value
                           })
                           .ToList();

            SalesListView.ItemsSource = sales;

            var today = DateTime.Today.ToString("yyyy-MM-dd");
            var todaySales = sales.Where(s => s.Date.StartsWith(today))
                                  .Sum(s => double.Parse(s.Total));

            TodayTotalSalesTextBlock.Text = $"${todaySales:F2}";

            var overallTotalSales = sales.Sum(s => double.Parse(s.Total));
            OverallTotalSalesTextBlock.Text = $"${overallTotalSales:F2}";
        }

        private async void LoadStockData()
        {
            StorageFolder localFolder = ApplicationData.Current.LocalFolder;
            StorageFile stockFile = await localFolder.GetFileAsync(StockFileName);

            XDocument doc;
            using (Stream fileStream = await stockFile.OpenStreamForReadAsync())
            {
                doc = XDocument.Load(fileStream);
            }

            var stockItems = doc.Element("stock")
                                .Elements("item")
                                .Select(i => i.Element("name")?.Value)
                                .ToList();

            StockListView.ItemsSource = stockItems;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainMenuPage));
        }
    }
}
