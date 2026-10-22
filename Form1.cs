using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CmdLine;
using LST.Helpers;

namespace Updater
{
    public partial class Form1 : Form
    {
        private string windowCaption = "ZUpdater";
        private bool runHidden = false;
        private bool showLog = false;
        private string applicationVersionString;
        private Version applicationVersion;
        private Config config;
        private bool fatalError = false;
        UpdateFound bestUpdate = null;

        private CancellationTokenSource cancellationToken;

        public class UpdateFound
        {
            public string type;
            public string location;
            public string name;
            public string versionStr;
            public Version version;
            public string installerFileName;
            public string installerFileUrl;
        }

        // list of updates found at different sources
        public List<UpdateFound> UpdatesFoundList;

        public Form1()
        {
            InitializeComponent();
        }
                
        private void Form1_Load(object sender, EventArgs e)
        {
            // Runs before form show

            // Show ZUpdater version in log
            Version v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            LogMessage($"ZUpdater v{v.Major}.{v.Minor}.{v.Build}.{v.Revision}");

            // Process the command line
            // must do it in form_load to handle the -h (hidden) switch before the form is shown
            // otherwise it will appear briefly in hidden mode

            try
            {
                ProcessCommandLine();
            }
            catch (Exception ex)
            {
                // Command line error
                // fatal
                string errorText = $"ERROR: Command line error: {CleanNewline(ex.Message)}";
                SetFatalError( errorText);

                // show window with error 
                ShowMe(true);
                ShowLog(showLog);
                return;
            }

            // Load the config file 

            string configfileName = "ZUpdater.xml";
            string configfilePath = Path.Combine(Application.StartupPath, configfileName);

            LogMessage($"Load config file '{configfilePath}'");

            config = new Config();
            try
            {
                config.Load(configfilePath);
            }
            catch (Exception ex)
            {
                // config file error
                // fatal
                string errorText = $"ERROR: Could not load config file: {CleanNewline(ex.Message)}";
                SetFatalError( errorText);

                // show window with error 
                ShowMe(true);
                ShowLog(showLog);
                return;
            }

            // Change window title if specified
            if (String.IsNullOrEmpty(config.caption) == false)
            {
                windowCaption = config.caption;
                this.Text = windowCaption;
            }

            // add the config file switches
            runHidden |= config.hidden;
            showLog |= config.showlog;

            // Take the application version number from the xml file if not specified on the command line
            if (applicationVersionString is null)
            {
                applicationVersionString = config.version;
            }
            // version number given on command line or in xml file? 
            if (String.IsNullOrEmpty(applicationVersionString)
                || (applicationVersionString.Trim() == String.Empty))
            {
                string errorText = "ERROR: No application version number specified";
                SetFatalError(errorText);

                // show window with error 
                ShowMe(true);
                ShowLog(showLog);
                return;
            }

            // Convert application version number, string to Version
            // major.minor.build.revision scheme, requires 2 or 3 or 4 parts
            try
            {
                applicationVersion = Version.Parse(applicationVersionString);
            }
            catch (Exception)
            {
                string errorText = $"ERROR: Application version number is invalid: {applicationVersionString}";
                SetFatalError(errorText);

                // show window with error 
                ShowMe(true);
                ShowLog(showLog);
                return;
            }

            // show info
            LogMessage($"  caption='{config.caption}'");
            LogMessage($"  name='{config.name}'");
            LogMessage($"  processname='{config.processname}'");
            LogMessage($"  appversion={applicationVersionString}");
            LogMessage($"  runhidden={runHidden}");
            LogMessage($"  showlog={showLog}");

            // run visible or hidden?
            ShowMe(!runHidden);
            // with log?
            ShowLog(showLog);
        }

        // Runs after form is shown
        private async void Form1_Shown(object sender, EventArgs e)
        {
            // start check for update if there was no error
            if (!fatalError)
            {
                await RunUpdateAsync();
            }

            // exit updater? with window and log shown?
            if (fatalError)
            {
                // stay open
                // allow the user to see error and close
                ShowMe(true);
                ShowLog(showLog);
            }
            else
            {
                // if running hidden: close app, or we keep an invisible updater running
                if (runHidden)
                {
                    Application.Exit();
                }
                else
                {
                    if (showLog)
                    {
                        // stay open
                        // allow the user to read and close
                        ShowMe(true);
                        ShowLog(showLog);
                    }
                    else
                    {
                        Application.Exit();
                    }
                }                
            }
        }

        private void SetFatalError(string errorText)
        {
            LogMessage(errorText);
            SetStatus(errorText);
            fatalError = true;
            return;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // cancel the async tasks
            cancellationToken?.Cancel();
        }

