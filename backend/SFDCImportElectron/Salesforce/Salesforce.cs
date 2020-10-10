using Newtonsoft.Json;
using RestSharp;
using SFDCImportElectron.Converter;
using SFDCImportElectron.Logger;
using SFDCImportElectron.Model;
using SFDCImportElectron.Response;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Authentication;
using System.Threading;

namespace SFDCImportElectron.Salesforce
{
    class Salesforce : ICloneable
    {
        private readonly String ApiVersion = "v48.0"; //yeah, config variable

        public static int BatchSize = 200;
        public readonly int maxRecords = 200;
        private String Token { get; set; }
        private String ClientId { get; set; }
        private String ClientSecret { get; set; }
        private String Username { get; set; }
        private String Password { get; set; }
        private String LoginUrl { get; set; }
        public String InstanceUrl { get; set; }
        private Dictionary<String, Metadata> Meta { get; set; }
        private RestClient Client;
        private ILoggerInterface Logger { get; set; }
        private SalesforceBody body = new SalesforceBody();
        private String ParentObject;
        Dictionary<int, MappingPayload.Mapping> Mapping { get; set; }

        public virtual object Clone()
        {
            Salesforce clone = new Salesforce(ClientId, ClientSecret, Username, Password, LoginUrl, Logger);
            clone.Meta = this.Meta;
            clone.ParentObject = this.ParentObject;
            clone.Mapping = this.Mapping;

            clone.Token = this.Token;
            clone.InstanceUrl = this.InstanceUrl;
            clone.Client = new RestClient(InstanceUrl);

            return clone;
        }

        public Salesforce(String ClientId, String ClientSecret, String Username, string Password, String LoginUrl, ILoggerInterface Logger)
        {
            this.ClientId = ClientId;
            this.ClientSecret = ClientSecret;
            this.Username = Username;
            this.Password = Password;
            this.LoginUrl = LoginUrl;
            this.Logger = Logger;
            this.Mapping = new Dictionary<int, MappingPayload.Mapping>();

            Meta = new Dictionary<String, Metadata>();
        }

        public Salesforce(String token, String instanceUrl, ILoggerInterface Logger)
        {
            Token = "Bearer " + token;
            InstanceUrl = instanceUrl;
            Client = new RestClient(InstanceUrl);
            this.Logger = Logger;
            this.Mapping = new Dictionary<int, MappingPayload.Mapping>();
            Meta = new Dictionary<String, Metadata>();
        }

        public void Login()
        {
            Client = new RestClient(LoginUrl);

            RestRequest request = new RestRequest(LoginUrl + "/services/oauth2/token", Method.POST);
            request.AddParameter("grant_type", "password");
            request.AddParameter("client_id", ClientId);
            request.AddParameter("client_secret", ClientSecret);
            request.AddParameter("username", Username);
            request.AddParameter("password", Password);

            IRestResponse response = Client.Execute(request);

            if (HttpStatusCode.OK == response.StatusCode)
            {
                RestSharp.Serialization.Json.JsonDeserializer deserializer = new RestSharp.Serialization.Json.JsonDeserializer();
                Dictionary<String, String> body = deserializer.Deserialize<Dictionary<String, String>>(response);

                Token = "Bearer " + body["access_token"];
                InstanceUrl = body["instance_url"];
                Client = new RestClient(InstanceUrl);

                return;
            }

            throw new AuthenticationException("Login error! Check provided login data.");
        }

        public void RetrieveMetadata(String ObjectName)
        {
            RestRequest request = new RestRequest(InstanceUrl + "/services/data/" + ApiVersion + "/sobjects/" + ObjectName + "/describe", Method.GET);
            request.AddHeader("Authorization", Token);

            IRestResponse response = Client.Execute(request);

            if (HttpStatusCode.OK == response.StatusCode)
            {
                RestSharp.Serialization.Json.JsonDeserializer deserializer = new RestSharp.Serialization.Json.JsonDeserializer();
                Metadata desc = deserializer.Deserialize<Metadata>(response);
                Meta.Add(ObjectName, desc);

                return;
            }

            throw new ApplicationException("Error getting " + ObjectName + " Metadata");
        }

        public List<Sobject> RetrieveObjects()
        {
            RestRequest request = new RestRequest(InstanceUrl + "/services/data/" + ApiVersion + "/sobjects", Method.GET);
            request.AddHeader("Authorization", Token);

            IRestResponse response = Client.Execute(request);

            List<Sobject> objects = new List<Sobject>();

            if (HttpStatusCode.OK == response.StatusCode)
            {
                RestSharp.Serialization.Json.JsonDeserializer deserializer = new RestSharp.Serialization.Json.JsonDeserializer();

                //Console.WriteLine(response.Content);
                MetadataSobject sobjects = deserializer.Deserialize<MetadataSobject>(response);

                objects = sobjects.Sobjects;

            }

            return objects;
        }

