using System;
using System.IO;
using System.Runtime.InteropServices;

namespace LST.Helpers
{
    /// <summary>
    /// AppHelpers class holds App info
    /// </summary>
    public static class AppHelpers
    {
        public static Version VersionNum;
        public static string VersionString;
        public static string ApplicationStartupPath;
        public static string MyDocumentsDir;
        public static string MyDownloadsDir;

        private static readonly Guid DownloadsId = new Guid("374DE290-123F-4565-9164-39C4925E467B");

        // reference shell32.dll for SHGetKnownFolderPath
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern string SHGetKnownFolderPath(
            [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
            uint dwFlags,
            IntPtr hToken);

        static AppHelpers()
        {
            
            // Get version
            VersionNum = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            VersionString = "v" + VersionNum.Major + "." + VersionNum.Minor + "." + VersionNum.Build;
            if (VersionNum.Revision > 0) VersionString += "." + VersionNum.Revision;

            // Get Special Folders paths
            //ApplicationStartupPath = Application.StartupPath; // requires System.Windows.Forms
            ApplicationStartupPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            MyDocumentsDir = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments), "ModbusControl");
            MyDownloadsDir = SHGetKnownFolderPath(DownloadsId, 0, IntPtr.Zero);
        }

        /// <summary>
        /// Split a commandline string into command (program) and arguments string
        /// Both can be quoted
        /// </summary>
        public static bool SplitCommandlineString(string input, out string command, out string arguments)
        {
            command = null;
            arguments = null;

            input = input.Trim();

            if (input.Length == 0)
            {
                return false;
            }

            // quoted?
            int i = 1;
            if ((input.Length >= 1) && (input[0] == '"'))
            {
                // find end of quoted command
                while ((i < input.Length) && (input[i] != '"'))
                {
                    ++i;
                }
                // include end quote
                if (i < input.Length) ++i;
            }
            else
            {
                // find space at end of command
                while ((i < input.Length) && (input[i] != ' '))
                {
                    ++i;
                }
            }

            command = input.Substring(0, i);
            arguments = input.Substring(i).Trim();

            return true;
        }
        
    }
}
