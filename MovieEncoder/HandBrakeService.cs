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
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace MovieEncoder
{
    class HandBrakeService
    {
        private enum HandBrakeJsonType
        {
            Version,
            Progress,
            TitleSet
        }
        private enum ProcessingState
        {
            None,
            Encoding,
            Scanning,
            Muxing
        }

        private Process handBrakeProcess = null;
        private bool wasKilled = false;

        public readonly string HandBrakeCliExePath;
        public readonly string HandBrakeProfileFile;

        public readonly string HandBrakeSourceDir;
        public readonly string HandBrakeOutDir;

        public HandBrakeService(string handBrakeCliExePath, string handBrakeProfileFile, string handBrakeSourceDir, string handBrakeOutDir)
        {
            this.HandBrakeCliExePath = handBrakeCliExePath;
            this.HandBrakeProfileFile = handBrakeProfileFile;
            this.HandBrakeSourceDir = handBrakeSourceDir;
            this.HandBrakeOutDir = handBrakeOutDir;
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
            handBrakeProcess = new Process();
            handBrakeProcess.StartInfo = new ProcessStartInfo(HandBrakeCliExePath, cmdParams);
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
                progressReporter.AppendLog($"Output from Handbrake\r\n{error.ToString()}", LogEntryType.Debug);
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

            string cmdParams = $"--preset-import-file \"{HandBrakeProfileFile}\" -i \"{inputFile}\" -o \"{outputFile}\" --format av_mkv --subtitle scan --subtitle-forced --json";
            if (titleIndex != 0)
            {
                cmdParams += $" --title {titleIndex}";
            }

            handBrakeProcess = new Process();
            handBrakeProcess.StartInfo = new ProcessStartInfo(HandBrakeCliExePath, cmdParams);
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
                progressReporter.AppendLog($"Output from Handbrake\r\n{error.ToString()}", LogEntryType.Debug);
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
            //Debug.WriteLine(line);
            System.Threading.Thread.Sleep(100);
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
                    JToken jVersion = JToken.Parse(json.ToString());
                    progressReporter.AppendLog($"Using {jVersion["Name"]} {jVersion["VersionString"]} {jVersion["Arch"]}", LogEntryType.Debug);
                    break;
                case HandBrakeJsonType.Progress:
                    // process
                    JToken jProgress = JToken.Parse(json.ToString());
                    string state = (string)jProgress["State"];
                    string stateKey = "";
                    if (state == "WORKING")
                    {
                        stateKey = "Working";
                        if (processingState != ProcessingState.Encoding)
                        {
                            progressReporter.CurrentTask = "Encoding";
                            processingState = ProcessingState.Encoding;
                        }
                    }
                    else if (state == "SCANNING")
                    {
                        stateKey = "Scanning";
                        if (processingState != ProcessingState.Scanning)
                        {
                            progressReporter.CurrentTask = "Scanning";
                            processingState = ProcessingState.Scanning;
                        }
                    }
                    else if (state == "MUXING")
                    {
                        stateKey = "Muxing";
                        if (processingState != ProcessingState.Muxing)
                        {
                            progressReporter.CurrentTask = "Muxing";
                            processingState = ProcessingState.Muxing;
                        }
                    }
                    if (stopwatch.ElapsedMilliseconds > 1000)
                    {
                        if (stateKey != "")
                            progressReporter.CurrentProgress = ((double)jProgress[stateKey]["Progress"] * 100);

                        if (jProgress[stateKey] != null)
                        {
                            if (jProgress[stateKey]["ETASeconds"] != null)
                            {
                                progressReporter.Remaining = Utils.GetDuration((int)jProgress[stateKey]["ETASeconds"]);
                            }
                        }
                        stopwatch.Restart();
                    }
                    break;
                case HandBrakeJsonType.TitleSet:
                    JToken jTitleSet = JToken.Parse(json.ToString());
                    int mainFeature = -1;
                    if (jTitleSet["MainFeature"] != null)
                    {
                        mainFeature = (int)jTitleSet["MainFeature"];
                    }
                    if (jTitleSet["TitleList"] != null && ((JArray)jTitleSet["TitleList"]).Count > 0)
                    {
                        foreach (JToken jTitle in (JArray)jTitleSet["TitleList"])
                        {
                            DiskTitle diskTitle = new DiskTitle();
                            if (jTitle["Name"] != null)
                            {
                                diskTitle.TitleName = (string)jTitle["Name"];
                            }
                            if (jTitle["Path"] != null)
                            {
                                diskTitle.FullMKVPath = (string)jTitle["Path"];
                                diskTitle.FileName = Path.GetFileName(diskTitle.FullMKVPath);
                            }
                            if (jTitle["Index"] != null)
                            {
                                diskTitle.TitleIndex = (int)jTitle["Index"];
                                if (mainFeature == -1 || diskTitle.TitleIndex == mainFeature)
                                {
                                    diskTitle.MainMovie = true;
                                }
                            }
                            if (jTitle["Geometry"] != null)
                            {
                                if (jTitle["Geometry"]["Height"] != null)
                                {
                                    diskTitle.HorizontalResolution = (int)jTitle["Geometry"]["Height"];
                                }
                                if (jTitle["Geometry"]["Width"] != null)
                                {
                                    diskTitle.VerticalResolution = (int)jTitle["Geometry"]["Width"];
                                }
                            }
                            if (jTitle["VideoCodec"] != null)
                            {
                                diskTitle.VideoCodec = (string)jTitle["VideoCodec"];
                            }
                            if (jTitle["Duration"] != null)
                            {
                                int hours = 0;
                                if (jTitle["Duration"]["Hours"] != null)
                                {
                                    hours = (int)jTitle["Duration"]["Hours"];
                                }
                                int minutes = 0;
                                if (jTitle["Duration"]["Minutes"] != null)
                                {
                                    minutes = (int)jTitle["Duration"]["Minutes"];
                                }
                                int seconds = 0;
                                if (jTitle["Duration"]["Seconds"] != null)
                                {
                                    seconds = (int)jTitle["Duration"]["Seconds"];
                                }
                                diskTitle.Seconds = (hours * 60 * 60) + (minutes * 60) + seconds;
                            }
                            diskTitles.Add(diskTitle);
                        }
                    }
                    break;
            }

            return processingState;
        }
    }
}
