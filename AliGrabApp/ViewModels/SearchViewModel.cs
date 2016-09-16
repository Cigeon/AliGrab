using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AngleSharp.Extensions;
using AngleSharp.Parser.Html;
using AliGrabApp.Models;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using AliGrabApp.Commands;
using AngleSharp.Dom.Html;

namespace AliGrabApp.ViewModels
{
    public delegate void ItemsGrabbedHandler(ObservableCollection<AliItem> items);
    public delegate void SearchProgressHandler(ProgressBarModel pb);


    public class SearchViewModel : ViewModelBase
    {
        private bool _canExecute;
        private ICommand _searchCommand;
        private BackgroundWorker _bw = new BackgroundWorker();
        private int _itemNo;
        private bool _itemsNotFound = false;

        public ProgressBarModel ProgressBar { get; set; }  
        public ControlModel ButtonGo { get; set; }
        public ObservableCollection<AliItem> AliItems { get; set; }             
        public string QueryText { get; set; }

        public static event ItemsGrabbedHandler OnItemsGrabbed;
        public static event SearchProgressHandler OnSearchProgress;

        public SearchViewModel()
        {
            // Init
            ProgressBar = new ProgressBarModel { Visibility = Visibility.Hidden };
            ButtonGo = new ControlModel { IsEnabled = true };
            AliItems = new ObservableCollection<AliItem>();
            _itemNo = 0;
            // Commands status
            _canExecute = true;
            // Background worker settings
            _bw.WorkerReportsProgress = true;
            _bw.WorkerSupportsCancellation = true;
            _bw.ProgressChanged += ProgressChanged;
            _bw.DoWork += DoWork;
            _bw.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
            // Subscribe events
            StatusViewModel.OnTaskCanceled += CancelWork;
            StatusViewModel.OnTaskStarted += () => { ButtonGo.IsEnabled = false; };
            StatusViewModel.OnTaskFinished += () => { ButtonGo.IsEnabled = true; };
        }

        private void CancelWork()
        {
            // background worker cancalation
            _bw.CancelAsync();
        }

        public void OnWindowClosed(object sender, EventArgs e)
        {
            // background worker cancalation
            _bw.CancelAsync();
        }

