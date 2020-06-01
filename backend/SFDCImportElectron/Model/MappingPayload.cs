using System.Collections.Generic;

namespace SFDCImportElectron.Model
{
    public class MappingPayload
    {

        public class Mapping
        {

            public string from { get; set; }
            public string toObject { get; set; }
            public string toColumn { get; set; }

        }

        public string parent { get; set; }
        public List<Mapping> mapping { get; set; }

    }
}
