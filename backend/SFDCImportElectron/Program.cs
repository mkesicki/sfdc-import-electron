using System;
using ElectronCgi.DotNet;

namespace SFDCImportElectron
{
    class Program
    {
        static void Main(string[] args)
        {
            var connection = new ConnectionBuilder()
                                .WithLogging()
                                .Build();

            // expects a request named "greeting" with a string argument and returns a string
            connection.On<string, string>("greeting", name =>
            {
                return "Hello " + name;
            });

            // wait for incoming requests
            connection.Listen();
        }
    }
}