        /// <summary>
        /// Process the command line
        /// Like: ZUpdater [-v 1.15.8] [-h] [-l]
        /// Throws exception on error
        /// </summary>
        private void ProcessCommandLine()
        {
            CmdLine.CmdLine c = new CmdLine.CmdLine();

            // In windows we get the executable as the first argument. Handle it as a fixed first argument.
            CmdLineString ArgExecutable = new CmdLineString("Executable", EUsage.Fixed, "Executable");
            CmdLineString ArgApplicationVersion = new CmdLineString("v", EUsage.Optional, "Application version number");
            CmdLineSwitch ArgHidden = new CmdLineSwitch("h", EUsage.Optional, "Run hidden until update is found");
            CmdLineSwitch ArgShowLog = new CmdLineSwitch("l", EUsage.Optional, "Show log");

            c.RegisterParameter(ArgExecutable);
            c.RegisterParameter(ArgApplicationVersion);
            c.RegisterParameter(ArgHidden);
            c.RegisterParameter(ArgShowLog);

            // Parse
            string[] args = Environment.GetCommandLineArgs();
            c.Parse(args);

            // Set global parameters to arguments
            if (ArgApplicationVersion.Exists)
            {
                applicationVersionString = ArgApplicationVersion.Value;
            }
            runHidden = ArgHidden.Exists;
            showLog = ArgShowLog.Exists;
        }

         /// <summary>
        /// Run an action on the UI thread. Safe to call from any thread, and safe if the form is already closed.
        /// Uses BeginInvoke (post) and not Invoke (send) so the worker never blocks on the UI thread.
        /// </summary>
        private void UiInvoke(Action action)
        {
            if (IsDisposed) return;

            if (!InvokeRequired)
            {
                action();
                return;
            }

            try
            {
                BeginInvoke((Action)(() =>
                {
                    if (!IsDisposed) action();
                }));
            }
            catch (InvalidOperationException)
            {
                // The window handle is gone (app is closing) - nothing left to update.
                // (ObjectDisposedException derives from InvalidOperationException)
            }
        }

        /// <summary>
        /// Some exception messages have a newline at the end, clean that because it messes up our output.
        /// </summary>
        private string CleanNewline(string message)
        {
            return message.TrimEnd('\r', '\n');
        }
           
        /// <summary>
        /// Add log message in UI thread
        /// </summary>
        private void LogMessage(string message)
        {
            UiInvoke(() => logTextbox.AppendText(message + Environment.NewLine));
        }

        /// <summary>
        /// Add status message in UI thread
        /// </summary>
        private void SetStatus(string message)
        {
            UiInvoke(() => statusLabel.Text += Environment.NewLine + message);
        }

        /// <summary>
        /// Runs the whole update flow with one cancellation token and one place that handles errors.
        /// Never throws.
        /// </summary>
        private async Task RunUpdateAsync()
        {
            // already running?
            if (cancellationToken != null) return; 

            cancellationToken = new CancellationTokenSource();

            try
            {
                await UpdateFlowAsync(cancellationToken.Token);
            }
            catch (OperationCanceledException)
            {
                LogMessage("Cancelled.");
                SetStatus("Cancelled");
            }
            catch (Exception ex)
            {
                LogMessage("ERROR: " + CleanNewline(ex.Message));
                SetStatus("Failed");
            }
            finally
            {
                cancellationToken.Dispose();
                cancellationToken = null;
            }
        }

