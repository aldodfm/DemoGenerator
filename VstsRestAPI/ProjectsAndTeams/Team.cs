using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using VstsRestAPI.Viewmodel;
using VstsRestAPI.Viewmodel.ProjectAndTeams;
using Newtonsoft;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace VstsRestAPI.ProjectsAndTeams
{
    public class Team
    {
        public string lastFailureMessage;
        readonly IConfiguration _configuration;
        readonly string _credentials;

        public Team(IConfiguration configuration)
        {
            _configuration = configuration;
            _credentials = configuration.PersonalAccessToken;//Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", "", _configuration.PersonalAccessToken)));
        }

        public ListofProjectsResponse.Projects ListOfProjects()
        {
            ListofProjectsResponse.Projects viewModel = new ListofProjectsResponse.Projects();
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuration.UriString);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials);

                // connect to the REST endpoint            
                HttpResponseMessage response = client.GetAsync("_apis/projects?stateFilter=All&api-version=" + _configuration.VersionNumber).Result;
                // check to see if we have a succesfull respond
                if (response.IsSuccessStatusCode)
                {
                    // set the viewmodel from the content in the response
                    viewModel = response.Content.ReadAsAsync<ListofProjectsResponse.Projects>().Result;
                }
                viewModel.HttpStatusCode = response.StatusCode;
                return viewModel;
            }
        }

        public GetTeamResponse.Team CreateNewTeam(string json, string project)
        {
            GetTeamResponse.Team viewModel = new GetTeamResponse.Team();

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials);

                // serialize the fields array into a json string  
                //var patchValue = new StringContent(JsonConvert.SerializeObject(team), Encoding.UTF8, "application/json");
                var jsonContent = new StringContent(json, Encoding.UTF8, "application/json");
                var method = new HttpMethod("POST");

                var request = new HttpRequestMessage(method, _configuration.UriString + "/_apis/projects/" + project + "/teams?api-version=" + _configuration.VersionNumber) { Content = jsonContent };
                var response = client.SendAsync(request).Result;

                if (response.IsSuccessStatusCode)
                {
                    viewModel = response.Content.ReadAsAsync<GetTeamResponse.Team>().Result;
                }
                else
                {
                    var errorMessage = response.Content.ReadAsStringAsync();
                    string error = Utility.GeterroMessage(errorMessage.Result.ToString());
                    this.lastFailureMessage = error;
                }
            }
            return viewModel;
        }
        public TeamMemberResponse.TeamMembers GetTeamMembers(string projectName, string teamaName)
        {
            TeamMemberResponse.TeamMembers viewModel = new TeamMemberResponse.TeamMembers();
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuration.UriString);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials);
                HttpResponseMessage response = client.GetAsync("_apis/projects/" + projectName + "/teams/" + teamaName + "/members/?api-version=" + _configuration.VersionNumber).Result;
                if (response.IsSuccessStatusCode)
                {
                    viewModel = response.Content.ReadAsAsync<TeamMemberResponse.TeamMembers>().Result;
                    return viewModel;
                }
                else
                {
                    var errorMessage = response.Content.ReadAsStringAsync();
                    string error = Utility.GeterroMessage(errorMessage.Result.ToString());
                    this.lastFailureMessage = error;
                    return new TeamMemberResponse.TeamMembers();
                }
            }
        }

        public string CreateArea(string projectName, string areaName)
        {
            string createdAreaName = string.Empty;

            object node = new { name = areaName };

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials);

                // serialize the fields array into a json string  
                //var patchValue = new StringContent(JsonConvert.SerializeObject(team), Encoding.UTF8, "application/json");
                var jsonContent = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(node), Encoding.UTF8, "application/json");
                var method = new HttpMethod("POST");

                var request = new HttpRequestMessage(method, _configuration.UriString + projectName + "/_apis/wit/classificationNodes/areas?api-version=1.0") { Content = jsonContent };
                var response = client.SendAsync(request).Result;

                if (response.IsSuccessStatusCode)
                {
                    string result = response.Content.ReadAsStringAsync().Result;
                    JObject jobj = JObject.Parse(result);
                    createdAreaName = jobj["name"].ToString();
                    return createdAreaName;
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

        public bool SetAreaForTeams(string projectName, string teamName,string json)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials);

                var patchValue = new StringContent(json, Encoding.UTF8, "application/json"); 

                var method = new HttpMethod("PATCH");

                var request = new HttpRequestMessage(method, _configuration.UriString + projectName + "/" + teamName + "/_apis/work/teamsettings/teamfieldvalues?api-version=2.0-preview.1") { Content = patchValue };
                var response = client.SendAsync(request).Result;
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    var errorMessage = response.Content.ReadAsStringAsync();
                    string error = Utility.GeterroMessage(errorMessage.Result.ToString());
                    this.lastFailureMessage = error;
                    return false;
                }
            }
        }

        public string GetTeamSetting(string projectName)
        {
            TeamSettingResponse.TeamSetting viewModel = new TeamSettingResponse.TeamSetting();

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuration.UriString);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials);

                HttpResponseMessage response = client.GetAsync(projectName + "/_apis/work/teamsettings?api-version=3.0-preview").Result;

                if (response.IsSuccessStatusCode)
                {
                    viewModel = response.Content.ReadAsAsync<TeamSettingResponse.TeamSetting>().Result;
                    return viewModel.backlogIteration.id;
                }
                else
                {
                    return string.Empty;
                }

            }
        }

        public bool SetBackLogIterationForTeam(string IterationId, string projectName, string teamName)
        {
            object objJSON = new { Backlogiteration = IterationId };

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials);

                var postValue = new StringContent(JsonConvert.SerializeObject(objJSON), Encoding.UTF8, "application/json");

                var method = new HttpMethod("PATCH");

                var request = new HttpRequestMessage(method, _configuration.UriString + projectName + "/" + teamName + "/_apis/work/teamsettings?api-version=3.0-preview") { Content = postValue };
                var response = client.SendAsync(request).Result;
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    var errorMessage = response.Content.ReadAsStringAsync();
                    string error = Utility.GeterroMessage(errorMessage.Result.ToString());
                    this.lastFailureMessage = error;
                    return false;
                }
            }
        }

        public TeamIterationsResponse.Iterations GetAllIterations(string projectName)
        {
            TeamIterationsResponse.Iterations viewModel = new TeamIterationsResponse.Iterations();

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuration.UriString);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials);

                HttpResponseMessage response = client.GetAsync(projectName + "/_apis/work/teamsettings/iterations?api-version=v2.0-preview").Result;

                if (response.IsSuccessStatusCode)
                {
                    viewModel = response.Content.ReadAsAsync<TeamIterationsResponse.Iterations>().Result;
                    return viewModel;
                }
                else
                {
                    return new TeamIterationsResponse.Iterations();
                }

            }
        }

        public bool SetIterationsForTeam(string IterationId, string teamName, string projectName)
        {
            object objJSON = new { id = IterationId };

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials);

                var jsonContent = new StringContent(JsonConvert.SerializeObject(objJSON), Encoding.UTF8, "application/json");
                var method = new HttpMethod("POST");

                var request = new HttpRequestMessage(method, _configuration.UriString + projectName + "/" + teamName + "/_apis/work/teamsettings/iterations?api-version=v2.0-preview") { Content = jsonContent };
                var response = client.SendAsync(request).Result;

                if (response.IsSuccessStatusCode)
                {
                    return true;
                }
                else
                {
                    var errorMessage = response.Content.ReadAsStringAsync();
                    string error = Utility.GeterroMessage(errorMessage.Result.ToString());
                    this.lastFailureMessage = error;
                    return false;
                }
            }
        }  
    }
}
   