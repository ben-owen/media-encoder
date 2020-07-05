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
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Threading;

namespace MovieEncoder
{
    public enum LogEntryType
    {
        Normal,
        Info,
        Error,
    }

    public class ProgressReporter : INotifyPropertyChanged
    {
        private string _currentTask;
        private readonly StringBuilder _log = new StringBuilder();
        private bool _isError = false;
        private string _remaining = "Test";
        private Job _currentJob;
        private readonly Dictionary<Job, TextElement> _jobLogPosition = new Dictionary<Job, TextElement>();
        private Table _logTable;
        private bool _shutdown;

        public Job CurrentJob
        {
            get { return _currentJob; }
            set
            {
                _currentJob = value;
                if (_currentJob != null)
                {
                    MaxProgress = _currentJob.MaxProgress;
                    CurrentProgress = _currentJob.CurrentProgress;
                }
                OnPropertyChanged();
            }
        }

        public JobQueue JobQueue { get; } = new JobQueue();

        public double MaxProgress
        {
            get
            {
                return _currentJob != null ? _currentJob.MaxProgress : 100;
            }
            set
            {
                if (_currentJob != null)
                {
                    _currentJob.MaxProgress = value;
                    OnPropertyChanged();
                }
            }
        }
        public double CurrentProgress
        {
            get
            {
                return _currentJob != null ? _currentJob.CurrentProgress : 0;
            }
            set
            {
                if (_currentJob != null)
                {
                    _currentJob.CurrentProgress = value;
                    OnPropertyChanged();
                }
            }
        }

        public string CurrentTask
        {
            get { return _currentTask; }
            set
            {
                _currentTask = value;
                AppendLog(value, LogEntryType.Normal);
                OnPropertyChanged();
                OnPropertyChanged("Log");
            }
        }

        public bool IsError
        {
            get { return _isError; }
            set
            {
                _isError = value;
                OnPropertyChanged("IsProgressShown");
                OnPropertyChanged();
            }
        }

        public bool Shutdown
        {
            get { return _shutdown; }
            set { _shutdown = value; OnPropertyChanged(); }
        }

        public Visibility IsProgressShown
        {
            get { return _isError ? Visibility.Hidden : Visibility.Visible; }
        }

        public string Log
        {
            get { return this._log.ToString(); }
        }

        public FlowDocument LogDocument { get; internal set; }

        public string Remaining
        {
            get { return _remaining; }
            set { _remaining = value; OnPropertyChanged(); }
        }

        public Dispatcher Dispatcher { get; internal set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public ProgressReporter()
        {
            JobQueue.EnableCollectionSynchronization();
            _currentTask = "Stopped";
            _isError = false;
            Remaining = "";
            LogDocument = new FlowDocument();

            CreateLogDocumentTable();
        }

        internal void ClearLog()
        {
            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                LogDocument.Blocks.Clear();
                CreateLogDocumentTable();
            }));

            JobQueue.ClearJobLog();
            OnPropertyChanged("LogDocument");
        }

        internal TextElement GetLogDocumentBlock(Job job)
        {
            TextElement block = null;
            if (_jobLogPosition.ContainsKey(job))
                block = _jobLogPosition[job];
            return block;
        }

        private void CreateLogDocumentTable()
        {
            _logTable = new Table();
            _logTable.RowGroups.Add(new TableRowGroup());

            TableColumn col1 = new TableColumn();
            col1.Width = new GridLength(120, GridUnitType.Pixel);
            col1.Background = Brushes.AliceBlue;
            _logTable.Columns.Add(col1);

            TableColumn col2 = new TableColumn();
            col2.Width = new GridLength(100, GridUnitType.Star);
            _logTable.Columns.Add(col2);

            LogDocument.Blocks.Add(_logTable);
        }

        public void UpdateProgress(double total, double current)
        {
            this.MaxProgress = total;
            this.CurrentProgress = current;
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        internal void Reset()
        {
            CurrentJob = null;

            IsError = false;
            CurrentProgress = 0.0;
            MaxProgress = 100.0;
            Remaining = "";
        }

        internal void AddError(string message)
        {
            _currentTask = $"ERROR: {message}";
            AppendLog(_currentTask, LogEntryType.Error);
            IsError = true;
        }

        internal void AppendLog(string message, LogEntryType type = LogEntryType.Normal)
        {
            string msg = message.Trim();
            if (msg != "")
            {
                DateTime now = DateTime.Now;

                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    TableRow row = new TableRow
                    {
                        Tag = new object[] { _currentJob, type }
                    };

                    Paragraph paragraph = new Paragraph();
                    paragraph.Margin = new Thickness(0);

                    Run dtr = new Run(now.ToString("yyyy-MM-dd HH:mm:ss - "));
                    row.Cells.Add(new TableCell(new Paragraph(dtr)));

                    Run msgr = new Run(msg);

                    row.Cells.Add(new TableCell(new Paragraph(msgr)));

                    ColorRow(row, true);
                    _logTable.RowGroups[0].Rows.Add(row);

                    // Add log position
                    if (_currentJob != null && !_jobLogPosition.ContainsKey(_currentJob))
                    {
                        _jobLogPosition.Add(_currentJob, row);
                    }

                    // Setup size for columns
                    Size measure = MeasureLogDocumentString(dtr, dtr.Text);
                    Size dtSize = MeasureLogDocumentString(dtr, dtr.Text);
                    if (_logTable.Columns[0].Width.Value < dtSize.Width)
                    {
                        _logTable.Columns[0].Width = new GridLength(dtSize.Width);
                    }

                    // Do not overload the system
                    System.Threading.Thread.Sleep(10);
                    OnPropertyChanged("LogDocument");
                }));
            }
        }

        internal void ReColorLog(bool normal = true)
        {
            // go through each row
            foreach (TableRow row in _logTable.RowGroups[0].Rows)
            {
                ColorRow(row, normal);
            }
        }

        private void ColorRow(TableRow row, bool normal = true)
        {
            System.Diagnostics.Debug.Assert(row.Cells.Count == 2);
            System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                Block dt = row.Cells[0].Blocks.FirstBlock;
                Block entry = row.Cells[1].Blocks.FirstBlock;
                Job job = (Job)((object[])row.Tag)[0];
                LogEntryType type = (LogEntryType)((object[])row.Tag)[1];
                if (normal == true)
                {
                    dt.Foreground = Brushes.Gray;

                    switch (type)
                    {
                        case LogEntryType.Error:
                            entry.Foreground = Brushes.Red;
                            break;
                        case LogEntryType.Normal:
                            entry.Foreground = Brushes.Black;
                            break;
                        case LogEntryType.Info:
                            entry.Foreground = Brushes.Gray;
                            break;
                    }
                }
                else
                {
                    dt.Foreground = Brushes.LightGray;
                    entry.Foreground = Brushes.Gray;
                }
            }));
        }

        private Size MeasureLogDocumentString(TextElement element, string candidate)
        {
            var formattedText = new FormattedText(
                candidate,
                CultureInfo.CurrentCulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface(element.FontFamily, element.FontStyle, element.FontWeight, element.FontStretch),
                element.FontSize,
                Brushes.Gray,
                new NumberSubstitution(),
                1);

            return new Size(formattedText.Width, formattedText.Height);
        }
    }
}
