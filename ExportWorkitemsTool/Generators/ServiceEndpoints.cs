using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using TemplatesGeneratorTool.ViewModel;

namespace TemplatesGeneratorTool.Generators
{
    public class ServiceEndpoints
    {
        readonly VstsRestAPI.IConfiguration _sourceConfig;
        readonly string _sourceCredentials;


        public ServiceEndpoints(VstsRestAPI.IConfiguration configuration)
        {
            _sourceConfig = configuration;
            _sourceCredentials = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", "", _sourceConfig.PersonalAccessToken)));
        }

        public string ExportServiceEndPoints(string project)
        {
            string serviceEndPoint = string.Empty;

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_sourceConfig.UriString);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", _sourceCredentials);

                HttpResponseMessage response = client.GetAsync(_sourceConfig.UriString + string.Format("/DefaultCollection/{0}/_apis/distributedtask/serviceendpoints?api-version=3.0-preview.1", project)).Result;
                if (response.IsSuccessStatusCode)
                {
                    ServiceEndPointsResponse.Service services = Newtonsoft.Json.JsonConvert.DeserializeObject<ServiceEndPointsResponse.Service>(response.Content.ReadAsStringAsync().Result.ToString());
                   
                    if (services.count > 0)
                    {
                        serviceEndPoint = services.value.FirstOrDefault().name;
                        if (!Directory.Exists(@"Templates\ServiceEndpoints"))
                        {
                            Directory.CreateDirectory(@"Templates\ServiceEndpoints");
                        }
                        int count = 1;
                        foreach (var service in services.value)
                        {
                            string fetchedServiceJSON = JsonConvert.SerializeObject(service, Formatting.Indented);
                            System.IO.File.WriteAllText(@"Templates\ServiceEndpoints\ServiceEndPoint" + count + ".json", fetchedServiceJSON);
                            count = count + 1;
                        }
                    }
                }
            }
            return serviceEndPoint;
        }
    }
}
   
