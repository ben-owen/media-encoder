﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace MovieEncoder.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "16.6.0.0")]
    internal sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string MakeMkvConExePath {
            get {
                return ((string)(this["MakeMkvConExePath"]));
            }
            set {
                this["MakeMkvConExePath"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string HandBrakeCliExePath {
            get {
                return ((string)(this["HandBrakeCliExePath"]));
            }
            set {
                this["HandBrakeCliExePath"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string HandBrakeOutDir {
            get {
                return ((string)(this["HandBrakeOutDir"]));
            }
            set {
                this["HandBrakeOutDir"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string HandBrakeProfileFile {
            get {
                return ((string)(this["HandBrakeProfileFile"]));
            }
            set {
                this["HandBrakeProfileFile"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool MakeMkvBackupAll {
            get {
                return ((bool)(this["MakeMkvBackupAll"]));
            }
            set {
                this["MakeMkvBackupAll"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string MakeMkvOutDir {
            get {
                return ((string)(this["MakeMkvOutDir"]));
            }
            set {
                this["MakeMkvOutDir"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("False")]
        public bool MakeMkvKeepFiles {
            get {
                return ((bool)(this["MakeMkvKeepFiles"]));
            }
            set {
                this["MakeMkvKeepFiles"] = value;
            }
        }
        
        [global::System.Configuration.UserScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("")]
        public string HandBrakeSourceDir {
            get {
                return ((string)(this["HandBrakeSourceDir"]));
            }
            set {
                this["HandBrakeSourceDir"] = value;
            }
        }
    }
}
