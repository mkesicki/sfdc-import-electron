using System;
using System.Collections.Generic;

namespace SFDCImportElectron.Model
{
    class Record
    {
        public Dictionary<string, string> attributes { get; set; }

        public Dictionary<String, object> fields { get; set; }

        public Dictionary<string, SalesforceBody> children { get; set; }
    }
}
