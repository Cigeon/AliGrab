using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using AliGrabApp.ViewModels;

namespace AliGrabApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void SearchViewControl_OnLoaded(object sender, RoutedEventArgs e)
        {
            var searchViewModel = new SearchViewModel();
            SearchViewControl.DataContext = searchViewModel;
        }

        private void ExplorerViewControl_OnLoaded(object sender, RoutedEventArgs e)
        {
            var explorerViewModel = new ExplorerViewModel();
            ExplorerViewControl.DataContext = explorerViewModel;
        }

        private void ResultViewControl_OnLoaded(object sender, RoutedEventArgs e)
        {
            var resultViewModel = new ResultViewModel();
            ResultViewControl.DataContext = resultViewModel;
        }

        private void StatusViewControl_OnLoaded(object sender, RoutedEventArgs e)
        {
            var statusViewModel = new StatusViewModel();
            StatusViewControl.DataContext = statusViewModel;
        }
    }
}
