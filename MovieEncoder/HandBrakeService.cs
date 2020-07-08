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
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MovieEncoder
{
    public class HandBrakeService
    {
        public enum HandBrakeJsonType
        {
            Version,
            Progress,
            TitleSet
        }
        public enum ProcessingState
        {
            None,
            Encoding,
            Scanning,
            Muxing
        }
        public enum OutputType
        {
            MKV,
            MP4
        }

        public class HBVersion
        {
            public class VersionData
            {
                public int Major { get; set; }
                public int Minor { get; set; }
                public int Point { get; set; }
            }

            public string Arch { get; set; }
            public string Name { get; set; }
            public bool Official { get; set; }
            public string RepoDate { get; set; }
            public string RepoHash { get; set; }
            public string System { get; set; }
            public string Type { get; set; }
            public VersionData Version { get; set; }
            public string VersionString { get; set; }
        }

        public class HBTitleSet
        {
            public int MainFeature { get; set; }
            public TitleListData[] TitleList { get; set; }
            public class TitleListData
            {
                public int AngleCount { get; set; }
                public AudioListData[] AudioList { get; set; }
                public class AudioListData { }
                public ChapterListData[] ChapterList { get; set; }
                public class ChapterListData
                {
                    public DurationData Duration { get; set; }
                    public string Name { get; set; }
                }
                public DurationData Duration { get; set; }
                public class DurationData
                {
                    public int Hours { get; set; }
                    public int Minutes { get; set; }
                    public int Seconds { get; set; }
                    public int Ticks { get; set; }

                }
                public FrameRateData FrameRate { get; set; }
                public class FrameRateData
                {
                    public int Den { get; set; }
                    public int Num { get; set; }
                };
                public GeometryData Geometry { get; set; }
                public class GeometryData
                {
                    public int Height { get; set; }
                    public PARData PAR { get; set; }
                    public class PARData
                    {
                        public int Den { get; set; }
                        public int Num { get; set; }
                    }
                    public int Width { get; set; }
                }
                public int Index { get; set; }
                public bool InterlaceDetected { get; set; }
                public string Name { get; set; }
                public string Path { get; set; }
                public int Playlist { get; set; }
                //"SubtitleList": [],
                public int Type { get; set; }
                public string VideoCodec { get; set; }
            }
        }

        public class HBProgress
        {
            public class ProgressState
            {
                public double Progress { get; set; }
            }

            public class ScanningState : ProgressState
            {
                public int Preview { get; set; }
                public int PreviewCount { get; set; }
                public int SequenceID { get; set; }
                public int Title { get; set; }
                public int TitleCount { get; set; }
            }

            public class WorkingState : ProgressState
            {
                public int ETASeconds { get; set; }
                public int Hours { get; set; }
                public int Minutes { get; set; }
                public int Pass { get; set; }
                public int PassCount { get; set; }
                public int PassID { get; set; }
                public int Paused { get; set; }
                public double Rate { get; set; }
                public double RateAvg { get; set; }
                public int Seconds { get; set; }
                public int SequenceID { get; set; }
            }

            public ScanningState Scanning { get; set; }
            public WorkingState Working { get; set; }
            public ProgressState Muxing { get; set; }
            public string State { get; set; }

            public double GetCurrentProgress()
            {
                switch (State)
                {
                    case "WORKING":
                        // Calc the current progess as part of the total pass
                        double progress = this.Working.Progress;
                        if (Working.PassCount > 0)
                        {
                            progress = ((Working.Pass - 1) / (double)Working.PassCount) + (Working.Progress / (double)Working.PassCount);
                        }
                        return progress;
                    case "SCANNING":
                        return this.Scanning.Progress;
                    case "MUXING":
                        return this.Muxing.Progress;
                }
                return 0.0;
            }

            public string GetETAString()
            {
                if (State == "WORKING")
                {
                    if (Working.ETASeconds != 0 || Working.Pass > 1)
                    {
                        StringBuilder eta = new StringBuilder();
                        if (Working.PassCount > 0)
                        {
                            if (Working.ETASeconds != 0)
                            {
                                eta.Append(Utils.GetDuration(Working.ETASeconds));
                                eta.Append(" for ");
                            }
                            eta.Append("Pass ");
                            eta.Append(Working.Pass);
                            eta.Append(" of ");
                            eta.Append(Working.PassCount);
                            return eta.ToString();
                        }
                        else
                        {
                            return Utils.GetDuration(Working.ETASeconds);
                        }
                    }
                }
                return null;
            }

            public string GetStateTitle()
            {
                return State[0] + State.Substring(1).ToLower();
            }
        }

        private Process handBrakeProcess = null;
        private bool wasKilled = false;

        public readonly string HandBrakeCliExePath;
        public readonly string HandBrakeProfileFile;

        public readonly string HandBrakeSourceDir;
        public readonly string HandBrakeOutDir;
        public readonly OutputType MovieOutputType;
        public readonly bool ForceSubtitles;

        public HandBrakeService(string handBrakeCliExePath, string handBrakeProfileFile, string handBrakeSourceDir, string handBrakeOutDir, OutputType outputType, bool forceSubtitles)
        {
            HandBrakeCliExePath = handBrakeCliExePath;
            HandBrakeProfileFile = handBrakeProfileFile;
            HandBrakeSourceDir = handBrakeSourceDir;
            HandBrakeOutDir = handBrakeOutDir;
            MovieOutputType = outputType;
            ForceSubtitles = forceSubtitles;
        }

        internal List<DiskTitle> Scan(string file, int titleIndex, bool allTitles, ProgressReporter progressReporter)
        {
            List<DiskTitle> diskTitles = new List<DiskTitle>();

            StopRunningProcess();
            wasKilled = false;

            string cmdParams = $"-i \"{file}\" --scan --no-dvdnav --json";
            if (!allTitles)
            {
                cmdParams += " --main-feature";
            } 
            else
            {
                cmdParams += $" --title {titleIndex}";
            }
            handBrakeProcess = new Process
            {
                StartInfo = new ProcessStartInfo(HandBrakeCliExePath, cmdParams)
            };
            handBrakeProcess.StartInfo.CreateNoWindow = true;
            handBrakeProcess.StartInfo.RedirectStandardOutput = true;
            handBrakeProcess.StartInfo.RedirectStandardError = true;
            handBrakeProcess.StartInfo.UseShellExecute = false;

            handBrakeProcess.EnableRaisingEvents = true;
            StringBuilder output = new StringBuilder();
            StringBuilder error = new StringBuilder();

            bool success = false;
            StringBuilder json = null;
            HandBrakeJsonType jsonType = HandBrakeJsonType.Version;
            ProcessingState processingState = ProcessingState.None;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

            handBrakeProcess.OutputDataReceived += new DataReceivedEventHandler(
                delegate (object sender, DataReceivedEventArgs e)
                {
                    string line = e.Data;
                    if (line == null)
                    {
                        return;
                    }

                    ProcessJson(progressReporter, diskTitles, ref json, ref jsonType, ref processingState, stopwatch, line);
                }
            );

            handBrakeProcess.ErrorDataReceived += new DataReceivedEventHandler(
                delegate (object sender, DataReceivedEventArgs e)
                {
                    error.Append(e.Data);
                    error.Append("\r\n");

                    progressReporter.AppendLog(e.Data, LogEntryType.Trace);
                }
            );
            handBrakeProcess.Start();
            handBrakeProcess.BeginOutputReadLine();
            handBrakeProcess.BeginErrorReadLine();
            handBrakeProcess.WaitForExit();
            if (handBrakeProcess != null)
            {
                handBrakeProcess.CancelOutputRead();
                handBrakeProcess.CancelErrorRead();
                success = handBrakeProcess.ExitCode == 0;
            }

            ProcessJsonString(progressReporter, diskTitles, json, jsonType, processingState, stopwatch);

            if (!success && !wasKilled)
            {
                progressReporter.AppendLog($"Output from Handbrake\r\n{error}", LogEntryType.Debug);
            }
            return diskTitles;
        }

        private void StopRunningProcess()
        {
            if (handBrakeProcess != null)
            {
                try
                {
                    if (!handBrakeProcess.HasExited)
                    {
                        wasKilled = true;
                        handBrakeProcess.Kill();
                    }
                }
                catch (InvalidOperationException)
                {
                    // ignore
                }
                handBrakeProcess = null;
            }
        }

        public void Shutdown()
        {
            StopRunningProcess();
        }

        internal bool Encode(string inputFile, int titleIndex, string outputFile, ProgressReporter progressReporter)
        {
            bool success = false;

            StopRunningProcess();
            wasKilled = false;

            string outputFormat = MovieOutputType == OutputType.MP4 ? "av_mp4" : "av_mkv";
            string subtitles = ForceSubtitles == true ? "--subtitle scan --subtitle-forced" : "";

            string cmdParams = $"--preset-import-file \"{HandBrakeProfileFile}\" -i \"{inputFile}\" -o \"{outputFile}\" --format {outputFormat} {subtitles} --json";
            if (titleIndex != 0)
            {
                cmdParams += $" --title {titleIndex}";
            }

            handBrakeProcess = new Process
            {
                StartInfo = new ProcessStartInfo(HandBrakeCliExePath, cmdParams)
            };
            handBrakeProcess.StartInfo.CreateNoWindow = true;
            handBrakeProcess.StartInfo.RedirectStandardOutput = true;
            handBrakeProcess.StartInfo.RedirectStandardInput = true;
            handBrakeProcess.StartInfo.RedirectStandardError = true;
            handBrakeProcess.StartInfo.UseShellExecute = false;

            handBrakeProcess.EnableRaisingEvents = true;
            StringBuilder error = new StringBuilder();

            List<DiskTitle> diskTitles = new List<DiskTitle>();
            StringBuilder json = null;
            HandBrakeJsonType jsonType = HandBrakeJsonType.Version;
            ProcessingState processingState = ProcessingState.None;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            handBrakeProcess.OutputDataReceived += new DataReceivedEventHandler(
                delegate (object sender, DataReceivedEventArgs e)
                {
                    // append the new data to the data already read-in
                    string line = e.Data;
                    if (line == null)
                    {
                        return;
                    }

                    ProcessJson(progressReporter, diskTitles, ref json, ref jsonType, ref processingState, stopwatch, line);
                }
            );

            handBrakeProcess.ErrorDataReceived += new DataReceivedEventHandler(
                delegate (object sender, DataReceivedEventArgs e)
                {
                    error.Append(e.Data);
                    error.Append("\r\n");

                    progressReporter.AppendLog(e.Data, LogEntryType.Trace);
                }
            );
            handBrakeProcess.Start();
            handBrakeProcess.BeginOutputReadLine();
            handBrakeProcess.BeginErrorReadLine();
            handBrakeProcess.WaitForExit();
            if (handBrakeProcess != null)
            {
                handBrakeProcess.CancelOutputRead();
                handBrakeProcess.CancelErrorRead();
                success = handBrakeProcess.ExitCode == 0;
            }

            ProcessJsonString(progressReporter, diskTitles, json, jsonType, processingState, stopwatch);
            if (!success)
            {
                if (!wasKilled)
                {
                    progressReporter.AppendLog($"Output from Handbrake\r\n{error}", LogEntryType.Debug);
                }
                File.Delete(outputFile);
            }
            return success;
        }

        private static void ProcessJson(ProgressReporter progressReporter, List<DiskTitle> diskTitles, ref StringBuilder json, ref HandBrakeJsonType jsonType, ref ProcessingState processingState, Stopwatch stopwatch, string line)
        {
            Match match = Regex.Match(line, "^(\\S.+):\\s*{");
            if (match.Success)
            {
                // check the old version
                if (json != null)
                {
                    processingState = ProcessJsonString(progressReporter, diskTitles, json, jsonType, processingState, stopwatch);
                }
                // start of json
                json = new StringBuilder();
                json.Append("{ ");
                string type = match.Groups[1].Value;
                switch (type)
                {
                    case "Version":
                        jsonType = HandBrakeJsonType.Version;
                        break;
                    case "Progress":
                        jsonType = HandBrakeJsonType.Progress;
                        break;
                    case "JSON Title Set":
                        jsonType = HandBrakeJsonType.TitleSet;
                        break;
                }
            }
            else if (json != null)
            {
                json.Append(line);
            }
        }

        private static ProcessingState ProcessJsonString(ProgressReporter progressReporter, List<DiskTitle> diskTitles, StringBuilder json, HandBrakeJsonType jsonType, ProcessingState processingState, Stopwatch stopwatch)
        {
            if (json == null)
            {
                return processingState;
            }
            switch (jsonType)
            {
                case HandBrakeJsonType.Version:
                    HBVersion jVersion = JsonSerializer.Deserialize<HBVersion>(json.ToString());
                    //JToken jVersion = JToken.Parse(json.ToString());
                    progressReporter.AppendLog($"Using {jVersion.Name} {jVersion.VersionString} {jVersion.Arch}", LogEntryType.Debug);
                    break;
                case HandBrakeJsonType.Progress:
                    // process
                    HBProgress jProgress = JsonSerializer.Deserialize<HBProgress>(json.ToString());
                    if (jProgress.State == "WORKING")
                    {
                        if (processingState != ProcessingState.Encoding)
                        {
                            progressReporter.CurrentTask = "Encoding";
                            processingState = ProcessingState.Encoding;
                        }
                    }
                    else if (jProgress.State == "SCANNING")
                    {
                        if (processingState != ProcessingState.Scanning)
                        {
                            progressReporter.CurrentTask = "Scanning";
                            processingState = ProcessingState.Scanning;
                        }
                    }
                    else if (jProgress.State == "MUXING")
                    {
                        if (processingState != ProcessingState.Muxing)
                        {
                            progressReporter.CurrentTask = "Muxing";
                            processingState = ProcessingState.Muxing;
                        }
                        // TODO Remove
                        Debug.WriteLine(json.ToString());
                    } else
                    {
                        // TODO Remove
                        Debug.WriteLine(json.ToString());
                    }
                    if (stopwatch.ElapsedMilliseconds > 1000)
                    {
                        progressReporter.CurrentProgress = (jProgress.GetCurrentProgress() * 100);
                        progressReporter.Remaining = jProgress.GetETAString();
                        stopwatch.Restart();
                    }
                    break;
                case HandBrakeJsonType.TitleSet:
                    HBTitleSet jTitleSet = JsonSerializer.Deserialize<HBTitleSet>(json.ToString());
                    int mainFeature = jTitleSet.MainFeature;
                    foreach (HBTitleSet.TitleListData jTitle in jTitleSet.TitleList)
                    {
                        DiskTitle diskTitle = new DiskTitle
                        {
                            TitleName = jTitle.Name,
                            FullMKVPath = jTitle.Path
                        };
                        diskTitle.FileName = Path.GetFileName(diskTitle.FullMKVPath);
                        diskTitle.TitleIndex = jTitle.Index;
                        if (mainFeature == -1 || diskTitle.TitleIndex == mainFeature)
                        {
                            diskTitle.MainMovie = true;
                        }
                        diskTitle.HorizontalResolution = jTitle.Geometry.Height;
                        diskTitle.VerticalResolution = jTitle.Geometry.Width;
                        diskTitle.VideoCodec = jTitle.VideoCodec;
                        diskTitle.Seconds = (jTitle.Duration.Hours * 60 * 60) + (jTitle.Duration.Minutes * 60) + jTitle.Duration.Seconds;
                        diskTitle.Chapters = jTitle.ChapterList.Length;
                        diskTitles.Add(diskTitle);
                    }
                    break;
            }

            return processingState;
        }
    }
}
