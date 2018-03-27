using TemplatesGeneratorTool.Generators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TemplatesGeneratorTool
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string PAT = string.Empty;
            string accountName = string.Empty;
            string projectName = string.Empty;

            Console.WriteLine("Please enter Account Name");
            accountName = Console.ReadLine();

            Console.WriteLine("Please enter Personel access token");
            PAT = Console.ReadLine();

            Console.WriteLine("Please enter project name");
            projectName = Console.ReadLine();

           
            Configuration sourceconfig = new Configuration() { UriString = "https://" + accountName + ".visualstudio.com:", PersonalAccessToken = PAT, Project = projectName };
            VstsRestAPI.Configuration vstsAPIConfiguration = new VstsRestAPI.Configuration() { UriString = "https://" + accountName + ".visualstudio.com:", PersonalAccessToken = PAT, Project = projectName };

            Console.WriteLine("Generating templates....");
            Console.WriteLine();


            Dictionary<string, string> queryList = new Dictionary<string, string>();
            ExportQueries objQuery = new ExportQueries(vstsAPIConfiguration);
            //queryList = objQuery.GetQueries(projectName);
            queryList = objQuery.GetQueriesByPath(projectName, string.Empty);

            ExportWidgetsAndCharts objWidgetAndCharts = new ExportWidgetsAndCharts(vstsAPIConfiguration);
            objWidgetAndCharts.GetWidgetsAndCharts(projectName, queryList);
            Console.WriteLine("Queries and widget JSONs are saved into Template folder");
            Console.WriteLine("");


            Teams objTeam = new Teams(vstsAPIConfiguration);
            objTeam.ExportTeams(projectName);
            Console.WriteLine("Teams JSON is saved into Template folder");
            Console.WriteLine("");

            ServiceEndpoints objService = new ServiceEndpoints(vstsAPIConfiguration);

            string serviceEndPoint = string.Empty;
            serviceEndPoint = objService.ExportServiceEndPoints(projectName);
            Console.WriteLine("ServiceEndPoint JSONs are saved into Template folder");
            Console.WriteLine("");

            SourceCode objSourceCode = new SourceCode(vstsAPIConfiguration);
            objSourceCode.ExportSourceCode(projectName);
            Console.WriteLine("ImportSourceCode JSON is saved into Template folder");
            Console.WriteLine("");

            BoardColumns objColumn = new BoardColumns(vstsAPIConfiguration);
            objColumn.ExportBoardColumns(projectName);
            Console.WriteLine("BoardColumn JSON file is saved into Template folder");
            Console.WriteLine("");

            GenerateWIFromSource wiql = new GenerateWIFromSource(sourceconfig, accountName);
            wiql.UpdateWorkItem();
            Console.WriteLine("Work item JSON files are saved into Templates folder");
            Console.WriteLine("");

            BuildDefinitions objBuild = new BuildDefinitions(vstsAPIConfiguration, accountName);
            ReleaseDefinitions objRelease = new ReleaseDefinitions(vstsAPIConfiguration, accountName);
            objBuild.ExportBuildDefinitions(projectName);
            objRelease.ExportReleaseDefinitions(projectName, serviceEndPoint);
            Console.WriteLine("Build and Release Definitions are saved into Templates folder");
            Console.WriteLine("");

            CardFieldsAndCardStyles objCards = new CardFieldsAndCardStyles(vstsAPIConfiguration);
            objCards.GetCardFields(projectName);
            objCards.GetCardStyles(projectName);
            Console.WriteLine("CardField and CardStyle JSON files are saved into Templates folder");
            Console.WriteLine("");

            Iterations objIterations = new Iterations(vstsAPIConfiguration, accountName);
            objIterations.GetIterations();
            Console.WriteLine("iterations JSON file is saved into Templates folder");
            Console.WriteLine("");
            
            PullRequests objPullRequest = new PullRequests(vstsAPIConfiguration);
            objPullRequest.ExportPullRequests(projectName);
            Console.WriteLine("PullReqests and comments JSON files are saved into Templates folder");
            Console.WriteLine("");

            Console.WriteLine("Completed generating templates from " + projectName);
            var wait = Console.ReadLine();
        }
    }
}
      