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

        public int getErrorSize();
        public int getSucessSize();
    }
}
