using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AliGrabApp.Models;

namespace AliGrabApp.ViewModels
{
    public class StatusViewModel : ViewModelBase
    {
        public ProgressBarModel ProgressBar { get; set; }

        public StatusViewModel()
        {
            ProgressBar = new ProgressBarModel();
            SearchViewModel.OnSearchProgress += UpdateProgressBar;
        }

        private void UpdateProgressBar(ProgressBarModel pb)
        {
            ProgressBar.Value = pb.Value;
            ProgressBar.Content = pb.Content;
            ProgressBar.Percentage = pb.Percentage;
            ProgressBar.Progress = pb.Progress;
            ProgressBar.Visibility = pb.Visibility;
        }
    }
}
