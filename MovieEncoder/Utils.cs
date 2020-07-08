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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace MovieEncoder
{
    class Utils
    {
        public static string GetDuration(int seconds)
        {
            if (seconds <= 0)
            {
                return "";
            }

            StringBuilder dur = new StringBuilder();
            if (seconds / (60 * 60) > 0)
            {
                int hours = seconds / (60 * 60);
                seconds -= (hours * 60 * 60);
                dur.Append(hours);
                dur.Append(" Hour");
                if (hours > 1)
                {
                    dur.Append("s");
                }
            }
            if (seconds / 60 > 0)
            {
                int minutes = seconds / 60;
                seconds -= (minutes * 60);
                if (dur.Length > 0)
                {
                    dur.Append(" ");
                }
                dur.Append(minutes);
                dur.Append(" Minute");
                if (minutes > 1)
                {
                    dur.Append("s");
                }
            }
            if (dur.Length > 0)
            {
                dur.Append(" ");
            }
            dur.Append(seconds);
            dur.Append(" Second");
            if (seconds > 1)
            {
                dur.Append("s");
            }
            return dur.ToString();
        }

        internal static void CopyFile(string sourcePath, string destPath, ProgressReporter progressReporter)
        {
            byte[] buffer = new byte[1024 * 1024]; // 1MB buffer
            progressReporter.CurrentTask = $"Copying '{sourcePath}' to '{destPath}'";
            progressReporter.MaxProgress = 100.0;
            progressReporter.CurrentProgress = 0.0;

            if (Directory.Exists(sourcePath))
            {
                throw new Exception("SourcePath is a directory");
            }

            if (Directory.Exists(destPath))
            {
                throw new Exception("DestPath is a directory");
            }

            bool canceled = false;
            using (FileStream source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read))
            {
                long fileLength = source.Length;
                using (FileStream dest = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                {
                    long totalBytes = 0;
                    int currentBlockSize = 0;

                    while ((currentBlockSize = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        totalBytes += currentBlockSize;
                        double percentage = (double)totalBytes * 100.0 / fileLength;

                        dest.Write(buffer, 0, currentBlockSize);

                        progressReporter.CurrentProgress = percentage;

                        if (progressReporter.Shutdown)
                        {
                            canceled = true;
                            break;
                        }
                    }
                }
            }
            if (canceled)
            {
                File.Delete(destPath);
            }
        }

        internal static bool IsFileLocked(FileInfo file)
        {
            FileStream stream = null;
            if (!file.Exists)
            {
                return false;
            }
            try
            {
                stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.None);
            }
            catch (IOException)
            {
                //the file is unavailable because it is:
                //still being written to
                //or being processed by another thread
                //or does not exist (has already been processed)
                return true;
            }
            finally
            {
                if (stream != null)
                    stream.Close();
            }

            //file is not locked
            return false;
        }

        public static bool IsMovieFile(string path)
        {
            if (path != null && path.EndsWith(".mkv") || path.EndsWith(".mp4"))
            {
                return true;
            }
            return false;
        }
    }
}
