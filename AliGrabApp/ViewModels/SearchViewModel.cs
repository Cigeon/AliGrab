﻿using System;
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
        private bool _itemsNotFound = false;

        public ProgressBarModel ProgressBar { get; set; }  
        public ControlModel ButtonGo { get; set; }
        public ObservableCollection<AliItem> AliItems { get; set; }
        public ObservableCollection<ProxyServer> ProxyServers { set; get; }
        public ProxyServer CurrentProxy { get; set; }
        public string Url { get; set; }
        

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

            // Get proxy servers list
            GetProxyServers();

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
                        Debug.WriteLine(itemsCount);
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
                    Debug.WriteLine(nextPageUrl);
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
                    MessageBox.Show("Web resource is not available. \n \n" +
                                    "- Check the connection to the Internet on your computer.\n" +
                                    "- Perhaps there was a server error, please try again.\n \n" +
                                    "Message: " + ex.Message,
                                    "Error!",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
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
        }

        private IHtmlDocument RetrievePageProxy(string url)
        {
            IHtmlDocument document = null;
            var count = 0;
            var retries = 20;
            bool success = false;
            while(!success && count < retries)
            {
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
                    continue;
                }
                success = true;                
            }

            return document;
        }

        private void ChangeProxy()
        {
            
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
                _bw.ReportProgress(percent, String.Format("Proxy [" + CurrentProxy.Ip + ":" + CurrentProxy.Port + "]"
                                                            + "  Items grabbing   {0} of {1}", _itemNo, itemsCount));

            }

            return aliItems;
        }

        private IEnumerable<string> GetItemsUrls(IHtmlDocument document)
        {
            //var prodRawLinks = document.QuerySelectorAll("div.item > div.img > div.pic > a.picRind");
            var prodRawLinks = document.QuerySelectorAll("a.picRind");
            foreach (var link in prodRawLinks)
            {
                Debug.WriteLine(link.GetAttribute("href"));
                yield return "http:" + link.GetAttribute("href");
            }
        }

        private AliItem GetItemFromPage(string page, string url)
        {
            // check for html.gr__login_aliexpress_com
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

            if (expire == "завершен)            
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
                ProgressBar.Content = "Get available proxy servers";
                ProgressBar.Visibility = Visibility.Visible;
                // Send current progress to status view
                OnSearchProgress?.Invoke(ProgressBar);
                // Disable start button
                ButtonGo.IsEnabled = false;
            }
        }

    }
}
