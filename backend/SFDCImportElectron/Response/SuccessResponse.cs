using System.Collections.Generic;

namespace SFDCImportElectron.Response
{
    public class ResultSuccess
    {
        public string referenceId { get; set; }
        public string id { get; set; }
    }

    public class SuccessResponse
    {
        public bool hasErrors { get; set; }
        public List<ResultSuccess> results { get; set; }
    }
}
