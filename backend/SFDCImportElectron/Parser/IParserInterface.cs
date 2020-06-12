using System;
using System.Collections.Generic;

namespace SFDCImportElectron.Parser
{
    interface IParserInterface
    {
        Dictionary<String, String> Row { get; set; }
        public Dictionary<int, string> Header { get; set; }
        public Boolean isInProgress { get; set; }

        public int Size { get; set; }

        int Success { get; set; }
        int Error { get; set; }

        public Dictionary<int, string> GetHeader();
        public Dictionary<String, String> ReadRow();
        public void Parse();     
    }
}
