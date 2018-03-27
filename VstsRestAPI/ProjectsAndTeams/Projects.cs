using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using VstsRestAPI;
using VstsRestAPI.Viewmodel;
using VstsRestAPI.Viewmodel.ProjectAndTeams;
using Newtonsoft;
using Newtonsoft.Json.Linq;



namespace VstsRestAPI.ProjectsAndTeams
{
    public class Projects
    {
        public string lastFailureMessage;
        readonly IConfiguration _configuration;
        readonly string _credentials;

        public Projects(IConfiguration configuration)
        {
            _configuration = configuration;
            _credentials = configuration.PersonalAccessToken;//Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", "", _configuration.PersonalAccessToken)));
        }

        public bool IsAccountHasProjects()
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuration.UriString);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials);

                // connect to the REST endpoint            
                HttpResponseMessage response = client.GetAsync("_apis/projects?stateFilter=All&api-version=" + _configuration.VersionNumber).Result;
                // check to see if we have a succesfull respond
                return response.StatusCode == System.Net.HttpStatusCode.OK;
            }
           // return false;
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
                HttpResponseMessage response = client.GetAsync("_apis/projects?stateFilter=All&api-version=2.2").Result;
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

        public string CreateTeamProject(string json)
        {
            //string json = System.IO.File.ReadAllText(Server.MapPath("~") + @"\JSON\CreateTeamProject.json");
            //json = json.Replace("$ProjectName$", txtProjectName.Text).Replace("$ProjectDescription$", txtProjectName.Text).Replace("$TemplateId$", ddlProcesses.SelectedValue);

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuration.UriString);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials);

                //var jsonContent = new StringContent(JsonConvert.SerializeObject(json), Encoding.UTF8, "application/json");
                var jsonContent = new StringContent(json, Encoding.UTF8, "application/json");
                var method = new HttpMethod("POST");

                var request = new HttpRequestMessage(method,  "_apis/projects?api-version=" + _configuration.VersionNumber + "-preview") { Content = jsonContent };
                var response = client.SendAsync(request).Result;

                //HttpResponseMessage response = client.PostAsync("_apis/process/processes?api-version=2.2", jsonContent

                if (response.IsSuccessStatusCode)
                {
                    string result = response.Content.ReadAsStringAsync().Result;
                    string projectId = JObject.Parse(result)["id"].ToString();
                    return projectId;
                }
                else
                {
                    var errorMessage = response.Content.ReadAsStringAsync();
                    string error = Utility.GeterroMessage(errorMessage.Result.ToString());
                    this.lastFailureMessage = error;
                    return "-1";
                }
            }
        }

        public string GetProjectIdByName(string projectName)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuration.UriString);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials);

                HttpResponseMessage response = client.GetAsync("_apis/projects/" + projectName + "?includeCapabilities=false&api-version=" + _configuration.VersionNumber).Result;

                if (response.IsSuccessStatusCode)
                {
                    string result = response.Content.ReadAsStringAsync().Result;
                    string projectId = JObject.Parse(result)["id"].ToString();
                    return projectId;
                }
                else
                {
                    var errorMessage = response.Content.ReadAsStringAsync();
                    string error = Utility.GeterroMessage(errorMessage.Result.ToString());
                    this.lastFailureMessage = error;
                    return Guid.Empty.ToString();
                }
            }
        }

        public string GetProjectStateByName(string project)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuration.UriString);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _credentials);

                HttpResponseMessage response = client.GetAsync("_apis/projects/" + project + "?includeCapabilities=true&api-version=" + _configuration.VersionNumber).Result;

                if (response.IsSuccessStatusCode)
                {
                    string result = response.Content.ReadAsStringAsync().Result;
                    string projectStatus = JObject.Parse(result)["state"].ToString();
                    return projectStatus;
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

    }
}

              
