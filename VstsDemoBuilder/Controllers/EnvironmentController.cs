using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using System.Diagnostics;
using VstsDemoBuilder.Models;
using VstsRestAPI;
using VstsRestAPI.Build;
using VstsRestAPI.Git;
using VstsRestAPI.ProjectsAndTeams;
using VstsRestAPI.Release;
using VstsRestAPI.Service;
using VstsRestAPI.Viewmodel.ProjectAndTeams;
using VstsRestAPI.WorkItemAndTracking;
using VstsDemoBuilder.Extensions;
using VstsRestAPI.Queues;
using VstsRestAPI.Viewmodel.WorkItem;
using VstsRestAPI.Viewmodel.QuerysAndWidgets;
using VstsRestAPI.QuerysAndWidgets;
using System.Dynamic;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.ExtensionManagement.WebApi;
using System.Net;
using VstsRestAPI.Viewmodel.Repository;
using VstsRestAPI.TestManagement;
using System.Net.Http;
using System.Text;
using System.Net.Http.Headers;
using Microsoft.VisualStudio.Services.Client;

namespace VstsDemoBuilder.Controllers
{
    public class EnvironmentController : Controller
    {
        #region Variables & Properties
        private static object objLock = new object();
        private static Dictionary<string, string> statusMessages;
        delegate string[] ProcessEnvironment(Project model, string PAT, string accountName);
        public bool isDefaultRepoTodetele = true;
        public string websiteUrl = string.Empty;
        public string templateUsed = string.Empty;
        public string projectName = string.Empty;
        AccessDetails AccessDetails = new AccessDetails();


        private static Dictionary<string, string> StatusMessages
        {
            get
            {
                if (statusMessages == null)
                {
                    statusMessages = new Dictionary<string, string>();
                }

                return statusMessages;
            }
            set
            {
                statusMessages = value;
            }
        }
        #endregion

        #region Manage Status Messages
        public void AddMessage(string id, string message)
        {
            lock (objLock)
            {
                if (id.EndsWith("_Errors"))
                {
                    StatusMessages[id] = (StatusMessages.ContainsKey(id) ? StatusMessages[id] : string.Empty) + message;
                }
                else
                {
                    StatusMessages[id] = message;
                }
            }
        }

        public void RemoveKey(string id)
        {
            lock (objLock)
            {
                StatusMessages.Remove(id);
            }
        }
        [HttpGet]
        [AllowAnonymous]
        public ContentResult GetCurrentProgress(string id)
        {
            this.ControllerContext.HttpContext.Response.AddHeader("cache-control", "no-cache");
            var currentProgress = GetStatusMessage(id).ToString();
            return Content(currentProgress);
        }
        [HttpGet]
        [AllowAnonymous]
        public string GetStatusMessage(string id)
        {
            lock (objLock)
            {
                string message = string.Empty;
                if (StatusMessages.Keys.Count(x => x == id) == 1)
                {
                    message = StatusMessages[id];
                }
                else
                {
                    return "100";
                }

                if (id.EndsWith("_Errors"))
                {
                    RemoveKey(id);
                }

                return message;
            }
        }
        [AllowAnonymous]
        public ContentResult GetTemplate(string TemplateName)
        {
            string templatesPath = Server.MapPath("~") + @"\Templates\";
            string template = string.Empty;

            if (System.IO.File.Exists(templatesPath + Path.GetFileName(TemplateName) + @"\ProjectTemplate.json"))
            {
                Project objP = new Project();
                template = objP.ReadJsonFile(templatesPath + Path.GetFileName(TemplateName) + @"\ProjectTemplate.json");
            }
            return Content(template);
        }
        #endregion

        #region Controller Actions
        [HttpGet]
        [AllowAnonymous]
        public ActionResult Create(Project model)
        {
            try
            {
                if (Session["visited"] != null)
                {
                    if (Session["templateName"] != null && Session["templateId"] != null && Session["templateName"].ToString() != "" && Session["templateId"].ToString() != "")
                    {
                        model.TemplateName = Session["templateName"].ToString();
                        model.TemplateId = Session["templateId"].ToString();
                    }

                    if (Session["PAT"] != null)
                    {

                        AccessDetails.access_token = Session["PAT"].ToString();
                        ProfileDetails Profile1 = GetProfile(AccessDetails);
                        Session["User"] = Profile1.displayName;

                        Accounts.AccountList accountList1 = GetAccounts(Profile1.id, AccessDetails);

                        model.accessToken = AccessDetails.access_token;
                        Session["PAT"] = AccessDetails.access_token;
                        model.refreshToken = AccessDetails.refresh_token;
                        model.Email = Profile1.emailAddress;
                        model.Name = Profile1.displayName;
                        model.accountsForDropdown = new List<string>();

                        if (accountList1.count > 0)
                        {
                            foreach (var account in accountList1.value)
                            {
                                model.accountsForDropdown.Add(account.accountName);
                            }
                            model.accountsForDropdown.Sort();
                            model.hasAccount = true;
                        }

                        model.Templates = new List<string>();
                        model.accountUsersForDdl = new List<SelectListItem>();
                        TemplateSetting privateTemplates1 = new TemplateSetting();
                        string[] dirTemplates1 = Directory.GetDirectories(Server.MapPath("~") + @"\Templates");

                        foreach (string template in dirTemplates1)
                        {
                            model.Templates.Add(Path.GetFileName(template));
                        }
                        if (System.IO.File.Exists(Server.MapPath("~") + @"\Templates\TemplateSetting.json"))
                        {
                            string privateTemplatesJson = model.ReadJsonFile(Server.MapPath("~") + @"\Templates\TemplateSetting.json");
                            privateTemplates1 = Newtonsoft.Json.JsonConvert.DeserializeObject<TemplateSetting>(privateTemplatesJson);
                        }
                        model.SupportEmail = System.Configuration.ConfigurationManager.AppSettings["SupportEmail"];
                        foreach (string template in privateTemplates1.privateTemplates)
                        {
                            model.Templates.Remove(template);
                        }
                        if (!string.IsNullOrEmpty(model.TemplateName))
                        {
                            if (string.IsNullOrEmpty(model.TemplateId)) { model.TemplateId = ""; }

                            foreach (var template in privateTemplates1.privateTemplateKeys)
                            {
                                if (template.key.ToLower() == model.TemplateId.ToLower() && template.value.ToLower() == model.TemplateName.ToLower())
                                {
                                    model.SelectedTemplate = template.value;
                                    model.Templates.Add(template.value);
                                }
                            }
                        }
                        return View(model);
                    }
                    else
                    {
                        string code = Request.QueryString["code"];

                        string redirectUrl = System.Configuration.ConfigurationManager.AppSettings["RedirectUri"];
                        string clientId = System.Configuration.ConfigurationManager.AppSettings["ClientSecret"];

                        string accessRequestBody = GenerateRequestPostData(clientId, code, redirectUrl);

                        AccessDetails = GetAccessToken(accessRequestBody);

                        //AccessDetails.access_token = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiIsIng1dCI6Im9PdmN6NU1fN3AtSGpJS2xGWHo5M3VfVjBabyJ9.eyJuYW1laWQiOiI5ZjNlMTMyOS0yNzE3LTYxZWMtOTE1Yy04ODdlZDRjY2YxZjEiLCJzY3AiOiJ2c28uYnVpbGRfZXhlY3V0ZSB2c28uY29kZV9tYW5hZ2UgdnNvLmRhc2hib2FyZHNfbWFuYWdlIHZzby5leHRlbnNpb25fbWFuYWdlIHZzby5pZGVudGl0eSB2c28ucHJvamVjdF9tYW5hZ2UgdnNvLnJlbGVhc2VfbWFuYWdlIHZzby5zZXJ2aWNlZW5kcG9pbnRfbWFuYWdlIHZzby53b3JrX2Z1bGwiLCJpc3MiOiJhcHAudnNzcHMudmlzdWFsc3R1ZGlvLmNvbSIsImF1ZCI6ImFwcC52c3Nwcy52aXN1YWxzdHVkaW8uY29tIiwibmJmIjoxNTIwMzM5MDc1LCJleHAiOjE1MjAzNDI2NzV9.COT7_dbKxTIHH2QaGaDoovfS1_22kuzt4zBecsDPBmnxhr4oKA6Ulii7kuOJ0pC_aMoWDLNkuKBlB3dIgynW2wgslxVBuP1wEJrxDuzzX2a1Gq6eHI9h6T_gynNoJ59cKSJCIsnLlULZvnTzYwwNPYuUX0ay9xz7LBNB25Obh_nKf3AQ77vWl8W3hNlF-THzmVfNq0g1qYYo5g9A-r6j5Z_PIC30QSteWUhTKUMo8J3oRdXlP7Ihg8o9oRYpf0mNqyoLJMtDSDVtMe7LI45x817COncYs3JO9cAZJ435EsRQE3CQC0C8uxWzHhPI7HKhe0cxMxB4b0BfoqSEnmBH1Q";
                        //AccessDetails.refresh_token = "eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzI1NiIsIng1dCI6Im9PdmN6NU1fN3AtSGpJS2xGWHo5M3VfVjBabyJ9.eyJuYW1laWQiOiI5ZjNlMTMyOS0yNzE3LTYxZWMtOTE1Yy04ODdlZDRjY2YxZjEiLCJhY2kiOiJiZmU5Njk1OC00OTczLTQ0ODQtYTk2My1iYzE1ZTYxM2JkYTgiLCJzY3AiOiJ2c28uYWdlbnRwb29sc19tYW5hZ2UgdnNvLmJ1aWxkX2V4ZWN1dGUgdnNvLmNoYXRfbWFuYWdlIHZzby5jb2RlX2Z1bGwgdnNvLmNvZGVfc3RhdHVzIHZzby5jb2Rlc2VhcmNoIHZzby5jb25uZWN0ZWRfc2VydmVyIHZzby5kYXNoYm9hcmRzIHZzby5kYXNoYm9hcmRzX21hbmFnZSB2c28uZW50aXRsZW1lbnRzIHZzby5leHRlbnNpb24uZGF0YV93cml0ZSB2c28uZXh0ZW5zaW9uX21hbmFnZSB2c28uZ2FsbGVyeV9hY3F1aXJlIHZzby5nYWxsZXJ5X21hbmFnZSB2c28uaWRlbnRpdHlfbWFuYWdlIHZzby5sb2FkdGVzdF93cml0ZSB2c28ubm90aWZpY2F0aW9uX21hbmFnZSB2c28ucGFja2FnaW5nX21hbmFnZSB2c28ucHJvZmlsZV93cml0ZSB2c28ucHJvamVjdF9tYW5hZ2UgdnNvLnJlbGVhc2VfbWFuYWdlIHZzby5zZWN1cml0eV9tYW5hZ2UgdnNvLnNlcnZpY2VlbmRwb2ludF9tYW5hZ2UgdnNvLnRhc2tncm91cHNfbWFuYWdlIHZzby50ZXN0X3dyaXRlIHZzby53aWtpX3dyaXRlIHZzby53b3JrX2Z1bGwgdnNvLndvcmtpdGVtc2VhcmNoIiwiaXNzIjoiYXBwLnZzc3BzLnZpc3VhbHN0dWRpby5jb20iLCJhdWQiOiJhcHAudnNzcHMudmlzdWFsc3R1ZGlvLmNvbSIsIm5iZiI6MTUwOTA4NjkyNywiZXhwIjoxNTQwNjIyOTI3fQ.NiKU5c1LhIZL5nAnyC5y98EEmQjduXLfUoa9_l9A8kElLwBAXStxAa_FK6V64PoXt2nAT50l8gHviYIS-uhDl1mLdixSm4bn4D3dCcPwRmMrDb_FvWBZne_J0aDiwbG4P6-yz6xZK4IJ_DCqotpYaCR0vi8mv4xb7A5uTY3Ygjl2Ivr_G0Jn0Xvjvgi9vusvGKeptBd35uMzOnFybcbB2prM_RZi14BqjanPdTni6WYEJaOd3lcV5Z7HwmwlP3XdgSGOMwqrpsUJhKN68biOjge2sFkrCmOGb24BwvIN2sqN1UbyZzKh8DTISexsGXxeE013NoHuB8R23UY32UAaDw";

                        ProfileDetails Profile = GetProfile(AccessDetails);
                        Session["User"] = Profile.displayName;

                        Accounts.AccountList accountList = GetAccounts(Profile.id, AccessDetails);

                        model.accessToken = AccessDetails.access_token;
                        Session["PAT"] = AccessDetails.access_token;
                        model.refreshToken = AccessDetails.refresh_token;
                        model.Email = Profile.emailAddress;
                        model.Name = Profile.displayName;
                        model.accountsForDropdown = new List<string>();

                        if (accountList.count > 0)
                        {
                            foreach (var account in accountList.value)
                            {
                                model.accountsForDropdown.Add(account.accountName);
                            }
                            model.accountsForDropdown.Sort();
                            model.hasAccount = true;
                        }

                        model.Templates = new List<string>();
                        model.accountUsersForDdl = new List<SelectListItem>();
                        TemplateSetting privateTemplates = new TemplateSetting();
                        string[] dirTemplates = Directory.GetDirectories(Server.MapPath("~") + @"\Templates");

                        foreach (string template in dirTemplates)
                        {
                            model.Templates.Add(Path.GetFileName(template));
                        }
                        if (System.IO.File.Exists(Server.MapPath("~") + @"\Templates\TemplateSetting.json"))
                        {
                            string privateTemplatesJson = model.ReadJsonFile(Server.MapPath("~") + @"\Templates\TemplateSetting.json");
                            privateTemplates = Newtonsoft.Json.JsonConvert.DeserializeObject<TemplateSetting>(privateTemplatesJson);
                        }
                        model.SupportEmail = System.Configuration.ConfigurationManager.AppSettings["SupportEmail"];
                        foreach (string template in privateTemplates.privateTemplates)
                        {
                            model.Templates.Remove(template);
                        }
                        if (!string.IsNullOrEmpty(model.TemplateName))
                        {
                            if (string.IsNullOrEmpty(model.TemplateId)) { model.TemplateId = ""; }

                            foreach (var template in privateTemplates.privateTemplateKeys)
                            {
                                if (template.key.ToLower() == model.TemplateId.ToLower() && template.value.ToLower() == model.TemplateName.ToLower())
                                {
                                    model.SelectedTemplate = template.value;
                                    model.Templates.Add(template.value);
                                }
                            }
                        }
                        return View(model);
                    }
                }
                else
                {
                    Session.Clear();
                    return Redirect("../Account/Verify");
                }
            }
            catch (Exception ex)
            {
                return View();
            }

        }
        public string GenerateRequestPostData(string appSecret, string authCode, string callbackUrl)
        {
            return String.Format("client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer&client_assertion={0}&grant_type=urn:ietf:params:oauth:grant-type:jwt-bearer&assertion={1}&redirect_uri={2}",
                        HttpUtility.UrlEncode(appSecret),
                        HttpUtility.UrlEncode(authCode),
                        callbackUrl
                 );
        }
        public AccessDetails GetAccessToken(string body)
        {
            var client = new HttpClient();
            client.BaseAddress = new Uri("https://app.vssps.visualstudio.com");

            var request = new HttpRequestMessage(HttpMethod.Post, "/oauth2/token");

            var requestContent = body;
            request.Content = new StringContent(requestContent, Encoding.UTF8, "application/x-www-form-urlencoded");

            var response = client.SendAsync(request).Result;
            if (response.IsSuccessStatusCode)
            {
                string result = response.Content.ReadAsStringAsync().Result;
                AccessDetails details = Newtonsoft.Json.JsonConvert.DeserializeObject<AccessDetails>(result);
                return details;
            }
            return new AccessDetails();
        }


