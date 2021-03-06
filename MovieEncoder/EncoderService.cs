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
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Threading;

namespace MovieEncoder
{
    public enum BackupMode
    {
        MakeMKV,
        HandBrake,
        None
    }

    public class EncoderService
    {
        /* -- Fields -- */
        private readonly HandBrakeService handBrakeService;
        private readonly MakeMKVService makeMKVService;
        private Thread jobThread;
        private bool running;
        private ManagementEventWatcher managementEventWatcher;
        private readonly List<FileSystemWatcher> fileSystemWatchers = new List<FileSystemWatcher>();
        private JobQueue jobQueue = new JobQueue();
        private ProgressReporter progressReporter;

        /* -- Properties -- */
        public BackupMode GlobalBackupMethod
        {
            get { return (BackupMode)Enum.Parse(typeof(BackupMode), Properties.Settings.Default.GlobalBackupMethod); }
            set { Properties.Settings.Default.GlobalBackupMethod = value.ToString(); Properties.Settings.Default.Save(); }
        }

        public int GlobalMinMovieLen
        {
            get { return Properties.Settings.Default.GlobalMinMovieLen; }
            set { Properties.Settings.Default.GlobalMinMovieLen = value; Properties.Settings.Default.Save(); }
        }

        public bool MakeMkvKeepFiles
        {
            get { return Properties.Settings.Default.MakeMkvKeepFiles; }
            set { Properties.Settings.Default.MakeMkvKeepFiles = value; Properties.Settings.Default.Save(); }
        }

        public string MakeMkvConExePath
        {
            get { return Properties.Settings.Default.MakeMkvConExePath; }
            set
            {
                Properties.Settings.Default.MakeMkvConExePath = value;
                Properties.Settings.Default.Save();
                makeMKVService.MakeMKVConExePath = value;
            }
        }

        public string MakeMkvOutDir
        {
            get { return Properties.Settings.Default.MakeMkvOutDir; }
            set
            {
                Properties.Settings.Default.MakeMkvOutDir = value;
                Properties.Settings.Default.Save();
                makeMKVService.MakeMKVOutPath = value;
            }
        }

        public bool GlobalBackupAll
        {
            get { return Properties.Settings.Default.GlobalBackupAll; }
            set
            {
                Properties.Settings.Default.GlobalBackupAll = value;
                Properties.Settings.Default.Save();
                makeMKVService.MakeMKVBackupAll = value;
            }
        }

        public string HandBrakeCliExePath
        {
            get { return Properties.Settings.Default.HandBrakeCliExePath; }
            set
            {
                Properties.Settings.Default.HandBrakeCliExePath = value;
                Properties.Settings.Default.Save();
                handBrakeService.HandBrakeCliExePath = value;
            }
        }

        public string HandBrakeProfileFile
        {
            get { return Properties.Settings.Default.HandBrakeProfileFile; }
            set
            {
                Properties.Settings.Default.HandBrakeProfileFile = value;
                Properties.Settings.Default.Save();
                handBrakeService.HandBrakeProfileFile = value;
            }
        }

        public string HandBrakeSourceDir
        {
            get { return Properties.Settings.Default.HandBrakeSourceDir; }
            set
            {
                Properties.Settings.Default.HandBrakeSourceDir = value;
                Properties.Settings.Default.Save();
                handBrakeService.HandBrakeSourceDir = value;
            }
        }

        public string HandBrakeOutDir
        {
            get { return Properties.Settings.Default.HandBrakeOutDir; }
            set
            {
                Properties.Settings.Default.HandBrakeOutDir = value;
                Properties.Settings.Default.Save();
                handBrakeService.HandBrakeOutDir = value;
            }
        }

        public HandBrakeService.OutputType HandBrakeOutputType
        {
            get { return (HandBrakeService.OutputType)Enum.Parse(typeof(HandBrakeService.OutputType), Properties.Settings.Default.HandBrakeOutputType); }
            set
            {
                Properties.Settings.Default.HandBrakeOutputType = value.ToString();
                Properties.Settings.Default.Save();
                handBrakeService.MovieOutputType = value;
            }
        }

        public bool HandBrakeForceSubtitles
        {
            get { return Properties.Settings.Default.HandBrakeForceSubtitles; }
            set
            {
                Properties.Settings.Default.HandBrakeForceSubtitles = value;
                Properties.Settings.Default.Save();
                handBrakeService.ForceSubtitles = value;
            }
        }

