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
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace MovieEncoder
{
    /// <summary>
    /// Interaction logic for ProgressPage.xaml
    /// </summary>
    public partial class ProgressPage : Page, INotifyPropertyChanged
    {
        private Job _currentJob;

        private readonly ProgressReporter ProgressReporter;
        private System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();

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
            LogRichTextBox.Document = ProgressReporter.LogDocument;
        }

        private void PropertyReporter_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsError")
            {
                OnPropertyChanged("StatusColor");
            }
            else if (e.PropertyName == "Started")
            {
                OnPropertyChanged("RunButtonString");
            }
            else if (e.PropertyName == "CurrentJob")
            {
                // scroll to that job in the task list
                if (Application.Current != null)
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        Job job = ((ProgressReporter)sender).CurrentJob;
                        if (job != null && _currentJob != job)
                        {
                            ListBoxItem item = (ListBoxItem)JobListBox.ItemContainerGenerator.ContainerFromItem(job);
                            if (item == null)
                            {
                                JobListBox.UpdateLayout();
                                item = (ListBoxItem)JobListBox.ItemContainerGenerator.ContainerFromItem(job);
                            }
                            if (item != null && item.Content == job)
                            {
                                item.BringIntoView();
                            }
                        }
                        _currentJob = job;
                    }));
                }
            }
            else if (e.PropertyName == "Shutdown")
            {
                OnPropertyChanged("RunButtonString");
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
            Dispatcher.BeginInvoke(new Action(() => {
                ((RichTextBox)sender).ScrollToEnd();
            }));
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public string RunButtonString
        {
            get { return ProgressReporter.Shutdown ? "Start" : "Stop"; }
        }

        private void ListBoxItem_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem)
            {
                Job job = (Job)((ListBoxItem)sender).Content;
                if (job != null)
                {
                    // scroll to the log entries
                    TextElement block = ProgressReporter.GetLogDocumentBlock(job);
                    if (block != null)
                    {
                        double top = block.ContentStart.GetPositionAtOffset(0).GetCharacterRect(LogicalDirection.Forward).Top;
                        LogRichTextBox.ScrollToVerticalOffset(LogRichTextBox.VerticalOffset + top);
                        LogRichTextBox.ScrollToHorizontalOffset(0);
                    }
                }
            }
        }
    }
}
