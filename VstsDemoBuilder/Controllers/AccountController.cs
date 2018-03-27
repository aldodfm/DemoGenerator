using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using VstsDemoBuilder.Models;
using VstsRestAPI;
using VstsRestAPI.ProjectsAndTeams;

namespace VstsDemoBuilder.Controllers
{

    public class AccountController : Controller
    {
        AccessDetails AccessDetails = new AccessDetails();
        [HttpGet]
        [AllowAnonymous]
        public ActionResult Verify(LoginModel model, string id)
        {
            try
            {
                if (!string.IsNullOrEmpty(model.name) && !string.IsNullOrEmpty(model.templateid))
                {
                    Session["templateName"] = model.name;
                    Session["templateId"] = model.templateid;
                }
                else
                {
                    Session["templateName"] = "";
                    Session["templateId"] = "";
                }

                if (!string.IsNullOrEmpty(model.Event))
                {
                    string eventsTemplate = Server.MapPath("~") + @"\Templates\Events.json";
                    if (System.IO.File.Exists(eventsTemplate))
                    {
                        string eventContent = System.IO.File.ReadAllText(eventsTemplate);
                        var jItems = JObject.Parse(eventContent);
                        if (jItems[model.Event] != null)
                        {
                            model.Event = jItems[model.Event].ToString();
                        }
                        else
                        {
                            model.Event = string.Empty;
                        }
                    }
                }
            }
            catch { }

            return View(model);
        }

        //[HttpPost]
        //[AllowAnonymous]
        //public ActionResult Verify(LoginModel model)
        //{
        //    try
        //    {
        //        string _credentials = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", "", model.PAT)));
        //        Configuration _inputConfiguration = new Configuration() { UriString = "https://" + model.AccountName + ".visualstudio.com/DefaultCollection/", VersionNumber = "2.2", PersonalAccessToken = model.PAT };
        //        Projects objProject = new Projects(_inputConfiguration);
        //        bool isAccountValid = objProject.IsAccountHasProjects();
        //        if (isAccountValid)
        //        {

        //            Session["PAT"] = model.PAT;
        //            Session["AccountName"] = model.AccountName;

        //            FormsAuthenticationTicket ticket = new FormsAuthenticationTicket(1, model.AccountName, DateTime.Now, DateTime.Now.AddMinutes(FormsAuthentication.Timeout.TotalMinutes), false, model.PAT, FormsAuthentication.FormsCookiePath);
        //            string cookie = FormsAuthentication.Encrypt(ticket);
        //            HttpCookie ck = new HttpCookie(FormsAuthentication.FormsCookieName, cookie);
        //            ck.Path = FormsAuthentication.FormsCookiePath;
        //            Response.Cookies.Add(ck);

        //            return RedirectToAction("Create", "Environment", new { TemplateId = model.templateid, TemplateName = model.name });
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        model.Message = "LoginFailed: " + ex.Message;
        //        return RedirectToAction("Verify", new { AccountName = model.AccountName, PAT = model.PAT, templateid = model.templateid, name = model.name, Message = model.Message, Event = model.Event, id = string.Empty });
        //    }

        //    model.Message = "Invalid Account name or PAT";
        //    return RedirectToAction("Verify", new { AccountName = model.AccountName, PAT = model.PAT, templateid = model.templateid, name = model.name, Message = model.Message, Event = model.Event, id = string.Empty });
        //}

        //public bool VerifyUser(string accountName, string PAT)
        //{
        //    string _credentials = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", "", PAT)));
        //    Configuration _inputConfiguration = new Configuration() { UriString = "https://" + accountName + ".visualstudio.com/DefaultCollection/", VersionNumber = "2.2", PersonalAccessToken = PAT };
        //    Projects objProject = new Projects(_inputConfiguration);
        //    bool isAccountValid = objProject.IsAccountHasProjects();
        //    if (isAccountValid)
        //    {
        //        Session["PAT"] = PAT;
        //        Session["AccountName"] = accountName;
        //        return true;
        //    }
        //    else
        //    {
        //        return false;
        //    }
        //}
        [HttpGet]
        [AllowAnonymous]
        public string GetAccountName()
        {
            if (Session["AccountName"] != null)
            {
                string accountName = Session["AccountName"].ToString();
                return accountName;
            }
            else
            {
                return string.Empty;
            }
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult Index()
        {
            Session["visited"] = "1";
            string url = "https://app.vssps.visualstudio.com/oauth2/authorize?client_id={0}&response_type=Assertion&state=User1&scope=vso.build_execute%20vso.code_manage%20vso.dashboards_manage%20vso.extension_manage%20vso.identity%20vso.project_manage%20vso.release_manage%20vso.serviceendpoint_manage%20vso.work_full&redirect_uri={1}";

            string redirectUrl = System.Configuration.ConfigurationManager.AppSettings["RedirectUri"];
            string clientId = System.Configuration.ConfigurationManager.AppSettings["ClientId"];
            url = string.Format(url, clientId, redirectUrl);
            return Redirect(url);
        }
        [HttpGet]
        [AllowAnonymous]
        public ActionResult SignOut()
        {
            Session.Clear();
            
            return Redirect("https://app.vssps.visualstudio.com/_signout");
        }
    }
}
