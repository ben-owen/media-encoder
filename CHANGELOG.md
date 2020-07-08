# Changelog
Movie Encoder
Backup and re-encode bulk movies using projects MakeMKV and HandBrake

## TODO
- Check for available disk space before backing up movies.
- Support to keep HandBrake source files.
  - Possible cache file / db to note which files have been enoded so not to attempt on startup.
- Save process output to file.
- Put Handbrake backups into own folder.

## [1.0.1] - Unreleased
### Added
- Prevent opening multiple application instances.
- Clear log command.
- Color errors in the log.
- Scroll to log from selection in job list.
- Log information from MakeMKV scan.
- Added lines between job log entries.
- Add support for HandBrake to backup disks.
- Support MP4 output.
- Added flag for forced subtitles.
- Added support to filter out shorter movies in backups.

### Changed
- Changed how multiple backup job ran. Now split into a scan job and a backup job.
- Leave the performance page available after stopping the service.
- Code cleanups in progress code.
- Change color of error jobs in job list.
- Enhanced Handbrake progress.
- Clear log without clearing the current task log entries.

## [1.0.0] - 2020-07-04
### Added
- Automaticly backup one or all movies on a DVD or BluRay using MakeMKVCon.exe from the [MakeMVKCon](https://www.makemkv.com) project. 
  Will read disks when started and when disk changes.
- Re-encode bulk movies using the [HandBrakeCLI](https://handbrake.fr/) project.
