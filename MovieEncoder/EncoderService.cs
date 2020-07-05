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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Management;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Runtime.ExceptionServices;
using System.Windows.Media.Animation;

namespace MovieEncoder
{
    class EncoderService
    {
        private readonly HandBrakeService handBrakeService;
        private readonly MakeMKVService makeMKVService;
        private Thread jobThread;
        private bool running;
        private ManagementEventWatcher managementEventWatcher;
        private List<FileSystemWatcher> fileSystemWatchers = new List<FileSystemWatcher>();
        private JobQueue jobQueue = new JobQueue();
        private ProgressReporter progressReporter;

        public EncoderService()
        {
            makeMKVService = new MakeMKVService(Properties.Settings.Default.MakeMkvConExePath, Properties.Settings.Default.MakeMkvOutDir, Properties.Settings.Default.MakeMkvBackupAll);
            handBrakeService = new HandBrakeService(Properties.Settings.Default.HandBrakeCliExePath, Properties.Settings.Default.HandBrakeProfileFile, Properties.Settings.Default.HandBrakeSourceDir, Properties.Settings.Default.HandBrakeOutDir);
        }

        // TODO
        public void SetProgressReporter(ProgressReporter progress)
        {
            this.progressReporter = progress;
            this.jobQueue = this.progressReporter.JobQueue;
        }

        public void Start()
        {
            running = true;

            // Read ROM drives to backup
            DriveInfo[] allDrives = DriveInfo.GetDrives();
            foreach (DriveInfo driveInfo in allDrives)
            {
                try
                {
                    if (driveInfo.DriveType == DriveType.CDRom && driveInfo.VolumeLabel != null)
                    {
                        // Run MakeMKV to backup movie
                        BackupDiskJob job = new BackupDiskJob(makeMKVService, handBrakeService, (string)driveInfo.Name, Properties.Settings.Default.MakeMkvKeepFiles);
                        // move backups to the top of the queue
                        jobQueue.AddJob(job, true);
                    }
                } catch (IOException)
                {
                    // skip for device not ready
                }
            }

            // Setup Monitoring
            SetupDriveMonitoring();

            // Read in existing files for encode
            AddEncodingJobs(Properties.Settings.Default.HandBrakeSourceDir);
            

            // Start file monitoring
            SetupFileMonitoring(Properties.Settings.Default.HandBrakeSourceDir);

            jobThread = new Thread(new ThreadStart(JobRunner));
            jobThread.Start();
        }

        private void AddEncodingJobs(string path)
        {
            string[] files = Directory.GetFiles(path);
            foreach (string file in files)
            {
                if (Utils.IsMovieFile(file))
                {
                    EncodeMovieJob encodeMovieJob = new EncodeMovieJob(handBrakeService, file, Properties.Settings.Default.MakeMkvKeepFiles);
                    jobQueue.AddJob(encodeMovieJob);
                }
            }

            string[] dirs = Directory.GetDirectories(path);
            foreach (string dir in dirs)
            {
                AddEncodingJobs(dir);
            }
        }

        private void SetupDriveMonitoring()
        {
            WqlEventQuery q;
            ManagementOperationObserver observer = new ManagementOperationObserver();

            // Bind to local machine
            ConnectionOptions opt = new ConnectionOptions();
            opt.EnablePrivileges = true; //sets required privilege
            ManagementScope scope = new ManagementScope("root\\CIMV2", opt);

            try
            {
                q = new WqlEventQuery();
                q.EventClassName = "__InstanceModificationEvent";
                q.WithinInterval = new TimeSpan(0, 0, 1);

                // DriveType - 5: CDROM
                q.Condition = @"TargetInstance ISA 'Win32_LogicalDisk' and TargetInstance.DriveType = 5";
                managementEventWatcher = new ManagementEventWatcher(scope, q);

                // register async. event handler
                managementEventWatcher.EventArrived += new EventArrivedEventHandler(CDREventArrived);
                managementEventWatcher.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                if (managementEventWatcher != null)
                {
                    managementEventWatcher.Stop();
                }
            }
        }

        private void SetupFileMonitoring(string path)
        {
            // find if we have an existing watcher
            foreach (FileSystemWatcher fileSystemWatcher in fileSystemWatchers)
            {
                if (fileSystemWatcher.Path.Equals(path))
                {
                    return;
                }
            }

            try
            {
                FileSystemWatcher watcher = new FileSystemWatcher();
                watcher.Path = path;
                watcher.NotifyFilter = NotifyFilters.LastAccess
                                     | NotifyFilters.LastWrite
                                     | NotifyFilters.FileName
                                     | NotifyFilters.DirectoryName
                                     | NotifyFilters.CreationTime;
                watcher.Created += FileSystem_Created;
                //watcher.Changed += FileSystem_Created;
                watcher.Renamed += FileSystem_Renamed;

                fileSystemWatchers.Add(watcher);

                watcher.EnableRaisingEvents = true;

                // Read in other directories
                string[] files = Directory.GetDirectories(path);
                foreach (string file in files)
                {
                    SetupFileMonitoring(file);
                }
            }
            catch (UnauthorizedAccessException)
            {
                // ignore it
            }
            catch (FileNotFoundException)
            {
                // ignore it
            }
        }

