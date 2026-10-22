using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using System.IO;

namespace Updater
{
    [XmlRoot("updater")]
    public class Config
    {
        // private

        // public
        // the names of the public variables are also the XML tag names (case sensitive)

        public string caption
        { get; set; }

        public string name
        { get; set; }

        public string processname
        { get; set; }

        public string version
        { get; set; }

        public bool hidden
        { get; set; }

        public bool showlog
        { get; set; }

        // can appear multiple times in the XML
        [XmlElement("source")]
        public List<Source> sources
        { get; set; } = new List<Source>();

        // one <source> element
        public class Source
        {
            // <source type="...">
            [XmlAttribute("type")]
            public string type
            { get; set; }

            public string location
            { get; set; }
        }

        public void Load(string file)
        {
            XmlSerializer xs = new XmlSerializer(typeof(Config));
            Config tmpconfig;
            using (StreamReader sr = new StreamReader(file))
            {
                tmpconfig = xs.Deserialize(sr) as Config;
            }

            this.caption = tmpconfig.caption;
            this.name = tmpconfig.name;
            this.processname = tmpconfig.processname;
            this.version = tmpconfig.version;
            this.hidden = tmpconfig.hidden;
            this.showlog = tmpconfig.showlog;
            this.sources = tmpconfig.sources ?? new List<Source>();
        }
    }
}
