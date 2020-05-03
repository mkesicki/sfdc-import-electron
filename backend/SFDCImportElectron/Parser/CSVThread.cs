using SFDCImportElectron.Logger;
using ShellProgressBar;
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
        public Dictionary<string, List<string>> Header { get; set; }
        public Dictionary<string, List<string>> Relations { get; set; }

        public List<string> Columns { get; set; }
        public int Success { get; set; }
        public int Error { get; set; }
        public string Path { get; set; }
        private int Cores { get; set; }
        private ProgressBar StatusBar { get; set; }
        public int Size { get; set; }
        public int BatchSize { get; set; }
        private StreamReader CSV { get; set; }
        private List<StreamReader> FilesToParse { get; set; }
        private ILoggerInterface Logger { get; set; }

        private Dictionary<int, int> startLine { get; set; }

        private List<Thread> Threads = new List<Thread>();
        private List<Salesforce.Salesforce> sfdcs { get; set; }

        public CSVThread(String Path, ILoggerInterface Logger, Salesforce.Salesforce Sfdc)
        {
            Cores = Environment.ProcessorCount;
            Header = new Dictionary<string, List<string>>();
            Columns = new List<string>();
            Columns = new List<string>();
            Row = new Dictionary<string, string>();
            Relations = new Dictionary<string, List<string>>();
            startLine = new Dictionary<int, int>();
            sfdcs = new List<Salesforce.Salesforce>();

            this.Logger = Logger;

            if (!File.Exists(Path))
            {
                throw new FileNotFoundException("File to parse: {0} not found!", Path);
            }

            this.Path = Path;

            CSV = new StreamReader(Path);
            Size = File.ReadLines(Path).Count() - 1; //do not count header file

            //Console.WriteLine("Number of rows to process:  {0}", Size);

            //get Header
            sfdcs.Add(Sfdc);
            GetHeader();

            sfdcs[0].BatchSize = Sfdc.BatchSize = Relations.Count; //configure batch size according to number of relations

            //clone salesforce instances
            for (int i = 0; i < Cores - 1; i++)
            {
                sfdcs.Add((Salesforce.Salesforce)Sfdc.Clone());
            }

            //prepare progress bar
            //var options = new ProgressBarOptions
            //{
            //    ForegroundColor = ConsoleColor.Yellow,
            //    ForegroundColorDone = ConsoleColor.DarkGreen,
            //    BackgroundColor = ConsoleColor.DarkGray,
            //    BackgroundCharacter = '\u2593'
            //};

            //StatusBar = new ProgressBar(Size, "", options);

            //prepare copies of files for threads
            PrepareFileToParse();
        }

        private void ParseFile(Object core)
        {

            int cpu = (int)core;
            int line = 0;
            Dictionary<String, Dictionary<String, String>> payload = new Dictionary<String, Dictionary<String, String>>();

            using (StreamReader sr = FilesToParse[cpu])
            {
                while ((cpu == Cores - 1 || line < BatchSize) && !sr.EndOfStream)
                {
                    String message = sr.ReadLine();

                    //split line by column, add to payload, every batch limit size send to SFDC

                    String[] data = message.Split(",");

                    //Console.WriteLine(String.Format("cpu#{0} {1}", cpu, message));
                    sfdcs[cpu].PreparePayload(Relations, Header, data, line + this.startLine[cpu]);

                    //Logger.Info(String.Format("cpu#{0}: {1}", cpu, message));
                    line++;
                    //StatusBar.Tick();
                }
            }
        }

        public void Parse()
        {
            for (int i = 0; i < Cores; i++)
            {
                ParameterizedThreadStart start = new ParameterizedThreadStart(ParseFile);
                Thread t = new Thread(start);
                t.Start(i);
                Threads.Add(t);
            }

            bool loop = true;

            while (loop)
            {
                int count = 0;
                for (int i = 0; i < Cores; i++)
                {
                    if (Threads[i].IsAlive == false)
                    {
                        sfdcs[i].flush();
                        count++;
                    }
                }
                if (count == Cores) { loop = false; }
            }

            //StatusBar.Dispose();

            return;
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

        public Dictionary<String, List<string>> GetHeader()
        {
            String header = CSV.ReadLine();
            string[] labels = header.Split(',');

            Columns = labels.ToList<string>();
            int i = 0;

            foreach (String label in labels)
            {
                string[] parts = label.Split('.'); // separate object name from field name
                //Columns.Add(parts[0] + "." + parts[1]);

                List<string> tmp = new List<string>();
                List<string> relTmp = new List<string>();

                if (parts.Length == 3 && !Relations.ContainsKey(parts[2]))
                {
                    relTmp.Add(parts[0]);
                    Relations[parts[2]] = relTmp;
                    //Console.WriteLine("Skip column {0}", i);
                }

                if (Header.ContainsKey(parts[0]))
                {
                    tmp = Header[parts[0]];
                }

                tmp.Add(parts[1]);
                Header[parts[0]] = tmp;
                i++;
            }

            //foreach (string x in Columns) { Console.WriteLine("column: {0}", x); }

            //get Metadata for salesforce objects


            foreach (var key in Header.Keys)
            {
                sfdcs[0].RetrieveMetadata(key.ToString());
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