        /// <summary>
        /// The update flow itself. Runs on the UI thread, but every slow/blocking step is pushed to
        /// a thread-pool thread with Task.Run + await. After each await we are back on the UI
        /// thread, so dialogs, Application.Exit() and control access are safe here.
        /// </summary>
        private async Task UpdateFlowAsync(CancellationToken ct)
        {
            // Check the update sources (network shares, github, ...) on a background thread

            LogMessage("Check if update is available...");
            SetStatus("Check if update is available...");

            UpdatesFoundList = await Task.Run(() => CheckForUpdate(ct), ct);

            // which source has the highest version number?

            bestUpdate = null;
            Version highestVersion = null;
            foreach (UpdateFound upd in UpdatesFoundList)
            {
                if ((highestVersion is null) || (upd.version > highestVersion))
                {
                    bestUpdate = upd;
                    highestVersion = upd.version;
                }
            }

            // found nothing?
            if (bestUpdate == null)
            {
                LogMessage("No update found");
                SetStatus("No update found");
                return;
            }

            // found one
            
            LogMessage($"Highest version found = {bestUpdate.version}");
            LogMessage($"  type='{bestUpdate.type}' location='{bestUpdate.location}'");
          
            // compare update version to our application version
            // Version.CompareTo returns >0 if highestVersion > LocalVersion

            bool isNewerAvailable = bestUpdate.version.CompareTo(applicationVersion) > 0;

            if (isNewerAvailable == false)
            {
                LogMessage("No update is available");
                SetStatus("No update is available");

                // Show dialog if required
                if (!runHidden)
                {
                    MessageBox.Show("No update is available",
                                      windowCaption,
                                      MessageBoxButtons.OK,
                                      MessageBoxIcon.Information);
                }

                return;
            }

            LogMessage("A newer version is available");
            SetStatus("A newer version is available");

            // Ask to download and install

            DialogResult dr = MessageBox.Show("A newer version of " + config.name + " is available." + Environment.NewLine +
                                              "Do you want to download version " + bestUpdate.version.ToString() + " and install it?",
                                  windowCaption,
                                  MessageBoxButtons.YesNo,
                                  MessageBoxIcon.Question);
            if (dr != DialogResult.Yes)
            {
                SetStatus("Update canceled");
                LogMessage("Update canceled");

                return;
            }

            // The user wants to download the new installer
            // From now on show the window if we encounter an error

            LogMessage("Download new version...");
            SetStatus("Download new version...");

            // check Downloads directory
            if (!Directory.Exists(AppHelpers.MyDownloadsDir))
            {
                string errorMessage = $"ERROR: Download directory '{AppHelpers.MyDownloadsDir}' does not exist";
                SetFatalError(errorMessage);
                return;
            }

            // save in the Downloads directory
            string localSavePath = Path.Combine(AppHelpers.MyDownloadsDir, bestUpdate.installerFileName);
            LogMessage($"Local Save path = '{localSavePath}'");

            // Download the new installer on a background thread 

            UpdateFound to_download = bestUpdate;
            try
            { 
                await Task.Run(() => Download(to_download, localSavePath, ct), ct);
            }
            catch (Exception ex)
            {
                string errorMessage = $"ERROR: Download failed: {CleanNewline(ex.Message)}";
                SetFatalError(errorMessage);
                return;
            }

            // The updater window may have been closed while downloading: then end
            ct.ThrowIfCancellationRequested();

            // we have the file?
            if (!File.Exists(localSavePath))
            {
                string errorMessage = $"ERROR: Download finished but the installer file is missing ({localSavePath})";
                SetFatalError(errorMessage);
                return;
            }

            SetStatus("Start install...");

            // Terminate the running application 

            LogMessage("Stopping running application instances");

            try
            {
                Process[] procs = Process.GetProcessesByName(config.processname);
                if (procs.Length >= 1)
                {
                    foreach (Process proc in procs)
                    {
                        proc.Kill();
                    }
                }
            }
            catch (Exception ex)
            {
                SetFatalError($"ERROR: {CleanNewline(ex.Message)}");
                return;
            }

            // Run the new installer

            // Note:
            // Calling Process.Start(localInstallerPath) worked fine until an Admin-by-request update (v8.6.3).
            // It used to return immediately. Now it blocks till the installer has finished.
            // Starting cmd.exe with /C seems a good alternative. Just a pitty that we don't know if the installed started or not.
           
            LogMessage("Start the installer");
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/C \"" + localSavePath + "\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                var pr = Process.Start(psi);
                if (pr is null)
                {
                    SetFatalError("ERROR: Start installer failed");
                    return;
                }
                LogMessage("The installer is started");
            }
            catch (Exception ex)
            {
                string excepmsg = (ex.InnerException != null) ? ex.InnerException.Message : ex.Message;
                string errorMessage = $"ERROR: Could not start the installer: {excepmsg}";
                SetFatalError(errorMessage);
                return;
            }

            LogMessage("Finished");

            return;
        }

