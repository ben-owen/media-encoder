# Changelog
Movie Encoder
Backup and re-encode bulk movies using projects MakeMKV and HandBrake

## TODO
- Have mutlple modes of disk backups. Add support for HandBrake to backup disks.

## [1.0.1] - Unreleased
### Added
- Prevent opening multiple application instances.
- Clear log command.
- Color errors in the log.
- Scroll to log from selection in job list.
- Log information from MakeMKV scan.

### Changed
- Changed how multiple backup job ran. Now split into a scan job and a backup job.
- Leave the performance page available after stopping the service.
- Code cleanups in progress code.
- Change color of error jobs in job list.

## [1.0.0] - 2020-07-04
### Added
- Automaticly backup one or all movies on a DVD or BluRay using MakeMKVCon.exe from the [MakeMVKCon](https://www.makemkv.com) project. 
  Will read disks when started and when disk changes.
- Re-encode bulk movies using the [HandBrakeCLI](https://handbrake.fr/) project.