        private void DoWork(object sender, DoWorkEventArgs e)
        {
            // Initialize vars
            _itemNo = 0;
            _itemsNotFound = false;
            var itemsCount = 0;
            var itemsCountFinded = false;
            AliItems.Clear();

            // Replace space char with plus char
            var searchText = QueryText.Replace(' ', '+');
            // Initial page url
            var url = "http://aukro.ua/listing/listing.php?string=" + searchText + "&search_scope=";

            // Loop throw all pages 
            while (true)
            {
                // Check for background worker cancelation
                if (_bw.CancellationPending) { e.Cancel = true; break; }

                try
                {
                    // Get page with items
                    var page = RetrievePage(url);

                    // Generate structured document
                    var parser = new HtmlParser();
                    var document = parser.Parse(page);

                    // Get items count
                    if (!itemsCountFinded)
                    {
                        var items = document.QuerySelectorAll("#main-breadcrumb-search-hits").First().Text();
                        int index = items.IndexOf(" ");
                        if (index > 0) items = items.Substring(1, index);
                        itemsCount = int.Parse(items);
                        if (itemsCount == 0)
                        {
                            // Set flag items not found
                            _itemsNotFound = true;
                            break;
                        }
                        itemsCountFinded = true;
                    }

                    // Get url to the next page
                    var nextPageUrl = GetNextPageUrl(document);

                    // Get all items
                    var task = GetItemsFromPage(document, itemsCount);
                    var pageItems = task.Result;
                    foreach (var item in pageItems)
                    {
                        AliItems.Add(item);
                    }

                    // If next page url not found - break the loop
                    if (nextPageUrl.Equals("")) break;
                    // Set url for the next iteration
                    url = nextPageUrl;
                }
                catch (WebException ex)
                {
                    MessageBox.Show("Веб-ресурс недоступен. \n \n" +
                                    "- Проверьте подключение к сети интернет на Вашем компьютере.\n" +
                                    "- Возможно возникла ошибка сервера, попробуйте повторить поиск.\n \n" +
                                    "Message: " + ex.Message,
                                    "Ошибка!",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                }
            }
        }

        private string RetrievePage(string url)
        {
            // Get web page source code
            var itemClient = new WebClient();
            itemClient.Encoding = Encoding.UTF8;
            var itemPage = itemClient.DownloadString(url);
            return itemPage;
        }

        async Task<string> RetrievePageAsync(string url)
        {
            // Get web page source code
            var itemClient = new WebClient();
            itemClient.Encoding = Encoding.UTF8;
            var itemPage = await itemClient.DownloadStringTaskAsync(url);
            return itemPage;
        }

        private string GetNextPageUrl(IHtmlDocument document)
        {
            var url = "";

            // Get link to the next page
            try
            {
                url = "http://aukro.ua";
                url += document.QuerySelectorAll("div.pager-bottom > ul > li.next > a")
                    .First()
                    .GetAttribute("href");
            }
            catch (InvalidOperationException)
            {
                // If next page's link wasn't founded return empty string
                return "";
            }
            // Return url
            return url;
        }

        private async Task<ObservableCollection<AliItem>> GetItemsFromPage
            (IHtmlDocument document, int itemsCount)
        {
            // List of items
            var aliItems = new ObservableCollection<AliItem>();

            // Get items urls
            var itemsUrls = GetItemsUrls(document).ToList();

            // Get items pages
            var RetrievePagesTask = new List<Task<string>>();
            foreach (var itemsUrl in itemsUrls)
            {
                var task = RetrievePageAsync(itemsUrl);
                RetrievePagesTask.Add(task);
            }

            // Get items
            var item = 0;
            foreach (var task in RetrievePagesTask)
            {
                // Check for background worker cancelation
                if (_bw.CancellationPending) { break; }

                // Wait for retrive page
                var itemPage = await task;

                // Get item from the page
                var aliItem = GetItemFromPage(itemPage, itemsUrls[item]);

                // Check if item exist
                if (aliItem != null)
                {
                    aliItems.Add(aliItem);
                    item++;
                    _itemNo++;
                }
                else
                {
                    itemsCount--;
                }

                // Set progress bar value
                int percent = (int)(Convert.ToDouble(_itemNo) / Convert.ToDouble(itemsCount) * 100);
                _bw.ReportProgress(percent, String.Format("  Items grabbing   {0} of {1}", _itemNo, itemsCount));

            }

            return aliItems;
        }

        private IEnumerable<string> GetItemsUrls(IHtmlDocument document)
        {
            // Get all products links on page
            var prodRawLinks = document.QuerySelectorAll("div.photo > a");
            foreach (var link in prodRawLinks)
            {
                yield return "http://aukro.ua" + link.GetAttribute("href");
            }
        }

        private AliItem GetItemFromPage(string page, string url)
        {

            // Generate structured document
            var parser = new HtmlParser();
            var document = parser.Parse(page);

            // Greate aukro item for storing data
            var aliItem = new AliItem();

            // Get all data
            // check for expired item
            var expire = document.QuerySelectorAll("#timeLeftCounter")
                .First()
                .Text();

            if (expire == "завершен")
            {
                return null;
            }

            // Id
            aliItem.AliId = long.Parse(
                document.QuerySelectorAll("p.itemId > span")
                .First()
                .Text());
            // Title
            aliItem.Title = document.QuerySelectorAll("#siSocialLinks")
                .First()
                .GetAttribute("data-item-name");
            // Selling type
            aliItem.Type = document.QuerySelectorAll("input.show-item-btn")
                .First()
                .GetAttribute("value");
            // Price
            aliItem.Price = double.Parse(
                document.QuerySelectorAll("[itemprop='price']")
                    .First()
                    .GetAttribute("content"),
                System.Globalization.CultureInfo.InvariantCulture);
            // Price currency
            aliItem.PriceCurrency = document.QuerySelectorAll("[itemprop='priceCurrency']")
                .First()
                .GetAttribute("content");
            // Seller
            aliItem.Seller = document.QuerySelectorAll("div.sellerDetails > dl > dt")
                .First()
                .Text()
                .Replace("Продaвец ", "")
                .Trim();
            aliItem.Seller = aliItem.Seller.Remove(
                aliItem.Seller.IndexOf(" "),
                aliItem.Seller.Length - aliItem.Seller.IndexOf(" "));
            // Link
            aliItem.Link = url;
            // Description
            aliItem.Description = document.QuerySelectorAll("div.deliveryAndPayment > table")
                .Last()
                .Text()
                .Trim();
            // remove empty lines
            aliItem.Description = Regex.Replace(
                aliItem.Description,
                @"^\s+$[\r\n]*",
                "",
                RegexOptions.Multiline);
            // remove spaces from each lines
            aliItem.Description = string.Join(
                "\n",
                aliItem.Description.Split('\n').Select(s => s.Trim()));
            // Image
            // get image link
            var imgUrl = "";
            try
            {
                imgUrl = document.QuerySelectorAll("[itemprop='image']")
                .First()
                .GetAttribute("content");
            }
            catch (InvalidOperationException)
            {
                // if no image parse another element
                imgUrl = document.QuerySelectorAll("div.img")
                        .First()
                        .GetAttribute("style");
                // cut reference from string
                imgUrl = String.Join("", Regex.Matches(imgUrl, @"\'(.+?)\'")
                         .Cast<Match>()
                         .Select(m => m.Groups[1].Value));
            }
            // load image
            try
            {
                aliItem.Image = new WebClient().DownloadData(imgUrl);                  // async
            }
            catch (WebException)
            {
                // load default image
                aliItem.Image = new WebClient().DownloadData("http://static.allegrostatic.pl/site_images/209/0/layout/showItemNoPhoto.png");
            }

            return aliItem;
        }

        private void ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            // Set current progress
            ProgressBar.Value = e.ProgressPercentage;
            ProgressBar.Content = e.UserState;
            // Send current progress to status view
            OnSearchProgress?.Invoke(ProgressBar);
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (_itemsNotFound)
            {
                // Show alert
                MessageBox.Show("Nothing to show. Please, change your query!", 
                                "Info", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Information);
                return;
            }

            if (!e.Cancelled)
            {
                // Show alert
                MessageBox.Show("Items successfully copied!", 
                                "Info", 
                                MessageBoxButton.OK, 
                                MessageBoxImage.Information);

                // Send grabbed items to result view
                OnItemsGrabbed?.Invoke(AliItems);
            }
            else
            {
                // Show alert
                MessageBox.Show("Operation was canceled!",
                                "Info",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
            }

            // clear and hide progress bar
            ProgressBar.Content = "Ready";
            ProgressBar.Visibility = Visibility.Hidden;
            // Send current progress to status view
            OnSearchProgress?.Invoke(ProgressBar);

            // Enable search button
            ButtonGo.IsEnabled = true;
        }

        public ICommand SearchCommand
        {
            get
            {
                return _searchCommand ?? (_searchCommand = new Commands.CommandHandler(() => Search(), _canExecute));
            }
        }

        public void Search()
        {
            if (!_bw.IsBusy)
            {
                // Run task at the background
                _bw.RunWorkerAsync();
                // Show progress bar
                ProgressBar.Value = 0;
                ProgressBar.Content = "";
                ProgressBar.Visibility = Visibility.Visible;
                // Send current progress to status view
                OnSearchProgress?.Invoke(ProgressBar);
                // Disable start button
                ButtonGo.IsEnabled = false;
            }
        }

    }
}
