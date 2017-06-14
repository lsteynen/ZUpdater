using System;
using System.Collections.Generic;
using System.Text;
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
        // the names of the public variables are also the XML tag names

        public bool debug
        { get; set; }

        public string version
        { get; set; }

        public string caption
        { get; set; }

        public string name
        { get; set; }

        public string processname
        { get; set; }

        public string url
        { get; set; }

        public bool showerror
        { get; set; }

        public bool shownoupdate
        { get; set; }

        public void Load(string file)
        {
            XmlSerializer xs = new XmlSerializer(typeof(Config));
            StreamReader sr = new StreamReader(file);
            Config tmpconfig = xs.Deserialize(sr) as Config;
            this.debug = tmpconfig.debug;
            this.version = tmpconfig.version;
            this.caption = tmpconfig.caption;
            this.name = tmpconfig.name;
            this.processname = tmpconfig.processname;
            this.url = tmpconfig.url;
            this.showerror = tmpconfig.showerror;
            this.shownoupdate = tmpconfig.shownoupdate;
            sr.Close();
        }

        public void Save(string file)
        {
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", ""); // avoid namespace in XML file
            XmlSerializer serializer = new XmlSerializer(typeof(Config));
            using ( TextWriter writer = new StreamWriter( file))
            {
                serializer.Serialize(writer, this,ns);
            }  
        }
    }
}
