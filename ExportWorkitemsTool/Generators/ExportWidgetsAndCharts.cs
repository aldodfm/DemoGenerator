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
    public class ExportWidgetsAndCharts
    {
        readonly VstsRestAPI.IConfiguration _sourceConfig;
        readonly string _sourceCredentials;


        public ExportWidgetsAndCharts(VstsRestAPI.IConfiguration configuration)
        {
            _sourceConfig = configuration;
            _sourceCredentials = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", "", _sourceConfig.PersonalAccessToken)));
        }
        public void GetWidgetsAndCharts(string project, Dictionary<string, string> QueryList)
        {
            string dashBoardId = string.Empty;

            using (var clientOne = new HttpClient())
            {
                clientOne.BaseAddress = new Uri(_sourceConfig.UriString);
                clientOne.DefaultRequestHeaders.Accept.Clear();
                clientOne.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                clientOne.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", _sourceCredentials);

                HttpResponseMessage response = clientOne.GetAsync(project + "/_apis/Dashboard/Dashboards/??api-version=3.0-preview.2").Result;
                if (response.IsSuccessStatusCode)
                {
                    DashBoardResponse.Dashboard dashBoard = response.Content.ReadAsAsync<DashBoardResponse.Dashboard>().Result;
                    dashBoardId = dashBoard.dashboardEntries[0].id;
                }
            }
            if (!(string.IsNullOrEmpty(dashBoardId)))
            {
                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri(_sourceConfig.UriString);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", _sourceCredentials);

                    HttpResponseMessage response = client.GetAsync(project + "/" + project + "%20Team/_apis/Dashboard/Dashboards/" + dashBoardId + "/Widgets/?api-version=3.0-preview.2").Result;

                    if (response.IsSuccessStatusCode)
                    {
                        WidgetAndChartResponse.Widget Widgets = response.Content.ReadAsAsync<WidgetAndChartResponse.Widget>().Result;

                        if (Widgets.count > 0)
                        {
                           
                            if (!Directory.Exists(@"Templates\ChartsAndWidgets"))
                            {
                                Directory.CreateDirectory(@"Templates\ChartsAndWidgets");
                            }

                            foreach (WidgetAndChartResponse.Value widget in Widgets.value)
                            {
                                if (!string.IsNullOrEmpty(widget.settings))
                                {
                                    //string widgetName = string.Empty;
                                    foreach (var query in QueryList)
                                    {
                                        if (widget.settings.Contains(query.Key))
                                        {
                                            //widgetName = query.Value;
                                            widget.settings = widget.settings.Replace(query.Key, "$QueryId$");
                                            break;
                                        }
                                    }
                                    if (widget.settings.Contains("queryId"))
                                    {
                                        if (!Directory.Exists(@"Templates\ChartsAndWidgets\Widgets"))
                                        {
                                            Directory.CreateDirectory(@"Templates\ChartsAndWidgets\Widgets");
                                        }
                                        //if (string.IsNullOrEmpty(widgetName)) { widgetName = widget.name; }
                                        System.IO.File.WriteAllText(@"Templates\ChartsAndWidgets\Widgets\" + widget.name + ".json", JsonConvert.SerializeObject(widget, Formatting.Indented));
                                  

                                    }
                                    else
                                    {
                                        if (!Directory.Exists(@"Templates\ChartsAndWidgets\Charts"))
                                        {
                                            Directory.CreateDirectory(@"Templates\ChartsAndWidgets\Charts");
                                        }
                                        //if (string.IsNullOrEmpty(widgetName)) { widgetName = widget.name; }
                                        System.IO.File.WriteAllText(@"Templates\ChartsAndWidgets\Charts\" + widget.name + ".json", JsonConvert.SerializeObject(widget, Formatting.Indented));
                                      
                                    }
                                }
                                else
                                {
                                    if (!Directory.Exists(@"Templates\ChartsAndWidgets\WidgetsWithoutQuery"))
                                    {
                                        Directory.CreateDirectory(@"Templates\ChartsAndWidgets\WidgetsWithoutQuery");
                                    }
                                    System.IO.File.WriteAllText(@"Templates\ChartsAndWidgets\WidgetsWithoutQuery\" + widget.name + ".json", JsonConvert.SerializeObject(widget, Formatting.Indented));
                                
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}