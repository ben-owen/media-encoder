// Copyright 2020 Ben Owen
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;

namespace MovieEncoder
{
    /// <summary>
    /// Interaction logic for ProgressPage.xaml
    /// </summary>
    public partial class ProgressPage : Page, INotifyPropertyChanged
    {
        private ProgressReporter ProgressReporter;

        public event PropertyChangedEventHandler PropertyChanged;

        public string StatusColor
        {
            get { return ProgressReporter.IsError ? "#FFF75252" : "#4E87D4"; }
        }

        public ProgressPage()
        {
            ProgressReporter = ((App)Application.Current).ProgressReporter;
            ProgressReporter.PropertyChanged += PropertyReporter_PropertyChanged;

            InitializeComponent();

            DataContext = ProgressReporter;

            //jobs.Add(new JobInfo("Test A"));

            JobListBox.ItemsSource = ProgressReporter.JobQueue.GetJobs();

            //jobs.Add(new JobInfo("Test B"));
        }

        private void PropertyReporter_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsError")
            {
                OnPropertyChanged("StatusColor");
            }
            else if (e.PropertyName == "CurrentJob")
            {
                //jobs.Clear();
                //jobs.AddRange(new List<JobInfo>(Reporter.JobHistory));
                /*
                if (Reporter.CurrentJob != null && !jobs.Contains(Reporter.CurrentJob))
                {
                    jobs.Add(Reporter.CurrentJob);
                }
                */
            }
            else if (e.PropertyName == "MaxProgress")
            {
                /*if (ProgressReporter.JobQueue.GetQueue().Contains(ProgressReporter.CurrentJob))
                {
                    int index = ProgressReporter.JobQueue.GetQueue().IndexOf(ProgressReporter.CurrentJob);
                    ProgressReporter.JobQueue.GetQueue().ElementAt(index).MaxProgress = ProgressReporter.MaxProgress;
                }
                */
            }
            else if (e.PropertyName == "CurrentProgress")
            {
                /*
                if (jobs.Contains(ProgressReporter.CurrentJob))
                {
                    int index = jobs.IndexOf(ProgressReporter.CurrentJob);
                    jobs.ElementAt(index).CurrentProgress = ProgressReporter.CurrentProgress;
                }
                */
            }
        }

        private void StopEncoding_Click(object sender, RoutedEventArgs e)
        {
            ((App)Application.Current).EncoderService.Stop();

            this.NavigationService.GoBack();
            this.NavigationService.RemoveBackEntry();
        }

        private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ((TextBox)sender).ScrollToEnd();
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
