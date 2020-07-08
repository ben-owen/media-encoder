﻿// Copyright 2020 Ben Owen
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
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MovieEncoder
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        internal readonly EncoderService EncoderService = new EncoderService();
        internal readonly ProgressReporter ProgressReporter = new ProgressReporter();

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool SetForegroundWindow(IntPtr windowHandle);

        public App()
        {
            this.EncoderService.SetProgressReporter(this.ProgressReporter);
        }


        private void Application_Exit(object sender, ExitEventArgs e)
        {
            EncoderService.Stop();        
        }

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            new Mutex(true, "MovieEncoderApplication", out bool aIsNewInstance);
            if (!aIsNewInstance)
            {
                // Bring main window to front
                Process proc = Process.GetCurrentProcess();
                Process[] processes = Process.GetProcessesByName(proc.ProcessName);
                foreach (Process appProcess in processes)
                {
                    if (appProcess.MainWindowHandle != proc.MainWindowHandle)
                    {
                        SetForegroundWindow(appProcess.MainWindowHandle);
                        break;
                    }
                }
                App.Current.Shutdown();
            }
        }
    }
}
