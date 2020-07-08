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
using System.Windows.Media;

namespace MovieEncoder
{
    /// <summary>
    /// Interaction logic for ProgressPage.xaml
    /// </summary>
    public partial class ProgressPage : Page, INotifyPropertyChanged
    {
        private Job _currentJob;
        private ScrollViewer _logRichTextBoxScroll;
        private readonly ProgressReporter ProgressReporter;
        private readonly System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
        private bool _isLogScrollAtEnd = false;

        public event PropertyChangedEventHandler PropertyChanged;


        public string StatusColor
        {
            get { return ProgressReporter.IsError ? "#FFF75252" : "#4E87D4"; }
        }

        public ProgressPage()
        {
            ProgressReporter = ((App)Application.Current).ProgressReporter;
            ProgressReporter.PropertyChanged += PropertyReporter_PropertyChanged;
            stopwatch.Start();

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
                Action callback = new Action(() =>
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
                                    });
                if (Dispatcher.CheckAccess())
                {
                    callback.Invoke();
                }
                else
                {
                    Dispatcher.InvokeAsync(callback);
                }
            }
            else if (e.PropertyName == "Shutdown")
            {
                OnPropertyChanged("RunButtonString");
            }
            else if (e.PropertyName == "LogDocument")
            {
                if (_logRichTextBoxScroll != null)
                {
                    if (_isLogScrollAtEnd)
                    {
                        _logRichTextBoxScroll.ScrollToEnd();
                    }
                }
            }
        }

        private void StopEncoding_Click(object sender, RoutedEventArgs e)
        {
            if (!((App)Application.Current).EncoderService.IsStarted())
            {
                ((App)Application.Current).EncoderService.Start();
            }
            else
            {
                ((App)Application.Current).EncoderService.Stop();
            }
            ProgressReporter.Reset();
            OnPropertyChanged("RunButtonString");
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
            if (sender is ListBoxItem item)
            {
                Job job = (Job)item.Content;
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

        private void LogRichTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            _logRichTextBoxScroll = (ScrollViewer)((Border)VisualTreeHelper.GetChild(LogRichTextBox, 0)).Child;
            _logRichTextBoxScroll.ScrollChanged += LogRichTextBoxScroll_ScrollChanged;
        }

        private void LogRichTextBoxScroll_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            ScrollViewer scroll = (ScrollViewer)sender;
            _isLogScrollAtEnd = scroll.VerticalOffset == scroll.ScrollableHeight;
        }
    }
}
