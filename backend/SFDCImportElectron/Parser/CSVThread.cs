using SFDCImportElectron.Logger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace SFDCImportElectron.Parser
{
    class CSVThread :
        IParserInterface
    {
        public Dictionary<string, string> Row { get; set; }
        public Dictionary<int, string> Header { get; set; }

        public List<string> Columns { get; set; }
        public int Success { get; set; }
        public int Error { get; set; }
        public string Path { get; set; }
        private int Cores { get; set; }
        public int Size { get; set; }

        public int Processed { get { return _Processed; }  set {_Processed = value; } }
        public int BatchSize { get; set; }
        public int MinimumThreadSize { get; set; }

        private StreamReader CSV { get; set; }
        private List<StreamReader> FilesToParse { get; set; }
        private ILoggerInterface Logger { get; set; }

        private Dictionary<int, int> startLine { get; set; }

        private List<Thread> Threads = new List<Thread>();
        private List<Salesforce.Salesforce> sfdcs { get; set; }

        public Boolean isInProgress  { get; set;}

        public volatile int _Processed;


        public CSVThread(String Path, ILoggerInterface Logger, Salesforce.Salesforce Sfdc /*, String mapping*/)
        {
            Header = new Dictionary<int, string>();
            Columns = new List<string>();
            Columns = new List<string>();
            Row = new Dictionary<string, string>();
            startLine = new Dictionary<int, int>();
            sfdcs = new List<Salesforce.Salesforce>();
            MinimumThreadSize = 1000;
            isInProgress = false;
            _Processed = 0;

            this.Logger = Logger;

            if (!File.Exists(Path))
            {
                throw new FileNotFoundException("File to parse: {0} not found!", Path);
            }

            this.Path = Path;

            CSV = new StreamReader(Path);
            Size = File.ReadLines(Path).Count() - 1; //do not count header line

            Cores = (Size > MinimumThreadSize) ? Environment.ProcessorCount : 1;

            //get Header
            sfdcs.Add(Sfdc);
            GetHeader();

            sfdcs[0].BatchSize = Sfdc.BatchSize; //configure batch size according to number of relations @TODO implement it somehow
        }

        private void ParseFile(Object core)
        {

            int cpu = (int)core;
            int line = 0;
            Dictionary<String, Dictionary<String, String>> payload = new Dictionary<String, Dictionary<String, String>>();
            isInProgress = true;

            using (StreamReader sr = FilesToParse[cpu])
            {
                while ((cpu == Cores - 1 || line < BatchSize) && !sr.EndOfStream)
                {
                    String message = sr.ReadLine();

                    //split line by column, add to payload, every batch limit size send to SFDC
                    String[] data = message.Split(",");

                    //Console.WriteLine(String.Format("cpu#{0} {1}", cpu, message));
                     sfdcs[cpu].PreparePayload(data, line + this.startLine[cpu]);
                    //Logger.Info(String.Format("cpu#{0}: {1}", cpu, message));
                    line++;
                    Interlocked.Increment(ref _Processed);
                }
            }
        }

        public bool IsReady() {

            bool workersActive = true;

            int count = 0;
            for (int i = 0; i < Cores; i++)
            {
                if (Threads[i].IsAlive == false)
                {
                    sfdcs[i].flush();
                    count++;
                }
            }            

            if (count == Cores ) { workersActive = false; }

            if (workersActive == false)
            {
                isInProgress = false;
                Logger.Save();

                return true;               
            }

            return false;
        }

        public void  Parse()
        {
            //clone salesforce instances
            for (int i = 0; i < Cores - 1; i++)
            {
                sfdcs.Add((Salesforce.Salesforce)sfdcs[0].Clone());
            }

            //prepare copies of files for threads
            PrepareFileToParse();

            for (int i = 0; i < Cores; i++)
            {
                ParameterizedThreadStart start = new ParameterizedThreadStart(ParseFile);
                Thread t = new Thread(start);
                t.Start(i);
                Threads.Add(t);
            }          
        }

        private void PrepareFileToParse()
        {
            FilesToParse = new List<StreamReader>();

            DateTime foo = DateTime.UtcNow;
            long suffix = ((DateTimeOffset)foo).ToUnixTimeSeconds();

            for (int i = 0; i < Cores; i++)
            {
                String destFile = "tmp" + System.IO.Path.DirectorySeparatorChar + "parsed_" + suffix + "_" + i + ".csv";
                File.Copy(Path, destFile);
                FilesToParse.Add(new StreamReader(destFile));
            }

            BatchSize = (int)(Size / Cores);

            //set file pointers to read in right place
            MoveToFileLine();
        }

        public Dictionary<int, string> GetHeader()
        {
            String header = CSV.ReadLine();
            string[] labels = header.Split(',');
            int i = 0;

            foreach (String label in labels) {

                Header.Add(i, label);
                i++;
            }
          
            return Header;
        }

        public Dictionary<string, string> ReadRow()
        {
            throw new NotImplementedException();
        }

        private void MoveToFileLine()
        {
            for (int i = 0; i < Cores; i++)
            {
                int startLine = (i * BatchSize) + 1;
                this.startLine.Add(i, startLine);
                int readed = 0;

                StreamReader sr = FilesToParse[i];
                while (readed < startLine)
                {
                    sr.ReadLine();
                    readed++;
                }
            }
        }
    }
}
