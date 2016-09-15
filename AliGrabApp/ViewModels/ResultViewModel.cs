using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Entity;
using System.Data.Entity.Core;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AliGrabApp.Models;
using Microsoft.Win32;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace AliGrabApp.ViewModels
{
    public delegate void DbChangedHandler();

    public class ResultViewModel : ViewModelBase
    {
        private bool _canExecute;
        private ICommand _saveDbCommand;
        private ICommand _exportCommand;
        private BackgroundWorker _bw1 = new BackgroundWorker();
        private BackgroundWorker _bw2 = new BackgroundWorker();

        public ObservableCollection<AliItem> AliItems { get; set; }
        public string QueryText { get; set; }
        public ProgressBarModel ProgressBar { get; set; }
        public StatusBarModel StatusBar { get; set; }
        public ButtonModel Buttons { get; set; }


        public static event DbChangedHandler OnDbChanged = delegate { };

        public ResultViewModel()
        {
            // Init
            ProgressBar = new ProgressBarModel();
            StatusBar = new StatusBarModel();
            Buttons = new ButtonModel();
            AliItems = new ObservableCollection<AliItem>();
            // Commands status
            _canExecute = true;
            // Background worker1 settings
            _bw1.WorkerReportsProgress = true;
            _bw1.WorkerSupportsCancellation = true;
            _bw1.ProgressChanged += ProgressChanged;
            _bw1.DoWork += DoSaveDb;
            _bw1.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
            // Background worker2 settings
            _bw2.WorkerReportsProgress = true;
            _bw2.WorkerSupportsCancellation = true;
            _bw2.ProgressChanged += ProgressChanged;
            _bw2.DoWork += DoExport;
            _bw2.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
            // Change app status
            StatusBar.Status = "Готов";
            // Hide progress bar
            ProgressBar.Visibility = Visibility.Hidden;
            // Enable buttons
            Buttons.IsEnabled = true;
            // Subscribe on all items grabbed event
            SearchViewModel.OnItemsGrabbed += ShowResult;
            // Subscribe on open saved items event
            ExplorerViewModel.OnItemsOpened += ShowResult;
        }


        public void OnWindowClosed(object sender, EventArgs e)
        {
            // background worker cancalation
            _bw1.CancelAsync();
            _bw2.CancelAsync();
        }

        private void ShowResult(ObservableCollection<AliItem> items)
        {
            AliItems.Clear();
            foreach (var item in items)
            {
                AliItems.Add(new AliItem
                {
                    Image = item.Image,
                    AliId = item.AliId,
                    Title = item.Title,
                    Type = item.Type,
                    Seller = item.Seller,
                    Price = item.Price,
                    PriceCurrency = item.PriceCurrency,
                    Description = item.Description,
                    Link = item.Link
                });
            }
        }

        public ICommand SaveDbCommand
        {
            get
            {
                return _saveDbCommand ?? (_saveDbCommand = new Commands.CommandHandler(() => _bw1.RunWorkerAsync(), _canExecute));
            }
        }

        public ICommand ExportCommand
        {
            get
            {
                return _exportCommand ?? (_exportCommand = new Commands.CommandHandler(() => _bw2.RunWorkerAsync(), _canExecute));
            }
        }

        private void DoSaveDb(object sender, DoWorkEventArgs e)
        {
            // Disable buttons
            Buttons.IsEnabled = false;
            // Change app status
            StatusBar.Status = "Сохранение товаров ...";
            // Show progressbar
            ProgressBar.Visibility = Visibility.Visible;

            try
            {
                using (AliContext db = new AliContext())
                {
                    var group = new AliGroupModel();
                    group.Name = "Group default name (later change)";
                    group.Created = DateTime.Now;
                    group.Items = new ObservableCollection<AliItemModel>();

                    // Add all items
                    int counter = 0;
                    int itemsCount = AliItems.Count;
                    foreach (var item in AliItems)
                    {
                        var itemModel = new AliItemModel
                        {
                            AliId = item.AliId,
                            Title = item.Title,
                            Type = item.Type,
                            Price = item.Price,
                            PriceCurrency = item.PriceCurrency,
                            Seller = item.Seller,
                            Link = item.Link,
                            Description = item.Description,
                            Image = item.Image
                        };
                        group.Items.Add(itemModel);

                        // Set progress bar value
                        counter++;
                        int percent = (int)(Convert.ToDouble(counter) / Convert.ToDouble(itemsCount) * 100);
                        _bw2.ReportProgress(percent, String.Format("{0} из {1}", counter, itemsCount));
                    }
                    db.Groups.Add(group);
                    db.SaveChanges();
                }

                // Fire database changed event to refresh main window
                OnDbChanged();

                // Show message box
                // Show alert
                MessageBox.Show("Товары успешно сохранены!",
                                "Инфо",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
            }
            catch (EntityException ex)
            {
                // Show message box
                MessageBox.Show("Невозможно подключиться к базе данных. \n \n" +
                                "Проверьте наличие экземпляра базы данных MS SQL Server LocalDB на Вашем компьютере.\n \n" +
                                ex.Message,
                                "Ошибка!",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }


        }

        private void DoExport(object sender, DoWorkEventArgs e)
        {
            // Disable buttons
            Buttons.IsEnabled = false;

            try
            {
                // Show save file dialog
                var fileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    AddExtension = true,
                    DefaultExt = ".xlsx",
                    Filter = "Microsoft Excel (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                    Title = "Экспорт в файл"
                };

                var result = fileDialog.ShowDialog();
                if (result != true) return;
                var file = new FileStream(fileDialog.FileName, FileMode.Create);

                // Change app status
                StatusBar.Status = "Экспорт товаров в Excel ...";

                // Show progressbar
                ProgressBar.Visibility = Visibility.Visible;

                using (var ep = new ExcelPackage(file))
                {

                    ep.Workbook.Worksheets.Add("Aukro товары");
                    var ws = ep.Workbook.Worksheets[1];

                    // Create header
                    ws.Cells[1, 1].Value = "Изображение";
                    ws.Cells[1, 2].Value = "Id товара";
                    ws.Cells[1, 3].Value = "Название";
                    ws.Cells[1, 4].Value = "Цена";
                    ws.Cells[1, 5].Value = "Валюта";
                    ws.Cells[1, 6].Value = "Продавец";
                    ws.Cells[1, 7].Value = "Описание";
                    ws.Cells[1, 8].Value = "Url товара";

                    // Set column width
                    ws.Column(1).Width = 20.0;
                    ws.Column(2).Width = 12.0;
                    ws.Column(3).Width = 60.0;
                    ws.Column(4).Width = 10.0;
                    ws.Column(5).Width = 7.0;
                    ws.Column(6).Width = 15.0;
                    ws.Column(7).Width = 40.0;
                    ws.Column(8).Width = 80.0;

                    // Set alignment
                    for (int i = 2; i <= 8; i++)
                    {
                        ws.Column(i).Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                        ws.Column(i).Style.HorizontalAlignment =
                            i < 7 ? ExcelHorizontalAlignment.Center : ExcelHorizontalAlignment.Left;
                    }


                    // Add all items
                    int rowNo = 2;
                    int counter = 0;
                    int itemsCount = AliItems.Count;
                    foreach (var item in AliItems)
                    {
                        // Get image
                        using (var ms = new MemoryStream(item.Image))
                        {
                            using (var img = System.Drawing.Image.FromStream(ms))
                            {
                                if (img != null)
                                {
                                    var cellHeight = 100.0;                                         // magic number
                                                                                                    // Set row height to accommodate the picture
                                    ws.Row(rowNo).Height = cellHeight;

                                    // Add picture to cell
                                    var pic = ws.Drawings.AddPicture("Picture" + rowNo, img);
                                    // Position picture on desired column
                                    pic.From.Column = 0;
                                    pic.From.Row = rowNo - 1;
                                    // Set picture size to fit inside the cell
                                    int imageWidth = (int)(img.Width * cellHeight / img.Height);
                                    int imageHeight = (int)cellHeight;
                                    pic.SetSize(imageWidth, imageHeight);
                                }
                            }
                        }


                        ws.Cells[rowNo, 2].Value = item.AliId;
                        ws.Cells[rowNo, 3].Value = item.Title;
                        ws.Cells[rowNo, 4].Value = item.Price;
                        ws.Cells[rowNo, 5].Value = item.PriceCurrency;
                        ws.Cells[rowNo, 6].Value = item.Seller;
                        ws.Cells[rowNo, 7].Value = item.Description;
                        ws.Cells[rowNo, 8].Value = item.Link;
                        rowNo++;

                        // Set progress bar value
                        counter++;
                        int percent = (int)(Convert.ToDouble(counter) / Convert.ToDouble(itemsCount) * 100);
                        _bw2.ReportProgress(percent, String.Format("{0} из {1}", counter, itemsCount));

                        // Check for background worker cancelation
                        if (_bw2.CancellationPending)
                        {
                            e.Cancel = true;
                            // Finalize export
                            ep.Save();
                            file.Flush();
                            file.Close();

                            // Show alert
                            MessageBox.Show("Экспорт товаров прерван!",
                                            "Инфо",
                                            MessageBoxButton.OK,
                                            MessageBoxImage.Information);
                            return;
                        }
                    }
                    ep.Save();


                }
                // Finalize export
                file.Flush();
                file.Close();

                // Show alert
                MessageBox.Show("Экспорт товаров успешно выполнен!",
                                "Инфо",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Невозможно экспортировать данные в файл Excel. \n \n" +
                                "- Возможно у Вас нет прав для записи данных в выбранный Вами " +
                                "каталог. Попробуйте сохранить файл в другую директорию." +
                                "- Возможно данные повреждены. Попробуйте повторить Ваш поисковый запрос." +
                                ex.Message,
                                "Ошибка!",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }

        }

        private void ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            // Set current progress
            ProgressBar.Percentage = e.ProgressPercentage;
            ProgressBar.Progress = e.UserState.ToString();
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (!e.Cancelled)
            {

            }

            // Change app status
            StatusBar.Status = "Готов";
            // Hide progressbar
            ProgressBar.Percentage = 0;
            ProgressBar.Progress = "";
            ProgressBar.Visibility = Visibility.Hidden;
            //Enable buttons
            Buttons.IsEnabled = true;
        }

    }

}