        public int SetMapping(String jsonBody, Dictionary<int, string> Header)
        {
            MappingPayload tmp = JsonConvert.DeserializeObject<MappingPayload>(jsonBody);
            ParentObject = tmp.parent;

            Dictionary<int, MappingPayload.Mapping> mapping = new Dictionary<int, MappingPayload.Mapping>();

            foreach (KeyValuePair<int, String> entry in Header)
            {
                MappingPayload.Mapping map = tmp.find(entry.Value);
                Mapping.Add(entry.Key, map);
                if (!Meta.ContainsKey(map.toObject))
                {
                    RetrieveMetadata(map.toObject);
                }
            }

            BatchSize = (int)this.maxRecords / tmp.size;

            return BatchSize;
        }

        public void PreparePayload(String[] data, int line)
        {

            Dictionary<string, Dictionary<string, object>> body = new Dictionary<string, Dictionary<string, object>>();
            Record parent = new Record();

            int i = 0;
            foreach (String value in data)
            {

                //only if field was mapped
                if (Mapping.ContainsKey(i))
                {
                    MappingPayload.Mapping map = Mapping[i];

                    Dictionary<string, object> dataset = new Dictionary<string, object>();
                    dataset.Add(map.toColumn, value);

                    if (body.ContainsKey(map.toObject))
                    {
                        body[map.toObject].Add(map.toColumn, value);
                    }
                    else
                    {
                        body.Add(map.toObject, dataset);
                    }
                }
                i++;
            }

            parent.attributes.Add("type", ParentObject);
            parent.attributes.Add("referenceId", ParentObject + line.ToString());

            RestSharp.Serialization.Json.JsonSerializer serializer = new RestSharp.Serialization.Json.JsonSerializer();

            parent.fields = body[ParentObject];

            List<Record> records = new List<Record>();
            SalesforceBody children = new SalesforceBody();

            foreach (KeyValuePair<string, Dictionary<string, object>> entry in body)
            {
                if (entry.Key == ParentObject) continue;

                Record child = new Record();

                child.attributes.Add("type", entry.Key);
                child.attributes.Add("referenceId", entry.Key + line.ToString());
                child.fields = entry.Value;

                children.records = records;
                parent.children.Add(Meta[entry.Key].labelPlural, new SalesforceBody(child));
            }

            this.body.records.Add(parent);

            if (this.body.records.Count >= BatchSize) flush();
        }

        public async void flush()
        {
            if (body.records.Count == 0) return;

            String jsonBody = JsonConvert.SerializeObject(body, Formatting.None, new RecordObjectConverter());
            body = new SalesforceBody();

            String Url = InstanceUrl + "/services/data/" + ApiVersion + "/composite/tree/" + ParentObject;

            RestRequest request = new RestRequest(Url, Method.POST);
            request.AddHeader("Authorization", Token);
            request.AddHeader("Content-Type", "application/json");

            request.AddJsonBody(jsonBody);

            Thread.Sleep(500);
            IRestResponse response = await Client.ExecutePostAsync(request);

            RestSharp.Serialization.Json.JsonDeserializer deserializer = new RestSharp.Serialization.Json.JsonDeserializer();

            if (HttpStatusCode.Created == response.StatusCode)
            {
                SuccessResponse results = deserializer.Deserialize<SuccessResponse>(response);
                foreach (ResultSuccess result in results.results)
                {
                    Logger.Info(String.Format("Object Reference: {0} added with id: {1}", result.referenceId, result.id));
                }
            }
            else if (HttpStatusCode.BadRequest == response.StatusCode)
            {
                ErrorResponse errors = deserializer.Deserialize<ErrorResponse>(response);

                foreach (ResultError result in errors.results)
                {
                    String message = "";
                    message = String.Format("Object Reference: {0} has errors: ", result.referenceId);
                    foreach (Error error in result.errors)
                    {
                        message = message + error.message + " for fields [";
                        foreach (String errorMessage in error.fields)
                        {
                            message = message + errorMessage + ",";
                        }
                    }

                    message = message.Substring(0, message.Length - 1) + "]";
                    Logger.Error(message);
                }
            }
        }

        public Dictionary<String, List<String>> getMetadata()
        {

            Dictionary<string, List<String>> results = new Dictionary<string, List<String>>();

            foreach (KeyValuePair<string, Metadata> meta in Meta)
            {

                List<string> fields = new List<string>();

                foreach (Field field in meta.Value.fields)
                {

                    if (field.updateable)
                    {
                        fields.Add(field.name);
                    }
                }

                results.Add(meta.Key, fields);
            }

            return results;
        }
    }
}