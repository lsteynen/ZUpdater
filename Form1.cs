using System;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Net;
using System.IO;
using System.Xml;
using System.Diagnostics;

using CmdLine;

// TODO: Error meldingen wijzigen:
//  ShowErrors optioneel -e). If not: then users won't be warned when there's no connection of internet or the infourl is not available. 

namespace Updater
{
    public partial class Form1 : Form
    {
        private string Caption = "Updater";
        private bool IsShowInfo = false;
        private bool IsShowErrors = false;
        private bool IsShowNoUpdate = false;
        private string LocalVersionString;
        private Version LocalVersion;
        private Config Config;
        private string UpdateXmlString;
        private string UpdateVersionString;
        private Version UpdateVersion;
        private string UpdateFile;
        private Uri UriToInstallerFile;

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
            IsShowErrors |= Config.showerrors;
            IsShowNoUpdate |= Config.shownoupdate;

            // Take the current version number from the xml file if not specified on the command line
            if (LocalVersionString == null) LocalVersionString = Config.version;
            if (String.IsNullOrEmpty(LocalVersionString)
                || (LocalVersionString.Trim() == String.Empty))
            {
                Error("Error: No current version number specified");
                return;
            }

            Log("caption=" + Config.caption);
            Log("name=" + Config.name);
            Log("processname=" + Config.processname);
            Log("version=" + LocalVersionString);
            Log("infourl=" + Config.infourl);
            Log("showinfo=" + IsShowInfo);
            Log("showerrors=" + IsShowErrors);
            Log("shownoupdate=" + IsShowNoUpdate);

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

            // Check if we have a infourl
            if (String.IsNullOrEmpty(Config.infourl)
                || (Config.infourl.Trim() == String.Empty))
            {
                Error("Error: Update server URL is missing");
                return;
            }

            // Download the update server XML file into a string

            WebClient wc = new WebClient();
            try
            {
                UpdateXmlString = wc.DownloadString(Config.infourl);
                Log("Update server xml info file downloaded");
            }
            catch (Exception ex)
            {
                string err = (ex.InnerException != null) ? ex.InnerException.Message : ex.Message;
                if (IsShowErrors)
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
            try
            {
                ProcessUpdateInfoXml(UpdateXmlString);
            }
            catch (Exception ex)
            {
                Error("Error: In parsing update server XML file: " + ex.Message);
                return;
            }

            // Show info in the log
            Log("update_version=" + UpdateVersionString);
            Log("update_file=" + UpdateFile);

            // Determine the URI path to the installer file, 
            // combined from the path to UpdaterInfo.xml and 
            // the filename or (relative) path specified in updater server's Info.xml
            // Using the URI class takes care of slashes and backslashes in the path.
            Uri TmpUri = new Uri(Config.infourl);
            UriToInstallerFile = new Uri(TmpUri, UpdateFile);
            Log("update_uri=" + UriToInstallerFile.ToString());

            // Compare update server version to our version
            // Version.CompareTo returns >0 if UpdateVersion > LocalVersion
            bool IsNewerAvailable = UpdateVersion.CompareTo(LocalVersion) > 0;

            // Is a newer version available?
            if (IsNewerAvailable)
            {
                Log("> A newer version is available");

                // Ask to download or not
                DialogResult dr = MessageBox.Show("A newer version of " + Config.name + " (v" + UpdateVersionString + ") is available." + Environment.NewLine + 
                                                  "Do you want to download and install it?",
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
                string localInstallerPath = Path.Combine(System.IO.Path.GetTempPath(), Path.GetFileName(UpdateFile));
                try
                {
                    wci.DownloadFile(UriToInstallerFile, localInstallerPath);
                    Log("Installer file downloaded");
                }
                catch (Exception ex)
                {
                    string err = (ex.InnerException != null) ? ex.InnerException.Message : ex.Message;
                    Error("Error: Could not download installer file: " + err);
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
                    Error("Error: Could not start the installer: " + err);
                    return;
                }
            }
            else
            {
                Log("> No update is available");

                // Show dialog if required
                if (IsShowNoUpdate)
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
        // Example: Updater [-v 1.15.8] [-i] [-n] [-e]
        // Throw exception on error
        /// </summary>
        private void ProcessCommandLine()
        {
            CmdLine.CmdLine c = new CmdLine.CmdLine();

            // In winforms we get the executable as the first argument. Handle it as a fixed first argument.
            CmdLineString ArgExecutable = new CmdLineString("Executable", eUsage.Fixed, "Executable");
            CmdLineString ArgAppCurrentVersion = new CmdLineString("v", eUsage.Optional, "Application version number");
            CmdLineSwitch ArgShowInfo = new CmdLineSwitch("i", eUsage.Optional, "Show info");
            CmdLineSwitch ArgShowNoUpdate = new CmdLineSwitch("n", eUsage.Optional, "Show 'No update available' message");
            CmdLineSwitch ArgShowErrors = new CmdLineSwitch("e", eUsage.Optional, "Show error messages like configuration error or update server can't be reached");

            c.RegisterParameter(ArgExecutable);
            c.RegisterParameter(ArgAppCurrentVersion);
            c.RegisterParameter(ArgShowInfo);
            c.RegisterParameter(ArgShowNoUpdate);
            c.RegisterParameter(ArgShowErrors);

            // Parse
            string[] args = Environment.GetCommandLineArgs();
            c.Parse(args);

            // Set global parameters to arguments
            if (ArgAppCurrentVersion.Exists) LocalVersionString = ArgAppCurrentVersion.Value;
            IsShowInfo = ArgShowInfo.Exists;
            IsShowNoUpdate = ArgShowNoUpdate.Exists;
            IsShowErrors = ArgShowErrors.Exists;
        }

        /// <summary>
        /// Parse the updater server's Info.xml file
        /// Throws exception on error
        /// Result in class variables UpdateVersionString, UpdateVersion, UpdateFile
        /// </summary>
        public void ProcessUpdateInfoXml(string XmlString)
        {
            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(XmlString);

            // The root element
            XmlNode xroot = xmlDoc.SelectSingleNode("updater");
            if (xroot == null)
            {
                throw new Exception("XML root element <updater> not found.");
            }

            // Update version, required
            XmlNode xn1 = xroot.SelectSingleNode("version");
            if (xn1 == null)
            {
                throw new Exception("XML element <version> not found.");
            }
            UpdateVersionString = xn1.InnerText;
            // Get/check update version number
            try
            {
                UpdateVersion = Version.Parse(UpdateVersionString);
            }
            catch (Exception)
            {
                throw new Exception("XML element <version> is invalid.");
            }

            // Update file, required
            xn1 = xroot.SelectSingleNode("file");
            if (xn1 == null)
            {
                throw new Exception("XML element <file> not found.");
            }
            UpdateFile = xn1.InnerText;
            // Check if we have a URL
            if (String.IsNullOrEmpty(UpdateFile) || (UpdateFile.Trim() == String.Empty))
            {
                throw new Exception("XML element <file> is empty.");
            }
        }

        /// <summary>
        /// Make the main window visible
        /// </summary>
        private void ShowMe()
        {
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
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
