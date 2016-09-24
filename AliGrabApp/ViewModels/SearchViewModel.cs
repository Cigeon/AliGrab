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
using System.IO;

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
        private int _currItemNo;
        private bool _itemsNotFound = false;

        public ProgressBarModel ProgressBar { get; set; }  
        public ControlModel ButtonGo { get; set; }
        public ObservableCollection<AliItem> AliItems { get; set; }
        public ObservableCollection<ProxyServer> ProxyServers { set; get; }
        public ProxyServer CurrentProxy { get; set; }
        public string Url { get; set; }
        public List<string> BrokenUrls { get; set; }
        

        public static event ItemsGrabbedHandler OnItemsGrabbed;
        public static event SearchProgressHandler OnSearchProgress;

        public SearchViewModel()
        {
            // Init
            ProgressBar = new ProgressBarModel { Visibility = Visibility.Hidden };
            ButtonGo = new ControlModel { IsEnabled = true };
            AliItems = new ObservableCollection<AliItem>();
            ProxyServers = new ObservableCollection<ProxyServer>();
            CurrentProxy = new ProxyServer();
            BrokenUrls = new List<string>();
            _itemNo = 0;
            _currItemNo = 0;
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

            // Get proxy servers list
            //GetProxyServers();

            // Initial page url
            var url = Url;

            // Loop throw all pages 
            while (true)
            {
                // Check for background worker cancelation
                if (_bw.CancellationPending) { e.Cancel = true; break; }

                try
                {
                    // Get html document with items
                    var page = RetrievePage(url);

                    // Generate structured document
                    var parser = new HtmlParser();
                    var document = parser.Parse(page);

                    // Get items count
                    if (!itemsCountFinded)
                    {
                        var items = document.QuerySelectorAll("strong.search-count").First().Text();
                        items = items.Replace(",", "");
                        itemsCount = int.Parse(items);

                        if (itemsCount == 0)
                        {
                            // Set flag items not found
                            _itemsNotFound = true;
                            break;
                        }
                        itemsCountFinded = true;

                        Debug.WriteLine("Items count: " + itemsCount);
                    }

                    Debug.WriteLine("Current items list url: " + url);

                    // Get url to the next page
                    var nextPageUrl = GetNextPageUrl(document);

                    Debug.WriteLine("Next items list url: " + nextPageUrl);

                    // Get all items
                    var tmpItems = new ObservableCollection<AliItem>();
                    var task = GetItemsFromPage(document, itemsCount);
                    var pageItems = task.Result;                    
                    foreach (var item in pageItems)
                    {
                        tmpItems.Add(item);
                    }

                    //var tries = 0;
                    //var maxTries = 3;
                    //while (true)
                    //{
                    //    try
                    //    {
                    //        var task = GetItemsFromPage(document, itemsCount);
                    //        var pageItems = task.Result;
                    //        foreach (var item in pageItems)
                    //        {
                    //            AliItems.Add(item);
                    //        }
                    //    }
                    //    catch (Exception ex)
                    //    {
                    //        Debug.WriteLine("Repeat web request to " + url);
                    //        if (tries >= maxTries)
                    //        {
                    //            Debug.WriteLine("Broken url: " + url);
                    //            BrokenUrls.Add(url);
                    //            break;                                
                    //        }
                    //        tries++;
                    //    }
                    //}                    

                    // Copy grabbed items to collection
                    foreach (var item in tmpItems)
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
                    MessageBox.Show("Web resource is not available. \n \n" +
                                    "- Check the connection to the Internet on your computer.\n" +
                                    "- Perhaps there was a server error, please try again.\n \n" +
                                    "Message: " + ex.Message,
                                    "Error!",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);                    
                }
                catch (AggregateException ex)
                {
                    Debug.WriteLine("Repeat web request to ");
                    // Not calculate unadded items
                    _itemNo -= _currItemNo + 2;
                }
            }
        }

        private void GetProxyServers()
        {
            var proxyPage = RetrievePage("http://www.sslproxies.org/");
            // Generate structured document
            var proxyParser = new HtmlParser();
            var proxyDoc = proxyParser.Parse(proxyPage);
            // Get proxy list
            var proxyIp = proxyDoc.QuerySelectorAll("table.fpltable > tbody > tr > td:nth-child(1)");
            var proxyPort = proxyDoc.QuerySelectorAll("table.fpltable > tbody > tr > td:nth-child(2)");
            for (int i = 0; i < proxyIp.Length; i++)
            {
                ProxyServers.Add(new ProxyServer
                {
                    Ip = proxyIp[i].TextContent,
                    Port = proxyPort[i].TextContent
                });
            }
            CurrentProxy = ProxyServers.First();
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

            //var itemPage = "";
            //var tries = 0;
            //var maxTries = 3;
            //while (true)
            //{
            //    try
            //    {
            //        // Get web page source code
            //        var itemClient = new WebClient();
            //        itemClient.Encoding = Encoding.UTF8;
            //        itemPage = await itemClient.DownloadStringTaskAsync(url);
            //        break;
            //    }
            //    catch (AggregateException ex)
            //    {
            //        Debug.WriteLine("Repeat web request to " + url);
            //        if (tries >= maxTries)
            //        {
            //            Debug.WriteLine("Broken url: " + url);
            //            BrokenUrls.Add(url);
            //            break;
            //        }
            //        tries++;
            //    }
            //}

            //return itemPage;
        }

        private IHtmlDocument RetrievePageProxy(string url)
        {
            IHtmlDocument document = null;
            var count = 0;
            var retries = 20;
            bool success = false;
            while (!success)
            {
                if (count >= retries)
                {
                    MessageBox.Show("All proxy servers was used! Try again later.");
                    break;
                }
                string page = "";
                Debug.WriteLine(CurrentProxy.Ip);
                Debug.WriteLine(CurrentProxy.Port);
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                WebProxy proxy = new WebProxy(CurrentProxy.Ip, int.Parse(CurrentProxy.Port));
                proxy.BypassProxyOnLocal = false;
                request.Proxy = proxy;
                request.Method = "GET";

                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    Stream receiveStream = response.GetResponseStream();
                    StreamReader readStream = null;

                    if (response.CharacterSet == null)
                    {
                        readStream = new StreamReader(receiveStream);
                    }
                    else
                    {
                        readStream = new StreamReader(receiveStream, Encoding.GetEncoding(response.CharacterSet));
                    }

                    page = readStream.ReadToEnd();

                    response.Close();
                    readStream.Close();
                    success = true;
                }

                // Generate structured document
                var parser = new HtmlParser();
                document = parser.Parse(page);
                count++;

                // check for aliexpress blocking
                var items = document.QuerySelectorAll("html.gr__login_aliexpress_com").ToList();
                if (items?.Count > 0)
                {
                    ChangeProxy();
                    success = false;
                }
                              
            }

            return document;
        }

        private void ChangeProxy()
        {
            int index = ProxyServers.IndexOf(CurrentProxy);
            if (index < ProxyServers.Count - 1)
                CurrentProxy = ProxyServers.ElementAt(index + 1);
            else
                GetProxyServers();
        }

        private string GetNextPageUrl(IHtmlDocument document)
        {
            var url = "";

            // Get link to the next page
            try
            {
                url = "http:";
                url += document.QuerySelectorAll("a.page-next")
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

            Debug.WriteLine("Items count at list: " + itemsUrls.Count);
            foreach (var i in itemsUrls) { Debug.WriteLine(i); }

            // Get items pages
            var RetrievePagesTask = new List<Task<string>>();
            foreach (var itemsUrl in itemsUrls)
            {
                var task = RetrievePageAsync(itemsUrl);
                RetrievePagesTask.Add(task);
            }

            // Get items
            _currItemNo = 0;
            foreach (var task in RetrievePagesTask)
            {
                // Check for background worker cancelation
                if (_bw.CancellationPending) { break; }

                // Wait for retrive page
                var itemPage = await task;

                // Get item from the page
                var aliItem = GetItemFromPage(itemPage, itemsUrls[_currItemNo]);

                // Check if item exist
                if (aliItem != null)
                {
                    aliItems.Add(aliItem);
                    _currItemNo++;
                    _itemNo++;
                }
                else
                {
                    //itemsCount--;
                }

                // Set progress bar value
                int percent = (int)(Convert.ToDouble(_itemNo) / Convert.ToDouble(itemsCount) * 100);
                _bw.ReportProgress(percent, String.Format("Items grabbing {0} of {1}", _itemNo, itemsCount));
                //_bw.ReportProgress(percent, String.Format("Proxy [" + CurrentProxy.Ip + ":" + CurrentProxy.Port + "]"
                //                                            + "  Items grabbing   {0} of {1}", _itemNo, itemsCount));

            }

            return aliItems;
        }

        private IEnumerable<string> GetItemsUrls(IHtmlDocument document)
        {
            var querySelector = "";
            // Check view type (list or gallery)
            var type = document.QuerySelectorAll("#view-list").First().GetAttribute("href");
            if (type == "javascript:void(0);")
            {
                querySelector = "li.list-item > div > div > div.detail > h3 > a";
            }
            else
            {
                querySelector = "div.lazy-load > ul > li > div.item > div.img > div.pic > a.picRind";
            }

            //var prodRawLinks = document.QuerySelectorAll("div.item > div.img > div.pic > a.picRind");
            var prodRawLinks = document.QuerySelectorAll(querySelector);
            foreach (var link in prodRawLinks)
            {                
                yield return "http:" + link.GetAttribute("href");
            }
        }

        private AliItem GetItemFromPage(string page, string url)
        {
            Debug.WriteLine("Item url: " + url);
            // check for html.gr__login_aliexpress_com
            // Generate structured document
            var parser = new HtmlParser();
            var document = parser.Parse(page);

            // Greate aukro item for storing data
            var aliItem = new AliItem();

            //// Get all data
            //// check for expired item
            //var expire = document.QuerySelectorAll("#timeLeftCounter")
            //    .First()
            //    .Text();

            //if (expire == "завершен")            
            //{
            //    return null;
            //}

            // Title
            aliItem.Title = document.QuerySelectorAll("div.detail-wrap > h1.product-name")
                .First()
                .Text();

            Debug.WriteLine("-- Title: " + aliItem.Title);

            // Price
            aliItem.Price = document.QuerySelectorAll("div.p-price-content > span.p-price")
                    .First()
                    .Text();

            Debug.WriteLine("-- Price: " + aliItem.Price);

            // Price currency
            aliItem.PriceCurrency = document.QuerySelectorAll("span.p-symbol")
                .First()
                .Text();

            Debug.WriteLine("-- Currency: " + aliItem.PriceCurrency);

            // Unit
            aliItem.Unit = document.QuerySelectorAll("div.quantity-info-main > span.p-unit")
                .First()
                .Text();

            Debug.WriteLine("-- Unit: " + aliItem.Unit);

            // Seller
            aliItem.Seller = document.QuerySelectorAll("dl.store-intro > dd.store-name > a.store-lnk")
                .First()
                .Text();

            Debug.WriteLine("-- Seller: " + aliItem.Seller);

            // Link
            aliItem.Link = url;

            // Description
            aliItem.Description = document.QuerySelectorAll("ul.product-property-list")
                .Last()
                .Text();
            // remove empty lines
            aliItem.Description = Regex.Replace(
                aliItem.Description,
                @"^\s+$[\r\n]*",
                "",
                RegexOptions.Multiline);

            // Image
            // get image link
            var imgUrl = document.QuerySelectorAll("a.ui-image-viewer-thumb-frame > img")
                .First()
                .GetAttribute("src");

            // load image
            try
            {
                aliItem.Image = new WebClient().DownloadData(imgUrl);

                Debug.WriteLine("-- Image size: " + aliItem.Image.Length + " bytes");
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
                var message = "";
                if (BrokenUrls.Count > 0)
                {
                    message = "Not all items was copied! \n";
                    message += "Broken links: \n";
                    foreach (var item in BrokenUrls)
                    {
                        message += item + "\n";
                    }

                }
                else
                {
                    message = "Items was successfully copied! \n";
                }
                // Show alert
                MessageBox.Show(message, 
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
            // Clear broken urls list
            BrokenUrls.Clear();
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
