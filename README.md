# Movie Encoder
Bulk backup and re-encode movies using [MakeMVKCon](https://www.makemkv.com) and [HandBrakeCLI](https://handbrake.fr/).

## Development

### Dependencies

- Newtonsoft.Json
- WindowsAPICodePack-Core
- WindowsAPICodePack-Shell

### Installation

- Install using the installer.
- Use Visual Studio to create new binaries.

## How-to

### Setup
For Movie Backups download and install [MakeMKV](https://www.makemkv.com). This requires 'MakeMKVCon.exe' which is part of the standard install of MakeMKV.

For bulk movie re-encodes download and install [HandBrake](https://handbrake.fr/) and HandBrakeCLI. HandBrakeCLI can be found as an optional download and is not provided in the standard install.

Export out the encoding profile from HandBrake as a JSON file.

### Making Backups

To keep the backups from MakeMKV check the "Keep Movies" check box on the 'Preferences' page, then select the directory for the backups.
Choose 'Save All' to save all titles from the disk.

When "Keep Movies" is enabled they will be scheduled for reencoding however, if Movie Encoder is stopped any un-processed movies will not be found to be re-encoded.

### Encoding Videos

Setup two directories:
1. A directory to act as a queue to place movies to be re-encoded. Movies placed into the 'Source Dir' will be deleted after encoding.
2. A directory to store the final output.

### Start / Stop

Click on 'Start' to begin the process of backing up and re-encoding movies. Click 'Stop' or exit Media Encoder to stop the current job.

When running there is a 'Status' page that contains a list of jobs and a running log of what is being done. 
When there are errors from MakeMKVCon or HandBrakeCLI they will be written to the log.
