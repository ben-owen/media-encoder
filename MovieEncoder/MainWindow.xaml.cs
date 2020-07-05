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
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace MovieEncoder
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : NavigationWindow
    {
        public readonly PreferencesPage PreferencesPage;
        public readonly ProgressPage ProgressPage;

        public MainWindow()
        {
            InitializeComponent();

            PreferencesPage = new PreferencesPage();
            ProgressPage = new ProgressPage();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            // Shutdown the application.
            App.Current.Shutdown();
        }

        private void ExitMenu_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            Close();
        }

        private void AboutMenu_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            AboutWindow aboutWindow = new AboutWindow();
            aboutWindow.Owner = this;
            aboutWindow.ShowDialog();
        }

        private void ClearLogMenu_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            ((App)App.Current).ProgressReporter.ClearLog();
        }
    }
}
