using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using VstsRestAPI.Viewmodel.Repository;

namespace VstsRestAPI.Git
{
    public class Repository
    {
        public string lastFailureMessage;
        readonly IConfiguration _configuration;
        readonly string _credentials;

        public Repository(IConfiguration configuration)
        {
            _configuration = configuration;
            _credentials = configuration.PersonalAccessToken;//Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", "", _configuration.PersonalAccessToken)));
        }

        public bool getSourceCodeFromGitHub(string json, string Project, string RepositoryID)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials);

                var jsonContent = new StringContent(json, Encoding.UTF8, "application/json");
                var method = new HttpMethod("POST");

                var request = new HttpRequestMessage(method, _configuration.UriString + Project + "/_apis/git/repositories/" + RepositoryID + "/importRequests?api-version=" + _configuration.VersionNumber + "-preview") { Content = jsonContent };
                var response = client.SendAsync(request).Result;

                if (response.IsSuccessStatusCode)
                {
                    return response.IsSuccessStatusCode;
                }
                else
                {
                    var errorMessage = response.Content.ReadAsStringAsync();
                    string error = Utility.GeterroMessage(errorMessage.Result.ToString());
                    this.lastFailureMessage = error;
                }
            }
            return false;
        }
        public string GetRepositoryToDelete(string Project)
        {
            GetAllRepositoriesResponse.Repositories viewModel = new GetAllRepositoriesResponse.Repositories();
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuration.UriString);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials);

                HttpResponseMessage response = client.GetAsync(Project + "/_apis/git/repositories?api-version=" + _configuration.VersionNumber).Result;
                if (response.IsSuccessStatusCode)
                {
                    viewModel = response.Content.ReadAsAsync<GetAllRepositoriesResponse.Repositories>().Result;
                    string repository = viewModel.value.Where(x => x.name == Project).FirstOrDefault().id;
                    return repository;
                }
                else
                {
                    var errorMessage = response.Content.ReadAsStringAsync();
                    string error = Utility.GeterroMessage(errorMessage.Result.ToString());
                    this.lastFailureMessage = error;
                }
            }

            return string.Empty;
        }
        public string[] GetDefaultRepository(string RepoName)
        {
            string[] repo = new string[2];
            GetAllRepositoriesResponse.Repositories viewModel = new GetAllRepositoriesResponse.Repositories();
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuration.UriString);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials);

                HttpResponseMessage response = client.GetAsync(RepoName + "/_apis/git/repositories?api-version=" + _configuration.VersionNumber).Result;
                if (response.IsSuccessStatusCode)
                {
                    viewModel = response.Content.ReadAsAsync<GetAllRepositoriesResponse.Repositories>().Result;
                    if (viewModel.count > 0)
                    {
                        repo[0] = viewModel.value.FirstOrDefault().id;
                        repo[1] = viewModel.value.FirstOrDefault().name;
                    }
                    return repo;
                }
                else
                {
                    var errorMessage = response.Content.ReadAsStringAsync();
                    string error = Utility.GeterroMessage(errorMessage.Result.ToString());
                    this.lastFailureMessage = error;
                }
            }
            return repo;
        }

        public GetAllRepositoriesResponse.Repositories GetAllRepositories()
        {
            GetAllRepositoriesResponse.Repositories viewModel = new GetAllRepositoriesResponse.Repositories();
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuration.UriString);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials);

                HttpResponseMessage response = client.GetAsync("/_apis/git/repositories?api-version=" + _configuration.VersionNumber).Result;
                if (response.IsSuccessStatusCode)
                {
                    viewModel = response.Content.ReadAsAsync<GetAllRepositoriesResponse.Repositories>().Result;
                    return viewModel;
                }
                else
                {
                    var errorMessage = response.Content.ReadAsStringAsync();
                    string error = Utility.GeterroMessage(errorMessage.Result.ToString());
                    this.lastFailureMessage = error;
                }
            }

            return new GetAllRepositoriesResponse.Repositories();
        }

        public string[] CreateRepositorie(string name, string projectId)
        {
            string[] repository = new string[2];


            dynamic objJson = new System.Dynamic.ExpandoObject();
            objJson.name = name;
            objJson.project = new System.Dynamic.ExpandoObject();
            objJson.project.id = projectId;
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(objJson);

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials);

                var jsonContent = new StringContent(json, Encoding.UTF8, "application/json");
                var method = new HttpMethod("POST");

                var request = new HttpRequestMessage(method, _configuration.UriString + "/_apis/git/repositories?api-version=1.0") { Content = jsonContent };
                var response = client.SendAsync(request).Result;

                if (response.IsSuccessStatusCode)
                {
                    var responseDetails = response.Content.ReadAsStringAsync().Result;
                    JObject objResponse = JObject.Parse(responseDetails);
                    repository[0] = objResponse["id"].ToString();
                    repository[1] = objResponse["name"].ToString();
                    return repository;
                }
                else
                {
                    var errorMessage = response.Content.ReadAsStringAsync();
                    string error = Utility.GeterroMessage(errorMessage.Result.ToString());
                    this.lastFailureMessage = error;
                }
            }
            return repository;
        }
        public bool DeleteRepository(string repositoryId)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials);

                var method = new HttpMethod("DELETE");
                var request = new HttpRequestMessage(method, _configuration.UriString + "/_apis/git/repositories/" + repositoryId + "?api-version=2.0");
                var response = client.SendAsync(request).Result;
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
            }
            return false;
        }

        public string[] CreatePullRequest(string json, string repositoryId)
        {
            string[] pullRequest = new string[2];

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials);

                var jsonContent = new StringContent(json, Encoding.UTF8, "application/json");
                var method = new HttpMethod("POST");

                var request = new HttpRequestMessage(method, _configuration.UriString + "/_apis/git/repositories/" + repositoryId + "/pullRequests?api-version=" + _configuration.VersionNumber) { Content = jsonContent };
                var response = client.SendAsync(request).Result;

                if (response.IsSuccessStatusCode)
                {
                    var responseDetails = response.Content.ReadAsStringAsync().Result;
                    JObject objResponse = JObject.Parse(responseDetails);
                    pullRequest[0] = objResponse["pullRequestId"].ToString();
                    pullRequest[1] = objResponse["title"].ToString();

                    return pullRequest;
                }
                else
                {
                    var errorMessage = response.Content.ReadAsStringAsync();
                    string error = Utility.GeterroMessage(errorMessage.Result.ToString());
                    this.lastFailureMessage = error;
                    return pullRequest;
                }
            }
        }

        public string CreateCommentThread(string repositorId, string pullRequestId, string json)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials);

                var jsonContent = new StringContent(json, Encoding.UTF8, "application/json");
                var method = new HttpMethod("POST");

                var request = new HttpRequestMessage(method, _configuration.UriString + "/_apis/git/repositories/" + repositorId + "/pullRequests/" + pullRequestId + "/threads?api-version=3.0") { Content = jsonContent };
                var response = client.SendAsync(request).Result;

                if (response.IsSuccessStatusCode)
                {
                    var responseDetails = response.Content.ReadAsStringAsync().Result;
                    JObject objResponse = JObject.Parse(responseDetails);
                    string id = objResponse["id"].ToString();
                    return id;
                }
                else
                {
                    var errorMessage = response.Content.ReadAsStringAsync();
                    string error = Utility.GeterroMessage(errorMessage.Result.ToString());
                    this.lastFailureMessage = error;
                    return string.Empty;
                }
            }

        }

        public void AddCommentToThread(string repositorId, string pullRequestId, string threadId, string json)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials);

                var jsonContent = new StringContent(json, Encoding.UTF8, "application/json");
                var method = new HttpMethod("POST");

                var request = new HttpRequestMessage(method, _configuration.UriString + "/_apis/git/repositories/" + repositorId + "/pullRequests/" + pullRequestId + "/threads/" + threadId + "/comments?api-version=3.0") { Content = jsonContent };
                var response = client.SendAsync(request).Result;

                if (response.IsSuccessStatusCode)
                {
                    var responseDetails = response.Content.ReadAsStringAsync().Result;
                    JObject objResponse = JObject.Parse(responseDetails);
                }
                else
                {
                    var errorMessage = response.Content.ReadAsStringAsync();
                    string error = Utility.GeterroMessage(errorMessage.Result.ToString());
                    this.lastFailureMessage = error;
                }
            }
        }
    }
}


