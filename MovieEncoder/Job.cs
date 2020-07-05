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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace MovieEncoder
{
    public abstract class Job : INotifyPropertyChanged
    {
        private double _maxProgress = 100;
        private double _currentProgress = 0;
        private bool _isErrored = false;
        private bool _started = false;

        protected ProgressReporter progressReporter;

        abstract public string JobName { get; }

        public double MaxProgress
        { get { return _maxProgress; }  set { _maxProgress = value; OnPropertyChanged(); } }

        public double CurrentProgress
        { get { return _currentProgress; } set { _currentProgress = value; OnPropertyChanged(); } }

        public bool IsStarted
        { get { return _started; } set { _started = value; OnPropertyChanged(); } }

        public bool IsErrored
        { get { return _isErrored; } set { _isErrored = value; OnPropertyChanged(); OnPropertyChanged("ProgressColor"); } }

        public string ProgressColor
        { 
            get 
            { return IsErrored ? "#FFF75252" : "#FF39B200"; }
        }

        abstract public bool RunJob(JobQueue jobRunner);

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public void SetReporter(ProgressReporter progressReporter)
        {
            this.progressReporter = progressReporter;
        }
    }

    public class JobQueue
    {
        private readonly object _lock = new object();

        private readonly ObservableCollection<Job> jobQueue = new ObservableCollection<Job>();
        private readonly ObservableCollection<Job> allJobs = new ObservableCollection<Job>();

        internal void EnableCollectionSynchronization()
        {
            BindingOperations.EnableCollectionSynchronization(jobQueue, _lock);
            BindingOperations.EnableCollectionSynchronization(allJobs, _lock);
        }

        internal void ClearJobQueue()
        {
            lock (_lock)
            {
                // clear the job queue
                jobQueue.Clear();

                // clear all not started jobs
                for (int i = allJobs.Count - 1; i >= 0; i--)
                {
                    Job job = allJobs[i];
                    if (!job.IsStarted)
                    {
                        allJobs.RemoveAt(i);
                    }
                }
            }
        }

        internal void ClearJobLog()
        {
            // Remove jobs that are not in JobQueue
            lock (_lock)
            {
                for (int i = allJobs.Count - 1; i >= 0; i--)
                {
                    Job job = allJobs[i];
                    if (!jobQueue.Contains(job))
                    {
                        allJobs.RemoveAt(i);
                    }
                }
            }
        }

        internal void RemoveJob(Job job)
        {
            lock (_lock)
            {
                jobQueue.Remove(job);
            }
        }

        internal void AddJob(Job job, bool addFirst = false)
        {
            lock (_lock)
            {
                foreach (Job checkJob in jobQueue)
                {
                    if (checkJob.JobName.Equals(job.JobName))
                    {
                        return;
                    }
                }
                if (addFirst)
                {
                    jobQueue.Insert(0, job);

                    // Add the job after any run or running jobs
                    int i = 0;
                    for (i = 0; i < allJobs.Count; i++)
                    {
                        Job allJob = allJobs[i];
                        if (!allJob.IsStarted)
                        {
                            break;
                        }
                    }
                    if (i == allJobs.Count)
                    {
                        allJobs.Add(job);
                    }
                    else
                    {
                        allJobs.Insert(i, job);
                    }
                }
                else
                {
                    jobQueue.Add(job);
                    allJobs.Add(job);
                }
            }
        }

        internal Job GetFirstJob()
        {
            if (jobQueue.Count > 0)
                return jobQueue.First();
            return null;
        }

        internal IEnumerable<Job> GetQueue()
        {
            lock (_lock)
            {
                return jobQueue;
            }
        }

        internal IEnumerable<Job> GetJobs()
        {
            lock (_lock)
            {
                return allJobs;
            }
        }
    }

    [Serializable()]
    public class JobException : Exception
    {
        public JobException(string message) : base(message)
        {
        }
    }
}
