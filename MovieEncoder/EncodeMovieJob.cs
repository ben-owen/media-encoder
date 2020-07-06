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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        private string movieTitle;

        public EncodeMovieJob(HandBrakeService handBrakeService, string inputFilePath, int titleIndex, bool keepFiles)
        {
            this.handBrakeService = handBrakeService;
            this.InputFileName = inputFilePath;
            this.keepFiles = keepFiles;
            this.titleIndex = titleIndex;
        }

        public EncodeMovieJob(HandBrakeService handBrakeService, string inputFilePath, string movieTitle, int titleIndex, bool keepFiles)
        {
            this.handBrakeService = handBrakeService;
            this.InputFileName = inputFilePath;
            this.movieTitle = movieTitle;
            this.titleIndex = titleIndex;
            this.keepFiles = keepFiles;
        }

        public override string JobName => $"Encode Movie '{GetJobFileName()}'";

        private object GetJobFileName()
        {
            if (movieTitle != null)
            {
                return $"{InputFileName} {movieTitle}";
            } else
            {
                return Path.GetFileName(InputFileName);
            }
        }

        public override bool RunJob(JobQueue jobRunner)
        {
            // wait for any copy operation to finish
            if (Utils.IsFileLocked(new FileInfo(InputFileName)))
            {
                progressReporter.CurrentTask = $"Waiting on '{Path.GetFileName(InputFileName)}' to become available";
                while (Utils.IsFileLocked(new FileInfo(InputFileName)))
                {
                    System.Threading.Thread.Sleep(100);
                }
            }

            if (!File.Exists(InputFileName) && !Directory.Exists(InputFileName))
            {
                throw new JobException($"Could not find input file '{InputFileName}'");
            }

            progressReporter.CurrentTask = "Getting titles from file using Handbrake";
            //List<DiskTitle> diskTitles = handBrakeService.Scan(InputFileName, titleIndex, false, progressReporter);
            //if (diskTitles.Count > 0)
            {
                // only take 1st
                //DiskTitle diskTitle = diskTitles[0];

                // if input is a directory, then assume its a drive
                string outputFile;
                if (Directory.Exists(InputFileName) && movieTitle != null)
                {
                    // generate the output file based on the scan output
                    outputFile = Path.Combine(OutputDir, movieTitle);
                } else
                {
                    // skip if the output file already exists
                    outputFile = Path.Combine(OutputDir, Path.GetFileName(InputFileName));
                }
                if (File.Exists(outputFile)) {
                    throw new JobException($"Encoding may have already been run. Output file '{outputFile}' already exists.");
                }

                progressReporter.CurrentTask = "Start encoding using Handbrake";
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
                    } else
                    {
                        throw new Exception($"Missing output file {outputFile}. Not removing original file.");
                    }
                }
            }
            return false;
        }
    }
}
