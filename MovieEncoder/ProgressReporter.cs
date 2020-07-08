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
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace MovieEncoder
{
    public enum LogEntryType
    {
        Info,
        Debug,
        Error,
        Trace,
    }

    public class ProgressReporter : INotifyPropertyChanged
    {
        private string _currentTask;
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

                    // Add a new log line 
                    AddLogLine();
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
                AppendLog(value, LogEntryType.Info);
                OnPropertyChanged();
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

            Application.Current.Dispatcher.Invoke(() =>
            {
                LogDocument = new FlowDocument();
                CreateLogDocumentTable();
            });
        }

        internal void ClearLog()
        {
            _logTable.Dispatcher.Invoke(new Action(() =>
            {
                // find the current job log
                List<TableRow> jobRows = new List<TableRow>();
                if (_currentJob != null)
                {
                    foreach (TableRow row in _logTable.RowGroups[0].Rows)
                    {
                        if (row.Tag != null && ((object[])row.Tag)[0] == _currentJob)
                        {
                            jobRows.Add(row);
                        }
                    }
                }
                _jobLogPosition.Clear();
                _logTable.RowGroups[0].Rows.Clear();
                if (jobRows.Count > 0)
                {
                    foreach (TableRow row in jobRows)
                    {
                        _logTable.RowGroups[0].Rows.Add(row);
                        _jobLogPosition.Add((Job)((object[])row.Tag)[0], row);
                    }
                }
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

            TableColumn col1 = new TableColumn
            {
                Width = new GridLength(120, GridUnitType.Pixel),
                Background = Brushes.AliceBlue
            };
            _logTable.Columns.Add(col1);

            TableColumn col2 = new TableColumn
            {
                Width = new GridLength(100, GridUnitType.Star)
            };
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
            Remaining = "";
            OnPropertyChanged("MaxProgress");
            OnPropertyChanged("CurrentProgress");
        }

        internal void AddError(string message)
        {
            _currentTask = $"ERROR: {message}";
            AppendLog(_currentTask, LogEntryType.Error);
            IsError = true;
        }

        internal void AppendLog(string message, LogEntryType type = LogEntryType.Info)
        {
            if (message == null)
            {
                return;
            }
            string msg = message.Trim();
            if (msg != "")
            {
                if (type == LogEntryType.Trace)
                {
                    Debug.WriteLine(message);
                    return;
                }

                Action callback = new Action(() =>
                                        {
                                            DateTime now = DateTime.Now;

                                            TableRow row = new TableRow
                                            {
                                                Tag = new object[] { _currentJob, type }
                                            };

                                            Paragraph paragraph = new Paragraph
                                            {
                                                Margin = new Thickness(0)
                                            };

                                            Run dtr = new Run(now.ToString("yyyy-MM-dd HH:mm:ss - "));
                                            row.Cells.Add(new TableCell(new Paragraph(dtr)));

                                            Run msgr = new Run(msg);

                                            row.Cells.Add(new TableCell(new Paragraph(msgr)));

                                            ColorRow(row, true);
                                            // Add log position
                                            if (_currentJob != null && !_jobLogPosition.ContainsKey(_currentJob))
                                            {
                                                _jobLogPosition.Add(_currentJob, row);
                                            }

                                            _logTable.RowGroups[0].Rows.Add(row);

                                            // Setup size for columns
                                            Size dtSize = MeasureLogDocumentString(dtr, dtr.Text);
                                            if (_logTable.Columns[0].Width.Value < dtSize.Width)
                                            {
                                                _logTable.Columns[0].Width = new GridLength(dtSize.Width);
                                            }

                                            OnPropertyChanged("LogDocument");
                                        });
                if (System.Windows.Application.Current != null)
                {
                    if (System.Windows.Application.Current.Dispatcher.CheckAccess())
                    {
                        callback.Invoke();
                    }
                    else
                    {
                        _logTable.Dispatcher.InvokeAsync(callback);
                    }
                }
            }
        }

        public void AddLogLine()
        {
            Action callback = new Action(() =>
                               {
                                   TableRow row = new TableRow
                                   {
                                       FontSize = 0.004,
                                       Tag = new object[] { _currentJob, LogEntryType.Trace }
                                   };
                                   TableCell cell = new TableCell
                                   {
                                       ColumnSpan = 2,
                                       BorderBrush = Brushes.DarkGray,
                                       BorderThickness = new Thickness(0.5),
                                       Padding = new Thickness(0),
                                   };
                                   row.Cells.Add(cell);

                                   _logTable.RowGroups[0].Rows.Add(row);

                                   OnPropertyChanged("LogDocument");
                               });
            if (System.Windows.Application.Current != null)
            {
                if (System.Windows.Application.Current.Dispatcher.CheckAccess())
                {
                    callback.Invoke();
                }
                else
                {
                    _logTable.Dispatcher.InvokeAsync(callback);
                }
            }
        }

        internal void ReColorLog(bool normal = true)
        {
            // go through each row
            _logTable.Dispatcher.InvokeAsync(new Action(() =>
            {
                foreach (TableRow row in _logTable.RowGroups[0].Rows)
                {
                    ColorRow(row, normal);
                }
                OnPropertyChanged("LogDocument");
            }));
        }

        private void ColorRow(TableRow row, bool normal = true)
        {
            if (row.Cells.Count == 2)
            {
                Block dt = row.Cells[0].Blocks.FirstBlock;
                Block entry = row.Cells[1].Blocks.FirstBlock;
                LogEntryType type = (LogEntryType)((object[])row.Tag)[1];
                if (normal == true)
                {
                    dt.Foreground = Brushes.Gray;

                    switch (type)
                    {
                        case LogEntryType.Error:
                            entry.Foreground = Brushes.Red;
                            break;
                        case LogEntryType.Info:
                            entry.Foreground = Brushes.Black;
                            break;
                        case LogEntryType.Debug:
                            entry.Foreground = Brushes.Gray;
                            break;
                    }
                }
                else
                {
                    dt.Foreground = Brushes.LightGray;
                    entry.Foreground = Brushes.Gray;
                }
            }
        }

        private Size MeasureLogDocumentString(TextElement element, string candidate)
        {
            FormattedText formattedText = new FormattedText(
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
