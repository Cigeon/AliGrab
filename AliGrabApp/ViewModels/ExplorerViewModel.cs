using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Entity;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AliGrabApp.Commands;
using AliGrabApp.Models;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Logical;

namespace AliGrabApp.ViewModels
{
    public delegate void ItemsOpenedHandler(ObservableCollection<AliItem> items);

    public class ExplorerViewModel : ViewModelBase
    {
        private bool _canExecute;
        //private ICommand _startSearchCommand;
        private ICommand _testCommand;
        private ICommand _openCollectionCommand;
        private ICommand _deleteCollectionCommand;
        private BackgroundWorker _bw = new BackgroundWorker();

        public ControlModel LoadingAnimationModel { get; set; }
        public ObservableCollection<AliGroup> AliGroups { get; set; }

        public static event ItemsOpenedHandler OnItemsOpened;

        public ExplorerViewModel()
        {
            // Background worker1 settings
            _bw.WorkerReportsProgress = true;
            _bw.WorkerSupportsCancellation = true;
            _bw.ProgressChanged += ProgressChanged;
            _bw.DoWork += DoWork;
            _bw.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
            // Creates group collection
            AliGroups = new ObservableCollection<AliGroup>();
            // Commands status
            _canExecute = true;
            // Initialize and hide loading animation
            LoadingAnimationModel = new ControlModel { Visibility = Visibility.Hidden };
            // Run background worker
            _bw.RunWorkerAsync();
            // Subscribe for database changed event
            ResultViewModel.OnDbChanged += LoadGroups;


        }

        public void DoWork(object sender, DoWorkEventArgs e)
        {
            // Load groups from database
            LoadGroups();
        }

        public void LoadGroups()
        {
            // Show loading animation
            LoadingAnimationModel.Visibility = Visibility.Visible;

            Application.Current.Dispatcher.Invoke((Action)(() =>
            {
                try
                {
                    // Clear collection
                    AliGroups.Clear();

                    // Get groups from db 
                    using (var db = new AliContext())
                    {
                        var query = from g in db.Groups
                                    select new
                                    {
                                        g.Id,
                                        g.Name,
                                        g.Created,
                                        g.Items
                                    };

                        if (query != null)
                        {
                            foreach (var group in query)
                            {
                                AliGroups.Add(new AliGroup
                                {
                                    Id = group.Id,
                                    Created = group.Created,
                                    Name = group.Name,
                                    Items = group.Items
                                });
                            }
                        }

                        LoadingAnimationModel.Visibility = Visibility.Hidden;
                    }
                }
                catch (SqlException ex)
                {
                    MessageBox.Show("Невозможно подключиться к базе данных. \n \n" +
                                    "Проверьте наличие экземпляра базы данных MS SQL Server LocalDB на Вашем компьютере.\n \n" +
                                    ex.Message,
                                    "Ошибка!",
                                    MessageBoxButton.OK,
                                    MessageBoxImage.Error);
                    LoadingAnimationModel.Visibility = Visibility.Hidden;
                }

            }));
        }

        private void ProgressChanged(object sender, ProgressChangedEventArgs e)
        { }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (!e.Cancelled)
            {

            }

            // Hide loading animation
            LoadingAnimationModel.Visibility = Visibility.Hidden;
        }

        // Start search
        //public ICommand StartSearchCommand
        //{
        //    get
        //    {
        //        return _startSearchCommand ?? (_startSearchCommand = new Commands.CommandHandler(StartSearch, _canExecute));
        //    }
        //}

        //public void StartSearch()
        //{
        //    // Creates search window ----------------------------------- !!!!
        //    var searchWindow = new SearchWindow();
        //    searchWindow.ShowDialog();
        //}

        // Open saved collection
        public ICommand OpenCollectionCommand
        {
            get
            {
                return _openCollectionCommand ?? (_openCollectionCommand = new CommandHandlerParam(param => OpenCollection(param), _canExecute));
            }
        }

        public void OpenCollection(object param)
        {
            try
            {
                var aliItems = new ObservableCollection<AliItem>();
                // Get group by id
                var ai = AliGroups.First(g => g.Id.Equals(param)).Items;

                // Convert AukroItemModel to AukroItem
                foreach (var item in ai)
                {
                    aliItems.Add(new AliItem
                    {
                        Id = item.Id,
                        AliId = item.AliId,
                        Title = item.Title,
                        Type = item.Type,
                        Price = item.Price,
                        PriceCurrency = item.PriceCurrency,
                        Seller = item.Seller,
                        Link = item.Link,
                        Description = item.Description,
                        Image = item.Image
                    });
                }

                OnItemsOpened?.Invoke(aliItems);
                // Open result window ------------------------------------------------------- !!!!
                /*var resultWindow = new PreviewWindow();
                resultWindow.AliItems = aliItems;
                resultWindow.Show();*/
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message,
                                "Ошибка!",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        // Delete existing collection
        public ICommand DeleteCollectionCommand
        {
            get
            {
                return _deleteCollectionCommand ?? (_deleteCollectionCommand = new CommandHandlerParam(param => DeleteCollection(param), _canExecute));
            }
        }


        public void DeleteCollection(object param)
        {
            try
            {
                using (var db = new AliContext())
                {
                    // Select group and items for deletion
                    var gs = db.Groups.Where(g => g.Id == (Guid)param).Include(i => i.Items).ToList();

                    // Delete group
                    foreach (var g in gs) { db.Groups.Remove(g); }
                    db.SaveChanges();

                    // Delete group from datagrid
                    // Get group by id
                    var group = AliGroups.First(g => g.Id == (Guid)param);
                    // Remove current group
                    AliGroups.Remove(group);

                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message,
                                "Ошибка!",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }
    }
}