        /// <summary>
        /// Background work. These run on a thread-pool thread:
        /// NEVER touch a control (textBox1, label1, ...) or show a MessageBox here.
        /// LogMessage()/SetStatus() are fine, they marshal to the UI thread themselves.
        /// </summary>
        private List<UpdateFound> CheckForUpdate(CancellationToken ct)
        {
            var found = new List<UpdateFound>();

            // Get the versions that are available on the update servers
            
            if (config.sources.Count == 0)
            {
                LogMessage("ERROR: Update server sources are missing");
                return found;
            }

            // scan all sources for an update
            foreach (var source in config.sources)
            {
                // Blocking calls below can't be interrupted, so we check between sources
                ct.ThrowIfCancellationRequested();

                LogMessage($"Source: type='{source.type}' location='{source.location}'");

                if (source.type == "fileshare")
                {
                    // check if we have a location
                    if (String.IsNullOrEmpty(source.location) || (source.location.Trim() == String.Empty))
                    {
                        LogMessage("  ERROR: Update server source location is empty");
                        continue;
                    }

                    // create  a new UpdateInfoXmlSource object and get the data from the UpdaterInfo.xml file
                    UpdateInfoXmlSource fs = new UpdateInfoXmlSource();
                    fs.LogMessage += LogMessage;
                    try
                    {
                        fs.GetData(source.location);
                    }
                    catch (Exception ex)
                    {
                        LogMessage("  ERROR: Could not get the data from the update info XML file: " + CleanNewline(ex.Message));
                        continue;
                    }

                    // absolute or relative path? If relative, combine with the source location
                    string filePath = string.Empty;
                    if (Path.IsPathRooted(fs.FilePath))
                    {
                        // is absolute path
                        filePath = fs.FilePath;
                    }
                    else
                    {
                        // is relative path, combine with source location
                        string sourceDir = Path.GetDirectoryName(source.location);
                        filePath = Path.Combine(sourceDir, fs.FilePath);
                    }

                    // add to list of found updates
                    found.Add(new UpdateFound
                    {
                        type = source.type,
                        location = source.location,
                        name = fs.FilePath,
                        versionStr = fs.VersionStr,
                        version = fs.Version,
                        installerFileName = fs.FileName,
                        installerFileUrl = filePath
                    });
                }
                else if (source.type == "githubrelease")
                {
                    // create a new GithubReleaseSource object and get the data from the github API
                    GithubReleaseSource ghr = new GithubReleaseSource();
                    ghr.LogMessage += LogMessage;
                    try
                    {
                        ghr.GetData(source.location);
                    }
                    catch (Exception ex)
                    {
                        LogMessage("  ERROR: Could not get the data from the github API: " + CleanNewline(ex.Message));
                        continue;
                    }

                    // add to list of found updates
                    found.Add(new UpdateFound
                    {
                        type = source.type,
                        location = source.location,
                        name = ghr.asset_name,
                        versionStr = ghr.VersionStr,
                        version = ghr.Version,
                        installerFileName = ghr.asset_file_name,
                        installerFileUrl = ghr.asset_browser_download_url
                    });
                }
                else
                {
                    LogMessage($"  ERROR: Invalid update server source type: '{source.type}'");
                }
            }

            return found;
        }

        /// <summary>
        /// Do the actual download or get or copy
        /// depending on soucce type
        /// </summary>
        private void Download(UpdateFound update, string localSavePath, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            if (update.type == "fileshare")
            {
                try
                {
                    UpdateInfoXmlSource fs = new UpdateInfoXmlSource();
                    fs.Download(update.installerFileUrl, localSavePath);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    throw new Exception("Could not download the installer file: " + ex.Message, ex);
                }
            }
            else if (update.type == "githubrelease")
            {
                try
                {
                    GithubReleaseSource ghr = new GithubReleaseSource();
                    ghr.Download(update.installerFileUrl, localSavePath);
                }
                catch (Exception ex) when (!(ex is OperationCanceledException))
                {
                    throw new Exception("Could not download the installer file: " + ex.Message, ex);
                }
            }
            else
            {
                throw new InvalidOperationException("Invalid update source type: " + update.type);
            } 
        }

        /// <summary>
        /// Make the main window visible or not
        /// </summary>
        private void ShowMe(bool visible)
        {
            if (visible)
            {
                this.WindowState = FormWindowState.Normal;
                this.ShowInTaskbar = true;
            }
            else
            {
                this.WindowState = FormWindowState.Minimized;
                this.ShowInTaskbar = false;
            }
        }

        /// <summary>
        /// Show the log panel or not
        /// adjust window size accordingly
        /// </summary>
        private void ShowLog(bool visible) 
        {
            if (visible)
            {
                logPanel.Visible = true;
                // careful: logPanel.Height = 0 if Visible=false
                int minLogPanelHeight = 300;
                int logPanelHeight = logPanel.Height;
                if (logPanelHeight < minLogPanelHeight) logPanelHeight = minLogPanelHeight;
                int newHeight = logPanel.Top + logPanelHeight; 
                ClientSize = new Size(ClientSize.Width, newHeight);
            }
            else
            {
                logPanel.Visible = false;
                int newHeight = statusPanel.Height;
                ClientSize = new Size(ClientSize.Width, newHeight);                
            }    
        }
    }
}