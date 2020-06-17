using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;

namespace SFDCImportElectron.Logger
{
    class FileLogger : ILoggerInterface
    {
        public BlockingCollection<LogMessage> _logMessages = new BlockingCollection<LogMessage>();

        public BlockingCollection<LogMessage> _logErrors = new BlockingCollection<LogMessage>();

        public volatile int _Success;
        public volatile int _Errors;

        private String PathSuccess, PathError;

        public int Success { get { return _Success; } set { _Success = value; } }
        public int Errors { get { return _Errors; } set { _Errors = value; } }

        public FileLogger(String Dir)
        {
            if (!Directory.Exists(Dir))
            {
                Directory.CreateDirectory(Dir);
            }

            PathSuccess = Dir + Path.DirectorySeparatorChar + "success.md";
            PathError = Dir + Path.DirectorySeparatorChar + "error.md";

            if (File.Exists(PathSuccess))
            {
                File.Delete(PathSuccess);
            }

            if (File.Exists(PathError))
            {
                File.Delete(PathError);
            }

            _Errors = 0;
            _Success = 0;
        }

        public void Info(string message)
        {
            String s = String.Format("![#000000] {0}: {1} \n", DateTime.Now, message);
            AddMessage(s);
         
        }
        public void Warning(string message)
        {
            String s = String.Format("![#c5f015] {0}: {1} \n", DateTime.Now, message);
            AddMessage(s);
        }

        public void Error(string message)
        {
            String s = String.Format("![#f03c15] {0}: {1} \n", DateTime.Now, message);
            AddError(s);
        }

        private void SaveLog(BlockingCollection<LogMessage> logs)
        {

            if (0 == logs.Count)
            {
                return;
            }

            //Console.WriteLine("Store log: {0} entries", logs.Count);
            foreach (LogMessage msg in logs.GetConsumingEnumerable())
            {
                File.AppendAllText(msg.Filepath, msg.Text);
                if (logs.IsCompleted || logs.Count == 0)
                {
                    break;
                }
            }
        }

        public void Save()
        {
            SaveLog(_logMessages);
            SaveLog(_logErrors);
        }

        public void Close()
        {
            Save();
        }

        private void AddMessage(String Message)
        {
            _logMessages.Add(new LogMessage(PathSuccess, Message));
            Interlocked.Increment(ref _Success);
        }

        private void AddError(String Message)
        {
            _logErrors.Add(new LogMessage(PathError, Message));
            Interlocked.Increment(ref _Errors);
        }
    }
    public class LogMessage
    {
        public string Filepath { get; set; }
        public string Text { get; set; }

        public LogMessage(String path, String Message)
        {
            Filepath = path;
            Text = Message;
        }     
    }
}
