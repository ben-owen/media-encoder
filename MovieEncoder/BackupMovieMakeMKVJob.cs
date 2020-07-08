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

namespace MovieEncoder
{
    class BackupMovieMakeMKVJob : Job
    {
        private readonly MakeMKVService makeMKVService;
        private readonly HandBrakeService handBrakeService;
        private readonly DiskTitle diskTitle;
        private readonly bool keepMovies;

        public override string JobName => "Backup Movie " + diskTitle.FileName;

        public BackupMovieMakeMKVJob(MakeMKVService makeMKVService, HandBrakeService handBrakeService, DiskTitle diskTitle, bool keepMovies = false)
        {
            System.Diagnostics.Debug.Assert(diskTitle != null);
            this.makeMKVService = makeMKVService;
            this.handBrakeService = handBrakeService;
            this.diskTitle = diskTitle;
            this.keepMovies = keepMovies;
        }

        public override bool RunJob(JobQueue jobQueue)
        {
            // Backup
            if (!makeMKVService.Backup(diskTitle, progressReporter))
            {
                return false;
            }
            if (keepMovies)
            {
                // This means the source of the next job is the output of the current job. These files will not be deleted after encoding.
                jobQueue.AddJob(new EncodeMovieJob(handBrakeService, diskTitle.FullMKVPath, true));
            }
            return true;
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
