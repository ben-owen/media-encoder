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

        public EncodeMovieJob(HandBrakeService handBrakeService, string inputFilePath, bool keepFiles)
        {
            this.handBrakeService = handBrakeService;
            this.InputFileName = inputFilePath;
            this.keepFiles = keepFiles;
        }

        public override string JobName => $"Encode Movie '{Path.GetFileName(InputFileName)}'";

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

            if (!File.Exists(InputFileName))
            {
                throw new JobException($"Could not find input file '{InputFileName}'");
            }

            DiskTitle diskTitle = handBrakeService.Scan(InputFileName);
            if (diskTitle != null)
            {
                // skip if the output file already exists
                if (File.Exists(Path.Combine(OutputDir, Path.GetFileName(diskTitle.FullMKVPath)))) {
                    throw new JobException($"Encoding may have already been run. Output file '{diskTitle.FullMKVPath}' already exists.");
                }
                if (handBrakeService.Encode(diskTitle, OutputDir, progressReporter))
                {
                    // always delete from the source directory.
                    string sourceDirectory = handBrakeService.HandBrakeSourceDir;
                    if (sourceDirectory.EndsWith(Path.DirectorySeparatorChar.ToString()))
                    {
                        sourceDirectory = sourceDirectory.Substring(0, sourceDirectory.Length - 1);
                    }
                    if (!keepFiles || Path.GetDirectoryName(InputFileName).StartsWith(sourceDirectory)) {
                        // Delete the source file
                        File.Delete(diskTitle.FullMKVPath);
                        // check for source directory
                        string directory = Path.GetDirectoryName(diskTitle.FullMKVPath);
                        while (!directory.Equals(sourceDirectory))
                        {
                            // if the name of the directory matches the file name and
                            // the directory is empty, delete it.
                            if (diskTitle.FullMKVPath.StartsWith(directory))
                            {
                                if (Directory.GetFiles(directory).Length == 0)
                                {
                                    Directory.Delete(directory);
                                }
                            }
                            directory = Path.GetDirectoryName(directory);
                        }
                        // TODO 
                    }
                    return true;
                }
            }
            return false;
        }
    }
}