        /* -- Methods -- */
        public EncoderService()
        {
            makeMKVService = new MakeMKVService(MakeMkvConExePath, MakeMkvOutDir, GlobalBackupAll);
            handBrakeService = new HandBrakeService(HandBrakeCliExePath, HandBrakeProfileFile, HandBrakeSourceDir, HandBrakeOutDir, HandBrakeOutputType, HandBrakeForceSubtitles);
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
                        Job job = null;
                        if (GlobalBackupMethod == BackupMode.MakeMKV)
                        {
                            job = new BackupDiskMakeMKVJob(makeMKVService, handBrakeService, driveInfo.Name, MakeMkvKeepFiles, GlobalMinMovieLen);
                        }
                        else if (GlobalBackupMethod == BackupMode.HandBrake)
                        {
                            job = new BackupDiskHandBrakeJob(handBrakeService, driveInfo.Name.Replace("\\", ""), GlobalBackupAll, GlobalMinMovieLen);
                        }

                        if (job != null)
                        {
                            // move backups to the top of the queue
                            jobQueue.AddJob(job, true);
                        }
                    }
                }
                catch (IOException)
                {
                    // skip for device not ready
                }
            }

            // Setup Monitoring
            SetupDriveMonitoring();

            // Read in existing files for encode
            AddEncodingJobs(HandBrakeSourceDir);


            // Start file monitoring
            SetupFileMonitoring(HandBrakeSourceDir);

            progressReporter.Shutdown = false;

            jobThread = new Thread(new ThreadStart(JobRunner))
            {
                Name = "JobRunner"
            };
            jobThread.Start();
        }

        private void AddEncodingJobs(string path)
        {
            string[] files = Directory.GetFiles(path);
            foreach (string file in files)
            {
                if (Utils.IsMovieFile(file))
                {
                    EncodeMovieJob encodeMovieJob = new EncodeMovieJob(handBrakeService, file, MakeMkvKeepFiles);
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
            ConnectionOptions opt = new ConnectionOptions
            {
                EnablePrivileges = true //sets required privilege
            };
            ManagementScope scope = new ManagementScope("root\\CIMV2", opt);

            try
            {
                q = new WqlEventQuery
                {
                    EventClassName = "__InstanceModificationEvent",
                    WithinInterval = new TimeSpan(0, 0, 1),

                    // DriveType - 5: CDROM
                    Condition = @"TargetInstance ISA 'Win32_LogicalDisk' and TargetInstance.DriveType = 5"
                };
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
                FileSystemWatcher watcher = new FileSystemWatcher
                {
                    Path = path,
                    NotifyFilter = NotifyFilters.LastAccess
                                        | NotifyFilters.LastWrite
                                        | NotifyFilters.FileName
                                        | NotifyFilters.DirectoryName
                                        | NotifyFilters.CreationTime
                };
                watcher.Created += FileSystem_Created;
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
            catch (ArgumentException)
            {
                // for cases where the path was removed before we setup monitoring
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
                        EncodeMovieJob job = new EncodeMovieJob(handBrakeService, path, MakeMkvKeepFiles);
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
                        Job job = null;
                        if (GlobalBackupMethod == BackupMode.MakeMKV)
                        {
                            job = new BackupDiskMakeMKVJob(makeMKVService, handBrakeService, (string)mbo.Properties["DeviceID"].Value, MakeMkvKeepFiles, GlobalMinMovieLen);
                        }
                        else if (GlobalBackupMethod == BackupMode.HandBrake)
                        {
                            job = new BackupDiskHandBrakeJob(handBrakeService, mbo.Properties["DeviceID"].Value.ToString().Replace("\\", ""), GlobalBackupAll, GlobalMinMovieLen);
                        }
                        if (job != null)
                        {
                            jobQueue.AddJob(job, true);
                        }
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

            progressReporter.CurrentTask = "Stopped";
            progressReporter.Reset();
            progressReporter.Shutdown = true;
        }

        private void JobRunner()
        {
            running = true;
            Debug.WriteLine("Encode service starting");
            progressReporter.ReColorLog(false);
            while (running)
            {
                Job job = jobQueue.GetFirstJob();
                if (job != null)
                {
                    job.SetReporter(progressReporter);

                    progressReporter.Reset();
                    progressReporter.CurrentJob = job;
                    progressReporter.CurrentTask = "Starting Job " + job.JobName;

                    try
                    {
                        job.IsStarted = true;
                        if (job.RunJob(jobQueue))
                        {
                            progressReporter.CurrentTask = "Completed Job ";
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
                    }
                    catch (Exception e)
                    {
                        job.IsErrored = true;
                        job.MaxProgress = 1;
                        job.CurrentProgress = 1;

                        progressReporter.AddError(e.Message);
                        progressReporter.CurrentTask = "Failed Job ";
                        progressReporter.Reset();
                    }
                    jobQueue.RemoveJob(job);
                }
                else
                {
                    if (!progressReporter.CurrentTask.Equals("No Tasks"))
                    {
                        progressReporter.AddLogLine();
                        progressReporter.CurrentTask = "No Tasks";
                    }
                }
                Thread.Sleep(300);
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
