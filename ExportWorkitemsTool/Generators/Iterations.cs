using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VstsRestAPI;
using VstsRestAPI.Viewmodel.WorkItem;
using VstsRestAPI.WorkItemAndTracking;

namespace TemplatesGeneratorTool.Generators
{
    public class Iterations
    {
        readonly VstsRestAPI.IConfiguration _sourceConfig;
        readonly string _sourceCredentials;
        readonly string _accountName;

        public Iterations(VstsRestAPI.IConfiguration configuration, string accountName)
        {
            _accountName = accountName;
            _sourceConfig = configuration;
            _sourceCredentials = Convert.ToBase64String(System.Text.ASCIIEncoding.ASCII.GetBytes(string.Format("{0}:{1}", "", _sourceConfig.PersonalAccessToken)));
        }

        public void GetIterations()
        {
            ClassificationNodes cNodes = new ClassificationNodes(_sourceConfig);
            GetNodesResponse.Nodes iterationNodes = cNodes.GetIterations(_sourceConfig.Project);

            string fetchedPBIsJSON = JsonConvert.SerializeObject(iterationNodes, Formatting.Indented);
            System.IO.File.WriteAllText(@"Templates\Iterations.json", fetchedPBIsJSON);
        }

    }
}
