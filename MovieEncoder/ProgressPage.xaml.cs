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
using System.Windows.Documents;

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
            LogRichTextBox.Document = ProgressReporter.LogDocument;
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
            ((RichTextBox)sender).ScrollToEnd();
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public string RunButtonString
        {
            get { return ((App)Application.Current).EncoderService.IsStarted() ? "Stop" : "Start"; }
        }

        private void JobListBoxItem_Selected(object sender, RoutedEventArgs e)
        {
            Job job = (Job)((ListBoxItem)e.Source).Content;
            if (job != null)
            {
                // scroll to the log entries
                TextElement block = ProgressReporter.GetLogDocumentBlock(job);
                if (block != null)
                {
                    double top = block.ContentStart.GetPositionAtOffset(0).GetCharacterRect(LogicalDirection.Forward).Top;
                    double bottom = block.ContentStart.GetPositionAtOffset(0).GetCharacterRect(LogicalDirection.Forward).Bottom;
                    if (block.GetType() == typeof(TableRow))
                    {
                        System.Console.WriteLine($"{job.JobName} = Txt: {((Run)((Paragraph)((TableRow)block).Cells[1].Blocks.FirstBlock).Inlines.LastInline).Text}");
                        TextPointer start = ((Paragraph)((TableRow)block).Cells[0].Blocks.FirstBlock).Inlines.FirstInline.ContentStart;
                        TextPointer end = ((Paragraph)((TableRow)block).Cells[1].Blocks.FirstBlock).Inlines.FirstInline.ContentEnd;
                        LogRichTextBox.Selection.Select(start, end);
                        top = start.GetCharacterRect(LogicalDirection.Forward).Top;
                    }
                    System.Console.WriteLine($"{job.JobName} = Sel: {LogRichTextBox.Selection.Start.GetCharacterRect(LogicalDirection.Backward).Bottom}");
                    System.Console.WriteLine($"{job.JobName} = Top: {top}");
                    System.Console.WriteLine($"{job.JobName} = Tst: {block.ContentStart.GetPositionAtOffset(0).GetCharacterRect(LogicalDirection.Forward).Top}");
                    LogRichTextBox.ScrollToVerticalOffset(LogRichTextBox.VerticalOffset + top);
                    LogRichTextBox.ScrollToHorizontalOffset(0);
                }
            }
        }

        private void JobListBoxItem_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

        }
    }
}
