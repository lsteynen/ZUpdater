using System;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Net;
using System.IO;
using System.Xml;
using System.Diagnostics;

using CmdLine;

namespace Updater
{
    public partial class Form1 : Form
    {
        private string Caption = "Updater";
        private bool IsShowInfo = false;
        private bool IsShowCantReachServer = false;
        private bool IsShowNoupdate = false;
        private string LocalVersionString;
        private Version LocalVersion;
        private Config Config;
        private string UpdateXmlString;
        private string UpdateVersionString;
        private Version UpdateVersion;
        string UpdateInstallerPath;

        public Form1()
        {
            InitializeComponent();

            // Start the updater hidden
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
        }

        // Before form display
        private void Form1_Load(object sender, EventArgs e)
        {
            // Show version in log
            Version v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            Log("Updater v" + v.Major + "." + v.Minor + "." + v.Build + "." + v.Revision);

            // Process the command line
            // Can throw exception
            try
            {
                ProcessCommandLine();
            }
            catch (Exception ex)
            {
                // Command line error
                // Show window, show error, exit
                ShowMe();
                Error("Command line error: " + ex.Message);
                Application.Exit();
                return;
            }

            // In info mode? Then show the main window
            if (IsShowInfo)
            {
                ShowMe();
            }
        }

        // After form is shown
        private void Form1_Shown(object sender, EventArgs e)
        {
            // Load Config file
            // From the same location as this exe

            string configfileName = "Updater.xml";
            string configfilePath = Path.Combine(Application.StartupPath, configfileName);

            Config = new Config();
            try
            {
                Config.Load(configfilePath);
                Log("Config file " + configfilePath + " loaded");
            }
            catch(Exception ex)
            {
                Error("Error: Could not load config file " + configfileName + ". " + ex.Message);

                // Not in info mode? Then terminate on return
                if (IsShowInfo == false) Application.Exit();
                return;
            }

            // Change the caption if specified
            if (String.IsNullOrEmpty(Config.caption) == false)
            {
                Caption = Config.caption;
                this.Text = Caption;
            }

            // Process the config file parameters
            IsShowInfo |= Config.showinfo;
            IsShowCantReachServer |= Config.showcantreachserver;
            IsShowNoupdate |= Config.shownoupdate;

            // Take the current version number from the xml file if not specified on the command line
            if (LocalVersionString == null) LocalVersionString = Config.version;
            if (String.IsNullOrEmpty(LocalVersionString)
                || (LocalVersionString.Trim() == String.Empty))
            {
                Error("Error: No current version number specified");
                return;
            }

            // Show info in the info window
            Log(" currentversion=" + LocalVersionString);
            Log(" app name=" + Config.name);
            Log(" app processname=" + Config.processname);
            Log(" update url=" + Config.url);
            Log(" show info=" + IsShowInfo);
            Log(" show can't reach server=" + IsShowCantReachServer);
            Log(" show no update=" + IsShowNoupdate);

            // In info mode? Then show the main window
            if (IsShowInfo)
            {
                ShowMe();
            }

            // Do everything
            Work();

            Log("Finished");

            // Not in info mode? Then terminate on return
            if (IsShowInfo == false)
            {
                Application.Exit();
            }
        }

