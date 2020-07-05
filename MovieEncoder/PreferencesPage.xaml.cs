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
        // Properties for Settings

        public bool MakeMkvKeepFiles
        {
            get { return Properties.Settings.Default.MakeMkvKeepFiles; }
            set { 
                Properties.Settings.Default.MakeMkvKeepFiles = value; 
                Properties.Settings.Default.Save();
                NotifyPropertyChanged("IsBackupAllEnabled");
                NotifyPropertyChanged("IsMakeMkvOutputDirEnabled");
            }
        }

        public string MakeMkvConExePath
        {
            get { return Properties.Settings.Default.MakeMkvConExePath; }
            set { Properties.Settings.Default.MakeMkvConExePath = value; Properties.Settings.Default.Save(); NotifyPropertyChanged(); }
        }

        public string MakeMkvOutDir
        {
            get { return Properties.Settings.Default.MakeMkvOutDir; }
            set { Properties.Settings.Default.MakeMkvOutDir = value; Properties.Settings.Default.Save(); NotifyPropertyChanged(); }
        }

        public bool MakeMkvBackupAll
        {
            get { return Properties.Settings.Default.MakeMkvBackupAll; }
            set { Properties.Settings.Default.MakeMkvBackupAll = value; Properties.Settings.Default.Save(); NotifyPropertyChanged(); }
        }
        
        public string HandBrakeCliExePath
        {
            get { return Properties.Settings.Default.HandBrakeCliExePath; }
            set { Properties.Settings.Default.HandBrakeCliExePath = value; Properties.Settings.Default.Save(); NotifyPropertyChanged(); }
        }

        public string HandBrakeProfileFile
        {
            get { return Properties.Settings.Default.HandBrakeProfileFile; }
            set { Properties.Settings.Default.HandBrakeProfileFile = value; Properties.Settings.Default.Save(); NotifyPropertyChanged(); }
        }

        public string HandBrakeSourceDir
        {
            get { return Properties.Settings.Default.HandBrakeSourceDir; }
            set { Properties.Settings.Default.HandBrakeSourceDir = value; Properties.Settings.Default.Save(); NotifyPropertyChanged(); }
        }

        public string HandBrakeOutDir
        {
            get { return Properties.Settings.Default.HandBrakeOutDir; }
            set { Properties.Settings.Default.HandBrakeOutDir = value; Properties.Settings.Default.Save(); NotifyPropertyChanged(); }
        }

        public PreferencesPage()
        {
            InitializeComponent();
            DataContext = this;
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
            if (!((App)Application.Current).EncoderService.IsStarted())
            {
                if (ValidateSettings())
                {

                    ((App)Application.Current).EncoderService.Start();
                    ProgressPage progressPage = new ProgressPage();
                    this.NavigationService.Navigate(progressPage);
                    NotifyPropertyChanged("IsServiceStopped");
                    NotifyPropertyChanged("IsEnabledSaveAll");
                }
            }
            else
            {
                ((App)Application.Current).EncoderService.Stop();
                this.NavigationService.RemoveBackEntry();
                NotifyPropertyChanged("IsServiceStopped");
                NotifyPropertyChanged("IsEnabledSaveAll");
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

            if (errorMessage != null) {
                MessageBox.Show(errorMessage, "Preferences Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            return true;
        }

        public string StartEncodingButtonText
        {
            get { return ((App)Application.Current).EncoderService.IsStarted() ? "Stop" : "Start";  }
        }

        public bool IsServiceStopped
        {
            get { return !((App)Application.Current).EncoderService.IsStarted(); }
        }

        public bool IsBackupAllEnabled
        {
            get { return IsServiceStopped == true && this.MakeMkvKeepFiles == true; }
        }

        public bool IsMakeMkvOutputDirEnabled
        {
            get { return IsServiceStopped == true && MakeMkvKeepFiles == true; }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}


