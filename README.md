# Movie Encoder

Movie Encoder is for bulk backup and re-encoding movies using [MakeMVKCon](https://www.makemkv.com) 
and [HandBrakeCLI](https://handbrake.fr/).

## How-to

### Installation

- Install using the installer.
- Use Visual Studio to create new binaries.

### Setup

1. Determine the method to make Disk Backups: 
  * None - Do not backup and disks.
  * MakeMKV - Use `MakeMKVCon.exe` to backup disks.
  * HandBrake - Use `HandBrakeCli.exe` to backup disks.
2. Download and install [HandBrakeCLI](https://handbrake.fr/downloads2.php) and/or [HandBrake](https://handbrake.fr/). 
   Note: `HandBrakeCLI` is a seperate download from `HandBrake`.
3. Export out an encoding profile from `HandBrake`.
      1. In the HandBrake UI right click the preset from the `Presets` UI and choose `Export to file`.
3. Optional: Download and install [MakeMVK](https://www.makemkv.com) if using for backups.
4. Choose the `Backup Method`.
5. Setup each file and folder path in `Preferences`.
6. Click `Start` to begin backups and re-encoding.

### Re-Encoding Videos

Setup two directories for `HandBrake`:
1. A `HandBrake \ Source Dir` directory to act as a queue to be re-encoded. All movies place inside this directory will be 
   detected and re-encoded. NOTE: They will be deleted after encoding.
2. A `HandBrake \ Output Dir` directory to store the output from `HandBrake`.

### Making Backups

Choose the method to backup disks: `MakeMKV` or `HandBrake`.

Backups made with `MakeMKV` will be saved to `MakeMKV \ Output Dir` if `Keep Movies` is enabled. Otherwise movies will be saved to
the `HandBrake \ Source Dir` for re-encoding. 

If `Backup All` is selected then all movies on the disk will be backed up. If not the selected method will determine 
and only backup the main movie.

### Start / Stop

Click on `Start` to begin the process of backing up and re-encoding movies. Click `Stop` or exit Media Encoder to stop the current job.

When running there is a `Status` page that contains a list of jobs and a running log of what is being done. 

When there are errors from `MakeMKVCon.exe` or `HandBrakeCLI.exe` they will be written to the log.

## Development

### Dependencies

- Newtonsoft.Json
- WindowsAPICodePack-Core
- WindowsAPICodePack-Shell
