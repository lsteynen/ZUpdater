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
        private Config config;
        private string serverXmlString;
        private string MyVersion;
        private bool IsDebug = false;
        private bool IsShowError = true;
        private bool IsShowNoupdate = true;

        // TODO: onderscheid maken tussen configuratiefout en communicatiefouten?
        //       default configuratiefouten wel tonen maar communicatiefouten niet?

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
            // Updater [-v 1.15.8] [-d] [-e] [-n]
            CmdLine.CmdLine c = new CmdLine.CmdLine();
            // In winforms we get the executable as the first argument. Handle it as a fixed first argument.
            CmdLineString ArgExecutable = new CmdLineString("Executable", eUsage.Fixed, "Executable");
            CmdLineString ArgAppCurrentVersion = new CmdLineString("v", eUsage.Optional, "Application version number");
            CmdLineSwitch ArgDebug = new CmdLineSwitch("d", eUsage.Optional, "Show debug output");
            CmdLineSwitch ArgShowErrors = new CmdLineSwitch("e", eUsage.Optional, "Show info window on error");
            CmdLineSwitch ArgShowNoUpdate = new CmdLineSwitch("n", eUsage.Optional, "Show 'No update available' dialog");

            c.RegisterParameter(ArgExecutable);
            c.RegisterParameter(ArgAppCurrentVersion);
            c.RegisterParameter(ArgDebug);
            c.RegisterParameter(ArgShowErrors);
            c.RegisterParameter(ArgShowNoUpdate);

            // get command line parameters
            string[] args = Environment.GetCommandLineArgs();
            try
            {
                c.Parse(args);
            }
            catch (Exception ex)
            {
                Error("Command line error. " + ex.Message);
                return;
            }

            // Set global parameters to arguments
            if (ArgAppCurrentVersion.Exists) MyVersion = ArgAppCurrentVersion.Value;
            IsDebug = ArgDebug.Exists;
            IsShowError = ArgShowErrors.Exists;
            IsShowNoupdate = ArgShowNoUpdate.Exists;

            // In debug mode? Then show the main window
            if (IsDebug)
            {
                ShowMe();
            }
        }

        // After form is shown
        private void Form1_Shown(object sender, EventArgs e)
        {
            // Load config file
            // From the same location as this exe

            string configfileName = "Updater.xml";
            string configfilePath = Path.Combine(Application.StartupPath, configfileName);

            config = new Config();
            try
            {
                config.Load(configfilePath);
                Log("Config file " + configfileName + " loaded");
                Log("Config.debug=" + config.debug);
                Log("Config.version=" + config.version);
                Log("Config.caption=" + config.caption);
                Log("Config.name=" + config.name);
                Log("Config.processname=" + config.processname);
                Log("Config.url=" + config.url);
                Log("Config.showerror=" + config.showerror);
                Log("Config.shownoupdate=" + config.shownoupdate);
            }
            catch(Exception ex)
            {
                Error("Error: Could not load config file " + configfileName + ". " + ex.Message);

                // Not in debug mode? Then terminate on return
                if (IsDebug == false) Application.Exit();
                return;
            }

            // Show the updater caption
            if (String.IsNullOrEmpty(config.caption) == false)
            {
                this.Text = config.caption;
            }

            // TODO: -v versienr van command line nemen, dan die van xml negeren, error als geen
            // TODO: -d debug vlag van command line OR die van XML
            // TODO: -e sayerror vlag van command line OR die van XML
            // TODO: -n saynoupdate vlag van command line OR die van XML

            // Process the options
            // if no version given on command line: then take version from xml file
            if( MyVersion == null) MyVersion = config.version;
            IsDebug |= config.debug;
            IsShowError |= config.showerror;
            IsShowNoupdate |= config.shownoupdate;

            // In debug mode? Then show the main window
            if (IsDebug)
            {
                ShowMe();
            }

            // Do everything
            Work();

            Log("Finished");

            // Not in debug mode? Then terminate on return
            if (IsDebug == false) Application.Exit();
        }

        private void Work()
        {
            // Check if we have a local version number
            if (String.IsNullOrEmpty(MyVersion.Trim()))
            {
                Error("Error: Version number is missing");
                return;
            }

            // Check if we have a url
            if (String.IsNullOrEmpty(config.url)
               || (config.url.Trim() == String.Empty))
            {
                Error("Error: Server URL is missing");
                return;
            }

            // Download the remote XML file into a string

            WebClient wc = new WebClient();
            try
            {
                serverXmlString = wc.DownloadString(config.url);
                Log("Remote xml file downloaded");
                Log(serverXmlString);
            }
            catch (Exception ex)
            {
                string err = (ex.InnerException != null) ? ex.InnerException.Message : ex.Message;
                Error("Error: Could not download remote xml file. " + err);
                return;
            }
            wc.Dispose();

            // Parse the remote XML file (in a string)

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(serverXmlString);
            XmlNode xn;
            // Set defaults
            string serverVersionString = "";
            string serverUpdatePath = "";
            // <updater> is the root element
            xn = xmlDoc.SelectSingleNode("updater/version");
            if (xn != null) serverVersionString = xn.InnerText;
            xn = xmlDoc.SelectSingleNode("updater/path");
            if (xn != null) serverUpdatePath = xn.InnerText;

            // Get/check current version number
            // major.minor.build.revision scheme, requires 2 or 3 or 4 parts
            Version localVersion;
            try
            {
                localVersion = Version.Parse(MyVersion);
            }
            catch (Exception)
            {
                Error("Error: Current version number is invalid");
                return;
            }

            // Get/check remote version number
            Version serverVersion;
            try
            {
                serverVersion = Version.Parse(serverVersionString);
            }
            catch (Exception)
            {
                Error("Error: Remote version number is invalid");
                return;
            }

            // Check if we have a url
            if (String.IsNullOrEmpty(serverUpdatePath.Trim()))
            {
                Error("Error: Remote update path is missing");
                return;
            }

            // Compare server version to our version
            // Version.CompareTo returns >0 if serverversion > localversion
            bool IsNewerAvailable = serverVersion.CompareTo(localVersion) > 0;

            // Is a newer version available?
            if (IsNewerAvailable)
            {
                Log("* A newer version is available");

                // Ask to download or not
                DialogResult dr = MessageBox.Show("A newer version is available. Do you want to download and install it?",
                                  config.caption,
                                  MessageBoxButtons.YesNo,
                                  MessageBoxIcon.Question);
                if (dr == DialogResult.Yes)
                {
                    // continue
                }
                else
                {
                    Log("Download canceled");

                    // Not in debug mode? Then terminate on return
                    if (IsDebug == false) Application.Exit();
                    return;
                }

                // Download the new installer file
                // to the temporary directory
                WebClient wci = new WebClient();
                string localInstallerPath = System.IO.Path.GetTempPath() + Path.GetFileName(serverUpdatePath);
                try
                {
                    wci.DownloadFile(serverUpdatePath, localInstallerPath);
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
                    Process[] procs = Process.GetProcessesByName(config.processname);
                    if (procs.Length >= 1)
                    {
                        Log("Killing running instances");
                        foreach (Process proc in procs)
                        {
                            proc.Kill();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log(ex.Message);
                    MessageBox.Show(ex.Message);
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
                Log("* No update is available");

                // Show dialog if required
                if (IsShowNoupdate)
                {
                    DialogResult dr = MessageBox.Show("No update is available",
                                      config.caption,
                                      MessageBoxButtons.OK,
                                      MessageBoxIcon.Information);
                }
            }
        }

        private void ShowMe()
        {
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
        }

        private void Error(string text)
        {
            // Log it
            Log(text);

            // Show it if required
            if (IsShowError)
            {
                DialogResult dr = MessageBox.Show(text,
                                  config.caption,
                                  MessageBoxButtons.OK,
                                  MessageBoxIcon.Error);
            }
        }

        private void Log(string message)
        {
            textBox1.AppendText(message + Environment.NewLine);
        }

    }
}
