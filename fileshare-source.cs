using System;
using System.Xml;

namespace Updater
{
    // code and data to handle a fileshare source, where the UpdaterInfo.xml file is on a network share or local folder

    public class UpdateInfoXmlSource
    {
        public string VersionStr;
        public Version Version;
        public string FilePath;
        public string FileName;

        /// <summary>
        /// Log lessage callback
        /// </summary>
        public event Action<string> LogMessage;

        private void Log(string message)
        {
            LogMessage?.Invoke(message);
        }

        /// <summary>
        /// Load the data from the UpdaterInfo.xml file on a fileshare or local folder.
        /// Throw exception on error.
        /// </summary>
        /// <param name="infofilePath">Path to XML info file</param>
        public void GetData(string infofilePath)
        {
            Log($"  Check fileshare: '{infofilePath}'"); 

            string xmlStr = System.IO.File.ReadAllText(infofilePath);

            XmlDocument xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(xmlStr);

            // get root element
            XmlNode xroot = xmlDoc.SelectSingleNode("updater");
            if (xroot is null)
            {
                throw new Exception("XML root element <updater> not found.");
            }

            // get version
            XmlNode xn1 = xroot.SelectSingleNode("version");
            if (xn1 is null)
            {
                throw new Exception("XML element <version> not found.");
            }
            this.VersionStr = xn1.InnerText;

            // convert version string to Version object
            try
            {
                this.Version = Version.Parse(VersionStr);
            }
            catch (Exception)
            {
                throw new Exception("XML element <version> is invalid.");
            }
            Log($"  Version found: {this.Version}");

            // get file name or path
            xn1 = xroot.SelectSingleNode("file");
            if (xn1 is null)
            {
                throw new Exception("XML element <file> not found.");
            }
            this.FilePath = xn1.InnerText;
            Log($"  File found: '{this.FilePath}'");

            // Check if we have a URL
            if (String.IsNullOrEmpty(this.FilePath) || (this.FilePath.Trim() == String.Empty))
            {
                throw new Exception("XML element <file> is empty.");
            }

            // get file name 
            this.FileName = System.IO.Path.GetFileName(this.FilePath);

            return;
        }

        /// <summary>
        /// Copy the file from the fileshare or local folder to a local path.
        /// </summary>
        /// <param name="filepath">Source file path</param>
        /// <param name="localSavePath">Local destination path</param>
        public void Download(string filepath, string localSavePath)
        {
            Log($"Download file '{filepath}'");

            // overwrite: true replaces an existing file
            System.IO.File.Copy(filepath, localSavePath, overwrite: true);
        }
    }
}
