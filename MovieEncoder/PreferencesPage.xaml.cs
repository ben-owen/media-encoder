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
using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace MovieEncoder
{
    /// <summary>
    /// Interaction logic for PreferencesPage.xaml
    /// </summary>
    public partial class PreferencesPage : Page, INotifyPropertyChanged
    {
        /* -- Fields -- */
        private readonly EncoderService _encoderService;

        public event PropertyChangedEventHandler PropertyChanged;


        /* -- Properties -- */
        public IEnumerable<BackupMode> BackupModeValues
        {
            get
            {
                return Enum.GetValues(typeof(BackupMode)).Cast<BackupMode>();
            }
        }

        public IEnumerable<HandBrakeService.OutputType> HandBrakeOutputTypeValues
        {
            get
            {
                return Enum.GetValues(typeof(HandBrakeService.OutputType)).Cast<HandBrakeService.OutputType>();
            }
        }
        
        public BackupMode GlobalBackupMethod
        {
            get { return _encoderService.GlobalBackupMethod; }
            set
            {
                _encoderService.GlobalBackupMethod = value;
                NotifyPropertyChanged("IsBackupAllEnabled");
                NotifyPropertyChanged("IsMakeMkvEnabled");
                NotifyPropertyChanged("IsMakeMkvOutputDirEnabled");
                NotifyPropertyChanged();
            }
        }

        public bool MakeMkvKeepFiles
        {
            get { return _encoderService.MakeMkvKeepFiles; }
            set
            {
                _encoderService.MakeMkvKeepFiles = value;
                NotifyPropertyChanged("IsBackupAllEnabled");
                NotifyPropertyChanged("IsMakeMkvOutputDirEnabled");
                NotifyPropertyChanged();
            }
        }

        public int GlobalMinMovieLen
        {
            get { return _encoderService.GlobalMinMovieLen; }
            set { _encoderService.GlobalMinMovieLen = value; NotifyPropertyChanged(); }
        }

        public string MakeMkvConExePath
        {
            get { return _encoderService.MakeMkvConExePath; }
            set { _encoderService.MakeMkvConExePath = value; NotifyPropertyChanged(); }
        }

        public string MakeMkvOutDir
        {
            get { return _encoderService.MakeMkvOutDir; }
            set { _encoderService.MakeMkvOutDir = value; NotifyPropertyChanged(); }
        }

        public bool GlobalBackupAll
        {
            get { return _encoderService.GlobalBackupAll; }
            set { _encoderService.GlobalBackupAll = value; NotifyPropertyChanged(); }
        }

        public string HandBrakeCliExePath
        {
            get { return _encoderService.HandBrakeCliExePath; }
            set { _encoderService.HandBrakeCliExePath = value; NotifyPropertyChanged(); }
        }

        public string HandBrakeProfileFile
        {
            get { return _encoderService.HandBrakeProfileFile; }
            set { _encoderService.HandBrakeProfileFile = value; NotifyPropertyChanged(); }
        }

        public string HandBrakeSourceDir
        {
            get { return _encoderService.HandBrakeSourceDir; }
            set { _encoderService.HandBrakeSourceDir = value; NotifyPropertyChanged(); }
        }

        public string HandBrakeOutDir
        {
            get { return _encoderService.HandBrakeOutDir; }
            set { _encoderService.HandBrakeOutDir = value; NotifyPropertyChanged(); }
        }

        public HandBrakeService.OutputType HandBrakeOutputType
        {
            get { return _encoderService.HandBrakeOutputType; }
            set { _encoderService.HandBrakeOutputType = value; NotifyPropertyChanged(); }
        }

        public bool HandBrakeForceSubtitles
        {
            get { return _encoderService.HandBrakeForceSubtitles; }
            set { _encoderService.HandBrakeForceSubtitles = value; NotifyPropertyChanged(); }
        }

        public string StartEncodingButtonText
        {
            get { return _encoderService.IsStarted() ? "Stop" : "Start"; }
        }

        public bool IsServiceStopped
        {
            get { return !_encoderService.IsStarted(); }
        }

        public bool IsBackupAllEnabled
        {
            get { return IsServiceStopped == true && ((GlobalBackupMethod == BackupMode.MakeMKV && MakeMkvKeepFiles == true) || (GlobalBackupMethod == BackupMode.HandBrake)); }
        }

        public bool IsMakeMkvOutputDirEnabled
        {
            get { return IsServiceStopped == true && MakeMkvKeepFiles == true && GlobalBackupMethod == BackupMode.MakeMKV; }
        }

        public bool IsMakeMkvEnabled
        {
            get { return IsServiceStopped == true && GlobalBackupMethod == BackupMode.MakeMKV; }
        }

        public ProgressReporter ProgressReporter { get; }

        /* -- Methods -- */

        public PreferencesPage()
        {
            _encoderService = ((App)Application.Current).EncoderService;
            ProgressReporter = ((App)Application.Current).ProgressReporter;

            DataContext = this;
            InitializeComponent();

            ProgressReporter.PropertyChanged += ProgressReporter_PropertyChanged;
        }

        private void ProgressReporter_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Shutdown")
            {
                NotifyPropertyChanged("IsServiceStopped");
                NotifyPropertyChanged("IsEnabledSaveAll");
                NotifyPropertyChanged("IsBackupAllEnabled");
                NotifyPropertyChanged("StartEncodingButtonText");
            }
        }

        private void MkvSourceDirButton_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog openFolderDialog = new CommonOpenFileDialog();
            try
            {
                openFolderDialog.InitialDirectory = HandBrakeSourceDir;
            }
            catch (System.ArgumentException)
            {
                // ignore
            }
            openFolderDialog.IsFolderPicker = true;
            if (openFolderDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                HandBrakeSourceDir = openFolderDialog.FileName;
            }
        }

        private void HandbrakeButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            try
            {
                openFileDialog.InitialDirectory = Path.GetDirectoryName(HandBrakeCliExePath);
            }
            catch (System.ArgumentException)
            {
                // ignore
            }
            openFileDialog.Filter = "Handbrake (handbrakecli*.exe)|handbrakecli*.exe";
            if (openFileDialog.ShowDialog() == true)
            {
                HandBrakeCliExePath = openFileDialog.FileName;
            }
        }

        private void MakeMkvButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            try
            {
                openFileDialog.InitialDirectory = Path.GetDirectoryName(MakeMkvConExePath);
            }
            catch (System.ArgumentException)
            {
                // ignore
            }
            openFileDialog.Filter = "MakeMKVCon (makemkvcon*.exe)|makemkvcon*.exe";
            if (openFileDialog.ShowDialog() == true)
            {
                MakeMkvConExePath = openFileDialog.FileName;
            }
        }

        private void HandbrakeOutDirButton_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog openFolderDialog = new CommonOpenFileDialog();
            try
            {
                openFolderDialog.InitialDirectory = HandBrakeOutDir;
            }
            catch (System.ArgumentException)
            {
                // ignore
            }
            openFolderDialog.IsFolderPicker = true;
            if (openFolderDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                HandBrakeOutDir = openFolderDialog.FileName;
            }
        }

        private void HandbrakeProfileFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            try
            {
                openFileDialog.InitialDirectory = Path.GetDirectoryName(HandBrakeProfileFile);
            }
            catch (System.ArgumentException)
            {
                // ignore
            }
            openFileDialog.Filter = "Profiles (*.json)|*.json|All (*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                HandBrakeProfileFile = openFileDialog.FileName;
            }
        }

        private void MakeMkvAllDestDirButton_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog openFolderDialog = new CommonOpenFileDialog();
            try
            {
                openFolderDialog.InitialDirectory = MakeMkvOutDir;
            }
            catch (System.ArgumentException)
            {
                // ignore
            }
            openFolderDialog.IsFolderPicker = true;
            if (openFolderDialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                MakeMkvOutDir = openFolderDialog.FileName;
            }
        }

        private void StartEncoding_Click(object sender, RoutedEventArgs e)
        {
            if (!_encoderService.IsStarted())
            {
                if (ValidateSettings())
                {

                    _encoderService.Start();
                    MainWindow mainWindow = (MainWindow)Window.GetWindow(this);
                    NavigationService.Navigate(mainWindow.ProgressPage);
                    NotifyPropertyChanged("IsServiceStopped");
                    NotifyPropertyChanged("IsEnabledSaveAll");
                    NotifyPropertyChanged("IsBackupAllEnabled");
                }
            }
            else
            {
                _encoderService.Stop();
                this.NavigationService.RemoveBackEntry();
                NotifyPropertyChanged("IsServiceStopped");
                NotifyPropertyChanged("IsEnabledSaveAll");
                NotifyPropertyChanged("IsBackupAllEnabled");
            }
            NotifyPropertyChanged("StartEncodingButtonText");
        }

        private bool ValidateSettings()
        {
            string errorMessage = null;
            if (!File.Exists(MakeMkvConExePath))
            {
                errorMessage = "'MakeMKVCon.exe' file '" + MakeMkvConExePath + "' is not valid";
            }

            if (errorMessage == null && MakeMkvKeepFiles == true)
            {
                if (MakeMkvOutDir.Equals(HandBrakeSourceDir))
                {
                    errorMessage = "When keeping files 'MakeMKV Output Dir' and 'HandBrake Source Dir' can not be the same";
                }
            }

            if (errorMessage == null && !File.Exists(HandBrakeCliExePath))
            {
                errorMessage = "'HandBrakeCli.exe file ' '" + HandBrakeCliExePath + "' is not valid";
            }

            if (errorMessage == null && !File.Exists(HandBrakeProfileFile))
            {
                errorMessage = "'HandBrake profile file ' '" + HandBrakeProfileFile + "' is not valid";
            }

            if (errorMessage != null)
            {
                MessageBox.Show(errorMessage, "Preferences Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            return true;
        }

        private void NotifyPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}


