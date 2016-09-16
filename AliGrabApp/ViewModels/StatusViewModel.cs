using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using AliGrabApp.Commands;
using AliGrabApp.Models;
using System.Windows.Input;

namespace AliGrabApp.ViewModels
{
    public delegate void TaskCanceledHandler();
    public delegate void TaskStartedHandler();
    public delegate void TaskFinishedHandler();

    public class StatusViewModel : ViewModelBase
    {
        private bool _canExecute;
        private ICommand _cancelCommand;
        public ProgressBarModel ProgressBar { get; set; }
        public ButtonModel ButtonCancel { get; set; }

        public static event TaskCanceledHandler OnTaskCanceled;
        public static event TaskStartedHandler OnTaskStarted;
        public static event TaskFinishedHandler OnTaskFinished;

        public StatusViewModel()
        {
            // Commands status
            _canExecute = true;
            ProgressBar = new ProgressBarModel {Visibility = Visibility.Hidden, Content = "Ready"};
            ButtonCancel = new ButtonModel {Visibility = Visibility.Hidden};
            // Subscribe on progress bar event
            SearchViewModel.OnSearchProgress += UpdateProgressBar;
            ResultViewModel.OnProgress += UpdateProgressBar;
        }

        private void UpdateProgressBar(ProgressBarModel pb)
        {
            ProgressBar.Value = pb.Value;
            ProgressBar.Content = pb.Content;
            ProgressBar.Visibility = pb.Visibility;

            if (ProgressBar.Visibility == Visibility.Visible) OnTaskStarted?.Invoke();
            if (ProgressBar.Visibility == Visibility.Hidden) OnTaskFinished?.Invoke();
        }

        public ICommand CancelCommand
        {
            get
            {
                return _cancelCommand ?? (_cancelCommand = new CommandHandler(() => Cancel(), _canExecute));
            }
        }

        public void Cancel()
        {
            OnTaskCanceled?.Invoke();
        }

    }
}
