using System;
using ElectronCgi.DotNet;
using System.IO;
using Newtonsoft.Json;
using SFDCImportElectron.Logger;
using SFDCImportElectron.Parser;

namespace SFDCImportElectron
{
    class Program
    {
        static void Main()
        {
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
                String csv = args[5];

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

                FileLogger Logger = new FileLogger("logs");

                Salesforce.Salesforce SFDC = new Salesforce.Salesforce(ClientID, ClientSecret, Username, Password, LoginUrl, Logger);

                //CSVThread parser = new CSVThread(csv, Logger, SFDC);

                //Console.WriteLine("instance wtf : {0}", SFDC.InstanceUrl);

                return $"Logged to salesforce instance: {SFDC.InstanceUrl}";
            });

            // wait for incoming requests
            connection.Listen();
        }

        private static String Help()
        {
            return new String("SFDC Import is a simple console app to insert objects in Salesforce from CSV file. \n" +
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
