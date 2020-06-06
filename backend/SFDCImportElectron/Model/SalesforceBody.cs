using System;
using System.Collections.Generic;
using System.Text;

namespace SFDCImportElectron.Model
{
    class SalesforceBody
    {
        public List<Record> records { get; set; }

        public SalesforceBody() {
            records = new List<Record>();
        }

        public SalesforceBody(Record record) {
            records = new List<Record> { record };
        }
    }
}
