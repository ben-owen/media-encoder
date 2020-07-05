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

            JobListBox.ItemsSource = ProgressReporter.JobQueue.GetJobs();
        }

        private void PropertyReporter_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsError")
            {
                OnPropertyChanged("StatusColor");
            }
        }

        private void StopEncoding_Click(object sender, RoutedEventArgs e)
        {
            if (!((App)Application.Current).EncoderService.IsStarted())
            {
                ((App)Application.Current).EncoderService.Start();
            } else
            {
                ((App)Application.Current).EncoderService.Stop();
            }
            ProgressReporter.Reset();
            OnPropertyChanged("RunButtonString");
        }

        private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ((TextBox)sender).ScrollToEnd();
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public string RunButtonString
        {
            get { return ((App)Application.Current).EncoderService.IsStarted() ? "Stop" : "Start"; }
        }
    }
}
