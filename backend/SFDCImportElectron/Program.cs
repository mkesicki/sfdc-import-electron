using System;
using ElectronCgi.DotNet;
using System.IO;
using Newtonsoft.Json;
using SFDCImportElectron.Logger;
using SFDCImportElectron.Parser;
using System.Collections.Generic;
using SFDCImportElectron.Model;
using System.Linq;

namespace SFDCImportElectron
{

    //class public 
    class Program
    {
        static Salesforce.Salesforce SFDC;

        static IParserInterface parser { get; set; }

        static RestSharp.Serialization.Json.JsonSerializer serializer;

        static FileLogger Logger { get; set; }

        static String csv { get; set; }

        static void Main()
        {


            //String mapping = "{\"parent\":\"Account\",\"mapping\":[{\"from\":\"AName\",\"toObject\":\"Account\",\"toColumn\":\"Name\"},{\"from\":\"StageName\",\"toObject\":\"Opportunity\",\"toColumn\":\"StageName\"},{\"from\":\"CloseDate\",\"toObject\":\"Opportunity\",\"toColumn\":\"CloseDate\"},{\"from\":\"OName\",\"toObject\":\"Opportunity\",\"toColumn\":\"Name\"}]}";
            //FileLogger Logger = new FileLogger("logs");
            //SFDC = new Salesforce.Salesforce("3MVG91BJr_0ZDQ4tOAfzh.FvRVMjEdoK2rESiJ_ZFQtjuO3CnQyIT1m3U5TTh18JzZXzuCwDRVAJx5Ato8GVh", "DECB52F10FEAA5AECAF2930F78560DB21E18A7C7A0F02191DC5C1E52AE85AD68", "michal.kesicki2@cunning-fox-tgor5q.com", "Qfw5NukCUT3f6uU", "https://login.salesforce.com", Logger);
            //parser = new CSVThread(@"C:\Users\Michal\source\repos\sfdc-import-electron\data.csv", Logger, SFDC);
            //SFDC.SetMapping(mapping, parser.Header);
            //parser.Parse();
            //Environment.Exit(0);
            serializer = new RestSharp.Serialization.Json.JsonSerializer();

            var connection = new ConnectionBuilder()
                                .WithLogging()
                                .Build();

            // expects a request named "greeting" with a string argument and returns a string
            connection.On<string, string>("login", data  =>
            {
                /**
             * client_id
             * client_secret
             * username
             * password
             * path to file
             * login url
             * 
             **/

                string[] args = JsonConvert.DeserializeObject<string[]>(data);
                ////check number of arguments passed to applicaiton
                if (args.Length < 6)
                {
                    ////Console.WriteLine("You dind't pass all necessary parameters");
                    throw new ArgumentException(Help());
                }

                String Username = args[0];
                String Password = args[1];
                String ClientID = args[2];
                String ClientSecret = args[3];                
                String LoginUrl = args[4];
                csv = args[5];

                //create necessary directories
                if (!Directory.Exists("results"))
                {
                    Directory.CreateDirectory("results");
                }

                if (!Directory.Exists("tmp"))
                {
                    Directory.CreateDirectory("tmp");
                }

                if (!Directory.Exists("logs"))
                {
                    Directory.CreateDirectory("logs");
                }

                if (!File.Exists(csv))
                {
                    throw new FileNotFoundException("The file was not found!", csv);
                }

                Logger = new FileLogger("logs");

                SFDC = new Salesforce.Salesforce(ClientID, ClientSecret, Username, Password, LoginUrl, Logger);


                parser = new CSVThread(csv, Logger, SFDC);

                return $"Logged to salesforce instance: {SFDC.InstanceUrl}";
            });

            connection.On<string>("getSFDCObjects", () =>
            {
                List<Sobject> sobjects = SFDC.RetrieveObjects();

                return serializer.Serialize(sobjects);
            });


            connection.On<string>("getHeaderRow", () => {

                return serializer.Serialize(parser.Header.Values.ToList());

            });

            connection.On<string, string>("getMetadata", fields => {

                string[] args = JsonConvert.DeserializeObject<string[]>(fields);

                foreach (string name in args)
                {
                    SFDC.RetrieveMetadata(name);
                }

                Dictionary<String, List<string>> data = SFDC.getMetadata();

                return serializer.Serialize(data);
            });

            connection.On<string, string>("parse", mapping => {

                //parser = new CSVThread(csv, Logger, SFDC);

                SFDC.SetMapping(mapping, parser.Header);
                parser.Parse();

                return "{}";

            });

            connection.On<string>("getStatus", () => {


                bool ready = parser.IsReady();

                Dictionary<string, string> response = new Dictionary<string, string>();

                //Boolean x = false;
                //response.Add("isReady", x.ToString());
                //response.Add("all", "100");
                //response.Add("error", "10");
                //response.Add("success", "90");

                response.Add("isReady", ready.ToString());
                response.Add("all", parser.Size.ToString());
                response.Add("processed", parser.Processed.ToString());

                return serializer.Serialize(response);
            }); 

            // wait for incoming requests
            connection.Listen();
        }

        private static String Help()
        {
            return new String("SFDC Import is a simple electron app to insert objects in Salesforce from CSV file. \n" +
                "It creates object with realations and is parsing file with threads \n" +
                "Was creted for learn and fun but the idea of creating parent and child object in one call might \n" +
                "be useful in real case scenarios \n\n" +
                "Required Parameters (in that order): \n" +
                "--client_id - saleforce application client id \n" +
                "--client_secret - saleforce application client secret \n" +
                "--username - saleforce username \n" +
                "--password saleforce password \n" +
                "--login_url - saleforce login instance url \n" +
                "--path - path to CSV file \n"
             );            
        }
    }
}
