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
using System.Text;
using System.Windows;

namespace MovieEncoder
{
    public class ProgressReporter : INotifyPropertyChanged
    {
        private string _currentTask;
        private StringBuilder _log = new StringBuilder();
        private bool _isError = false;
        private string _remaining = "Test";
        private Job _currentJob;

        public Job CurrentJob
        {
            get { return _currentJob; }
            set
            {
                _currentJob = value; OnPropertyChanged();
            }
        }

        public JobQueue JobQueue { get; } = new JobQueue();

        /*
        public AsyncObservableCollection<JobInfo> JobHistory
        {
            get; set;
        }
        */

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
                //_maxProgress = value;
                //OnPropertyChanged();
            }
        }
        public double CurrentProgress
        {
            get
            {
                //return _currentProgress;
                return _currentJob != null ? _currentJob.CurrentProgress : 0;
            }
            set
            {
                if (_currentJob != null)
                {
                    _currentJob.CurrentProgress = value;
                    OnPropertyChanged();
                }
                /*
                _currentProgress = value;
                OnPropertyChanged();
                if (CurrentJob != null)
                {
                    CurrentJob.MaxProgress = _maxProgress;
                    CurrentJob.CurrentProgress = _currentProgress;
                    OnPropertyChanged("Jobs");
                }
                */
            }
        }

        public string CurrentTask
        {
            get { return _currentTask; }
            set
            {
                _currentTask = value;
                AppendLog(value);
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

        public string Remaining
        {
            get { return _remaining; }
            set { _remaining = value; OnPropertyChanged(); }
        }

        public ProgressReporter()
        {
            JobQueue.EnableCollectionSynchronization();
            _currentTask = "No Tasks";
            _isError = false;
            Remaining = "";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void WriteToLog(string line)
        {

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
            IsError = false;
            _currentTask = "No Tasks";
            CurrentJob = null;


            CurrentProgress = 0.0;
            MaxProgress = 100.0;

            OnPropertyChanged("CurrentTask");
        }

        internal void AddError(string message)
        {
            CurrentTask = "ERROR: " + message;
            IsError = true;
        }

        internal void AppendLog(string message)
        {
            string msg = message.Trim();
            if (msg != "")
            {
                DateTime now = DateTime.Now;
                _log.Append(now.ToString("yyyy-MM-dd HH:mm:ss - "));
                _log.Append(CurrentTask);
                _log.Append("\r\n");
                OnPropertyChanged("Log");
            }
        }
        /*
        internal void AddJob(JobInfo jobInfo)
        {
            CurrentJob = jobInfo;
            JobHistory.Add(jobInfo);
            OnPropertyChanged("CurrentJob");
        }
        */
    }
    /*
    public class JobInfo : INotifyPropertyChanged
    {
        private string _jobName;
        private double _maxProgress;
        private double _currentProgress;

        public JobInfo(string jobName)
        {
            this.JobName = jobName;
            this.MaxProgress = 100;
            this.CurrentProgress = 0;
        }

        public string JobName
        {
            get { return _jobName; }
            set { _jobName = value; OnPropertyChanged(); }
        }

        public double MaxProgress
        {
            get { return _maxProgress; }
            set { _maxProgress = value; OnPropertyChanged(); }
        }

        public double CurrentProgress
        {
            get { return _currentProgress; }
            set { _currentProgress = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
    */
}
