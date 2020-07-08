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
using System.Collections.Generic;
using System.Linq;

namespace MovieEncoder
{
    class BackupDiskMakeMKVJob : Job
    {
        private readonly MakeMKVService makeMKVService;
        private readonly HandBrakeService handBrakeService;
        private readonly string driveName;
        private readonly bool keepMovies;
        private readonly int minMovieLen;

        public override string JobName => "Backing Up Disk " + driveName;

        public BackupDiskMakeMKVJob(MakeMKVService makeMKVService, HandBrakeService handBrakeService, string driveName, bool keepMovies, int minMovieLen)
        {
            this.makeMKVService = makeMKVService;
            this.handBrakeService = handBrakeService;
            this.driveName = driveName;
            this.keepMovies = keepMovies;
            this.minMovieLen = minMovieLen;
        }

        public override bool RunJob(JobQueue jobQueue)
        {
            List<DiskTitle> diskTitles = makeMKVService.GetDiskTitles(driveName, progressReporter);
            if (diskTitles.Count > 0)
            {
                if (makeMKVService.MakeMKVBackupAll == false)
                {
                    // only do the main movie
                    List<DiskTitle> singleTitle = new List<DiskTitle>
                    {
                        PickWinner(diskTitles)
                    };
                    diskTitles = singleTitle;
                }
                else
                {
                    progressReporter.AppendLog($"Backing up {diskTitles.Count} movies", LogEntryType.Info);
                }
                foreach (DiskTitle diskTitle in diskTitles)
                {
                    if (diskTitle.Seconds >= minMovieLen)
                    {
                        // Backup Movie Job
                        jobQueue.AddJob(new BackupMovieMakeMKVJob(makeMKVService, handBrakeService, diskTitle, keepMovies), true);
                    }
                }

                return true;
            }

            return false;
        }

        private DiskTitle PickWinner(List<DiskTitle> diskTitles)
        {
            if (diskTitles.Count == 0)
            {
                return null;
            }

            diskTitles.Sort(ComparDiskTitleByDuration);
            diskTitles.Reverse();

            return diskTitles.ElementAt(0);
        }

        private static int ComparDiskTitleByDuration(DiskTitle x, DiskTitle y)
        {
            if (x == null)
            {
                if (y == null)
                {
                    return 0;
                }
                else
                {
                    return -1;
                }
            }
            else
            {
                if (y == null)
                {
                    return 1;
                }
                else
                {
                    if (x.Seconds < y.Seconds)
                    {
                        return -1;
                    }
                    else if (x.Seconds == y.Seconds)
                    {
                        return 0;
                    }
                    else
                    {
                        return 1;
                    }
                }
            }
        }
    }
}
