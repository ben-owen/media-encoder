﻿// Copyright 2020 Ben Owen
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
    public class ProgressReporter : INotifyPropertyChanged
    {
        private string _currentTask;
        private readonly StringBuilder _log = new StringBuilder();
        private bool _isError = false;
        private string _remaining = "Test";
        private Job _currentJob;
        private readonly Dictionary<Job, TextElement> _jobLogPosition = new Dictionary<Job, TextElement>();
        private Table _logTable;

        public Job CurrentJob
        {
            get { return _currentJob; }
            set
            {
                _currentJob = value;
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

        internal void ClearLog()
        {
            LogDocument.Blocks.Clear();
            CreateLogDocumentTable();
            JobQueue.ClearJobLog();
            OnPropertyChanged("LogDocument");
        }

        public string CurrentTask
        {
            get { return _currentTask; }
            set
            {
                _currentTask = value;
                AppendLog(value, false);
                OnPropertyChanged();
                OnPropertyChanged("Log");
            }
        }

        internal TextElement GetLogDocumentBlock(Job job)
        {
            TextElement block = null;
            if (_jobLogPosition.ContainsKey(job))
                block = _jobLogPosition[job];
            return block;
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
            get; set;
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

        public ProgressReporter()
        {
            JobQueue.EnableCollectionSynchronization();
            _currentTask = "No Tasks";
            _isError = false;
            Remaining = "";
            LogDocument = new FlowDocument();

            CreateLogDocumentTable();
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

        public event PropertyChangedEventHandler PropertyChanged;

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

            if (!_currentTask.Equals("No Tasks"))
            {
                _currentTask = "No Tasks";
            }
        }

        internal void AddError(string message)
        {
            _currentTask = $"ERROR: {message}";
            AppendLog(_currentTask, true);
            IsError = true;
        }

        internal void AppendLog(string message, bool error = false)
        {
            string msg = message.Trim();
            if (msg != "")
            {
                DateTime now = DateTime.Now;

                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() =>
                {
                    TableRow row = new TableRow
                    {
                        Tag = _currentJob
                    };

                    Paragraph paragraph = new Paragraph();
                    paragraph.Margin = new Thickness(0);

                    Run dtr = new Run(now.ToString("yyyy-MM-dd HH:mm:ss - "));
                    //dtr.Foreground = Brushes.Gray;
                    row.Cells.Add(new TableCell(new Paragraph(dtr)));

                    Run msgr = new Run(msg);
                    //if (error)
                    //    msgr.Foreground = Brushes.Red;
                    //else
                    //    msgr.Foreground = Brushes.Black;

                    row.Cells.Add(new TableCell(new Paragraph(msgr)));

                    // TODO - maybe make table a field instead
                    //Table table = (Table)LogDocument.Blocks.FirstBlock;

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
                Job job = (Job)row.Tag;
                if (normal == true)
                {
                    if (job?.IsErrored == true)
                    {
                        dt.Foreground = Brushes.Gray;
                        entry.Foreground = Brushes.Red;
                    }
                    else
                    {
                        dt.Foreground = Brushes.Gray;
                        entry.Foreground = Brushes.Black;
                    }
                }
                else
                {
                    dt.Foreground = Brushes.LightGray;
                    entry.Foreground = Brushes.LightGray;
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
