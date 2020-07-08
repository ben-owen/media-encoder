using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MovieEncoder
{
    class BackupDiskHandBrakeJob : Job
    {
        private readonly HandBrakeService handBrakeService;
        private readonly string driveName;
        private readonly bool backupAll;
        private readonly int minMovieLen;

        public override string JobName => "Scan Disk " + driveName;

        public BackupDiskHandBrakeJob(HandBrakeService handBrakeService, string driveName, bool backupAll, int minMovieLen)
        {
            this.handBrakeService = handBrakeService;
            this.driveName = driveName;
            this.backupAll = backupAll;
            this.minMovieLen = minMovieLen;
        }

        public override bool RunJob(JobQueue jobRunner)
        {
            List<DiskTitle> diskTitles = handBrakeService.Scan(driveName, 0, true, progressReporter);
            if (diskTitles.Count > 0)
            {
                int n = 0;
                // order them
                diskTitles.Sort((o1, o2) => {
                    if (o1.Seconds > o2.Seconds)
                    {
                        return -1;
                    }
                    else if (o1.Seconds == o2.Seconds)
                    {
                        return 0;
                    }
                    else
                    {
                        return 1;
                    }
                });
                DiskTitle mainTitle = diskTitles.Find((o) => o.MainMovie == true);
                if (mainTitle != null)
                {
                    diskTitles.Remove(mainTitle);
                    if (!backupAll)
                    {
                        diskTitles.Clear();
                        diskTitles.Add(mainTitle);
                    }
                    else
                    {
                        diskTitles.Insert(0, mainTitle);
                    }
                } else if (!backupAll) {
                    // pick the 1st 
                    mainTitle = diskTitles[0];
                    diskTitles.Clear();
                    diskTitles.Add(mainTitle);
                }

                foreach (DiskTitle title in diskTitles)
                {
                    if (title.Seconds >= minMovieLen)
                    {
                        string cleanTitle = GetMovieTitle(title) + String.Format("_t{0:00}.{1}", n++, handBrakeService.MovieOutputType == HandBrakeService.OutputType.MP4 ? "mp4" : "mkv");
                        jobRunner.AddJob(new EncodeMovieJob(handBrakeService, driveName, cleanTitle, title.TitleIndex, false));
                    }
                }
                return true;
            }
            return false;
        }

        private string GetMovieTitle(DiskTitle diskTitle)
        {
            // strip any non ASCII chars
            char[] title = diskTitle.TitleName.ToCharArray();
            char[] newTitle = new char[title.Length];
            int n = 0;
            for (int c = 0; c < title.Length; c++)
            {
                char ch = title[c];
                if (ch < 128)
                {
                    if (ch == ':' || ch == '\\')
                    {
                        newTitle[n++] = '-';
                    } else
                    {
                        newTitle[n++] = ch;
                    }
                }
            }
            string sNewTitle = new string(newTitle, 0, n);
            return sNewTitle.Replace(" - Blu-ray", "");
        }
    }
}
