using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using RestSharp;
using SFDCImportElectron.Logger;
using SFDCImportElectron.Model;
using SFDCImportElectron.Response;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Authentication;
using SFDCImportElectron.Converter;

namespace SFDCImportElectron.Salesforce
{
    class Salesforce : ICloneable
    {
        private readonly String ApiVersion = "v48.0"; //yeah, config variable

        private int _batchSize = 100;

        public int BatchSize {
            get {return _batchSize; }
            set { _batchSize = (int)(200/(value+1));}
        }
                private String Token { get; set; }
        private String ClientId { get; set; }
        private String ClientSecret { get; set; }
        private String Username { get; set; }
        private String Password { get; set; }
        private String LoginUrl { get; set; }
        public String InstanceUrl { get; set; }
        //private List<ObjectPayload> Payload { get; set; }
        private Dictionary<String, Metadata> Meta { get; set; }
        RestClient Client;
        private ILoggerInterface Logger { get; set; }

        private SalesforceBody body = new SalesforceBody();

        private String ParentObject;

        public virtual object Clone()
        {
            Salesforce clone = new Salesforce(ClientId, ClientSecret, Username, Password, LoginUrl, Logger);
            clone.Meta = this.Meta;
            clone._batchSize = this._batchSize;

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

            Meta = new Dictionary<String, Metadata>();
            //Payload = new List<ObjectPayload>();

            Login();
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

                Console.WriteLine("Logged to: " + InstanceUrl);    

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

        public void setMapping(String jsonBody)
        {
            MappingPayload mapping = JsonConvert.DeserializeObject<MappingPayload>(jsonBody);

            ParentObject = mapping.parent;
        }
       
        public void flush()
        {
            //if (Payload.Count == 0) return;

            //PrepareBody();

            String jsonBody = JsonConvert.SerializeObject(body, Formatting.None, new RecordObjectConverter());
            String Url = InstanceUrl + "/services/data/" + ApiVersion + "/composite/tree/" + ParentObject;

            RestRequest request = new RestRequest(Url, Method.POST);
            request.AddHeader("Authorization", Token);
            request.AddHeader("Content-Type", "application/json");

            request.AddJsonBody(jsonBody);

            IRestResponse response = Client.Execute(request);

            body = new SalesforceBody();
            //Payload = new List<ObjectPayload>();

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
                String message = "";
                foreach (ResultError result in errors.results)
                {
                    message = String.Format("Object Reference: {0} has errors: ", result.referenceId);
                    foreach (Error error in result.errors)
                    {
                        message = message + error.message + " for fiedds [";
                        foreach (String errorMessage in error.fields)
                        {
                            message = message + errorMessage + ",";
                        }
                    }

                    message = message.Substring(0, message.Length - 1) + "]";
                }
                Logger.Error(message);
            }
        }

        public Dictionary<String, List<String>> getMetadata() {

            Dictionary<string, List<String >> results = new Dictionary<string, List<String>>();

            foreach (KeyValuePair<string, Metadata> meta in Meta) {

                List<string> fields = new List<string>();

                foreach (Field field in meta.Value.fields) {

                    if (field.updateable) {
                        fields.Add(field.name);
                    }
                }

                results.Add(meta.Key, fields);
            }

            return results;
        }
    }

/*    public class ObjectPayload
    {
        public String Name { get; set; }
        public Dictionary<string, object> Fields { get; set; }
        public string Reference { get; set; }
        public List<ObjectPayload> Children { get; set; }
    }*/    
}

