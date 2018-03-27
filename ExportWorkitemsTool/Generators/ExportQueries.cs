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
    public class ExportQueries
    {
        readonly VstsRestAPI.IConfiguration _sourceConfig;
        readonly string _sourceCredentials;

        public ExportQueries(VstsRestAPI.IConfiguration configuration)
        {
            _sourceConfig = configuration;
            _sourceCredentials = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", "", _sourceConfig.PersonalAccessToken)));
        }

        public Dictionary<string,string> GetQueries(string project)
        {
            Dictionary<string, string> QueryList = new Dictionary<string, string>();
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_sourceConfig.UriString);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", _sourceCredentials);

                HttpResponseMessage response = client.GetAsync("/DefaultCollection/" + project + "/_apis/wit/queries?$depth=2&api-version=2.2").Result;

                if (response.IsSuccessStatusCode)
                {
                    QueryResponse.Query QuerysList = response.Content.ReadAsAsync<QueryResponse.Query>().Result;

                    if (QuerysList.count > 0)
                    {
                        string pathRoot = string.Empty;
                        string pathSub = string.Empty;

                        foreach (var query in QuerysList.value)
                        {
                            if (query.hasChildren)
                            {
                                if (query.isFolder)
                                {
                                    pathRoot = string.Format(@"Templates\Querys\{0}", query.name);
                                    Directory.CreateDirectory(pathRoot);
                                }

                                foreach (var child in query.children)
                                {
                                    if (child.isFolder)
                                    {
                                        pathSub = string.Format(@"{0}\{1}", pathRoot, child.name);
                                        Directory.CreateDirectory(pathSub);
                                        if (child.hasChildren)
                                        {
                                            foreach (var ch in child.children)
                                            {
                                                QueryList.Add(ch.id, ch.name);
                                                System.IO.File.WriteAllText(pathSub + @"\" + ch.name + ".json", JsonConvert.SerializeObject(ch, Formatting.Indented));
                                            }
                                        }

                                    }
                                    else
                                    {
                                        if (child.hasChildren)
                                        {
                                            foreach (var ch in child.children)
                                            {
                                                QueryList.Add(ch.id, ch.name);
                                                System.IO.File.WriteAllText(pathRoot + @"\" + ch.name + ".json", JsonConvert.SerializeObject(ch, Formatting.Indented));
                                            }
                                        }
                                        else
                                        {
                                            QueryList.Add(child.id, child.name);
                                            System.IO.File.WriteAllText(pathRoot + @"\" + child.name + ".json", JsonConvert.SerializeObject(child, Formatting.Indented));
                                        }
                                    }
                                }
                            }

                            else if (query.isFolder)
                            {
                                pathRoot = string.Format(@"Templates\Querys\{0}", query.name);
                                Directory.CreateDirectory(pathRoot);
                            }
                            else
                            {
                                QueryList.Add(query.id, query.name);
                                System.IO.File.WriteAllText(@"Templates\Querys\" + query.name + ".json", JsonConvert.SerializeObject(query, Formatting.Indented));
                            }
                        }
                    }
                }
            }
            return QueryList;
        }

        public Dictionary<string,string> GetQueriesByPath(string project, string path)
        {
            Dictionary<string, string> QueryList = new Dictionary<string, string>();

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_sourceConfig.UriString);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", _sourceCredentials);

                HttpResponseMessage response = client.GetAsync("/DefaultCollection/" + project + "/_apis/wit/queries/Shared%20Queries?$depth=1&api-version=2.2").Result;

                if (response.IsSuccessStatusCode)
                {
                    QueryByPathResponse.query QueryResponse = response.Content.ReadAsAsync<QueryByPathResponse.query>().Result;
                    if (QueryResponse.hasChildren)
                    {
                        foreach (var child in QueryResponse.children)
                        {
                            if (!(child.isFolder))
                            {
                                QueryList.Add(child.id, child.name);

                                if (!Directory.Exists(@"Templates\QueriesByPath"))
                                {
                                    Directory.CreateDirectory(@"Templates\QueriesByPath");
                                }
                                string fetchedCardFieldsJSON = JsonConvert.SerializeObject(child, Formatting.Indented);
                                System.IO.File.WriteAllText(@"Templates\QueriesByPath\" + child.name + ".json", fetchedCardFieldsJSON);
                            }
                        }
                    }
                }
            }
            return QueryList;
        }
    }
}

   


  