        public ProfileDetails GetProfile(AccessDetails accessDetails)
        {
            ProfileDetails Profile = new ProfileDetails();

            var client = new HttpClient();
            client.BaseAddress = new Uri("https://app.vssps.visualstudio.com");
            var request = new HttpRequestMessage(HttpMethod.Get, "/_apis/profile/profiles/me");

            var requestContent = string.Format(
                "site={0}&api-version={1}", Uri.EscapeDataString("https://app.vssps.visualstudio.com"), Uri.EscapeDataString("1.0"));

            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("Authorization", string.Format("Bearer {0}", accessDetails.access_token));
            try
            {
                var response = client.SendAsync(request).Result;
                if (response.StatusCode == HttpStatusCode.NonAuthoritativeInformation)
                {
                    AccessDetails = Refresh_AccessToken(accessDetails.refresh_token);
                    GetProfile(AccessDetails);
                }
                else if (response.IsSuccessStatusCode)
                {
                    string result = response.Content.ReadAsStringAsync().Result;
                    Profile = JsonConvert.DeserializeObject<ProfileDetails>(result);
                }
                else
                {
                    var errorMessage = response.Content.ReadAsStringAsync();
                    Profile = null;
                }
            }
            catch (Exception ex)
            {
                Profile.ErrorMessage = ex.Message;
            }
            return Profile;
        }

        public AccessDetails Refresh_AccessToken(string refreshToken)
        {
            using (var client = new HttpClient())
            {
                string redirectUri = System.Configuration.ConfigurationManager.AppSettings["RedirectUri"];
                string ClientSecret = System.Configuration.ConfigurationManager.AppSettings["ClientSecret"];
                var request = new HttpRequestMessage(HttpMethod.Post, "https://app.vssps.visualstudio.com/oauth2/token");
                var requestContent = string.Format(
                    "client_assertion_type=urn:ietf:params:oauth:client-assertion-type:jwt-bearer&client_assertion={0}&grant_type=refresh_token&assertion={1}&redirect_uri={2}",
                    HttpUtility.UrlEncode(ClientSecret),
                    HttpUtility.UrlEncode(refreshToken), redirectUri
                    );

                request.Content = new StringContent(requestContent, Encoding.UTF8, "application/x-www-form-urlencoded");
                try
                {
                    var response = client.SendAsync(request).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        string result = response.Content.ReadAsStringAsync().Result;
                        AccessDetails accesDetails = JsonConvert.DeserializeObject<AccessDetails>(result);
                        return accesDetails;
                    }
                    else
                    {
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    return null;
                }
            }
        }
        public Accounts.AccountList GetAccounts(string MemberID, AccessDetails Details)
        {
            if (Session["PAT"] != null)
            {
                Details.access_token = Session["PAT"].ToString();
            }
            Accounts.AccountList Accounts = new Accounts.AccountList();
            var client = new HttpClient();
            string requestContent = "https://app.vssps.visualstudio.com/_apis/Accounts?memberId=" + MemberID + "&api-version=3.2-preview";
            var request = new HttpRequestMessage(HttpMethod.Get, requestContent);
            request.Headers.Add("Authorization", "Bearer " + Details.access_token);
            try
            {
                var response = client.SendAsync(request).Result;
                if (response.StatusCode == HttpStatusCode.NonAuthoritativeInformation)
                {
                    Details = Refresh_AccessToken(Details.refresh_token);
                    return GetAccounts(MemberID, Details);
                }
                else if (response.IsSuccessStatusCode)
                {
                    string result = response.Content.ReadAsStringAsync().Result;
                    Accounts = JsonConvert.DeserializeObject<Accounts.AccountList>(result);
                }
                else
                {
                    var errorMessage = response.Content.ReadAsStringAsync();
                    Accounts = null;
                }
            }
            catch (Exception ex)
            {
                return Accounts;
            }
            return Accounts;
        }


        //public ActionResult Create(Project model)
        //{
        //    model.Templates = new List<string>();
        //    model.accountUsersForDdl = new List<SelectListItem>();
        //    TemplateSetting privateTemplates = new TemplateSetting();
        //    string[] dirTemplates = Directory.GetDirectories(Server.MapPath("~") + @"\Templates");

        //    foreach (string template in dirTemplates)
        //    {
        //        model.Templates.Add(Path.GetFileName(template));
        //    }
        //    if (System.IO.File.Exists(Server.MapPath("~") + @"\Templates\TemplateSetting.json"))
        //    {
        //        string privateTemplatesJson = model.ReadJsonFile(Server.MapPath("~") + @"\Templates\TemplateSetting.json");
        //        privateTemplates = Newtonsoft.Json.JsonConvert.DeserializeObject<TemplateSetting>(privateTemplatesJson);
        //    }
        //    model.SupportEmail = System.Configuration.ConfigurationManager.AppSettings["SupportEmail"];
        //    foreach (string template in privateTemplates.privateTemplates)
        //    {
        //        model.Templates.Remove(template);
        //    }
        //    if (!string.IsNullOrEmpty(model.TemplateName))
        //    {
        //        if (string.IsNullOrEmpty(model.TemplateId)) { model.TemplateId = ""; }

        //        foreach (var template in privateTemplates.privateTemplateKeys)
        //        {
        //            if (template.key.ToLower() == model.TemplateId.ToLower() && template.value.ToLower() == model.TemplateName.ToLower())
        //            {
        //                model.SelectedTemplate = template.value;
        //                model.Templates.Add(template.value);
        //            }
        //        }
        //    }
        //    string PAT = Session["PAT"].ToString();
        //    string accountName = Session["AccountName"].ToString();

        //    string _credentials = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", "", PAT)));
        //    Configuration _defaultConfiguration = new Configuration() { UriString = "https://" + accountName + ".visualstudio.com/DefaultCollection/", VersionNumber = "2.2", PersonalAccessToken = PAT };
        //    AccountMembers.Account accountMembers = new AccountMembers.Account();
        //    //GetAccount Members
        //    Account objAccount = new Account(_defaultConfiguration);
        //    accountMembers = objAccount.GetAccountMembers(accountName);
        //    if (accountMembers.count > 0)
        //    {
        //        foreach (var user in accountMembers.value)
        //        {
        //            model.accountUsersForDdl.Add(new SelectListItem
        //            {
        //                Text = user.member.displayName,
        //                Value = user.member.mailAddress
        //            });
        //        }
        //    }

