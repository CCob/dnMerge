using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Serialization;

namespace dnMerge {

    public class dnMergeConfig {

        public bool GeneratePDB { get; set; } = true;

        public bool OverwriteAssembly { get; set; } = true;

        [XmlArray("ExcludeReferences")]
        [XmlArrayItem("ReferenceName")]
        public string[] ExcludeReferences { get; set; } = new string[] { };
        
    }
}
