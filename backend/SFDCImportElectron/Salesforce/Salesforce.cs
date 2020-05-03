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
        private List<ObjectPayload> Payload { get; set; }

        private Dictionary<String, Metadata> Meta { get; set; }
        RestClient Client;
        private ILoggerInterface Logger { get; set; }

        private SalesforceBody body = new SalesforceBody();

        private String ObjectName;

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
            Payload = new List<ObjectPayload>();

            Login();
        }

        public void Login()
        {

            //Console.WriteLine("Login to salesforce: " + LoginUrl);

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

            throw new ApplicationException("Error getting object Metadata");
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

        public void PrintPayload(List<ObjectPayload> Payload)
        {
            foreach (ObjectPayload obj in Payload)
            {
                ////Console.WriteLine("payload object {0}", obj.Name);
                foreach (KeyValuePair<string, object> entry in obj.Fields)
                {
                    ////Console.WriteLine("field  {0} : {1}: ref {2}", entry.Key, entry.Value, obj.Reference);
                    foreach (var child in obj.Children)
                    {
                        ////Console.WriteLine("Child object: {0}", child.Name);
                        foreach (KeyValuePair<string, object> field in child.Fields)
                        {
                            ////Console.WriteLine("field  {0} : {1}: ref {2}", field.Key, field.Value.ToString(), child.Reference);
                        }
                    }
                }
            }
        }

        public void PreparePayload(Dictionary<string, List<string>> Relations, Dictionary<string, List<string>> Header, String[] data, int referenceNumber)
        {
            ////Console.WriteLine("REF#"+referenceNumber);
            List<ObjectPayload> Children = new List<ObjectPayload>();
            ObjectPayload parent = new ObjectPayload();
            //String ObjectName = "";
            int i = 0;
            foreach (KeyValuePair<string, List<String>> entry in Header)
            {
                Dictionary<String, object> fields = new Dictionary<String, object>();

                foreach (String column in entry.Value)
                {
                    fields.Add(column, data[i]);
                    i++;
                }

                if (Relations.ContainsKey(entry.Key))
                {
                    parent = new ObjectPayload { Name = entry.Key, Fields = fields, Reference = entry.Key + referenceNumber.ToString() };
                }
                else
                {
                    Children.Add(new ObjectPayload { Name = entry.Key, Fields = fields, Reference = entry.Key + referenceNumber.ToString() });
                }
            }

            if (parent != null)
            {
                if (String.IsNullOrEmpty(this.ObjectName))
                {
                    this.ObjectName = parent.Name;
                }
                Payload.Add(
                     new ObjectPayload { Name = parent.Name, Fields = parent.Fields, Reference = parent.Reference, Children = Children }
                );
            }
            else
            {
                foreach (ObjectPayload body in Children)
                {
                    if (String.IsNullOrEmpty(this.ObjectName))
                    {
                        this.ObjectName = body.Name;
                    }
                    Payload.Add(
                     new ObjectPayload { Name = body.Name, Fields = body.Fields, Reference = body.Reference }
                );
                }
            }

            if (Payload.Count >= BatchSize) flush();
        }

        public void PrepareBody()
        {
            List<Record> records = new List<Record>();
            //SalesforceBody body = new SalesforceBody();
            foreach (ObjectPayload PayloadObject in Payload)
            {
                Dictionary<string, string> Attributes = new Dictionary<string, string>();
                Attributes.Add("type", PayloadObject.Name);
                Attributes.Add("referenceId", "ref" + PayloadObject.Reference);

                Dictionary<string, SalesforceBody> children = new Dictionary<string, SalesforceBody>();
                List<Record> childrenObjects;

                Boolean isChildExists = false;
                Metadata parentMetadata = Meta[PayloadObject.Name];

                foreach (ObjectPayload Child in PayloadObject.Children)
                {
                    String keyName = FindRelationName(parentMetadata, Child.Name);

                    if (children.ContainsKey(keyName))
                    {
                        childrenObjects = children[keyName].records;
                        isChildExists = true;
                    }
                    else
                    {
                        childrenObjects = new List<Record>();
                    };

                    Dictionary<string, string> ChildAttributes = new Dictionary<string, string>();

                    ChildAttributes.Add("type", Child.Name);
                    ChildAttributes.Add("referenceId", "ref" + Child.Reference);

                    if (childrenObjects.Count == 0)
                    {
                        childrenObjects.Add(new Record { attributes = ChildAttributes, fields = Child.Fields });
                    }
                    else
                    {
                        childrenObjects.Add(new Record { fields = Child.Fields });
                    }

                    if (!isChildExists)
                    {
                        children.Add(keyName, new SalesforceBody { records = childrenObjects });
                    }
                    else
                    {
                        children[keyName].records = childrenObjects;
                    }
                }

                records.Add(
                    new Record { attributes = Attributes, fields = PayloadObject.Fields, children = children }
                );
            }

            body.records = records;
        }

        public void flush()
        {
            if (Payload.Count == 0) return;

            PrepareBody();

            ////Console.WriteLine("Flush salesforce data: {0}", body.records.Count);

            string jsonBody = JsonConvert.SerializeObject(body, Formatting.None, new RecordObjectConverter());
            ////Console.WriteLine("Salesforce payload: {0}", jsonBody);

            String Url = InstanceUrl + "/services/data/" + ApiVersion + "/composite/tree/" + ObjectName;

            RestRequest request = new RestRequest(Url, Method.POST);
            request.AddHeader("Authorization", Token);
            request.AddHeader("Content-Type", "application/json");

            request.AddJsonBody(jsonBody);

            IRestResponse response = Client.Execute(request);

            body = new SalesforceBody();
            Payload = new List<ObjectPayload>();

            ////Console.WriteLine(response.Content);
            //Environment.Exit(0);

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

        private String FindRelationName(Metadata Meta, String Name)
        {
            String relationName = "";

            foreach (ChildRelationship relationship in Meta.childRelationships)
            {
                if (relationship.childSObject.Equals(Name))
                {
                    return relationship.relationshipName;
                }
            }

            return relationName;
        }
    }

    public class ObjectPayload
    {
        public String Name { get; set; }
        public Dictionary<string, object> Fields { get; set; }
        public string Reference { get; set; }
        public List<ObjectPayload> Children { get; set; }
    }

    public class Record
    {
        public Dictionary<string, string> attributes { get; set; }

        public Dictionary<String, object> fields { get; set; }

        public Dictionary<string, SalesforceBody> children { get; set; }
    }

    public class SalesforceBody
    {
        public List<Record> records { get; set; }
    }

    internal class RecordObjectConverter : CustomCreationConverter<Record>
    {
        public override Record Create(Type objectType)
        {
            return new Record
            {
                children = new Dictionary<string, SalesforceBody>(),
                fields = new Dictionary<string, object>(),
                attributes = new Dictionary<string, string>()
            };
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteStartObject();

            // Write properties.
            var propertyInfos = value.GetType().GetProperties();
            foreach (var propertyInfo in propertyInfos)
            {
                // Skip the children & fields property.
                if (propertyInfo.Name == "children" || propertyInfo.Name == "fields")
                    continue;

                writer.WritePropertyName(propertyInfo.Name);
                var propertyValue = propertyInfo.GetValue(value);
                serializer.Serialize(writer, propertyValue);
            }

            // Write dictionary key-value pairs.
            var record = (Record)value;
            if (record.children != null)
            {
                foreach (var kvp in record.children)
                {
                    writer.WritePropertyName(kvp.Key);
                    serializer.Serialize(writer, kvp.Value);
                }
            }

            if (record.fields != null)
            {
                foreach (var kvp in record.fields)
                {
                    writer.WritePropertyName(kvp.Key);
                    serializer.Serialize(writer, kvp.Value);
                }
            }
            writer.WriteEndObject();
        }
        public override bool CanWrite
        {
            get { return true; }
        }

    }
}

