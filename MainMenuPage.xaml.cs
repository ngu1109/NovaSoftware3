using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Foundation.Collections;
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

        private void PosButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(PosPage));
        }
        private void SalesSummaryButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(SalesSummaryPage));
        }

        private void LogOutButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(MainPage));
        }
    }
}