        private void FileSystem_Renamed(object sender, RenamedEventArgs e)
        {
            ProcessFileSystemChange(e.FullPath);
        }

        private void FileSystem_Created(object sender, FileSystemEventArgs e)
        {
            ProcessFileSystemChange(e.FullPath);
        }

        private void ProcessFileSystemChange(string path)
        {
            // check if this is a directory
            if (Directory.Exists(path))
            {
                // create a new watcher
                SetupFileMonitoring(path);
            }
            else
            {
                // check if this is a movie file
                if (Utils.IsMovieFile(path))
                {
                    // Check if we have a job for this file
                    if (!FindEncodeJobForPath(path))
                    {
                        EncodeMovieJob job = new EncodeMovieJob(handBrakeService, path, Properties.Settings.Default.MakeMkvKeepFiles);
                        jobQueue.AddJob(job);
                    }
                    Console.WriteLine(path);
                }
            }
        }

        private bool FindEncodeJobForPath(string path)
        {
            foreach (Job job in jobQueue.GetQueue())
            {
                if (job.GetType() == typeof(EncodeMovieJob))
                {
                    if (((EncodeMovieJob)job).InputFileName.Equals(path))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void CDREventArrived(object sender, EventArrivedEventArgs e)
        {
            // New Disk!
            Debug.WriteLine("BackupService starting");
            // Get the Event object and display it
            PropertyData pd = e.NewEvent.Properties["TargetInstance"];

            if (pd != null)
            {
                ManagementBaseObject mbo = pd.Value as ManagementBaseObject;

                // if CD removed VolumeName == null
                if (mbo.Properties["DeviceID"].Value != null)
                {
                    if (mbo.Properties["VolumeName"].Value != null)
                    {
                        Debug.WriteLine("CD has been inserted: " + mbo.Properties["VolumeName"].Value);
                        // Run MakeMKV to backup movie
                        BackupDiskJob job = new BackupDiskJob(makeMKVService, handBrakeService, (string)mbo.Properties["DeviceID"].Value, Properties.Settings.Default.MakeMkvKeepFiles);
                        jobQueue.AddJob(job, true);
                    }
                    else
                    {
                        Debug.WriteLine("CD has been removed: " + mbo.Properties["DeviceID"].Value);
                    }
                }
                else
                {
                    Console.WriteLine("CD has been ejected");
                }
            }
        }

        public void Stop()
        {
            progressReporter.AppendLog("Stopping");
            progressReporter.Reset();
            progressReporter.Shutdown = true;

            if (managementEventWatcher != null)
            {
                managementEventWatcher.Stop();
            }

            foreach (FileSystemWatcher fileSystemWatcher in fileSystemWatchers)
            {
                fileSystemWatcher.EnableRaisingEvents = false;
            }
            fileSystemWatchers.Clear();

            this.running = false;
            this.handBrakeService.Shutdown();
            this.makeMKVService.Shutdown();
        }

        private void JobRunner()
        {
            running = true;
            Debug.WriteLine("Encode service starting");
            while (running)
            {
                Job job = jobQueue.GetFirstJob();
                if (job != null)
                {
                    progressReporter.Reset();
                    //progressReporter.JobQueue.AddJob(job);
                    progressReporter.CurrentJob = job;
                    Debug.WriteLine("Running Job Type " + job.GetType());
                    progressReporter.CurrentTask = "Starting Job " + job.JobName;

                    job.SetReporter(progressReporter);
                    try
                    {
                        if (job.RunJob(jobQueue))
                        {
                            progressReporter.CurrentTask = "Completed Job " + job.JobName;
                        } 
                        else
                        {
                            job.IsErrored = true;
                            job.MaxProgress = 1;
                            job.CurrentProgress = 1;
                        }
                        if (progressReporter.CurrentJob != null)
                        {
                            progressReporter.CurrentJob.CurrentProgress = progressReporter.CurrentJob.MaxProgress;
                        }
                        progressReporter.Reset();
                    } catch (Exception e)
                    {
                        job.IsErrored = true;
                        job.MaxProgress = 1;
                        job.CurrentProgress = 1;

                        progressReporter.AddError(e.Message);
                        progressReporter.CurrentTask = "Failed Job " + job.JobName;
                        progressReporter.Reset();
                    }
                    jobQueue.RemoveJob(job);
                }
                Thread.Sleep(100);
            }
            jobQueue.ClearJobQueue();
            progressReporter.Reset();
            Debug.WriteLine("Encode service stopping");
        }

        internal bool IsStarted()
        {
            return running;
        }
    }
}
