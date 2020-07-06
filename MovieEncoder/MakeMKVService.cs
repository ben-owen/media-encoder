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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MovieEncoder
{
    class MakeMKVService
    {
        public readonly string MakeMKVConExePath;
        public readonly string MakeMKVOutPath;

        public readonly bool MakeMKVBackupAll;
        private Process makeMKVProcess;

        public MakeMKVService(string makeMKVConExePath, string makeMKVOutPath, bool backupAll)
        {
            this.MakeMKVConExePath = makeMKVConExePath;
            this.MakeMKVBackupAll = backupAll;
            this.MakeMKVOutPath = makeMKVOutPath;
        }

        internal List<DiskTitle> GetDiskTitles(string drive, ProgressReporter progressReporter)
        {
            StopRunningProcess();

            makeMKVProcess = new Process();
            makeMKVProcess.StartInfo = new ProcessStartInfo(MakeMKVConExePath, "-r --cache=1 --progress=-stdout info dev:" + drive);
            makeMKVProcess.StartInfo.CreateNoWindow = true;
            makeMKVProcess.StartInfo.RedirectStandardOutput = true;
            makeMKVProcess.StartInfo.RedirectStandardInput = true;
            makeMKVProcess.StartInfo.UseShellExecute = false;

            makeMKVProcess.EnableRaisingEvents = true;
            StringBuilder output = new StringBuilder();

            string line = null;
            string title = null;
            string lastError = null;
            int titleCount = 0;
            DiskTitle[] diskTitles = null;

            int lastProgress = -1;
            long progressStarted = 0;
            makeMKVProcess.OutputDataReceived += new DataReceivedEventHandler(
                delegate (object sender, DataReceivedEventArgs e)
                {
                    Debug.WriteLine(e.Data);
                    line = e.Data;
                    if (line == null)
                    {
                        return;
                    }
                    if (line.StartsWith("TCOUNT:"))
                    {
                        titleCount = int.Parse(Regex.Split(line, ":")[1]);
                        diskTitles = new DiskTitle[titleCount];
                    }
                    else if (line.StartsWith("CINFO:"))
                    {
                        String[] cInfo = Regex.Split(line, "^.{3,5}:|,");
                        if (cInfo[1].Equals("2"))
                        {
                            title = cInfo[3].Replace("\"", "");
                        }
                    }
                    else if (line.StartsWith("TINFO:"))
                    {
                        String[] tInfo = Regex.Split(line.Replace("\"", ""), "^.{3,5}:|,");
                        int titleIdx = int.Parse(tInfo[1]);
                        DiskTitle diskTitle = diskTitles[titleIdx];
                        if (diskTitle == null)
                        {
                            diskTitle = new DiskTitle();
                            diskTitles[titleIdx] = diskTitle;
                            diskTitle.Drive = drive;
                            diskTitle.TitleIndex = titleIdx;
                            diskTitle.TitleName = title;
                        }
                        switch (tInfo[2])
                        {
                            case "2":
                                diskTitle.TitleName = tInfo[4].Replace("\"", "");
                                break;
                            case "8":
                                diskTitle.Chapters = int.Parse(tInfo[4].Replace("\"", ""));
                                break;
                            case "9":
                                String[] timeParts = Regex.Split(tInfo[4].Replace("\"", ""), ":");
                                int seconds = int.Parse(timeParts[timeParts.Length - 1]);
                                if (timeParts.Length >= 2)
                                {
                                    int minutes = int.Parse(timeParts[timeParts.Length - 2]);
                                    seconds += minutes * 60;
                                }
                                if (timeParts.Length >= 3)
                                {
                                    int hours = int.Parse(timeParts[timeParts.Length - 3]);
                                    seconds += hours * 60 * 60;
                                }
                                diskTitle.Seconds = seconds;
                                break;
                            case "19":
                                String resolution = tInfo[4];
                                MatchCollection mRes = Regex.Matches(resolution, "(\\d+)x(\\d+).*");
                                if (mRes.Count >= 3)
                                {
                                    diskTitle.HorizontalResolution = int.Parse(mRes[1].Value);
                                    diskTitle.VerticalResolution = int.Parse(mRes[2].Value);
                                }
                                break;
                            case "11":
                                diskTitle.Bytes = long.Parse(tInfo[4].Replace("\"", ""));
                                break;
                            case "24":
                                diskTitle.TitleIndex = int.Parse(tInfo[4].Replace("\"", ""));
                                break;
                            case "27":
                                diskTitle.FileName = tInfo[4].Replace("\"", "");
                                break;
                        }
                    }
                    else if (line.StartsWith("SINFO:"))
                    {
                        String[] sInfo = Regex.Split(line.Replace("\"", ""), "^.{3,5}:|,");
                        int titleIdx = int.Parse(sInfo[1]);
                        DiskTitle diskTitle = diskTitles[titleIdx];
                        switch (sInfo[3])
                        {
                            case "19":
                                String resolution = sInfo[5];
                                MatchCollection mRes = Regex.Matches(resolution, "(\\d+)x(\\d+).*");
                                if (mRes.Count >= 3)
                                {
                                    diskTitle.HorizontalResolution = int.Parse(mRes[1].Value);
                                    diskTitle.VerticalResolution = int.Parse(mRes[2].Value);
                                }
                                break;
                        }
                    }
                    else if (line.StartsWith("MSG:"))
                    {
                        String[] msg = Regex.Split(line.Replace("\"", ""), "^.{3,5}:|,");
                        int errorCode = int.Parse(msg[1]);
                        if (errorCode >= 2000 && errorCode < 3000) // == "2024")
                        {
                            lastError = msg[4];
                            progressReporter.AppendLog(lastError, LogEntryType.Error);
                        }
                        else
                        {
                            progressReporter.AppendLog(msg[4].Replace("\"", ""), LogEntryType.Debug);
                        }
                    }
                    else if (line.StartsWith("PRGT:"))
                    {
                        String[] progItems = Regex.Split(line, "^.{4}:|,");
                        progressReporter.CurrentTask = progItems[3].Replace("\"", "");
                    }
                    else if (line.StartsWith("PRGC:"))
                    {
                        String[] progItems = Regex.Split(line, "^.{4}:|,");
                        progressReporter.Remaining = progItems[3].Replace("\"", "");
                    }
                    else if (line.StartsWith("PRGV:"))
                    {
                        // update progess but not time to go
                        ProcessProgress(progressReporter, ref lastProgress, ref progressStarted, line, true);
                    }
                }
            );
            makeMKVProcess.Start();
            makeMKVProcess.BeginOutputReadLine();
            makeMKVProcess.WaitForExit();
            if (makeMKVProcess != null)
                makeMKVProcess.CancelOutputRead();

            if (lastError != null)
            {
                throw new JobException(lastError);
            }

            List<DiskTitle> titles = new List<DiskTitle>();
            if (diskTitles != null)
            {
                foreach (DiskTitle diskTitle in diskTitles)
                {
                    if (diskTitle != null)
                    {
                        titles.Add(diskTitle);
                    }
                }
            }
            return titles;
        }

        internal bool Backup(DiskTitle diskTitle, ProgressReporter progressReporter)
        {
            string dirName = DirectoryName(diskTitle.TitleName);
            string outDir = Path.Combine(MakeMKVOutPath, Path.GetFileNameWithoutExtension(dirName));
            string finalOutFile = Path.Combine(outDir, diskTitle.FileName);

            diskTitle.FullMKVPath = finalOutFile;

            // test this hasn't been done before
            if (File.Exists(finalOutFile))
            {
                throw new Exception("Looks like MakeMKV was already run. Found file \"" + finalOutFile + "\". Will not create a backup.");
            }
            Directory.CreateDirectory(outDir);

            StopRunningProcess();

            string cmd = "-r --decrypt --noscan --progress=-stdout --progress=-stdout --cache=1024 mkv dev:" + diskTitle.Drive + " " + diskTitle.TitleIndex + " \"" + outDir + "\"";

            makeMKVProcess = new Process();
            makeMKVProcess.StartInfo = new ProcessStartInfo(MakeMKVConExePath, cmd);
            makeMKVProcess.StartInfo.CreateNoWindow = true;
            makeMKVProcess.StartInfo.RedirectStandardOutput = true;
            makeMKVProcess.StartInfo.RedirectStandardInput = true;
            makeMKVProcess.StartInfo.UseShellExecute = false;

            makeMKVProcess.EnableRaisingEvents = true;
            StringBuilder output = new StringBuilder();

            int lastProgress = -1;
            long progressStarted = 0;
            makeMKVProcess.OutputDataReceived += new DataReceivedEventHandler(
                delegate (object sender, DataReceivedEventArgs e)
                {
                    string line = e.Data;
                    if (line == null)
                    {
                        return;
                    }
                    //Debug.WriteLine(line);

                    if (line.StartsWith("PRGV:"))
                    {
                        ProcessProgress(progressReporter, ref lastProgress, ref progressStarted, line, false);
                    }
                    else if (line.StartsWith("MSG:"))
                    {
                        string[] msg = Regex.Split(line, ",");
                        int msgCode = int.Parse(msg[2]);
                        //if (msgParts[4].Contains("\"Saving %1 titles into directory %2\""))
                        if (msgCode == 5014)
                        {
                            progressReporter.CurrentTask = msg[3].Replace("\"", "");
                        }
                        else
                        {
                            if (msgCode >= 2000 && msgCode < 3000)
                            {
                                progressReporter.AppendLog(msg[3].Replace("\"", ""), LogEntryType.Error);
                            }
                            else
                            {
                                progressReporter.AppendLog(msg[3].Replace("\"", ""), LogEntryType.Debug);
                            }

                            //progressReporter.AppendLog(msg[3].Replace("\"", ""), LogEntryType.Info);
                        }
                    }
                    else if (line.StartsWith("PRGT:"))
                    {
                        String[] progItems = Regex.Split(line, "^.{4}:|,");
                        progressReporter.CurrentTask = progItems[3].Replace("\"", "");
                    }
                    else if (line.StartsWith("PRGC:"))
                    {
                        String[] progItems = Regex.Split(line, "^.{4}:|,");
                        progressReporter.Remaining = progItems[3].Replace("\"", "");
                    }
                }
            );
            makeMKVProcess.Start();
            makeMKVProcess.BeginOutputReadLine();
            makeMKVProcess.WaitForExit();
            if (makeMKVProcess != null)
                makeMKVProcess.CancelOutputRead();
            return true;
        }

        private static void ProcessProgress(ProgressReporter progressReporter, ref int lastProgress, ref long progressStarted, string line, bool suppressRemaining)
        {
            long progressNow = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            String[] progItems = Regex.Split(line, "^.{4}:|,");
            int currentProgress = int.Parse(progItems[2]);
            if (lastProgress > currentProgress)
            {
                // need to reset
                progressStarted = progressNow;
            }
            lastProgress = currentProgress;
            // calc progress
            double pmsec = currentProgress / (double)(progressNow - progressStarted);
            int total = int.Parse(progItems[3]);
            int remain = total - currentProgress;
            int msToGo = (int)(remain / pmsec);

            progressReporter.MaxProgress = total;
            progressReporter.CurrentProgress = currentProgress;
            if (!suppressRemaining)
            {
                progressReporter.Remaining = Utils.GetDuration(msToGo / 1000);
            }
        }

        private int GetDiscIndex(string drive)
        {
            makeMKVProcess = new Process();
            makeMKVProcess.StartInfo = new ProcessStartInfo(MakeMKVConExePath, "-r --cache=1 info disk:9999");
            makeMKVProcess.StartInfo.CreateNoWindow = true;
            makeMKVProcess.StartInfo.RedirectStandardOutput = true;
            makeMKVProcess.StartInfo.RedirectStandardInput = true;
            makeMKVProcess.StartInfo.UseShellExecute = false;

            makeMKVProcess.EnableRaisingEvents = true;
            StringBuilder output = new StringBuilder();

            int driveIndex = -1;
            makeMKVProcess.OutputDataReceived += new DataReceivedEventHandler(
                delegate (object sender, DataReceivedEventArgs e)
                {
                    string line = e.Data;
                    if (line == null)
                    {
                        return;
                    }
                    if (line.StartsWith("DRV:"))
                    {
                        string[] drvItems = Regex.Split(line.Replace("\"", ""), "^.{3,5}:|,");
                        if (drvItems.Length > 7 && drvItems[7].Length > 0)
                        {
                            // Matched Drive
                            if (drive.ToCharArray()[0] == drvItems[7].ToCharArray()[0])
                            {
                                driveIndex = int.Parse(drvItems[1]);
                            }
                        }
                    }
                }
            );
            makeMKVProcess.Start();
            makeMKVProcess.BeginOutputReadLine();
            makeMKVProcess.WaitForExit();
            if (makeMKVProcess != null)
                makeMKVProcess.CancelOutputRead();

            return driveIndex;
        }

        private string DirectoryName(string title)
        {
            return title.Replace(":", "-").Replace("|", "");
        }

        private void StopRunningProcess()
        {
            if (makeMKVProcess != null)
            {
                try
                {
                    if (!makeMKVProcess.HasExited)
                    {
                        makeMKVProcess.Kill();
                    }
                } catch(System.InvalidOperationException)
                {
                    // ignore
                }
                makeMKVProcess = null;
            }
        }

        public void Shutdown()
        {
            StopRunningProcess();
        }
    }
}