        //    return View(model);
        //}
        [HttpPost]
        [AllowAnonymous]
        public JsonResult GetMembers(string accountName, string AccessToken)
        {
            Project mod = new Project();
            try
            {
                AccountMembers.Account accountMembers = new AccountMembers.Account();
                Configuration _defaultConfiguration = new Configuration() { UriString = "https://" + accountName + ".visualstudio.com/DefaultCollection/", VersionNumber = "2.2", PersonalAccessToken = AccessToken };
                Account objAccount = new Account(_defaultConfiguration);
                accountMembers = objAccount.GetAccountMembers(accountName, AccessToken);
                if (accountMembers.count > 0)
                {
                    foreach (var user in accountMembers.value)
                    {
                        mod.accountUsersForDdl.Add(new SelectListItem
                        {
                            Text = user.member.displayName,
                            Value = user.member.mailAddress
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return null;
            }
            return Json(mod, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        [AllowAnonymous]
        public bool StartEnvironmentSetupProcess(Project model)
        {
            //if (Session["PAT"] == null || Session["AccountName"] == null)
            //{
            //    return false;
            //}
            //string PAT = Session["PAT"].ToString();
            //string accountName = Session["AccountName"].ToString();

            Location.IPHostGenerator IpCon = new Location.IPHostGenerator();
            string IP = IpCon.GetVisitorDetails();
            string Region = IpCon.GetLocation(IP);
            model.Region = Region;
            Session["PAT"] = model.accessToken;
            Session["AccountName"] = model.accountName;
            AddMessage(model.id, string.Empty);
            AddMessage(model.id.ErrorId(), string.Empty);

            ProcessEnvironment processTask = new ProcessEnvironment(CreateProjectEnvironment);
            processTask.BeginInvoke(model, model.accessToken, model.accountName, new AsyncCallback(EndEnvironmentSetupProcess), processTask);
            return true;
        }

        public void EndEnvironmentSetupProcess(IAsyncResult result)
        {
            ProcessEnvironment processTask = (ProcessEnvironment)result.AsyncState;
            string[] strResult = processTask.EndInvoke(result);

            RemoveKey(strResult[0]);
            if (StatusMessages.Keys.Count(x => x == strResult[0] + "_Errors") == 1)
            {
                string errorMessages = statusMessages[strResult[0] + "_Errors"];
                if (errorMessages != "")
                {
                    //also, log message to file system
                    string LogPath = Server.MapPath("~") + @"\Log";
                    string accountName = strResult[1];
                    string fileName = string.Format("{0}_{1}.txt", templateUsed, DateTime.Now.ToString("ddMMMyyyy_HHmmss"));

                    if (!Directory.Exists(LogPath))
                        Directory.CreateDirectory(LogPath);
                    System.IO.File.AppendAllText(Path.Combine(LogPath, fileName), errorMessages);

                    //Create ISSUE work item with error details in VSTSProjectgenarator account
                    string PATBase64 = System.Configuration.ConfigurationManager.AppSettings["PATBase64"];
                    string URL = System.Configuration.ConfigurationManager.AppSettings["URL"];
                    string ProjectId = System.Configuration.ConfigurationManager.AppSettings["PROJECTID"];
                    string issueName = string.Format("{0}_{1}", templateUsed, DateTime.Now.ToString("ddMMMyyyy_HHmmss"));
                    IssueWI objIssue = new IssueWI();

                    errorMessages = errorMessages + Environment.NewLine + "TemplateUsed: " + templateUsed;
                    errorMessages = errorMessages + Environment.NewLine + "ProjectCreated : " + projectName;
                    errorMessages = errorMessages + Environment.NewLine + "WebsiteURL: " + websiteUrl;

                    objIssue.CreateIssueWI(PATBase64, "1.0", URL, issueName, errorMessages, ProjectId);

                }
            }
        }

        public string[] CreateProjectEnvironment(Project model, string PAT, string accountName)
        {
            PAT = model.accessToken;
            //define versions to be use
            string defaultVersion = System.Configuration.ConfigurationManager.AppSettings["Version2.2"];
            string ver2_0 = System.Configuration.ConfigurationManager.AppSettings["Version2.0"];
            string Ver3_0 = System.Configuration.ConfigurationManager.AppSettings["Version3.0"];
            string processTemplateId = Default.SCRUM;
            model.Environment = new EnvironmentValues();
            model.Environment.ServiceEndpoints = new Dictionary<string, string>();
            model.Environment.RepositoryIdList = new Dictionary<string, string>();
            model.Environment.pullRequests = new Dictionary<string, string>();
            ProjectTemplate template = null;
            ProjectSettings settings = null;
            List<WIMapData> WImapping = new List<WIMapData>();
            AccountMembers.Account accountMembers = new AccountMembers.Account();
            model.accountUsersForWi = new List<string>();
            websiteUrl = model.websiteUrl;
            templateUsed = model.SelectedTemplate;
            projectName = model.ProjectName;

            string LogWIT = System.Configuration.ConfigurationManager.AppSettings["LogWIT"];
            if (LogWIT == "true")
            {
                string PATBase64 = System.Configuration.ConfigurationManager.AppSettings["PATBase64"];
                string URL = System.Configuration.ConfigurationManager.AppSettings["URL"];
                string ProjectId = System.Configuration.ConfigurationManager.AppSettings["PROJECTID"];
                string ReportName = string.Format("{0}", "Analytics-DemoGenerator");
                IssueWI objIssue = new IssueWI();
                objIssue.CreateReportWI(PATBase64, "1.0", URL, websiteUrl, ReportName, "", templateUsed, ProjectId, model.Region);
            }
            //configuration setup
            string _credentials = model.accessToken;//Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", "", PAT)));
            Configuration _defaultConfiguration = new Configuration() { UriString = "https://" + accountName + ".visualstudio.com/DefaultCollection/", VersionNumber = defaultVersion, PersonalAccessToken = PAT };
            Configuration _releaseDefinitionConfiguration = new Configuration() { UriString = "https://" + accountName + ".vsrm.visualstudio.com/DefaultCollection/", VersionNumber = defaultVersion, PersonalAccessToken = PAT };
            Configuration _createReleaseConfiguration = new Configuration() { UriString = "https://" + accountName + ".vsrm.visualstudio.com/DefaultCollection/", VersionNumber = Ver3_0, PersonalAccessToken = PAT };
            Configuration _configuration3_0 = new Configuration() { UriString = "https://" + accountName + ".visualstudio.com/DefaultCollection/", VersionNumber = Ver3_0, PersonalAccessToken = PAT, Project = model.ProjectName };
            Configuration _configuration2_0 = new Configuration() { UriString = "https://" + accountName + ".visualstudio.com/DefaultCollection/", VersionNumber = ver2_0, PersonalAccessToken = PAT };
            Configuration _cardConfiguration = new Configuration() { UriString = "https://" + accountName + ".visualstudio.com:", VersionNumber = ver2_0, PersonalAccessToken = PAT };

            string templatesFolder = Server.MapPath("~") + @"\Templates\";
            string projTemplateFile = string.Format(templatesFolder + @"{0}\ProjectTemplate.json", model.SelectedTemplate);
            string projectSettingsFile = string.Empty;

            //initialize project template and settings
            if (System.IO.File.Exists(projTemplateFile))
            {
                string templateItems = model.ReadJsonFile(projTemplateFile);
                template = JsonConvert.DeserializeObject<ProjectTemplate>(templateItems);

                projectSettingsFile = System.IO.Path.Combine(templatesFolder + model.SelectedTemplate, template.ProjectSettings);
                if (System.IO.File.Exists(projectSettingsFile))
                {
                    settings = JsonConvert.DeserializeObject<ProjectSettings>(model.ReadJsonFile(projectSettingsFile));

                    if (!string.IsNullOrWhiteSpace(settings.type))
                    {
                        if (settings.type.ToLower() == TemplateType.Scrum.ToString().ToLower()) processTemplateId = Default.SCRUM;
                        else if (settings.type.ToLower() == TemplateType.Agile.ToString().ToLower()) processTemplateId = Default.Agile;
                        else if (settings.type.ToLower() == TemplateType.CMMI.ToString().ToLower()) processTemplateId = Default.CMMI;
                    }
                }
            }
            else
            {
                AddMessage(model.id, "Project Template not found");
                StatusMessages[model.id] = "100";
                return new string[] { model.id, accountName };
            }

            //create team project
            //AddMessage(model.id, string.Format("Creating project {0}...", model.ProjectName));
            string jsonProject = model.ReadJsonFile(templatesFolder + "CreateProject.json");
            jsonProject = jsonProject.Replace("$projectName$", model.ProjectName).Replace("$processTemplateId$", processTemplateId);

            Projects proj = new Projects(_defaultConfiguration);
            string projectId = proj.CreateTeamProject(jsonProject);

            if (projectId == "-1")
            {
                AddMessage(model.id, proj.lastFailureMessage);
                Thread.Sleep(1000);
                return new string[] { model.id, accountName };
            }
            else
            {
                AddMessage(model.id, string.Format("Project {0} created", model.ProjectName));
            }

            //Check for project state 
            Stopwatch watch = new Stopwatch();
            watch.Start();
            string projectStatus = string.Empty;
            Projects objProject = new Projects(_defaultConfiguration);
            while (projectStatus.ToLower() != "wellformed")
            {
                projectStatus = objProject.GetProjectStateByName(model.ProjectName);
                if (watch.Elapsed.Minutes >= 5)
                {
                    return new string[] { model.id, accountName };
                }
            }
            watch.Stop();

            //get project id after successfull in VSTS
            model.Environment.ProjectId = objProject.GetProjectIdByName(model.ProjectName);
            model.Environment.ProjectName = model.ProjectName;

            //Install required extensions
            if (model.isExtensionNeeded && model.isAgreeTerms)
            {
                bool isInstalled = InstallExtensions(model, model.accountName, model.accessToken);
                if (isInstalled) { AddMessage(model.id, "Required extensions are installed"); }
            }

            //create teams
            CreateTeams(templatesFolder, model, template.Teams, _defaultConfiguration, model.id, template.TeamArea);

            //current user Details
            string teamName = model.ProjectName + " team";
            TeamMemberResponse.TeamMembers teamMembers = GetTeamMembers(model.ProjectName, teamName, _defaultConfiguration, model.id);

            var teamMember = teamMembers.value.FirstOrDefault();
            if (teamMember != null) model.Environment.UserUniquename = teamMember.uniqueName;
            if (teamMember != null) model.Environment.UserUniqueId = teamMember.id;


            //update board columns and rows
            //AddMessage(model.id, "Updating board columns,rows,styles and enabling Epic...");
            BoardColumn objBoard = new BoardColumn(_defaultConfiguration);
            objBoard.RefreshBoard(model.ProjectName);
            string updateSwimLanesJSON = System.IO.Path.Combine(templatesFolder + model.SelectedTemplate, template.BoardRows);
            SwimLanes objSwimLanes = new SwimLanes(_configuration2_0);
            bool isUpdated = objSwimLanes.UpdateSwimLanes(updateSwimLanesJSON, model.ProjectName);

            bool success = UpdateBoardColumn(templatesFolder, model, template.BoardColumns, _configuration2_0, model.id);
            if (success)
            {
                //update Card Fields
                UpdateCardFields(templatesFolder, model, template.CardField, _cardConfiguration, model.id);
                //Update card styles
                UpdateCardStyles(templatesFolder, model, template.CardStyle, _cardConfiguration, model.id);
                //Enable Epic Backlog
                EnableEpic(templatesFolder, model, template.SetEpic, _configuration3_0, model.id);
                AddMessage(model.id, "Board-Column, Swimlanes, Styles are updated and Epics enabled");

                EnableEpic(templatesFolder, model, template.SetEpic, _configuration3_0, model.id);
            }

            //update sprint dates
            //AddMessage(model.id, "Updating sprint dates...");
            UpdateSprintItems(model, _defaultConfiguration, settings);
            //UpdateIterations(model, _defaultConfiguration, templatesFolder, "iterations.json");
            RenameIterations(model, _defaultConfiguration, settings.renameIterations);
            AddMessage(model.id, "Sprint dates updated");

            //create service endpoint
            //AddMessage(model.id, "Creating service endpoint...");
            List<string> lstEndPointsJsonPath = new List<string>();
            string serviceEndPointsPath = templatesFolder + model.SelectedTemplate + @"\ServiceEndpoints";
            if (System.IO.Directory.Exists(serviceEndPointsPath))
            {
                System.IO.Directory.GetFiles(serviceEndPointsPath).ToList().ForEach(i => lstEndPointsJsonPath.Add(i));
            }
            string endPointJson = string.Format(templatesFolder + @"{0}\{1}", model.SelectedTemplate, template.CreateService);
            lstEndPointsJsonPath.Add(endPointJson);
            CreateServiceEndPoint(model, lstEndPointsJsonPath, _configuration3_0);
            AddMessage(model.id, "Service endpoints created");

            //create agent queues on demand
            Queue queue = new Queue(_configuration3_0);
            model.Environment.AgentQueues = queue.GetQueues();
            if (settings.queues != null && settings.queues.Count > 0)
            {
                foreach (string aq in settings.queues)
                {
                    if (model.Environment.AgentQueues.ContainsKey(aq)) continue;
                    var id = queue.CreateQueue(aq);
                    if (id > 0) model.Environment.AgentQueues[aq] = id;
                }
            }

            //import source code from GitHub
            //AddMessage(model.id, "Importing source code...");

            List<string> lstImportSourceCodeJsonPaths = new List<string>();
            string importSourceCodePath = templatesFolder + model.SelectedTemplate + @"\ImportSourceCode";
            if (System.IO.Directory.Exists(importSourceCodePath))
            {
                System.IO.Directory.GetFiles(importSourceCodePath).ToList().ForEach(i => lstImportSourceCodeJsonPaths.Add(i));
            }
            foreach (string importSourceCode in lstImportSourceCodeJsonPaths)
            {
                ImportSourceCode(templatesFolder, model, importSourceCode, _defaultConfiguration, _configuration3_0, model.id);
            }
            if (isDefaultRepoTodetele)
            {
                Repository objRepository = new Repository(_defaultConfiguration);
                string repositoryToDelete = objRepository.GetRepositoryToDelete(model.ProjectName);
                bool isDeleted = objRepository.DeleteRepository(repositoryToDelete);
            }
            AddMessage(model.id, "Source code imported");

            //Create Pull request
            Thread.Sleep(10000);
            List<string> lstPullRequestJsonPaths = new List<string>();
            string PullRequestFolder = templatesFolder + model.SelectedTemplate + @"\PullRequests";
            if (System.IO.Directory.Exists(PullRequestFolder))
            {
                System.IO.Directory.GetFiles(PullRequestFolder).ToList().ForEach(i => lstPullRequestJsonPaths.Add(i));
            }
            foreach (string pullReq in lstPullRequestJsonPaths)
            {
                CreatePullRequest(templatesFolder, model, pullReq, _configuration3_0);
            }

            //Configure account users
            if (model.UserMethod == "Select")
            {
                model.selectedUsers = model.selectedUsers.TrimEnd(',');
                model.accountUsersForWi = model.selectedUsers.Split(',').ToList();
            }
            else if (model.UserMethod == "Random")
            {
                //GetAccount Members
                Account objAccount = new Account(_defaultConfiguration);
                //accountMembers = objAccount.GetAccountMembers(accountName, AccessToken);
                foreach (var member in accountMembers.value)
                {
                    model.accountUsersForWi.Add(member.member.mailAddress);
                }
            }
            //import work items
            //AddMessage(model.id, "Creating work items ...");
            string featuresFilePath = System.IO.Path.Combine(templatesFolder + model.SelectedTemplate, template.FeaturefromTemplate);
            string productBackLogPath = System.IO.Path.Combine(templatesFolder + model.SelectedTemplate, template.PBIfromTemplate);
            string taskPath = System.IO.Path.Combine(templatesFolder + model.SelectedTemplate, template.TaskfromTemplate);
            string testCasePath = System.IO.Path.Combine(templatesFolder + model.SelectedTemplate, template.TestCasefromTemplate);
            string bugPath = System.IO.Path.Combine(templatesFolder + model.SelectedTemplate, template.BugfromTemplate);
            string epicPath = System.IO.Path.Combine(templatesFolder + model.SelectedTemplate, template.EpicfromTemplate);
            string userStoriesPath = System.IO.Path.Combine(templatesFolder + model.SelectedTemplate, template.UserStoriesFromTemplate);
            string testPlansPath = string.Empty;
            string testSuitesPath = string.Empty;
            if (model.SelectedTemplate.ToLower() == "myshuttle2")
            {
                testPlansPath = System.IO.Path.Combine(templatesFolder + model.SelectedTemplate, template.TestPlanfromTemplate);
                testSuitesPath = System.IO.Path.Combine(templatesFolder + model.SelectedTemplate, template.TestSuitefromTemplate);
            }

            if (model.SelectedTemplate.ToLower() == "myshuttle")
            {
                testPlansPath = System.IO.Path.Combine(templatesFolder + model.SelectedTemplate, template.TestPlanfromTemplate);
                testSuitesPath = System.IO.Path.Combine(templatesFolder + model.SelectedTemplate, template.TestSuitefromTemplate);
            }
            Dictionary<string, string> WorkItems = new Dictionary<string, string>();

            if (System.IO.File.Exists(featuresFilePath)) WorkItems.Add("Feature", model.ReadJsonFile(featuresFilePath));
            if (System.IO.File.Exists(productBackLogPath)) WorkItems.Add("Product Backlog Item", model.ReadJsonFile(productBackLogPath));
            if (System.IO.File.Exists(taskPath)) WorkItems.Add("Task", model.ReadJsonFile(taskPath));
            if (System.IO.File.Exists(testCasePath)) WorkItems.Add("Test Case", model.ReadJsonFile(testCasePath));
            if (System.IO.File.Exists(bugPath)) WorkItems.Add("Bug", model.ReadJsonFile(bugPath));
            if (System.IO.File.Exists(userStoriesPath)) WorkItems.Add("User Story", model.ReadJsonFile(userStoriesPath));
            if (System.IO.File.Exists(epicPath)) WorkItems.Add("Epic", model.ReadJsonFile(epicPath));
            if (System.IO.File.Exists(testPlansPath)) WorkItems.Add("Test Plan", model.ReadJsonFile(testPlansPath));
            if (System.IO.File.Exists(testSuitesPath)) WorkItems.Add("Test Suite", model.ReadJsonFile(testSuitesPath));


            ImportWorkItems import = new ImportWorkItems(_defaultConfiguration, model.Environment.BoardRowFieldName);
            if (System.IO.File.Exists(projectSettingsFile))
            {
                string AttchmentFilesFolder = string.Format(templatesFolder + @"{0}\WorkItemAttachments", model.SelectedTemplate);
                if (lstPullRequestJsonPaths.Count > 0)
                {
                    if (model.SelectedTemplate == "MyHealthClinic")
                    {
                        WImapping = import.ImportWorkitems(WorkItems, model.ProjectName, model.Environment.UserUniquename, model.ReadJsonFile(projectSettingsFile), AttchmentFilesFolder, model.Environment.RepositoryIdList["MyHealthClinic"], model.Environment.ProjectId, model.Environment.pullRequests, model.UserMethod, model.accountUsersForWi);
                    }
                    else
                    {
                        WImapping = import.ImportWorkitems(WorkItems, model.ProjectName, model.Environment.UserUniquename, model.ReadJsonFile(projectSettingsFile), AttchmentFilesFolder, model.Environment.RepositoryIdList[model.SelectedTemplate], model.Environment.ProjectId, model.Environment.pullRequests, model.UserMethod, model.accountUsersForWi);
                    }
                }
                else
                {
                    WImapping = import.ImportWorkitems(WorkItems, model.ProjectName, model.Environment.UserUniquename, model.ReadJsonFile(projectSettingsFile), AttchmentFilesFolder, string.Empty, model.Environment.ProjectId, model.Environment.pullRequests, model.UserMethod, model.accountUsersForWi);
                }
                AddMessage(model.id, "Work Items created");
            }

            //Creat TestPlans and TestSuites
            List<string> lstTestPlansJsonPaths = new List<string>();
            string TestPlansFolder = templatesFolder + model.SelectedTemplate + @"\TestPlans";
            if (System.IO.Directory.Exists(TestPlansFolder))
            {
                System.IO.Directory.GetFiles(TestPlansFolder).ToList().ForEach(i => lstTestPlansJsonPaths.Add(i));
            }
            //if (lstTestPlansJsonPaths.Count > 0) { AddMessage(model.id, "Creating testplans, testsuites and testcases..."); }
            foreach (string testPlan in lstTestPlansJsonPaths)
            {
                CreateTestManagement(WImapping, model, testPlan, templatesFolder, _defaultConfiguration);
            }
            if (lstTestPlansJsonPaths.Count > 0) { AddMessage(model.id, "TestPlans, TestSuites and TestCases created"); }

            //create build Definition
            //AddMessage(model.id, "Creating build definition...");
            string BuildDefinitionsPath = templatesFolder + model.SelectedTemplate + @"\BuildDefinitions";
            model.BuildDefinitions = new List<BuildDef>();
            if (System.IO.Directory.Exists(BuildDefinitionsPath))
            {
                System.IO.Directory.GetFiles(BuildDefinitionsPath, "*.json", SearchOption.AllDirectories).ToList().ForEach(i => model.BuildDefinitions.Add(new Models.BuildDef() { FilePath = i }));
            }
            bool isBuild = CreateBuildDefinition(templatesFolder, model, _defaultConfiguration, model.id);
            if (isBuild)
            {
                AddMessage(model.id, "Build definition created");
            }

            //Queue a Build
            string BuildJson = string.Format(templatesFolder + @"{0}\QueueBuild.json", model.SelectedTemplate);
            if (System.IO.File.Exists(BuildJson))
            {
                QueueABuild(model, BuildJson, _defaultConfiguration);
            }

            //create release Definition
            //AddMessage(model.id, "Creating release definition...");
            string ReleaseDefinitionsPath = templatesFolder + model.SelectedTemplate + @"\ReleaseDefinitions";
            model.ReleaseDefinitions = new List<ReleaseDef>();
            if (System.IO.Directory.Exists(ReleaseDefinitionsPath))
            {
                System.IO.Directory.GetFiles(ReleaseDefinitionsPath, "*.json", SearchOption.AllDirectories).ToList().ForEach(i => model.ReleaseDefinitions.Add(new Models.ReleaseDef() { FilePath = i }));
            }
            bool IsReleased = CreateReleaseDefinition(templatesFolder, model, _releaseDefinitionConfiguration, _configuration3_0, model.id, teamMembers);
            if (IsReleased)
            {
                AddMessage(model.id, "Release definition created");
            }

            //Create query and widgets
            List<string> lstDashboardQueriesPath = new List<string>();
            string dashboardQueriesPath = templatesFolder + model.SelectedTemplate + @"\Dashboard\Queries";
            if (System.IO.Directory.Exists(dashboardQueriesPath))
            {
                System.IO.Directory.GetFiles(dashboardQueriesPath).ToList().ForEach(i => lstDashboardQueriesPath.Add(i));
            }
            //AddMessage(model.id, "Creating queries,widgets and charts...");
            CreateQueryAndWidgets(templatesFolder, model, lstDashboardQueriesPath, _defaultConfiguration, _configuration2_0, _configuration3_0, _releaseDefinitionConfiguration);
            AddMessage(model.id, "Queries, Widgets and Charts created");
            Thread.Sleep(2000);

            StatusMessages[model.id] = "100";
            return new string[] { model.id, accountName };
        }
        #endregion

        #region Project Setup Operations
        private void CreateTeams(string templatesFolder, Project model, string teamsJSON, Configuration _defaultConfiguration, string id, string teamAreaJSON)
        {
            try
            {
                string jsonTeams = string.Format(templatesFolder + @"{0}\{1}", model.SelectedTemplate, teamsJSON);
                if (System.IO.File.Exists(jsonTeams))
                {
                    Team objTeam = new Team(_defaultConfiguration);
                    jsonTeams = model.ReadJsonFile(jsonTeams); //System.IO.File.ReadAllText(jsonTeams);
                    JArray jTeams = JsonConvert.DeserializeObject<JArray>(jsonTeams);
                    Newtonsoft.Json.Linq.JContainer teamsParsed = Newtonsoft.Json.JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JContainer>(jsonTeams);

                    //AddMessage(id, string.Format("Creating {0} teams...", teamsParsed.Count));
                    //get Backlog Iteration Id
                    string backlogIteration = objTeam.GetTeamSetting(model.ProjectName);
                    //get all Iterations
                    TeamIterationsResponse.Iterations iterations = objTeam.GetAllIterations(model.ProjectName);

                    foreach (var jTeam in jTeams)
                    {
                        GetTeamResponse.Team teamResponse = objTeam.CreateNewTeam(jTeam.ToString(), model.ProjectName);
                        if (!(string.IsNullOrEmpty(teamResponse.id)))
                        {
                            string areaName = objTeam.CreateArea(model.ProjectName, teamResponse.name);
                            string updateAreaJSON = string.Format(templatesFolder + @"{0}\{1}", model.SelectedTemplate, teamAreaJSON);
                            if (System.IO.File.Exists(updateAreaJSON))
                            {
                                updateAreaJSON = model.ReadJsonFile(updateAreaJSON);
                                updateAreaJSON = updateAreaJSON.Replace("$ProjectName$", model.ProjectName).Replace("$AreaName$", areaName);
                                bool IsUpdated = objTeam.SetAreaForTeams(model.ProjectName, teamResponse.name, updateAreaJSON);
                            }
                            bool isBackLogIterationUpdated = objTeam.SetBackLogIterationForTeam(backlogIteration, model.ProjectName, teamResponse.name);
                            if (iterations.count > 0)
                            {
                                foreach (var iteration in iterations.value)
                                {
                                    bool isIterationUpdated = objTeam.SetIterationsForTeam(iteration.id, teamResponse.name, model.ProjectName);
                                }
                            }
                        }
                    }
                    if (!(string.IsNullOrEmpty(objTeam.lastFailureMessage)))
                    {
                        AddMessage(id.ErrorId(), "Error while creating teams: " + objTeam.lastFailureMessage + Environment.NewLine);
                    }
                    else
                    {
                        AddMessage(id, string.Format("{0} team(s) created", teamsParsed.Count));
                    }
                }
            }
            catch (Exception ex)
            {
                AddMessage(id.ErrorId(), "Error while creating teams: " + ex.Message + ex.StackTrace + Environment.NewLine);
            }
        }

        private TeamMemberResponse.TeamMembers GetTeamMembers(string projectName, string teamName, Configuration _configuration, string id)
        {
            try
            {
                TeamMemberResponse.TeamMembers viewModel = new TeamMemberResponse.TeamMembers();
                Team objTeam = new Team(_configuration);
                viewModel = objTeam.GetTeamMembers(projectName, teamName);

                if (!(string.IsNullOrEmpty(objTeam.lastFailureMessage)))
                {
                    AddMessage(id.ErrorId(), "Error while getting team members: " + objTeam.lastFailureMessage + Environment.NewLine);
                }
                return viewModel;
            }
            catch (Exception ex)
            {
                AddMessage(id.ErrorId(), "Error while getting team members: " + ex.Message + ex.StackTrace + Environment.NewLine);
            }

            return new TeamMemberResponse.TeamMembers();
        }

        private void CreateWorkItems(string templatesFolder, Project model, string workItemJSON, Configuration _defaultConfiguration, string id)
        {
            try
            {
                string jsonWorkItems = string.Format(templatesFolder + @"{0}\{1}", model.SelectedTemplate, workItemJSON);
                if (System.IO.File.Exists(jsonWorkItems))
                {
                    WorkItemNew objWorkItem = new WorkItemNew(_defaultConfiguration);
                    jsonWorkItems = model.ReadJsonFile(jsonWorkItems);
                    Newtonsoft.Json.Linq.JContainer WorkItemsParsed = Newtonsoft.Json.JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JContainer>(jsonWorkItems);

                    AddMessage(id, "Creating " + WorkItemsParsed.Count + " work items...");

                    jsonWorkItems = jsonWorkItems.Replace("$version$", _defaultConfiguration.VersionNumber);
                    bool workItemResult = objWorkItem.CreateWorkItemUsingByPassRules(model.ProjectName, jsonWorkItems);

                    if (!(string.IsNullOrEmpty(objWorkItem.lastFailureMessage)))
                    {
                        AddMessage(id.ErrorId(), "Error while creating workitems: " + objWorkItem.lastFailureMessage + Environment.NewLine);
                    }
                }

            }
            catch (Exception ex)
            {
                AddMessage(id.ErrorId(), "Error while creating workitems: " + ex.Message + ex.StackTrace + Environment.NewLine);
            }
        }

        private bool UpdateBoardColumn(string templatesFolder, Project model, string BoardColumnsJSON, Configuration _defaultConfiguration, string id)
        {
            bool res = false;
            try
            {
                string jsonBoardColumns = string.Format(templatesFolder + @"{0}\{1}", model.SelectedTemplate, BoardColumnsJSON);
                if (System.IO.File.Exists(jsonBoardColumns))
                {
                    BoardColumn objBoard = new BoardColumn(_defaultConfiguration);
                    jsonBoardColumns = model.ReadJsonFile(jsonBoardColumns);
                    bool BoardColumnResult = objBoard.UpdateBoard(model.ProjectName, jsonBoardColumns);

                    if (BoardColumnResult)
                    {
                        model.Environment.BoardRowFieldName = objBoard.rowFieldName;
                        res = true;
                    }
                    else if (!(string.IsNullOrEmpty(objBoard.lastFailureMessage)))
                    {
                        AddMessage(id.ErrorId(), "Error while updating board column " + objBoard.lastFailureMessage + Environment.NewLine);
                    }
                }
            }
            catch (Exception ex)
            {
                AddMessage(id.ErrorId(), "Error while updating board column " + ex.Message + ex.StackTrace + Environment.NewLine);
            }
            return res;
        }

        private void UpdateCardFields(string templatesFolder, Project model, string json, Configuration _configuration, string id)
        {
            try
            {
                json = string.Format(templatesFolder + @"{0}\{1}", model.SelectedTemplate, json);
                if (System.IO.File.Exists(json))
                {
                    json = model.ReadJsonFile(json);
                    Cards objCards = new Cards(_configuration);
                    objCards.UpdateCardField(model.ProjectName, json);

                    if (!string.IsNullOrEmpty(objCards.LastFailureMessage))
                    {
                        AddMessage(id.ErrorId(), "Error while updating card fields: " + objCards.LastFailureMessage + Environment.NewLine);
                    }
                }
            }
            catch (Exception ex)
            {
                AddMessage(id.ErrorId(), "Error while updating card fields: " + ex.Message + ex.StackTrace + Environment.NewLine);
            }

        }

        private void UpdateCardStyles(string templatesFolder, Project model, string json, Configuration _configuration, string id)
        {
            try
            {
                json = string.Format(templatesFolder + @"{0}\{1}", model.SelectedTemplate, json);
                if (System.IO.File.Exists(json))
                {
                    json = model.ReadJsonFile(json);
                    Cards objCards = new Cards(_configuration);
                    objCards.ApplyRules(model.ProjectName, json);

                    if (!string.IsNullOrEmpty(objCards.LastFailureMessage))
                    {
                        AddMessage(id.ErrorId(), "Error while updating card styles: " + objCards.LastFailureMessage + Environment.NewLine);
                    }
                }
            }
            catch (Exception ex)
            {
                AddMessage(id.ErrorId(), "Error while updating card styles: " + ex.Message + ex.StackTrace + Environment.NewLine);
            }

        }

        private void EnableEpic(string templatesFolder, Project model, string json, Configuration _config3_0, string id)
        {
            try
            {
                json = string.Format(templatesFolder + @"{0}\{1}", model.SelectedTemplate, json);
                if (System.IO.File.Exists(json))
                {
                    json = model.ReadJsonFile(json);
                    Cards objCards = new Cards(_config3_0);
                    Projects project = new Projects(_config3_0);
                    objCards.EnablingEpic(model.ProjectName, json, model.ProjectName);

                    if (!string.IsNullOrEmpty(objCards.LastFailureMessage))
                    {
                        AddMessage(id.ErrorId(), "Error while Setting Epic Settings: " + objCards.LastFailureMessage + Environment.NewLine);
                    }
                }
            }
            catch (Exception ex)
            {
                AddMessage(id.ErrorId(), "Error while Setting Epic Settings: " + ex.Message + ex.StackTrace + Environment.NewLine);
            }

        }

        private void UpdateWorkItems(string templatesFolder, Project model, string workItemUpdateJSON, Configuration _defaultConfiguration, string id, string currentUser, string ProjectSettingsJSON)
        {
            try
            {
                string jsonWorkItemsUpdate = string.Format(templatesFolder + @"{0}\{1}", model.SelectedTemplate, workItemUpdateJSON);
                string jsonProjectSettings = string.Format(templatesFolder + @"{0}\{1}", model.SelectedTemplate, ProjectSettingsJSON);
                if (System.IO.File.Exists(jsonWorkItemsUpdate))
                {
                    WorkItemNew objWorkItem = new WorkItemNew(_defaultConfiguration);
                    jsonWorkItemsUpdate = model.ReadJsonFile(jsonWorkItemsUpdate);
                    jsonProjectSettings = model.ReadJsonFile(jsonProjectSettings);

                    bool workItemUpdateResult = objWorkItem.UpdateWorkItemUsingByPassRules(jsonWorkItemsUpdate, model.ProjectName, currentUser, jsonProjectSettings);
                    if (!(string.IsNullOrEmpty(objWorkItem.lastFailureMessage)))
                    {
                        AddMessage(id.ErrorId(), "Error while updating work items: " + objWorkItem.lastFailureMessage + Environment.NewLine);
                    }
                }
            }
            catch (Exception ex)
            {
                AddMessage(id.ErrorId(), "Error while updating work items: " + ex.Message + ex.StackTrace + Environment.NewLine);
            }
        }

        private void UpdateIterations(Project model, Configuration _defaultConfiguration, string templatesFolder, string iterationsJSON)
        {
            try
            {
                string jsonIterations = string.Format(templatesFolder + @"{0}\{1}", model.SelectedTemplate, iterationsJSON);
                if (System.IO.File.Exists(jsonIterations))
                {
                    iterationsJSON = model.ReadJsonFile(jsonIterations);
                    ClassificationNodes ObjClassification = new ClassificationNodes(_defaultConfiguration);

                    GetNodesResponse.Nodes nodes = ObjClassification.GetIterations(model.ProjectName);

                    GetNodesResponse.Nodes projectNode = JsonConvert.DeserializeObject<GetNodesResponse.Nodes>(iterationsJSON);

                    if (projectNode.hasChildren)
                    {
                        foreach (var child in projectNode.children)
                        {
                            CreateIterationNode(model, ObjClassification, child, nodes);
                        }
                    }

                    if (projectNode.hasChildren)
                    {
                        foreach (var child in projectNode.children)
                        {
                            path = string.Empty;
                            MoveIterationNode(model, ObjClassification, child);
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                AddMessage(model.id.ErrorId(), "Error while updating iteration: " + ex.Message + ex.StackTrace + Environment.NewLine);
            }
        }

        private void CreateIterationNode(Project model, ClassificationNodes ObjClassification, GetNodesResponse.Child child, GetNodesResponse.Nodes currentIterations)
        {
            string[] defaultSprints = new string[] { "Sprint 1", "Sprint 2", "Sprint 3", "Sprint 4", "Sprint 5", "Sprint 6", };
            if (defaultSprints.Contains(child.name))
            {
                var nd = (currentIterations.hasChildren) ? currentIterations.children.FirstOrDefault(i => i.name == child.name) : null;
                if (nd != null) child.id = nd.id;
            }
            else
            {
                var node = ObjClassification.CreateIteration(model.ProjectName, child.name);
                child.id = node.id;
            }

            if (child.hasChildren && child.children != null)
            {
                foreach (var c in child.children)
                {
                    CreateIterationNode(model, ObjClassification, c, currentIterations);
                }
            }
        }

        string path = string.Empty;
        private void MoveIterationNode(Project model, ClassificationNodes ObjClassification, GetNodesResponse.Child child)
        {
            if (child.hasChildren && child.children != null)
            {
                foreach (var c in child.children)
                {
                    path += child.name + "\\";
                    var nd = ObjClassification.MoveIteration(model.ProjectName, path, c.id);

                    if (c.hasChildren)
                    {
                        MoveIterationNode(model, ObjClassification, c);
                    }
                }
            }
        }

        private void UpdateSprintItems(Project model, Configuration _defaultConfiguration, ProjectSettings settings)
        {
            try
            {
                ClassificationNodes ObjClassification = new ClassificationNodes(_defaultConfiguration);
                bool ClassificationNodesResult = ObjClassification.UpdateIterationDates(model.ProjectName, settings.type);

                if (!(string.IsNullOrEmpty(ObjClassification.LastFailureMessage)))
                {
                    AddMessage(model.id.ErrorId(), "Error while updating sprint items: " + ObjClassification.LastFailureMessage + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                AddMessage(model.id.ErrorId(), "Error while updating sprint items: " + ex.Message + ex.StackTrace + Environment.NewLine);
            }
        }
        public void RenameIterations(Project model, Configuration _defaultConfiguration, Dictionary<string, string> renameIterations)
        {
            try
            {
                if (renameIterations != null && renameIterations.Count > 0)
                {
                    ClassificationNodes ObjClassification = new ClassificationNodes(_defaultConfiguration);
                    bool IsRenamed = ObjClassification.RenameIteration(model.ProjectName, renameIterations);
                }
            }
            catch (Exception ex)
            {
                AddMessage(model.id.ErrorId(), "Error while renaming iterations: " + ex.Message + ex.StackTrace + Environment.NewLine);
            }
        }
        private void ImportSourceCode(string templatesFolder, Project model, string sourceCodeJSON, Configuration _defaultConfiguration, Configuration importSourceConfiguration, string id)
        {

            try
            {
                string[] repositoryDetail = new string[2];
                //string jsonSourceCode = string.Format(templatesFolder + @"{0}\{1}", model.SelectedTemplate, sourceCodeJSON);
                if (System.IO.File.Exists(sourceCodeJSON))
                {
                    Repository objRepository = new Repository(_defaultConfiguration);
                    string repositoryName = Path.GetFileName(sourceCodeJSON).Replace(".json", "");
                    if (model.ProjectName.ToLower() == repositoryName.ToLower())
                    {
                        repositoryDetail = objRepository.GetDefaultRepository(model.ProjectName);
                        isDefaultRepoTodetele = false;
                    }
                    else
                    {
                        repositoryDetail = objRepository.CreateRepositorie(repositoryName, model.Environment.ProjectId);
                    }
                    model.Environment.RepositoryIdList[repositoryDetail[1]] = repositoryDetail[0];

                    string jsonSourceCode = model.ReadJsonFile(sourceCodeJSON);

                    //update endpoint ids
                    foreach (string endpoint in model.Environment.ServiceEndpoints.Keys)
                    {
                        string placeHolder = string.Format("${0}$", endpoint);
                        jsonSourceCode = jsonSourceCode.Replace(placeHolder, model.Environment.ServiceEndpoints[endpoint]);
                    }

                    Repository objRepositorySourceCode = new Repository(importSourceConfiguration);
                    bool copySourceCode = objRepositorySourceCode.getSourceCodeFromGitHub(jsonSourceCode, model.ProjectName, repositoryDetail[0]);

                    if (!(string.IsNullOrEmpty(objRepository.lastFailureMessage)))
                    {
                        AddMessage(id.ErrorId(), "Error while importing source code: " + objRepository.lastFailureMessage + Environment.NewLine);
                    }

                }

            }
            catch (Exception ex)
            {

                AddMessage(id.ErrorId(), "Error while importing source code: " + ex.Message + ex.StackTrace + Environment.NewLine);

            }
        }

        private void CreatePullRequest(string templatesFolder, Project model, string pullRequestJsonPath, Configuration _configuration3_0)
        {
            try
            {
                if (System.IO.File.Exists(pullRequestJsonPath))
                {
                    string commentFile = Path.GetFileName(pullRequestJsonPath);
                    string repositoryId = string.Empty;
                    if (model.SelectedTemplate == "MyHealthClinic") { repositoryId = model.Environment.RepositoryIdList["MyHealthClinic"]; }
                    else { repositoryId = model.Environment.RepositoryIdList[model.SelectedTemplate]; }

                    pullRequestJsonPath = model.ReadJsonFile(pullRequestJsonPath);
                    pullRequestJsonPath = pullRequestJsonPath.Replace("$reviewer$", model.Environment.UserUniqueId);
                    Repository objRepository = new Repository(_configuration3_0);
                    string[] pullReqResponse = new string[2];

                    pullReqResponse = objRepository.CreatePullRequest(pullRequestJsonPath, repositoryId);

                    if (pullReqResponse != null)
                    {
                        model.Environment.pullRequests.Add(pullReqResponse[1], pullReqResponse[0]);
                        commentFile = string.Format(templatesFolder + @"{0}\PullRequests\Comments\{1}", model.SelectedTemplate, commentFile);
                        if (System.IO.File.Exists(commentFile))
                        {
                            commentFile = model.ReadJsonFile(commentFile);
                            PullRequestComments.Comments commentsList = JsonConvert.DeserializeObject<PullRequestComments.Comments>(commentFile);
                            if (commentsList.count > 0)
                            {
                                foreach (PullRequestComments.Value thread in commentsList.value)
                                {
                                    string threadID = objRepository.CreateCommentThread(repositoryId, pullReqResponse[0], JsonConvert.SerializeObject(thread));
                                    if (!string.IsNullOrEmpty(threadID))
                                    {
                                        if (thread.Replies != null && thread.Replies.Count > 0)
                                        {
                                            foreach (var reply in thread.Replies)
                                            {
                                                objRepository.AddCommentToThread(repositoryId, pullReqResponse[0], threadID, JsonConvert.SerializeObject(reply));
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddMessage(model.id.ErrorId(), "Error while creating pull Requests: " + ex.Message + ex.StackTrace + Environment.NewLine);
            }
        }

        private void CreateServiceEndPoint(Project model, List<string> jsonPaths, Configuration _defaultConfiguration)
        {
            try
            {
                string serviceEndPointId = string.Empty;
                foreach (string jsonPath in jsonPaths)
                {
                    string jsonCreateService = jsonPath;
                    if (System.IO.File.Exists(jsonCreateService))
                    {

                        ServiceEndPoint objService = new ServiceEndPoint(_defaultConfiguration);
                        jsonCreateService = model.ReadJsonFile(jsonCreateService);
                        jsonCreateService = jsonCreateService.Replace("$ProjectName$", model.ProjectName);
                        if (model.SelectedTemplate.ToLower() == "sonarqube")
                        {
                            if (!string.IsNullOrEmpty(model.SonarQubeDNS))
                            {
                                jsonCreateService = jsonCreateService.Replace("$URL$", model.SonarQubeDNS);
                            }
                        }
                        else if (model.SelectedTemplate.ToLower() == "octopus")
                        {
                            var URL = model.Parameters["OctopusURL"];
                            var APIKey = model.Parameters["APIkey"];
                            if (!string.IsNullOrEmpty(URL.ToString()) && !string.IsNullOrEmpty(APIKey.ToString()))
                            {
                                jsonCreateService = jsonCreateService.Replace("$URL$", URL).Replace("$Apikey$", APIKey);

                            }
                        }
                        var endpoint = objService.CreateServiceEndPoint(jsonCreateService, model.ProjectName);

                        if (!(string.IsNullOrEmpty(objService.lastFailureMessage)))
                        {
                            AddMessage(model.id.ErrorId(), "Error while creating service endpoint: " + objService.lastFailureMessage + Environment.NewLine);
                        }
                        else
                        {
                            model.Environment.ServiceEndpoints[endpoint.name] = endpoint.id;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddMessage(model.id.ErrorId(), "Error while creating service endpoint: " + ex.Message + ex.StackTrace + Environment.NewLine);
            }
        }

        private void CreateTestManagement(List<WIMapData> WImapping, Project model, string testPlanJson, string templateFolder, Configuration _defaultConfiguration)
        {
            try
            {
                if (System.IO.File.Exists(testPlanJson))
                {
                    List<WIMapData> TestCaseMap = new List<WIMapData>();
                    TestCaseMap = WImapping.Where(x => x.WIType == "Test Case").ToList();

                    string fileName = Path.GetFileName(testPlanJson);
                    testPlanJson = model.ReadJsonFile(testPlanJson);

                    testPlanJson = testPlanJson.Replace("$project$", model.ProjectName);
                    TestManagement objTest = new TestManagement(_defaultConfiguration);
                    string[] testPlanResponse = new string[2];
                    testPlanResponse = objTest.CreateTestPlan(testPlanJson, model.ProjectName);

                    if (testPlanResponse != null)
                    {
                        string testSuiteJson = string.Format(templateFolder + @"{0}\TestPlans\TestSuites\{1}", model.SelectedTemplate, fileName);
                        if (System.IO.File.Exists(testSuiteJson))
                        {
                            testSuiteJson = model.ReadJsonFile(testSuiteJson);
                            testSuiteJson = testSuiteJson.Replace("$planID$", testPlanResponse[0]).Replace("$planName$", testPlanResponse[1]);
                            foreach (var WI in WImapping)
                            {
                                string placeHolder = string.Format("${0}$", WI.oldID);
                                testSuiteJson = testSuiteJson.Replace(placeHolder, WI.newID);
                            }
                            TestSuite.TestSuites lstTestSuites = JsonConvert.DeserializeObject<TestSuite.TestSuites>(testSuiteJson);
                            if (lstTestSuites.count > 0)
                            {
                                foreach (var TS in lstTestSuites.value)
                                {
                                    string[] testSuiteResponse = new string[2];
                                    string testSuiteJSON = JsonConvert.SerializeObject(TS);
                                    testSuiteResponse = objTest.CreatTestSuite(testSuiteJSON, testPlanResponse[0], model.ProjectName);
                                    if (testSuiteResponse != null)
                                    {
                                        string testCasesToAdd = string.Empty;
                                        foreach (string id in TS.TestCases)
                                        {
                                            foreach (var WImap in TestCaseMap)
                                            {
                                                if (WImap.oldID == id)
                                                {
                                                    testCasesToAdd = testCasesToAdd + WImap.newID + ",";
                                                }
                                            }
                                        }
                                        testCasesToAdd = testCasesToAdd.TrimEnd(',');
                                        bool IsTestCasesAddded = objTest.AddTestCasesToSuite(testCasesToAdd, testPlanResponse[0], testSuiteResponse[0], model.ProjectName);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AddMessage(model.id.ErrorId(), "Error while creating test plan and test suites: " + ex.Message + ex.StackTrace + Environment.NewLine);
            }
        }

        private bool CreateBuildDefinition(string templatesFolder, Project model, Configuration _defaultConfiguration, string id)
        {
            bool flag = false;
            try
            {
                foreach (BuildDef buildDef in model.BuildDefinitions)
                {
                    if (System.IO.File.Exists(buildDef.FilePath))
                    {
                        BuildDefinition objBuild = new BuildDefinition(_defaultConfiguration);
                        string jsonBuildDefinition = model.ReadJsonFile(buildDef.FilePath);

                        //update repositoryId 
                        foreach (string repository in model.Environment.RepositoryIdList.Keys)
                        {
                            string placeHolder = string.Format("${0}$", repository);
                            jsonBuildDefinition = jsonBuildDefinition.Replace(placeHolder, model.Environment.RepositoryIdList[repository]);
                        }

                        //update endpoint ids
                        foreach (string endpoint in model.Environment.ServiceEndpoints.Keys)
                        {
                            string placeHolder = string.Format("${0}$", endpoint);
                            jsonBuildDefinition = jsonBuildDefinition.Replace(placeHolder, model.Environment.ServiceEndpoints[endpoint]);
                        }

                        string[] buildResult = objBuild.CreateBuildDefinition(jsonBuildDefinition, model.ProjectName);

                        if (!(string.IsNullOrEmpty(objBuild.lastFailureMessage)))
                        {
                            AddMessage(id.ErrorId(), "Error while creating build definition: " + objBuild.lastFailureMessage + Environment.NewLine);
                        }
                        buildDef.Id = buildResult[0];
                        buildDef.Name = buildResult[1];
                    }
                    flag = true;
                }
                return flag;
            }

            catch (Exception ex)
            {
                AddMessage(id.ErrorId(), "Error while creating build definition: " + ex.Message + ex.StackTrace + Environment.NewLine);
            }
            return flag;
        }
        private void QueueABuild(Project model, string json, Configuration _configuration)
        {
            try
            {
                string jsonQueueABuild = json;
                if (System.IO.File.Exists(jsonQueueABuild))
                {
                    string buildId = model.BuildDefinitions.FirstOrDefault().Id;

                    jsonQueueABuild = model.ReadJsonFile(jsonQueueABuild);
                    jsonQueueABuild = jsonQueueABuild.Replace("$buildId$", buildId.ToString());
                    BuildDefinition objBuild = new BuildDefinition(_configuration);
                    int queueId = objBuild.QueueBuild(jsonQueueABuild, model.ProjectName);

                    if (!string.IsNullOrEmpty(objBuild.lastFailureMessage))
                    {
                        AddMessage(model.id.ErrorId(), "Error while Queueing build: " + objBuild.lastFailureMessage + Environment.NewLine);
                    }
                }
            }
            catch (Exception ex)
            {
                AddMessage(model.id.ErrorId(), "Error while Queueing Build: " + ex.Message + ex.StackTrace + Environment.NewLine);
            }
        }

        private bool CreateReleaseDefinition(string templatesFolder, Project model, Configuration _releaseConfiguration, Configuration _config3_0, string id, TeamMemberResponse.TeamMembers teamMembers)
        {
            bool flag = false;
            try
            {
                var teamMember = teamMembers.value.FirstOrDefault();
                foreach (ReleaseDef relDef in model.ReleaseDefinitions)
                {
                    if (System.IO.File.Exists(relDef.FilePath))
                    {
                        ReleaseDefinition objRelease = new ReleaseDefinition(_releaseConfiguration);
                        Thread.Sleep(5000);
                        string jsonReleaseDefinition = model.ReadJsonFile(relDef.FilePath);
                        jsonReleaseDefinition = jsonReleaseDefinition.Replace("$ProjectName$", model.Environment.ProjectName)
                                             .Replace("$ProjectId$", model.Environment.ProjectId)
                                             .Replace("$OwnerUniqueName$", teamMember.uniqueName)
                                             .Replace("$OwnerId$", teamMember.id)
                                  .Replace("$OwnerDisplayName$", teamMember.displayName);

                        //Adding randon UUID to website name
                        string UUID = Guid.NewGuid().ToString();
                        UUID = UUID.Substring(0, 8);
                        jsonReleaseDefinition = jsonReleaseDefinition.Replace("$UUID$", UUID);

                        foreach (BuildDef ObjBuildDef in model.BuildDefinitions)
                        {
                            //update build ids
                            string placeHolder = string.Format("${0}-id$", ObjBuildDef.Name);
                            jsonReleaseDefinition = jsonReleaseDefinition.Replace(placeHolder, ObjBuildDef.Id);

                            //update agent queue ids
                            foreach (string queue in model.Environment.AgentQueues.Keys)
                            {
                                placeHolder = string.Format("${0}$", queue);
                                jsonReleaseDefinition = jsonReleaseDefinition.Replace(placeHolder, model.Environment.AgentQueues[queue].ToString());
                            }

                            //update endpoint ids
                            foreach (string endpoint in model.Environment.ServiceEndpoints.Keys)
                            {
                                placeHolder = string.Format("${0}$", endpoint);
                                jsonReleaseDefinition = jsonReleaseDefinition.Replace(placeHolder, model.Environment.ServiceEndpoints[endpoint]);
                            }
                        }
                        string[] releaseDef = objRelease.CreateReleaseDefinition(jsonReleaseDefinition, model.ProjectName);
                        relDef.Id = releaseDef[0];
                        relDef.Name = releaseDef[1];

                        if (!(string.IsNullOrEmpty(objRelease.lastFailureMessage)))
                        {
                            AddMessage(id.ErrorId(), "Error while creating release definition: " + objRelease.lastFailureMessage + Environment.NewLine);
                        }
                    }
                    flag = true;
                }
                return flag;

            }
            catch (Exception ex)
            {
                AddMessage(id.ErrorId(), "Error while creating release definition: " + ex.Message + ex.StackTrace + Environment.NewLine);
            }
            flag = false;
            return flag;
        }

        public void CreateRelease(string templatesFolder, Project model, string json, Configuration _configuration, string id, int releaseDefinitionId)
        {
            try
            {
                string jsonCreateRelease = string.Format(templatesFolder + @"{0}\{1}", model.SelectedTemplate, json);
                if (System.IO.File.Exists(jsonCreateRelease))
                {
                    jsonCreateRelease = model.ReadJsonFile(jsonCreateRelease);
                    jsonCreateRelease = jsonCreateRelease.Replace("$id", jsonCreateRelease.ToString());
                    ReleaseDefinition objRelease = new ReleaseDefinition(_configuration);
                    objRelease.CreateRelease(jsonCreateRelease, model.ProjectName);

                    if (!string.IsNullOrEmpty(objRelease.lastFailureMessage))
                    {
                        AddMessage(id.ErrorId(), "Error while creating release: " + objRelease.lastFailureMessage + Environment.NewLine);
                    }
                }
            }
            catch (Exception ex)
            {
                AddMessage(id.ErrorId(), "Error while creating release: " + ex.Message + ex.StackTrace + Environment.NewLine);
            }
        }

        public void CreateQueryAndWidgets(string templatesFolder, Project model, List<string> lstQueries, Configuration _defaultConfiguration, Configuration _configuration2, Configuration _configuration3, Configuration releaseConfig)
        {
            try
            {
                Querys objWidget = new Querys(_configuration3);
                Querys objQuery = new Querys(_defaultConfiguration);
                List<QueryResponse> queryResults = new List<QueryResponse>();

                //GetDashBoardDetails
                string dashBoardId = objWidget.GetDashBoardId(model.ProjectName);
                string eTagString = objWidget.GetDashboardeTag(dashBoardId, model.ProjectName);
                int eTag = 0;
                if (!string.IsNullOrEmpty(eTagString))
                {
                    eTag = Convert.ToInt32(eTagString);
                }

                if (!string.IsNullOrEmpty(objQuery.lastFailureMessage))
                {
                    AddMessage(model.id.ErrorId(), "Error while getting dashboardId: " + objWidget.lastFailureMessage + Environment.NewLine);
                }

                foreach (string query in lstQueries)
                {
                    //create query
                    string json = model.ReadJsonFile(query);
                    QueryResponse response = objQuery.CreateQuery(model.ProjectName, json);
                    queryResults.Add(response);

                    if (!string.IsNullOrEmpty(objQuery.lastFailureMessage))
                    {
                        AddMessage(model.id.ErrorId(), "Error while creating query: " + objQuery.lastFailureMessage + Environment.NewLine);
                        return;
                    }

                    string chartName = Path.GetFileName(query);
                    string jsonChart = string.Format(templatesFolder + @"{0}\Dashboard\Charts\{1}", model.SelectedTemplate, chartName);

                    if (System.IO.File.Exists(jsonChart))
                    {
                        //create chart
                        json = model.ReadJsonFile(jsonChart);
                        json = json.Replace("$name$", response.name).Replace("$QueryId$", response.id).Replace("$QueryName$", response.name).Replace("$eTag$", eTag.ToString());
                        bool chartResponse = objWidget.CreateWidget(model.ProjectName, dashBoardId, json);
                        if (chartResponse) { eTag = eTag + 1; }

                    }
                    string widgetName = Path.GetFileName(query);
                    string jsonWidget = string.Format(templatesFolder + @"{0}\Dashboard\Widgets\{1}", model.SelectedTemplate, widgetName);

                    if (System.IO.File.Exists(jsonWidget))
                    {
                        //create widget
                        json = model.ReadJsonFile(jsonWidget);
                        json = json.Replace("$name$", response.name).Replace("$QueryId$", response.id).Replace("$QueryName$", response.name).Replace("$eTag$", eTag.ToString());
                        bool widgetResponse = objWidget.CreateWidget(model.ProjectName, dashBoardId, json);
                        if (widgetResponse) { eTag = eTag + 1; }
                    }
                    if (!string.IsNullOrEmpty(objQuery.lastFailureMessage))
                    {
                        AddMessage(model.id.ErrorId(), "Error while creating widget and charts: " + objWidget.lastFailureMessage + Environment.NewLine);
                    }
                }
                //Create DashBoards
                string dashBoardTemplate = string.Format(templatesFolder + @"{0}\Dashboard\Dashboard.json", model.SelectedTemplate);
                if (System.IO.File.Exists(dashBoardTemplate))
                {
                    dynamic dashBoard = new System.Dynamic.ExpandoObject();
                    dashBoard.name = "Working";
                    dashBoard.position = 4;

                    string jsonDashBoard = Newtonsoft.Json.JsonConvert.SerializeObject(dashBoard);
                    string dashBoardIdToDelete = objWidget.CreateNewDashBoard(model.ProjectName, jsonDashBoard);

                    bool isDashboardDeleted = objWidget.DeleteDefaultDashboard(model.ProjectName, dashBoardId);

                    if (model.SelectedTemplate.ToLower() == "bikesharing360")
                    {
                        if (isDashboardDeleted)
                        {
                            dashBoardTemplate = model.ReadJsonFile(dashBoardTemplate);

                            string xamarin_DroidBuild = model.BuildDefinitions.Where(x => x.Name == "Xamarin.Droid").FirstOrDefault().Id;
                            string xamarin_IOSBuild = model.BuildDefinitions.Where(x => x.Name == "Xamarin.iOS").FirstOrDefault().Id;
                            string RidesApiBuild = model.BuildDefinitions.Where(x => x.Name == "RidesApi").FirstOrDefault().Id;

                            ReleaseDefinition objrelease = new ReleaseDefinition(releaseConfig);
                            int[] AndroidEnvironmentIds = objrelease.GetEnvironmentIdsByName(model.ProjectName, "Xamarin.Android", "Test in HockeyApp", "Publish to store");
                            string AndroidbuildDefId = model.BuildDefinitions.Where(x => x.Name == "Xamarin.Droid").FirstOrDefault().Id;
                            string AndroidreleaseDefId = model.ReleaseDefinitions.Where(x => x.Name == "Xamarin.Android").FirstOrDefault().Id;

                            int[] IOSEnvironmentIds = objrelease.GetEnvironmentIdsByName(model.ProjectName, "Xamarin.iOS", "Test in HockeyApp", "Publish to store");
                            string IOSbuildDefId = model.BuildDefinitions.Where(x => x.Name == "Xamarin.iOS").FirstOrDefault().Id;
                            string IOSreleaseDefId = model.ReleaseDefinitions.Where(x => x.Name == "Xamarin.iOS").FirstOrDefault().Id;

                            string RidesApireleaseDefId = model.ReleaseDefinitions.Where(x => x.Name == "RidesApi").FirstOrDefault().Id;
                            QueryResponse OpenUserStories = objQuery.GetQueryByPathAndName(model.ProjectName, "Open User Stories", "Shared%20Queries/Current%20Iteration");

                            dashBoardTemplate = dashBoardTemplate.Replace("$RidesAPIReleaseId$", RidesApireleaseDefId).Replace("$RidesAPIBuildId$", RidesApiBuild)
                                                                                                                                                                                  .Replace("$repositoryId$", model.Environment.RepositoryIdList.Where(x => x.Key.ToLower() == "bikesharing360").FirstOrDefault().Value)
                                                                                                                                                                                  .Replace("$IOSBuildId$", IOSbuildDefId).Replace("$IOSReleaseId$", IOSreleaseDefId).Replace("$IOSEnv1$", IOSEnvironmentIds[0].ToString()).Replace("$IOSEnv2$", IOSEnvironmentIds[1].ToString())
                                                                                                                                                                                  .Replace("$Xamarin.iOS$", xamarin_IOSBuild)
                                                                                                                                                                                  .Replace("$Xamarin.Droid$", xamarin_DroidBuild)
                                                                                                                                                                                  .Replace("$AndroidBuildId$", AndroidbuildDefId).Replace("$AndroidreleaseDefId$", AndroidreleaseDefId).Replace("$AndroidEnv1$", AndroidEnvironmentIds[0].ToString()).Replace("$AndroidEnv2$", AndroidEnvironmentIds[1].ToString())
                                                                                                                                                                                  .Replace("$OpenUserStoriesId$", OpenUserStories.id)
                                                                                                                                                                                  .Replace("$projectId$", model.Environment.ProjectId);

                            string isDashBoardCreated = objWidget.CreateNewDashBoard(model.ProjectName, dashBoardTemplate);
                            objWidget.DeleteDefaultDashboard(model.ProjectName, dashBoardIdToDelete);
                        }
                    }
                    if (model.SelectedTemplate == "MyHealthClinic" || model.SelectedTemplate == "PartsUnlimited" || model.SelectedTemplate == "PartsUnlimited-agile")
                    {
                        if (isDashboardDeleted)
                        {
                            dashBoardTemplate = model.ReadJsonFile(dashBoardTemplate);

                            QueryResponse FeedBack = objQuery.GetQueryByPathAndName(model.ProjectName, "Feedback", "Shared%20Queries");
                            QueryResponse UnfinishedWork = objQuery.GetQueryByPathAndName(model.ProjectName, "Unfinished Work", "Shared%20Queries/Current%20Sprint");


                            dashBoardTemplate = dashBoardTemplate.Replace("$Feedback$", FeedBack.id).
                                         Replace("$AllItems$", queryResults.Where(x => x.name == "All Items").FirstOrDefault().id).
                                         Replace("$UserStories$", queryResults.Where(x => x.name == "User Stories").FirstOrDefault().id).
                                         Replace("$TestCase$", queryResults.Where(x => x.name == "Test Case-Readiness").FirstOrDefault().id).
                                         Replace("$teamID$", "").
                                         Replace("$teamName$", model.ProjectName + " Team").
                                         Replace("$projectID$", model.Environment.ProjectId).
                                         Replace("$Unfinished Work$", UnfinishedWork.id).
                                         Replace("$projectId$", model.Environment.ProjectId).
                                         Replace("$projectName$", model.ProjectName);


                            if (model.SelectedTemplate == "MyHealthClinic")
                            {
                                dashBoardTemplate = dashBoardTemplate.Replace("$ReleaseDefId$", model.ReleaseDefinitions.Where(x => x.Name == "MyHealthClinicE2E").FirstOrDefault().Id).
                                             Replace("$ActiveBugs$", queryResults.Where(x => x.name == "Active Bugs").FirstOrDefault().id).
                                             Replace("$MyHealthClinicE2E$", model.BuildDefinitions.Where(x => x.Name == "MyHealthClinicE2E").FirstOrDefault().Id).
                                                 Replace("$RepositoryId$", model.Environment.RepositoryIdList.Where(x => x.Key.ToLower() == "myhealthclinic").FirstOrDefault().Value);
                            }
                            if (model.SelectedTemplate == "PartsUnlimited" || model.SelectedTemplate == "PartsUnlimited-agile")
                            {
                                QueryResponse WorkInProgress = objQuery.GetQueryByPathAndName(model.ProjectName, "Work in Progress", "Shared%20Queries/Current%20Sprint");

                                dashBoardTemplate = dashBoardTemplate.Replace("$ReleaseDefId$", model.ReleaseDefinitions.Where(x => x.Name == "PartsUnlimitedE2E").FirstOrDefault().Id).
                                          Replace("$ActiveBugs$", queryResults.Where(x => x.name == "Critical Bugs").FirstOrDefault().id).
                                          Replace("$PartsUnlimitedE2E$", model.BuildDefinitions.Where(x => x.Name == "PartsUnlimitedE2E").FirstOrDefault().Id)
                                          .Replace("$WorkinProgress$", WorkInProgress.id)
                                .Replace("$RepositoryId$", model.Environment.RepositoryIdList.Where(x => x.Key.ToLower() == "partsunlimited").FirstOrDefault().Value);

                            }
                            string isDashBoardCreated = objWidget.CreateNewDashBoard(model.ProjectName, dashBoardTemplate);
                            objWidget.DeleteDefaultDashboard(model.ProjectName, dashBoardIdToDelete);

                        }
                    }
                    if (model.SelectedTemplate.ToLower() == "bikesharing 360")
                    {
                        if (isDashboardDeleted)
                        {
                            dashBoardTemplate = model.ReadJsonFile(dashBoardTemplate);
                            QueryResponse UnfinishedWork = objQuery.GetQueryByPathAndName(model.ProjectName, "Unfinished Work", "Shared%20Queries/Current%20Sprint");
                            string AllItems = queryResults.Where(x => x.name == "All Items").FirstOrDefault().id;
                            string repositoryId = model.Environment.RepositoryIdList.Where(x => x.Key.ToLower() == "bikesharing360").FirstOrDefault().Key;
                            string BikeSharing360_PublicWeb = model.BuildDefinitions.Where(x => x.Name == "BikeSharing360-PublicWeb").FirstOrDefault().Id;

                            dashBoardTemplate = dashBoardTemplate.Replace("$BikeSharing360-PublicWeb$", BikeSharing360_PublicWeb)
                                         .Replace("$All Items$", AllItems)
                                         .Replace("$repositoryId$", repositoryId)
                                         .Replace("$Unfinished Work$", UnfinishedWork.id)
                                         .Replace("$projectId$", model.Environment.ProjectId);

                            string isDashBoardCreated = objWidget.CreateNewDashBoard(model.ProjectName, dashBoardTemplate);
                            objWidget.DeleteDefaultDashboard(model.ProjectName, dashBoardIdToDelete);
                        }
                    }
                    if (model.SelectedTemplate == "MyShuttleDocker")
                    {
                        if (isDashboardDeleted)
                        {
                            dashBoardTemplate = model.ReadJsonFile(dashBoardTemplate);
                            var BuildDefId = model.BuildDefinitions.FirstOrDefault();
                            dashBoardTemplate = dashBoardTemplate.Replace("$BuildDefId$", BuildDefId.Id)
                                  .Replace("$projectId$", model.Environment.ProjectId)
                                  .Replace("$PBI$", queryResults.Where(x => x.name == "Product Backlog Items").FirstOrDefault().id)
                                  .Replace("$Bugs$", queryResults.Where(x => x.name == "Bugs").FirstOrDefault().id)
                                  .Replace("$AllWorkItems$", queryResults.Where(x => x.name == "All Work Items").FirstOrDefault().id)
                                  .Replace("$Test Plan$", queryResults.Where(x => x.name == "Test Plans").FirstOrDefault().id)
                                  .Replace("$Test Cases$", queryResults.Where(x => x.name == "Test Cases").FirstOrDefault().id)
                                  .Replace("$Feature$", queryResults.Where(x => x.name == "Feature").FirstOrDefault().id)
                                  .Replace("$Tasks$", queryResults.Where(x => x.name == "Tasks").FirstOrDefault().id)
                                         .Replace("$RepoMyShuttleDocker$", model.Environment.RepositoryIdList.Where(x => x.Key == "MyShuttleDocker").FirstOrDefault().Value);


                            string isDashBoardCreated = objWidget.CreateNewDashBoard(model.ProjectName, dashBoardTemplate);
                            objWidget.DeleteDefaultDashboard(model.ProjectName, dashBoardIdToDelete);
                        }
                    }
                    if (model.SelectedTemplate == "MyShuttle")
                    {
                        if (isDashboardDeleted)
                        {
                            dashBoardTemplate = model.ReadJsonFile(dashBoardTemplate);
                            dashBoardTemplate = dashBoardTemplate
                            .Replace("$PBI$", queryResults.Where(x => x.name == "Product Backlog Items").FirstOrDefault().id)
                            .Replace("$Bugs$", queryResults.Where(x => x.name == "Bugs").FirstOrDefault().id)
                            .Replace("$AllWorkItems$", queryResults.Where(x => x.name == "All Work Items").FirstOrDefault().id)
                            .Replace("$TestPlan$", queryResults.Where(x => x.name == "Test Plans").FirstOrDefault().id)
                            .Replace("$Test Cases$", queryResults.Where(x => x.name == "Test Cases").FirstOrDefault().id)
                            .Replace("$Features$", queryResults.Where(x => x.name == "Feature").FirstOrDefault().id)
                            .Replace("$Tasks$", queryResults.Where(x => x.name == "Tasks").FirstOrDefault().id)
                            .Replace("$TestSuite$", queryResults.Where(x => x.name == "Test Suites").FirstOrDefault().id);


                            string isDashBoardCreated = objWidget.CreateNewDashBoard(model.ProjectName, dashBoardTemplate);
                            objWidget.DeleteDefaultDashboard(model.ProjectName, dashBoardIdToDelete);
                        }
                    }
                    if (model.SelectedTemplate.ToLower() == "myshuttle2")
                    {
                        if (isDashboardDeleted)
                        {
                            dashBoardTemplate = model.ReadJsonFile(dashBoardTemplate);

                            dashBoardTemplate = dashBoardTemplate.Replace("$TestCases$", queryResults.Where(x => x.name == "Test Cases").FirstOrDefault().id)
                                         .Replace("$AllWorkItems$", queryResults.Where(x => x.name == "All Work Items").FirstOrDefault().id)
                                         .Replace("$PBI$", queryResults.Where(x => x.name == "Product Backlog Items").FirstOrDefault().id)
                                         .Replace("$RepoMyShuttleCalc$", model.Environment.RepositoryIdList["MyShuttleCalc"])
                                         .Replace("$TestPlan$", queryResults.Where(x => x.name == "Test Plans").FirstOrDefault().id)
                                         .Replace("$Tasks$", queryResults.Where(x => x.name == "Tasks").FirstOrDefault().id)
                                         .Replace("$Bugs$", queryResults.Where(x => x.name == "Bugs").FirstOrDefault().id)
                                         .Replace("$Features$", queryResults.Where(x => x.name == "Feature").FirstOrDefault().id)
                                         .Replace("$RepoMyShuttle2$", model.Environment.RepositoryIdList.Where(x => x.Key.ToLower() == "myshuttle2").FirstOrDefault().Value);


                            string isDashBoardCreated = objWidget.CreateNewDashBoard(model.ProjectName, dashBoardTemplate);
                            objWidget.DeleteDefaultDashboard(model.ProjectName, dashBoardIdToDelete);
                        }
                    }
                    if (model.SelectedTemplate.ToLower() == "docker" || model.SelectedTemplate.ToLower() == "php" || model.SelectedTemplate.ToLower() == "sonarqube" || model.SelectedTemplate.ToLower() == "github" || model.SelectedTemplate.ToLower() == "whitesource bolt" || model.SelectedTemplate == "DeploymentGroups" || model.SelectedTemplate == "Octopus")
                    {
                        if (isDashboardDeleted)
                        {
                            dashBoardTemplate = model.ReadJsonFile(dashBoardTemplate);
                            dashBoardTemplate = dashBoardTemplate.Replace("$Task$", queryResults.Where(x => x.name == "Tasks").FirstOrDefault().id)
                                         .Replace("$AllWorkItems$", queryResults.Where(x => x.name == "All Work Items").FirstOrDefault().id)
                                         .Replace("$Feature$", queryResults.Where(x => x.name == "Feature").FirstOrDefault().id)
                                         .Replace("$Projectid$", model.Environment.ProjectId)
                                         .Replace("$Epic$", queryResults.Where(x => x.name == "Epics").FirstOrDefault().id);

                            if (model.SelectedTemplate.ToLower() == "docker")
                            {
                                dashBoardTemplate = dashBoardTemplate.Replace("$BuildDocker$", model.BuildDefinitions.Where(x => x.Name == "Docker").FirstOrDefault().Id)
                                .Replace("$ReleaseDocker$", model.ReleaseDefinitions.Where(x => x.Name == "Docker").FirstOrDefault().Id)
                                  .Replace("$PBI$", queryResults.Where(x => x.name == "Product Backlog Items").FirstOrDefault().id);
                            }
                            else if (model.SelectedTemplate.ToLower() == "php")
                            {
                                dashBoardTemplate = dashBoardTemplate.Replace("$buildPHP$", model.BuildDefinitions.Where(x => x.Name == "PHP").FirstOrDefault().Id)
                        .Replace("$releasePHP$", model.ReleaseDefinitions.Where(x => x.Name == "PHP").FirstOrDefault().Id)
                                 .Replace("$PBI$", queryResults.Where(x => x.name == "Product Backlog Items").FirstOrDefault().id);
                            }
                            else if (model.SelectedTemplate.ToLower() == "sonarqube")
                            {
                                dashBoardTemplate = dashBoardTemplate.Replace("$BuildSonarQube$", model.BuildDefinitions.Where(x => x.Name == "SonarQube").FirstOrDefault().Id)
                                .Replace("$PBI$", queryResults.Where(x => x.name == "Product Backlog Items").FirstOrDefault().id);

                            }
                            else if (model.SelectedTemplate.ToLower() == "github")
                            {
                                dashBoardTemplate = dashBoardTemplate.Replace("$PBI$", queryResults.Where(x => x.name == "Product Backlog Items").FirstOrDefault().id)
                                             .Replace("$buildGitHub$", model.BuildDefinitions.Where(x => x.Name == "GitHub").FirstOrDefault().Id)
                                             .Replace("$Hosted$", model.Environment.AgentQueues["Hosted"].ToString())
                                             .Replace("$releaseGitHub$", model.ReleaseDefinitions.Where(x => x.Name == "GitHub").FirstOrDefault().Id);

                            }
                            else if (model.SelectedTemplate.ToLower() == "whitesource bolt")
                            {
                                dashBoardTemplate = dashBoardTemplate.Replace("$PBI$", queryResults.Where(x => x.name == "Product Backlog Items").FirstOrDefault().id)
                                          .Replace("$buildWhiteSource$", model.BuildDefinitions.Where(x => x.Name == "WhiteSourceBolt").FirstOrDefault().Id);
                            }

                            else if (model.SelectedTemplate == "DeploymentGroups")
                            {
                                QueryResponse WorkInProgress = objQuery.GetQueryByPathAndName(model.ProjectName, "Work in Progress", "Shared%20Queries/Current%20Sprint");
                                dashBoardTemplate = dashBoardTemplate.Replace("$WorkinProgress$", WorkInProgress.id);
                            }

                            else if (model.SelectedTemplate == "Octopus")
                            {
                                var BuildDefId = model.BuildDefinitions.FirstOrDefault();
                                if (BuildDefId != null)
                                {
                                    dashBoardTemplate = dashBoardTemplate.Replace("$BuildDefId$", BuildDefId.Id)
                                            .Replace("$PBI$", queryResults.Where(x => x.name == "Product Backlog Items").FirstOrDefault().id);
                                }
                            }


                            string isDashBoardCreated = objWidget.CreateNewDashBoard(model.ProjectName, dashBoardTemplate);
                            objWidget.DeleteDefaultDashboard(model.ProjectName, dashBoardIdToDelete);
                        }
                    }
                }
                //Update WorkInProgress ,UnfinishedWork Queries,Test Cases,Blocked Tasks queries.
                string UpdateQueryString = string.Empty;

                UpdateQueryString = "SELECT [System.Id],[System.Title],[Microsoft.VSTS.Common.BacklogPriority],[System.AssignedTo],[System.State],[Microsoft.VSTS.Scheduling.RemainingWork],[Microsoft.VSTS.CMMI.Blocked],[System.WorkItemType] FROM workitemLinks WHERE ([Source].[System.TeamProject] = @project AND [Source].[System.IterationPath] UNDER @currentIteration AND ([Source].[System.WorkItemType] IN GROUP 'Microsoft.RequirementCategory' OR [Source].[System.WorkItemType] IN GROUP 'Microsoft.TaskCategory' ) AND [Source].[System.State] <> 'Removed' AND [Source].[System.State] <> 'Done') AND ([System.Links.LinkType] = 'System.LinkTypes.Hierarchy-Forward')  AND ([Target].[System.WorkItemType] IN GROUP 'Microsoft.TaskCategory' AND [Target].[System.State] <> 'Done' AND [Target].[System.State] <> 'Removed') ORDER BY [Microsoft.VSTS.Common.BacklogPriority],[Microsoft.VSTS.Scheduling.Effort], [Microsoft.VSTS.Scheduling.RemainingWork],[System.Id] MODE (Recursive)";
                dynamic queryObject = new System.Dynamic.ExpandoObject();
                queryObject.wiql = UpdateQueryString;
                bool isUpdated = objQuery.UpdateQuery("Shared%20Queries/Current%20Sprint/Unfinished Work", model.Environment.ProjectName, Newtonsoft.Json.JsonConvert.SerializeObject(queryObject));

                UpdateQueryString = "SELECT [System.Id],[System.WorkItemType],[System.Title],[System.AssignedTo],[System.State],[Microsoft.VSTS.Scheduling.RemainingWork] FROM workitems WHERE [System.TeamProject] = @project AND [System.IterationPath] UNDER @currentIteration AND [System.WorkItemType] IN GROUP 'Microsoft.TaskCategory' AND [System.State] = 'In Progress' ORDER BY [System.AssignedTo],[Microsoft.VSTS.Common.BacklogPriority],[System.Id]";
                queryObject.wiql = UpdateQueryString;
                isUpdated = objQuery.UpdateQuery("Shared%20Queries/Current%20Sprint/Work in Progress", model.Environment.ProjectName, Newtonsoft.Json.JsonConvert.SerializeObject(queryObject));

                UpdateQueryString = "SELECT [System.Id],[System.WorkItemType],[System.Title],[System.State],[Microsoft.VSTS.Common.Priority] FROM workitems WHERE [System.TeamProject] = @project AND [System.IterationPath] UNDER @currentIteration AND [System.WorkItemType] IN GROUP 'Microsoft.TestCaseCategory' ORDER BY [Microsoft.VSTS.Common.Priority],[System.Id] ";
                queryObject.wiql = UpdateQueryString;
                isUpdated = objQuery.UpdateQuery("Shared%20Queries/Current%20Sprint/Test Cases", model.Environment.ProjectName, Newtonsoft.Json.JsonConvert.SerializeObject(queryObject));

                UpdateQueryString = "SELECT [System.Id],[System.WorkItemType],[System.Title],[Microsoft.VSTS.Common.BacklogPriority],[System.AssignedTo],[System.State],[Microsoft.VSTS.CMMI.Blocked] FROM workitems WHERE [System.TeamProject] = @project AND [System.IterationPath] UNDER @currentIteration AND [System.WorkItemType] IN GROUP 'Microsoft.TaskCategory' AND [Microsoft.VSTS.CMMI.Blocked] = 'Yes' AND [System.State] <> 'Removed' ORDER BY [Microsoft.VSTS.Common.BacklogPriority], [System.Id]";
                queryObject.wiql = UpdateQueryString;
                isUpdated = objQuery.UpdateQuery("Shared%20Queries/Current%20Sprint/Blocked Tasks", model.Environment.ProjectName, Newtonsoft.Json.JsonConvert.SerializeObject(queryObject));

            }
            catch (Exception ex)
            {
                AddMessage(model.id.ErrorId(), "Error while creating Queries and Widgets: " + ex.Message + ex.StackTrace + Environment.NewLine);
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public JsonResult CheckForInstalledExtensions(string selectedTemplate, string token, string Account)
        {
            try
            {
                string accountName = string.Empty;
                string PAT = string.Empty;

                //if (Session["AccountName"] != null && Session["PAT"] != null)
                //{
                //    accountName = Session["AccountName"].ToString();
                //    PAT = Session["PAT"].ToString();
                //}
                accountName = Account;
                PAT = token;
                string templatesFolder = Server.MapPath("~") + @"\Templates\";
                string projTemplateFile = string.Format(templatesFolder + @"{0}\Extensions.json", selectedTemplate);
                if (!(System.IO.File.Exists(projTemplateFile)))
                {
                    return Json(new { message = "Template not found", status = "false" }, JsonRequestBehavior.AllowGet);
                }

                string templateItems = System.IO.File.ReadAllText(projTemplateFile);
                var template = JsonConvert.DeserializeObject<RequiredExtensions.Extension>(templateItems);
                string requiresExtensionNames = string.Empty;
                string requiredMicrosoftExt = string.Empty;
                string requiredThirdPartyExt = string.Empty;
                string FinalExtensionString = string.Empty;

                //Check for existing extensions
                if (template.Extensions.Length > 0)
                {
                    Dictionary<string, bool> dict = new Dictionary<string, bool>();
                    foreach (RequiredExtensions.ExtensionWithLink ext in template.Extensions)
                    {
                        dict.Add(ext.name, false);
                    }

                    var connection = new VssConnection(new Uri(string.Format("https://{0}.visualstudio.com", accountName)), new VssOAuthCredential(PAT));

                    var client = connection.GetClient<ExtensionManagementHttpClient>();
                    var installed = client.GetInstalledExtensionsAsync().Result;
                    var extensions = installed.Where(x => x.Flags == 0).ToList();

                    var trustedFlagExtensions = installed.Where(x => x.Flags == ExtensionFlags.Trusted).ToList();
                    extensions.AddRange(trustedFlagExtensions);

                    foreach (var ext in extensions)
                    {
                        foreach (var extension in template.Extensions)
                        {
                            if (extension.name.ToLower() == ext.ExtensionDisplayName.ToLower())
                            {
                                dict[extension.name] = true;
                            }
                        }
                    }
                    var required = dict.Where(x => x.Value == false).ToList();
                    if (required.Count > 0)
                    {
                        requiresExtensionNames = "<p>One or more extension is not installed/enabled in your VSTS account.</p><label style='font-weight: 400; text-align: justify; padding-left: 5px;'> You will need to install and enable them in order to proceed. If you agree with the terms below, the required extensions will be installed automatically for the selected account when the project is provisioned, otherwise install them manually and try refreshing the page.</label> <br/><br/>";
                        var InstalledExtensions = dict.Where(x => x.Value == true).ToList();
                        if (InstalledExtensions.Count > 0)
                        {
                            foreach (var ins in InstalledExtensions)
                            {
                                string link = "<img src=\"/Images/check-10.png\"/> " + template.Extensions.Where(x => x.name == ins.Key).FirstOrDefault().link;
                                requiresExtensionNames = requiresExtensionNames + link + "<br/><br/>";
                            }
                        }
                        foreach (var req in required)
                        {
                            string publisher = template.Extensions.Where(x => x.name == req.Key).FirstOrDefault().Publisher;
                            if (publisher == "microsoft")
                            {
                                string link = "<img src=\"/Images/cross10_new.png\"/> " + template.Extensions.Where(x => x.name == req.Key).FirstOrDefault().link;
                                requiredMicrosoftExt = requiredMicrosoftExt + link + "<br/><br/>";
                            }
                            else
                            {
                                string link = "<img src=\"/Images/cross10_new.png\"/> " + template.Extensions.Where(x => x.name == req.Key).FirstOrDefault().link;
                                requiredThirdPartyExt = requiredThirdPartyExt + link + "<br/><br/>";
                            }
                        }
                        if (!string.IsNullOrEmpty(requiredMicrosoftExt))
                        {
                            requiredMicrosoftExt = requiredMicrosoftExt + "<div id='agreeTerms'><label style = 'font-weight: 400; text-align: justify; padding-left: 5px;'><input type = 'checkbox' class='terms' id = 'agreeTermsConditions' placeholder='microsoft' /> &nbsp; I agree on behalf of all users in the account that the extension(s) is provided as Additional Software under the <a href = 'https://go.microsoft.com/fwlink/?LinkID=266231' target = '_blank'> Microsoft Online Services Terms </a> and <a href = 'https://go.microsoft.com/fwlink/?LinkId=131004&clcid=0x409' target = '_blank'> Microsoft Online Services Privacy Statement</a></label></div></br>";
                        }
                        if (!string.IsNullOrEmpty(requiredThirdPartyExt))
                        {
                            requiredThirdPartyExt = requiredThirdPartyExt + "<div id='ThirdPartyAgreeTerms'><label style = 'font-weight: 400; text-align: justify; padding-left: 5px;'><input type = 'checkbox' class='terms' id = 'ThirdPartyagreeTermsConditions' placeholder='thirdparty' /> &nbsp; The extension(s) is offered to you for your use by a third party, not Microsoft. By proceeding, you agree to the license and privacy policy, if any, for this extension.</label></div></br>";
                        }
                        FinalExtensionString = requiresExtensionNames + requiredMicrosoftExt + requiredThirdPartyExt;
                        //requiresExtensionNames = requiresExtensionNames + "<p style='color:red;' data-toggle='tooltip' title='Required extensions will be installed to your VSTS by the tool'>one or more extension is not installed/enabled in your VSTS account. Tool will install the extension(s) to your VSTS Account</p>";
                        //requiresExtensionNames = requiresExtensionNames + "</b>The following extension which is required for the above selected template is either installed or not enabled on the account. You will need to fix them in order to continue.“Refresh” the page after you have the extension installed and enabled";
                        return Json(new { message = FinalExtensionString, status = "false" }, JsonRequestBehavior.AllowGet);
                    }
                    else
                    {
                        var InstalledExtensions = dict.Where(x => x.Value == true).ToList();
                        if (InstalledExtensions.Count > 0)
                        {
                            requiresExtensionNames = "All required extensions are installed/enabled in your VSTS account :<br/><br/><b>";
                            foreach (var ins in InstalledExtensions)
                            {
                                string link = "<img src=\"/Images/check-10.png\"/> " + template.Extensions.Where(x => x.name == ins.Key).FirstOrDefault().link;
                                requiresExtensionNames = requiresExtensionNames + link + "<br/><br/>";
                            }
                            return Json(new { message = requiresExtensionNames, status = "true" }, JsonRequestBehavior.AllowGet);
                        }
                    }

                }
                else { requiresExtensionNames = "no extensions required"; return Json(requiresExtensionNames, JsonRequestBehavior.AllowGet); }
                return Json(new { message = requiresExtensionNames, status = "false" }, JsonRequestBehavior.AllowGet);

            }
            catch (Exception ex)
            {
                return Json(new { message = "Error", status = "false" }, JsonRequestBehavior.AllowGet);
            }
        }

        public bool InstallExtensions(Project model, string accountName, string PAT)
        {
            try
            {

                string templatesFolder = Server.MapPath("~") + @"\Templates\";
                string projTemplateFile = string.Format(templatesFolder + @"{0}\Extensions.json", model.SelectedTemplate);
                if (!(System.IO.File.Exists(projTemplateFile)))
                {
                    return false;
                }

                string templateItems = System.IO.File.ReadAllText(projTemplateFile);
                var template = JsonConvert.DeserializeObject<RequiredExtensions.Extension>(templateItems);
                string requiresExtensionNames = string.Empty;

                //Check for existing extensions
                if (template.Extensions.Length > 0)
                {
                    Dictionary<string, bool> dict = new Dictionary<string, bool>();
                    foreach (RequiredExtensions.ExtensionWithLink ext in template.Extensions)
                    {
                        dict.Add(ext.name, false);
                    }
                    var connection = new VssConnection(new Uri(string.Format("https://{0}.visualstudio.com", accountName)), new VssOAuthCredential(PAT));
                    var client = connection.GetClient<ExtensionManagementHttpClient>();
                    var installed = client.GetInstalledExtensionsAsync().Result;
                    var extensions = installed.Where(x => x.Flags == 0).ToList();

                    var trustedFlagExtensions = installed.Where(x => x.Flags == ExtensionFlags.Trusted).ToList();
                    extensions.AddRange(trustedFlagExtensions);

                    foreach (var ext in extensions)
                    {
                        foreach (var extension in template.Extensions)
                        {
                            if (extension.name.ToLower() == ext.ExtensionDisplayName.ToLower())
                            {
                                dict[extension.name] = true;
                            }
                        }
                    }
                    var required = dict.Where(x => x.Value == false).ToList();

                    if (required.Count > 0)
                    {
                        foreach (var req in required)
                        {
                            string publisherName = template.Extensions.Where(x => x.name == req.Key).FirstOrDefault().PublisherId;
                            string extensionName = template.Extensions.Where(x => x.name == req.Key).FirstOrDefault().ExtensionId;

                            InstalledExtension extension = null;
                            extension = client.InstallExtensionByNameAsync(publisherName, extensionName).Result;
                        }

                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                AddMessage(model.id.ErrorId(), "Error while Installing extensions: " + ex.Message + ex.StackTrace + Environment.NewLine);
                return false;
            }
        }

        public JsonResult SendEmail(Email model)
        {
            Email objEmail = new Email();
            string subject = "VSTS Demogenerator error detail";

            var bodyContent = System.IO.File.ReadAllText(System.Web.HttpContext.Current.Server.MapPath("~/EmailTemplates/ErrorDetail.html"));

            bodyContent = bodyContent.Replace("$body$", model.ErrorLog);
            bodyContent = bodyContent.Replace("$AccountName$", model.AccountName);
            bodyContent = bodyContent.Replace("$Email$", model.EmailAddress);
            string toEmail = System.Configuration.ConfigurationManager.AppSettings["toEmail"];
            bool isMailSent = objEmail.sendEmail(toEmail, bodyContent, subject);
            if (isMailSent)
            {
                return Json(new { sent = "true" }, JsonRequestBehavior.AllowGet);
            }
            return Json(new { sent = "false" }, JsonRequestBehavior.AllowGet);

        }
        #endregion
    }
}





