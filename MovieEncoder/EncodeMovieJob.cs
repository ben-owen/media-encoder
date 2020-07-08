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
using System.IO;

namespace MovieEncoder
{
    class EncodeMovieJob : Job
    {
        internal readonly string InputFileName;
        internal string OutputDir
        {
            get { return handBrakeService.HandBrakeOutDir; }
        }
        private readonly HandBrakeService handBrakeService;
        private readonly bool keepFiles;
        private readonly int titleIndex;
        private readonly int movieIndex;
        private readonly string movieTitle;

        public EncodeMovieJob(HandBrakeService handBrakeService, string inputFilePath, bool keepFiles)
        {
            this.handBrakeService = handBrakeService;
            this.InputFileName = inputFilePath;
            this.keepFiles = keepFiles;
            this.titleIndex = 0;
        }

        public EncodeMovieJob(HandBrakeService handBrakeService, string inputFilePath, string movieTitle, int titleIndex, int movieIndex, bool keepFiles)
        {
            this.handBrakeService = handBrakeService;
            this.InputFileName = inputFilePath;
            this.movieTitle = movieTitle;
            this.titleIndex = titleIndex;
            this.movieIndex = movieIndex;
            this.keepFiles = keepFiles;
        }

        public override string JobName => $"Encode Movie '{GetJobFileName()}'";

        private object GetJobFileName()
        {
            if (movieTitle != null)
            {
                return $"{InputFileName} {movieTitle}";
            }
            else
            {
                return Path.GetFileName(InputFileName);
            }
        }

        public override bool RunJob(JobQueue jobRunner)
        {
            if (!File.Exists(InputFileName) && !Directory.Exists(InputFileName))
            {
                throw new JobException($"Could not find input file '{InputFileName}'");
            }

            // wait for any copy operation to finish
            if (Utils.IsFileLocked(new FileInfo(InputFileName)))
            {
                progressReporter.CurrentTask = $"Waiting on '{Path.GetFileName(InputFileName)}' to become available";
                while (Utils.IsFileLocked(new FileInfo(InputFileName)))
                {
                    System.Threading.Thread.Sleep(100);
                }
            }

            progressReporter.CurrentTask = "Getting titles from file using Handbrake";

            // if input is a directory, then assume its a drive
            string outputFile;
            if (Directory.Exists(InputFileName) && movieTitle != null)
            {
                // generate the output file based on the scan output
                // get movie name
                outputFile = Path.Combine(OutputDir, movieTitle);
                Directory.CreateDirectory(outputFile);
                outputFile = Path.Combine(outputFile, movieTitle + String.Format("_t{0:00}", movieIndex));
            }
            else
            {
                outputFile = Path.Combine(OutputDir, Path.GetFileName(InputFileName));
            }

            outputFile = RenameExtension(outputFile);

            // skip if the output file already exists
            if (File.Exists(outputFile))
            {
                throw new JobException($"Encoding may have already been run. Output file '{outputFile}' already exists.");
            }

            progressReporter.CurrentTask = $"Start encoding from '{InputFileName}' to '{outputFile}' using Handbrake";
            if (handBrakeService.Encode(InputFileName, titleIndex, outputFile, progressReporter))
            {
                // if the output file exists, remove the input file
                if (File.Exists(outputFile))
                {
                    // always delete from the source directory.
                    string sourceDirectory = handBrakeService.HandBrakeSourceDir;
                    if (sourceDirectory.EndsWith(Path.DirectorySeparatorChar.ToString()))
                    {
                        sourceDirectory = sourceDirectory.Substring(0, sourceDirectory.Length - 1);
                    }
                    if (movieTitle == null && (!keepFiles || Path.GetDirectoryName(InputFileName).StartsWith(sourceDirectory)))
                    {
                        // Delete the source file
                        File.Delete(InputFileName);
                        // check for source directory
                        string directory = Path.GetDirectoryName(InputFileName);
                        while (!directory.Equals(sourceDirectory))
                        {
                            // if the name of the directory matches the file name and
                            // the directory is empty, delete it.
                            if (InputFileName.StartsWith(directory))
                            {
                                if (Directory.GetFiles(directory).Length == 0)
                                {
                                    Directory.Delete(directory);
                                }
                            }
                            directory = Path.GetDirectoryName(directory);
                        }
                    }
                    return true;
                }
                else
                {
                    throw new Exception($"Missing output file {outputFile}. Not removing original file.");
                }
            }
            return false;
        }

        private string RemoveExtension(string movieTitle)
        {
            int idx = movieTitle.LastIndexOf('.');
            return movieTitle.Substring(0, idx - 1);
        }

        private string RenameExtension(string outputFile)
        {
            string ext = ".mkv";
            if (handBrakeService.MovieOutputType == HandBrakeService.OutputType.MP4)
            {
                ext = ".mp4";
            }

            if (!outputFile.EndsWith(ext))
            {
                // rename
                int idx = outputFile.LastIndexOf('.');
                if (idx == -1)
                {
                    idx = outputFile.Length;
                }
                outputFile = outputFile.Substring(0, idx) + ext;
            }
            return outputFile;
        }
    }
}
