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
    class BackupMovieJob : Job
    {
        private MakeMKVService makeMKVService;
        private readonly HandBrakeService handBrakeService;
        private readonly string driveName;
        private readonly bool keepMovies;

        public override string JobName => "Backup Disk " + driveName;

        public BackupMovieJob(MakeMKVService makeMKVService, HandBrakeService handBrakeService, string driveName, bool keepMovies = false)
        {
            this.makeMKVService = makeMKVService;
            this.handBrakeService = handBrakeService;
            this.driveName = driveName;
            this.keepMovies = keepMovies;
        }

        public override bool RunJob(JobQueue jobQueue)
        {
            // TODO - Update progress to reflect multiple files when saving all

            List<DiskTitle> diskTitles = makeMKVService.GetDiskTitles(driveName, progressReporter);
            DiskTitle diskTitle = PickWinner(diskTitles);
            if (makeMKVService.MakeMKVBackupAll == false)
            {
                diskTitles = new List<DiskTitle>();
                diskTitles.Add(diskTitle);
            }
            foreach (DiskTitle disk in diskTitles) 
            {
                // Backup
                if (!makeMKVService.Backup(disk, progressReporter))
                {
                    return false;
                }
                if (keepMovies)
                {
                    // This means the source of the next job is the output of the current job. These files will not be deleted after encoding.
                    jobQueue.AddJob(new EncodeMovieJob(handBrakeService, diskTitle.FullMKVPath, true));
                }
            }        
            return true;
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

        private static int ComparDiskTitleByHorizontalResolution(DiskTitle x, DiskTitle y)
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
                    if (x.HorizontalResolution < y.HorizontalResolution)
                    {
                        return -1;
                    }
                    else if (x.HorizontalResolution == y.HorizontalResolution)
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
