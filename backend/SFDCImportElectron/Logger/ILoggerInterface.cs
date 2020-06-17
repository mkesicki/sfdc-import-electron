using System;

namespace SFDCImportElectron.Logger
{
    interface ILoggerInterface
    {
        void Info(String message);
        public void Warning(String message);
        public void Error(String message);
        public void Save();
        public void Close();   

        public int Success { get; set; }
        public int Errors { get; set; }
    }
}
