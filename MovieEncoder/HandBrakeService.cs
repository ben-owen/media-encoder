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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MovieEncoder
{
    class HandBrakeService
    {
        private Process handBrakeProcess = null;

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

        internal DiskTitle Scan(string file)
        {
            DiskTitle diskTitle = null;

            StopRunningProcess();

            handBrakeProcess = new Process();
            handBrakeProcess.StartInfo = new ProcessStartInfo(HandBrakeCliExePath, "-i \"" + file + "\" --scan --main-feature --no-dvdnav --json");
            handBrakeProcess.StartInfo.CreateNoWindow = true;
            handBrakeProcess.StartInfo.RedirectStandardOutput = true;
            handBrakeProcess.StartInfo.RedirectStandardError = true;
            handBrakeProcess.StartInfo.UseShellExecute = false;

            handBrakeProcess.EnableRaisingEvents = true;
            StringBuilder output = new StringBuilder();
            StringBuilder error = new StringBuilder();

            handBrakeProcess.OutputDataReceived += new DataReceivedEventHandler(
                delegate (object sender, DataReceivedEventArgs e)
                {
                    output.Append(e.Data);
                    output.Append("\r\n");
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
            }

            using (StringReader reader = new StringReader(output.ToString()))
            {
                bool readJsonProgress = false;
                bool readJsonTitle = false;
                string line = string.Empty;
                StringBuilder jsonString = null;
                List<StringBuilder> jsonTitles = new List<StringBuilder>();
                do
                {
                    line = reader.ReadLine();
                    if (line != null)
                    {
                        //Console.WriteLine(line);
                        Match match = Regex.Match(line, "^(\\w+\\s*)+:.*", RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            if (readJsonProgress)
                            {
                                Newtonsoft.Json.Linq.JToken.Parse(jsonString.ToString());
                                //readProgressJson(currentJobInfo, jsonProgress);
                            }
                            if (readJsonTitle && jsonString != null)
                            {
                                jsonTitles.Add(jsonString);
                            }
                            readJsonTitle = false;
                            readJsonProgress = false;
                            jsonString = new StringBuilder();
                            if (line.StartsWith("Progress:"))
                            {
                                readJsonProgress = true;
                                jsonString.Append(line.Substring(9).Trim());
                            }
                            else if (line.StartsWith("JSON Title Set:"))
                            {
                                readJsonTitle = true;
                                jsonString.Append(line.Substring(15));
                            }
                        }
                        else if (readJsonTitle || readJsonProgress)
                        {
                            jsonString.Append(line);
                        }
                    }
                } while (line != null);

                if (readJsonProgress)
                {
                    Newtonsoft.Json.Linq.JToken.Parse(jsonString.ToString());
                    //readProgressJson(currentJobInfo, jsonProgress);
                }
                if (readJsonTitle)
                {
                    jsonTitles.Add(jsonString);
                }

                if (jsonTitles.Count > 0)
                {
                    jsonString = jsonTitles[0];
                    Newtonsoft.Json.Linq.JToken jTokens = Newtonsoft.Json.Linq.JToken.Parse(jsonString.ToString());
                    if (jTokens["TitleList"] != null && ((Newtonsoft.Json.Linq.JArray)jTokens["TitleList"]).Count > 0)
                    {
                        // we are only taking the 1st title
                        Newtonsoft.Json.Linq.JToken jsonTitle = jTokens["TitleList"][0];
                        diskTitle = new DiskTitle();
                        if (jsonTitle["Name"] != null)
                        {
                            diskTitle.TitleName = (string)jsonTitle["Name"];
                        }
                        if (jsonTitle["Path"] != null)
                        {
                            diskTitle.FullMKVPath = (string)jsonTitle["Path"];
                            diskTitle.FileName = Path.GetFileName(diskTitle.FullMKVPath);
                        }
                        if (jsonTitle["Index"] != null)
                        {
                            diskTitle.TitleIndex = (int)jsonTitle["Index"];
                        }
                        if (jsonTitle["Geometry"] != null)
                        {
                            if (jsonTitle["Geometry"]["Height"] != null)
                            {
                                diskTitle.HorizontalResolution = (int)jsonTitle["Geometry"]["Height"];
                            }
                            if (jsonTitle["Geometry"]["Width"] != null)
                            {
                                diskTitle.VerticalResolution = (int)jsonTitle["Geometry"]["Width"];
                            }
                        }
                        if (jsonTitle["VideoCodec"] != null)
                        {
                            diskTitle.VideoCodec = (string)jsonTitle["VideoCodec"];
                        }
                    }
                }
            }

            return diskTitle;
        }

        private void StopRunningProcess()
        {
            if (handBrakeProcess != null)
            {
                try
                {
                    if (!handBrakeProcess.HasExited)
                    {
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

        internal bool Encode(DiskTitle diskTitle, string outPath, ProgressReporter reporter)
        {
            bool success = false;

            StopRunningProcess();

            string outFilePath = Path.Combine(outPath, Path.GetFileName(diskTitle.FullMKVPath));
            handBrakeProcess = new Process();
            handBrakeProcess.StartInfo = new ProcessStartInfo(HandBrakeCliExePath, "--preset-import-file \"" + HandBrakeProfileFile + "\" -i \"" + diskTitle.FullMKVPath +
                "\" -o \"" + outFilePath + "\" --format av_mkv --subtitle scan --subtitle - forced --json");
            handBrakeProcess.StartInfo.CreateNoWindow = true;
            handBrakeProcess.StartInfo.RedirectStandardOutput = true;
            handBrakeProcess.StartInfo.RedirectStandardInput = true;
            handBrakeProcess.StartInfo.RedirectStandardError = true;
            handBrakeProcess.StartInfo.UseShellExecute = false;

            handBrakeProcess.EnableRaisingEvents = true;
            StringBuilder output = new StringBuilder();

            bool scanning = false;
            bool encoding = false;
            bool muxing = false;
            bool readJson = false;
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            StringBuilder jsonString = new StringBuilder();
            handBrakeProcess.OutputDataReceived += new DataReceivedEventHandler(

                delegate (object sender, DataReceivedEventArgs e)
                {
                    // append the new data to the data already read-in
                    string line = e.Data;
                    if (line == null)
                    {
                        return;
                    }
                    //Debug.WriteLine(e.Data);
                    Match match = Regex.Match(line, "^(\\w+\\s*)+:.*", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        if (readJson && reporter != null)
                        {
                            JToken jTokens = JToken.Parse(jsonString.ToString());
                            string state = (string)jTokens["State"];
                            string stateKey = "";
                            if (state == "WORKING")
                            {
                                stateKey = "Working";
                                if (!encoding)
                                {
                                    reporter.CurrentTask = "Encoding "; // + Path.GetFileName(diskTitle.FullMKVPath) + "' to '" + Path.GetDirectoryName(outPath) + "'";
                                    encoding = true;
                                }
                                reporter.CurrentProgress = ((double)jTokens[stateKey]["Progress"] * 100);
                            }
                            else if (state == "SCANNING")
                            {
                                stateKey = "Scanning";
                                if (!scanning)
                                {
                                    reporter.CurrentTask = "Scanning "; // + Path.GetFileName(diskTitle.FullMKVPath) + "'";
                                    scanning = true;
                                }
                                reporter.CurrentProgress = ((double)jTokens[stateKey]["Progress"] * 100);
                            }
                            else if (state == "MUXING")
                            {
                                stateKey = "Muxing";
                                if (!muxing)
                                {
                                    reporter.CurrentTask = "Muxing "; // + Path.GetFileName(diskTitle.FullMKVPath) + "'";
                                    muxing = true;
                                }
                                reporter.CurrentProgress = ((double)jTokens[stateKey]["Progress"] * 100);
                            }
                            if (stopwatch.ElapsedMilliseconds > 1000)
                            {
                                if (jTokens[stateKey] != null)
                                {
                                    if (jTokens[stateKey]["ETASeconds"] != null)
                                    {
                                        reporter.Remaining = Utils.GetDuration((int)jTokens[stateKey]["ETASeconds"]);
                                    }
                                }
                                stopwatch.Restart();
                            }
                        }
                        readJson = false;
                        jsonString = new StringBuilder();
                        if (line.StartsWith("Progress:"))
                        {
                            readJson = true;
                            jsonString.Append(line.Substring(9).Trim());
                        }
                    }
                    else if (readJson)
                    {
                        jsonString.Append(line);
                    }
                }
            );

            handBrakeProcess.ErrorDataReceived += new DataReceivedEventHandler(
                delegate (object sender, DataReceivedEventArgs e)
                {
                    output.Append(e.Data);
                    output.Append("\r\n");
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
            if (!success)
            {
                reporter.AddError(output.ToString());
                File.Delete(outFilePath);
            }
            /*
        boolean success = false;
        Process exec = null;
        Path outPath = movie.isHd() ? properties.getHandbreak().getHdDestDir() : properties.getHandbreak().getSdDestDir();
        outPath = outPath.resolve(srcPath.getFileName());
        try {
            exec = ProcessUtil.execWithoutStdErr(properties.getHandbreak().getHandbreakcliPathString(), "--preset-import-file",
                    properties.getHandbreak().getHdPresetFileString(), "-i", srcPath.toString(),
                    "-o", outPath.toString(), "--format", "av_mkv", "--subtitle", "scan", "--subtitle-forced", "--json");
            InputStream stdout = exec.getInputStream();
            InputStream stderr = exec.getErrorStream();
            BufferedReader binout = new BufferedReader(new InputStreamReader(stdout));
            BufferedReader binerr = new BufferedReader(new InputStreamReader(stderr));
            String line = null;
            boolean readingOut = true;
            boolean readingErr = true;
            long reportTime = System.currentTimeMillis() + TimeUnit.SECONDS.toMillis(10);
            encoding = true;
            boolean readJson = false;
            StringBuffer jsonString = new StringBuffer();
            currentJobInfo.setSourcePath(srcPath);
            currentJobInfo.setDestPath(outPath);
            currentJobInfo.setTaskCount(1);
            currentJobInfo.setTaskIndex(1);
            while (readingOut || readingErr) {
                if (!binout.ready() && !binerr.ready() && !exec.isAlive()) {
                    log.info("TEST 6 out {}, err {}, exec {}", binout.ready(), binerr.ready(), exec);
                    break;
                }
                if (binout.ready()) {
                    line = binout.readLine();
                    if (line == null) {
                        readingOut = false;
                    }
                    if (line.matches("(\\w+\\s*)+:.*")) {
                        if (readJson) {
                            JsonNode jsonProgress = objectMapper.readTree(jsonString.toString());
                            readProgressJson(currentJobInfo, jsonProgress);
                            if (System.currentTimeMillis() > reportTime) {
                                reportTime = System.currentTimeMillis() + TimeUnit.SECONDS.toMillis(10);
                                logProgress(srcPath, jsonProgress, currentJobInfo);
                            }
                        }
                        readJson = false;
                        jsonString = new StringBuffer();
                        if (line.startsWith("Progress:")) {
                            readJson = true;
                            jsonString.append(line.substring(9).trim());
                        }
                    } else if (readJson) {
                        jsonString.append(line);
                    }
                    if (System.currentTimeMillis() > reportTime) {
                        reportTime = System.currentTimeMillis() + TimeUnit.SECONDS.toMillis(10);
                        log.info(line);
                    }
                }
                if (binerr.ready()) {
                    line = binerr.readLine();
                    if (line == null) {
                        readingErr = false;
                    }
                    log.debug(line);
                }
                Thread.sleep(0);
            }
            if (exec.exitValue() == 0) {
                success = true;
            }
        } catch (IOException e) {
            throw new RuntimeException(e);
        } catch (InterruptedException e) {
            //ignore
        } finally {
            if (!success) {
                // remove the partial file
                try {
                    exec.destroyForcibly();
                    exec.onExit().get(1, TimeUnit.SECONDS);
                    Files.deleteIfExists(outPath);
                } catch (IOException | InterruptedException | ExecutionException | TimeoutException ex) {
                    log.error(ex.getLocalizedMessage(), ex);
                }
            }
            currentJobInfo = null;
            encoding = false;
        }
        return success;
            */
            return success;
        }
    }
}
