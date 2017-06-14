using System;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Net;
using System.IO;
using System.Xml;
using System.Diagnostics;

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

        // TODO: gebruik IsDebug
        // TODO: IsShowError
        // TODO: onderscheid maken tussen configuratiefout en communicatiefouten?
        //       default configuratiefout wel tonen maar communicatiefouten niet?
        // TODO: IsShowNoupdate: messagebox tonen als er geen update is

        // TODO: alles hiden als niet debug?
        // TODO: tonen als IsShowError en error

        public Form1()
        {
            InitializeComponent();
        }

        // After form is shown
        private void Form1_Shown(object sender, EventArgs e)
        {
            // Show version
            Version v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            Log("Updater v" + v.Major + "." + v.Minor + "." + v.Build + "." + v.Revision); 

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
                Log("Error: Could not load config file " + configfileName + ". " + ex.Message);
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
            MyVersion = config.version;
            IsDebug = config.debug;
            IsShowError = config.showerror;
            IsShowNoupdate = config.shownoupdate;

            // Check if we have a local version number
            if (String.IsNullOrEmpty(MyVersion.Trim()))
            {
                Log("Error: Version number is missing");
                return;
            }

            // Check if we have a url
            if (String.IsNullOrEmpty(config.url.Trim()))
            {
                Log("Error: Server URL is missing");
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
                Log("Error: Could not download remote xml file. " + err);
                return;
            }
            wc.Dispose();

            // Parse the remote XML file (in a string)

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(serverXmlString);
            XmlNode xn;
            // Set defaults
            string serverVersionString="";
            string serverUpdatePath="";
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
                Log("Error: Current version number is invalid");
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
                Log("Error: Remote version number is invalid");
                return;
            }
            
            // Check if we have a url
            if (String.IsNullOrEmpty(serverUpdatePath.Trim()))
            {
                Log("Error: Remote update path is missing");
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
                DialogResult dr = MessageBox.Show("An update is available. Do you want to download and install it?",
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
                    Log("Error: Could not download installer file. " + err);
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
                    Log("Error: Could not start the installer. " + err);
                    return;
                }
            }
            else
            {
                Log("* No update is available");

                // Show dialog if required
                DialogResult dr = MessageBox.Show("No update is available",
                                  config.caption,
                                  MessageBoxButtons.OK,
                                  MessageBoxIcon.Information);
            }

            Log("Finished");

            // Terminate this updater
            // If we're not in debug mode

            if (IsDebug == false)
            {
                Application.Exit();
            }
        }

        private void Log(string message)
        {
            textBox1.AppendText(message + Environment.NewLine);
        }

    }
}
