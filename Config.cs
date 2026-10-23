using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using ZUpdater.Properties;
using static Updater.Config;

namespace Updater
{
    [XmlRoot("updater")]
    public class Config
    {
        [XmlElement("caption")]
        public string Caption
        { get; set; }

        [XmlElement("name")]
        public string Name
        { get; set; }

        [XmlElement("processname")]
        public string ProcessName
        { get; set; }

        [XmlElement("version")]
        public string Version
        { get; set; }

        [XmlElement("hidden")]
        public bool Hidden
        { get; set; }

        [XmlElement("showlog")]
        public bool Showlog
        { get; set; }

        // can appear multiple times in the XML
        [XmlElement("source")]
        public List<Source> Sources
        { get; set; } = new List<Source>();

        // one <source> element
        public class Source
        {
            // <source type="...">
            [XmlAttribute("type")]
            public string Type
            { get; set; }

            [XmlElement("location")]
            public string Location
            { get; set; }

            [XmlElement("assetpattern")]
            public string AssetPattern
            { get; set; }
        }

        public void Load(string file)
        {
            XmlSerializer xs = new XmlSerializer(typeof(Config));
            Config tmpConfig;
            using (StreamReader sr = new StreamReader(file))
            {
                tmpConfig = xs.Deserialize(sr) as Config;
            }

            Validate( tmpConfig);   // throws with all problems listed

            // get what we need
            this.Caption = tmpConfig.Caption;
            this.Name = tmpConfig.Name;
            this.ProcessName = tmpConfig.ProcessName;
            this.Version = tmpConfig.Version;
            this.Hidden = tmpConfig.Hidden;
            this.Showlog = tmpConfig.Showlog;
            this.Sources = tmpConfig.Sources ?? new List<Source>();
        }

        private void Validate(Config c)   
        {
            // caption required
            if (string.IsNullOrWhiteSpace(c.Caption))
                throw new InvalidDataException("<caption> is required.");

            // name required
            if (string.IsNullOrWhiteSpace(c.Name))
                throw new InvalidDataException("<name> is required.");

            // processname required
            if (string.IsNullOrWhiteSpace(c.ProcessName))
                throw new InvalidDataException("<processname> is required.");

            // name required
            if (string.IsNullOrWhiteSpace(c.Name))
                throw new InvalidDataException("<name> is required.");

            // at least 1 source
            if (c.Sources == null || c.Sources.Count == 0)
            {
                throw new InvalidDataException("At least 1 <source> is required.");
            }

            // validate each source
            for (int i = 0; i < c.Sources.Count; i++)
            {
                Source s = c.Sources[i];
                string errorPrefix = $"<source>[{i}]: ";

                // type
                if (string.IsNullOrWhiteSpace(s.Type))
                {
                    throw new InvalidDataException(errorPrefix + $"<source> type is missing.");
                }
                if ((s.Type == "fileshare" || s.Type == "githubrelease"))
                {
                    // OK
                }
                else
                {
                    throw new InvalidDataException(errorPrefix + $"<source> type '{s.Type}' is invalid.");
                }

                // location
                if (string.IsNullOrWhiteSpace(s.Location))
                {
                    throw new InvalidDataException(errorPrefix + $"<location> is missing.");
                }

                // assetpattern
                if (s.Type == "githubrelease")
                {
                    if (string.IsNullOrWhiteSpace(s.AssetPattern))
                    {
                        throw new InvalidDataException(errorPrefix + $"<assetpattern> is missing.");
                    }

                    // validate regex
                    try
                    {
                        Regex regex = new Regex(s.AssetPattern);
                    }
                    catch (ArgumentException ex)
                    {
                        throw new InvalidDataException(errorPrefix + $"Invalid assetpattern regex '{s.AssetPattern}': {ex.Message}");
                    }
                }
            }
        }
    }
}