        private void Work()
        {
            // Convert current version number
            // major.minor.build.revision scheme, requires 2 or 3 or 4 parts
            try
            {
                LocalVersion = Version.Parse(LocalVersionString);
            }
            catch (Exception)
            {
                Error("Error: Current version number is invalid: " + LocalVersionString);
                return;
            }

            // Check if we have a url
            if (String.IsNullOrEmpty(Config.url)
                || (Config.url.Trim() == String.Empty))
            {
                Error("Error: Update server URL is missing");
                return;
            }

            // Download the server XML file into a string

            WebClient wc = new WebClient();
            try
            {
                UpdateXmlString = wc.DownloadString(Config.url);
                Log("Server xml info file downloaded");
            }
            catch (Exception ex)
            {
                string err = (ex.InnerException != null) ? ex.InnerException.Message : ex.Message;
                if (IsShowCantReachServer)
                {
                    Error("Error: Could not reach the update server");
                }
                else
                {
                    Log("Error: Could not reach the update server. " + err);
                }
                return;
            }
            wc.Dispose();

            // Parse the remote XML file (in a string)

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(UpdateXmlString);
            XmlNode xn;
            // <updater> is the root element
            xn = xmlDoc.SelectSingleNode("updater/version");
            if (xn != null) UpdateVersionString = xn.InnerText;
            xn = xmlDoc.SelectSingleNode("updater/path");
            if (xn != null) UpdateInstallerPath = xn.InnerText;

            // Show settings in the log
            Log(" server version=" + UpdateVersionString);
            Log(" server installer path=" + UpdateInstallerPath);

            // Get/check update version number
            try
            {
                UpdateVersion = Version.Parse(UpdateVersionString);
            }
            catch (Exception)
            {
                Error("Error: Server's update version number is invalid");
                return;
            }

            // Check if we have a url
            if (String.IsNullOrEmpty(UpdateInstallerPath)
                || (UpdateInstallerPath.Trim() == String.Empty))
            {
                Error("Error: Server's installer path is invalid");
                return;
            }

            // Compare server version to our version
            // Version.CompareTo returns >0 if UpdateVersion > LocalVersion
            bool IsNewerAvailable = UpdateVersion.CompareTo(LocalVersion) > 0;

            // Is a newer version available?
            if (IsNewerAvailable)
            {
                Log("> A newer version is available");

                // Ask to download or not
                DialogResult dr = MessageBox.Show("A newer version of " + Config.name + " is available. Do you want to download and install it?",
                                  Caption,
                                  MessageBoxButtons.YesNo,
                                  MessageBoxIcon.Question);
                if (dr == DialogResult.Yes)
                {
                    // continue
                }
                else
                {
                    Log("Download canceled");
                    if (IsShowInfo == false) Application.Exit();
                    return;
                }

                // Download the new installer file
                // to the temporary directory
                WebClient wci = new WebClient();
                string localInstallerPath = System.IO.Path.GetTempPath() + Path.GetFileName(UpdateInstallerPath);
                try
                {
                    wci.DownloadFile(UpdateInstallerPath, localInstallerPath);
                    Log("Installer file downloaded");
                }
                catch (Exception ex)
                {
                    string err = (ex.InnerException != null) ? ex.InnerException.Message : ex.Message;
                    Error("Error: Could not download installer file. " + err);
                    return;
                }
                wci.Dispose();

                // Close running copies of the application before running the installer

                try
                {
                    Process[] procs = Process.GetProcessesByName(Config.processname);
                    if (procs.Length >= 1)
                    {
                        foreach (Process proc in procs)
                        {
                            Log("Killing running instance");
                            proc.Kill();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log(ex.Message);
                }

                // Run the installer

                try
                {
                    Process.Start(localInstallerPath);
                    Log("The installer is started");
                }
                catch (Exception ex)
                {
                    string err = (ex.InnerException != null) ? ex.InnerException.Message : ex.Message;
                    Error("Error: Could not start the installer. " + err);
                    return;
                }
            }
            else
            {
                Log("> No update is available");

                // Show dialog if required
                if (IsShowNoupdate)
                {
                    DialogResult dr = MessageBox.Show("No update is available",
                                      Caption,
                                      MessageBoxButtons.OK,
                                      MessageBoxIcon.Information);
                }
            }
        }

        /// <summary>
        /// // Process the command line
        // Example: Updater [-v 1.15.8] [-i] [-n] [-s]
        // Throw exception on error
        /// </summary>
        private void ProcessCommandLine()
        {
            CmdLine.CmdLine c = new CmdLine.CmdLine();

            // In winforms we get the executable as the first argument. Handle it as a fixed first argument.
            CmdLineString ArgExecutable = new CmdLineString("Executable", eUsage.Fixed, "Executable");
            CmdLineString ArgAppCurrentVersion = new CmdLineString("v", eUsage.Optional, "Application version number");
            CmdLineSwitch ArgShowInfo = new CmdLineSwitch("i", eUsage.Optional, "Show info");
            CmdLineSwitch ArgShowNoUpdate = new CmdLineSwitch("n", eUsage.Optional, "Show 'No update available' dialog");
            CmdLineSwitch ArgShowCantReachServer = new CmdLineSwitch("s", eUsage.Optional, "Show 'Cant Reach Server' error");

            c.RegisterParameter(ArgExecutable);
            c.RegisterParameter(ArgAppCurrentVersion);
            c.RegisterParameter(ArgShowInfo);
            c.RegisterParameter(ArgShowNoUpdate);
            c.RegisterParameter(ArgShowCantReachServer);

            // Parse
            string[] args = Environment.GetCommandLineArgs();
            c.Parse(args);

            // Set global parameters to arguments
            if (ArgAppCurrentVersion.Exists) LocalVersionString = ArgAppCurrentVersion.Value;
            IsShowInfo = ArgShowInfo.Exists;
            IsShowNoupdate = ArgShowNoUpdate.Exists;
            IsShowCantReachServer = ArgShowCantReachServer.Exists;
        }

        /// <summary>
        /// Make the main window visible
        /// </summary>
        private void ShowMe()
        {
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
        }

        private string GetCommandLineAsString()
        {
            string[] args = Environment.GetCommandLineArgs();
            string result = String.Empty;
            for (int i = 1; i < args.Length; ++i)
            {
                result += " " + args[i];
            }
            return result;
        }

        private void Error(string text)
        {
            // Log it
            Log(text);

            // Show it
            DialogResult dr = MessageBox.Show(text,
                                Caption,
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
        }

        private void Log(string message)
        {
            textBox1.AppendText(message + Environment.NewLine);
        }

    }
}
