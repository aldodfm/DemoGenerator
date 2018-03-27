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
	public class DeliveryPlans
	{
		readonly VstsRestAPI.IConfiguration _sourceConfig;
		readonly string _sourceCredentials;
		readonly string _accountName;

		public DeliveryPlans(VstsRestAPI.IConfiguration configuration)
		{
			_sourceConfig = configuration;
			_sourceCredentials = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", "", _sourceConfig.PersonalAccessToken)));
		}

		public void GetDeliveryPlans(string project)
		{
			using (var client = new HttpClient())
			{
				client.BaseAddress = new Uri(_sourceConfig.UriString);
				client.DefaultRequestHeaders.Accept.Clear();
				client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", _sourceCredentials);

				HttpResponseMessage response = client.GetAsync(_sourceConfig.UriString + string.Format("/DefaultCollection/{0}/_apis/work/plans?api-version=3.0-preview.1", project)).Result;
				if (response.IsSuccessStatusCode)
				{
					PlanViewModel.AllPlans allPLans = Newtonsoft.Json.JsonConvert.DeserializeObject<PlanViewModel.AllPlans>(response.Content.ReadAsStringAsync().Result.ToString());
					if (allPLans.count > 0)
					{
						using (var clientOne = new HttpClient())
						{
							clientOne.BaseAddress = new Uri(_sourceConfig.UriString);
							clientOne.DefaultRequestHeaders.Accept.Clear();
							clientOne.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
							clientOne.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", _sourceCredentials);

							int planCount = 1;
							foreach (var plan in allPLans.value)
							{
								HttpResponseMessage PlanResponse = client.GetAsync(_sourceConfig.UriString + string.Format("/DefaultCollection/{0}/_apis/work/plans/{1}?api-version=3.0-preview.1", project, plan.id)).Result;

								if (response.IsSuccessStatusCode)
								{
									PlanViewModel.DeliveryPlan DeliveryPLan = Newtonsoft.Json.JsonConvert.DeserializeObject<PlanViewModel.DeliveryPlan>(response.Content.ReadAsStringAsync().Result.ToString());
									if (!Directory.Exists(@"Templates\DeliveryPLans"))
									{
										Directory.CreateDirectory(@"Templates\DeliveryPLans");
									}

									string fetchedDeliveryPlan = JsonConvert.SerializeObject(PlanResponse, Formatting.Indented);
									System.IO.File.WriteAllText(@"Templates\DeliveryPLans\DeliveryPlan" + planCount + ".json", fetchedDeliveryPlan);
									planCount = planCount + 1;
								}
							}
						}
					}
				}
			}
		}
	}
}
