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
        private bool IsDebug = false;
        private bool IsShowError = true;
        private bool IsShowNoupdate = true;

        // TODO: gebruik IsDebug
        // TODO: IsShowError
        // TODO: IsShowNoupdate
        // TODO: alles hiden als niet debug?

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

#if false
// Temporary code for creating XML files
            Config config1 = new Config();
            config1.debug = true;
            config1.version = "1.2.3.4";
            config1.title = "Modbuscontrol updater";
            config1.name = "Modbuscontrol";
            //config1.url = @"http://server1.be/updater/Info.xml";
            config1.url = @"file://C:\Users\Luc\Projecten\Csharp\Updater\Updater\Updatersource\Updater\Info.xml";
            config1.showerror = true;
            config1.shownoupdate = false;
            config1.Save("Updater.xml");
#endif
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
                Log("Config.title=" + config.title);
                Log("Config.name=" + config.name);
                Log("Config.url=" + config.url);
                Log("Config.showerror=" + config.showerror);
                Log("Config.shownoupdate=" + config.shownoupdate);
            }
            catch(Exception ex)
            {
                Log("Error: Could not load config file " + configfileName + ". " + ex.Message);
                return;
            }

            // Show the updater title
            if (String.IsNullOrEmpty(config.title) == false)
            {
                this.Text = config.title;
            }

            // TODO: -v versienr van command line nemen, dan die van xml negeren
            // TODO: -d debug vlag van command line OR die van XML
            // TODO: -e sayerror vlag van command line OR die van XML
            // TODO: -n saynoupdate vlag van command line OR die van XML

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

            // Parse the serverXmlString

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(serverXmlString); 
            // <Updater> is the root element
            string serverVersionString;
            string serverUpdatePath;
            try
            {
                serverVersionString = xmlDoc.SelectSingleNode("updater/version").InnerText;
                serverUpdatePath = xmlDoc.SelectSingleNode("updater/path").InnerText;
            }
            catch (Exception)
            {
                Log("Error: Error in remote xml file");
                return;
            }

            // Compare server version to our version

            // major.minor.build.revision scheme, requires 2 or 3 or 4 parts
            Version localVersion = Version.Parse(config.version);
            Version serverVersion = Version.Parse(serverVersionString);

            // Version.CompareTo returns >0 if serverversion > localversion
            bool IsNewerAvailable = serverVersion.CompareTo(localVersion) > 0;

            // Is a newer version available?
            if (IsNewerAvailable)
            {
                Log("** A newer version is available");

                // TODO
                // ask to download or not?

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

                // TODO
                // Close running copies of the application before running the installer

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
                Log("** No update is available");
            }

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
