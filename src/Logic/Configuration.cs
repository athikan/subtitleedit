﻿using System;
using System.IO;

namespace Nikse.SubtitleEdit.Logic
{

    /// <summary>
    /// Configuration settings via Singleton pattern
    /// </summary>
    public class Configuration
    {
        private string _baseDir;
        private string _dataDir;
        private Settings _settings;

        public static Configuration Instance
        {
            get { return Nested.instance; }
        }

        private Configuration()
        {
        }

        private class Nested
        {
            internal static readonly Configuration instance = new Configuration();
        }

        public static string SettingsFileName
        {
            get
            {
                return DataDirectory + "Settings.xml";
            }
        }

        public static string DictionariesFolder
        {
            get
            {
                return DataDirectory + "Dictionaries" + Path.DirectorySeparatorChar;
            }
        }

        public static string IconsFolder
        {
            get
            {
                return BaseDirectory + "Icons" + Path.DirectorySeparatorChar;
            }
        }

        public static bool IsRunningOnLinux()
        {
            return Environment.OSVersion.Platform == PlatformID.Unix;
        }

        public static bool IsRunningOnMac()
        {
            return Environment.OSVersion.Platform == PlatformID.MacOSX;
        }

        public static string TesseractDataFolder
        {
            get
            {
                if (IsRunningOnLinux() || IsRunningOnMac())
                {
                    if (Directory.Exists("/usr/share/tesseract-ocr/tessdata"))
                        return "/usr/share/tesseract-ocr/tessdata";
                    else if (Directory.Exists("/usr/share/tesseract/tessdata"))
                        return "/usr/share/tesseract/tessdata";
                    else if (Directory.Exists("/usr/share/tessdata"))
                        return "/usr/share/tessdata";
                }
                return TesseractFolder + "tessdata";
            }
        }

        public static string TesseractOriginalFolder
        {
            get
            {
                return BaseDirectory + "Tesseract" + Path.DirectorySeparatorChar;
            }
        }

        public static string TesseractFolder
        {
            get
            {
                return DataDirectory + "Tesseract" + Path.DirectorySeparatorChar;
            }
        }

        public static string VobSubCompareFolder
        {
            get
            {
                return DataDirectory + "VobSub" + Path.DirectorySeparatorChar;
            }
        }

        public static string OcrFolder
        {
            get
            {
                return DataDirectory + "Ocr" + Path.DirectorySeparatorChar;
            }
        }

        public static string WaveFormsFolder
        {
            get
            {
                return DataDirectory + "WaveForms" + Path.DirectorySeparatorChar;
            }
        }

        public static string SpectrogramsFolder
        {
            get
            {
                return DataDirectory + "Spectrograms" + Path.DirectorySeparatorChar;
            }
        }

        public static string AutoBackupFolder
        {
            get
            {
                return DataDirectory + "AutoBackup" + Path.DirectorySeparatorChar;
            }
        }

        public static string PluginsDirectory
        {
            get
            {
                return Path.Combine(Configuration.DataDirectory, "Plugins");
            }
        }

        public static string DataDirectory
        {
            get
            {
                if (Instance._dataDir == null)
                {
                    if (IsRunningOnLinux() || IsRunningOnMac())
                    {
                        Instance._dataDir = BaseDirectory;
                    }
                    else
                    {
                        string installerPath = GetInstallerPath();
                        bool hasUninstallFiles = Directory.GetFiles(BaseDirectory, "unins*.*").Length > 0;
                        bool hasDictionaryFolder = Directory.Exists(Path.Combine(BaseDirectory, "Dictionaries"));
                        string appDataRoamingPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Subtitle Edit");

                        if ((installerPath != null && installerPath.ToLower().TrimEnd(Path.DirectorySeparatorChar) == BaseDirectory.ToLower().TrimEnd(Path.DirectorySeparatorChar)) ||
                            hasUninstallFiles ||
                            (!hasDictionaryFolder && Directory.Exists(Path.Combine(appDataRoamingPath, "Dictionaries"))))
                        {
                            if (Directory.Exists(appDataRoamingPath))
                            {
                                Instance._dataDir = appDataRoamingPath + Path.DirectorySeparatorChar;
                            }
                            else
                            {
                                try
                                {
                                    Directory.CreateDirectory(appDataRoamingPath);
                                    Directory.CreateDirectory(Path.Combine(appDataRoamingPath, "Dictionaries"));
                                    Instance._dataDir = appDataRoamingPath + Path.DirectorySeparatorChar;
                                }
                                catch
                                {
                                    Instance._dataDir = BaseDirectory;
                                    System.Windows.Forms.MessageBox.Show("Please re-install Subtitle Edit (installer version)");
                                    System.Windows.Forms.Application.ExitThread();
                                }
                            }
                        }
                        else
                        {
                            Instance._dataDir = BaseDirectory;
                        }
                    }
                }
                return Instance._dataDir;
            }
        }

        private static string GetInstallerPath()
        {
            string installerPath = null;
            var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\SubtitleEdit_is1");
            if (key != null)
            {
                string temp = (string)key.GetValue("InstallLocation");
                if (temp != null && Directory.Exists(temp))
                    installerPath = temp;
            }
            if (installerPath == null)
            {
                key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Microsoft\Windows\CurrentVersion\Uninstall\SubtitleEdit_is1");
                if (key != null)
                {
                    string temp = (string)key.GetValue("InstallLocation");
                    if (temp != null && Directory.Exists(temp))
                        installerPath = temp;
                }
            }
            return installerPath;
        }

        public static string BaseDirectory
        {
            get
            {
                if (Instance._baseDir == null)
                {
                    System.Reflection.Assembly a = System.Reflection.Assembly.GetEntryAssembly();
                    if (a != null)
                        Instance._baseDir = Path.GetDirectoryName(a.Location);
                    else
                        Instance._baseDir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

                    if (!Instance._baseDir.EndsWith(Path.DirectorySeparatorChar.ToString()))
                        Instance._baseDir += Path.DirectorySeparatorChar;
                }
                return Instance._baseDir;
            }
        }

        public static Settings Settings
        {
            get
            {
                if (Instance._settings == null)
                    Instance._settings = Settings.GetSettings();
                return Instance._settings;
            }
        }
    }
}
