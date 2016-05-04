using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DocsByReflection;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NUnit.Framework;
using Octokit;
using OctokitUrlMethodsGenerator.Http;

namespace OctokitUrlMethodsGenerator
{
    [TestFixture]
    public class TestJsonResponseEquality
    {
        [TestCase("octokit", "octokit.net", 7528679)]
        public async Task Test(string owner, string name, int repositoryId)
        {
            var client = new OctokitHttpClient();

            var firstUrl = ApiUrls.Issue(owner, name, 1);
            var secondUrl = ApiUrls.Issue(repositoryId, 1);

            var firstRespond = await client.GetAsync(firstUrl, HttpMethod.Get);
            var secondRespond = await client.GetAsync(secondUrl, HttpMethod.Get);

            Assert.AreEqual(firstRespond, secondRespond, "Firs url: " + firstUrl + Environment.NewLine + "Second url" + secondUrl);

            Console.WriteLine(firstUrl);
            Console.WriteLine(secondUrl);
        }

        // [TestCase(@"C:\Users\efremov_aa\Source\Repos\OctokitUrlMethodsGenerator\octokit\Octokit\Helpers\ApiUrls.cs", @"C:\Users\efremov_aa\Source\Repos\OctokitUrlMethodsGenerator\OctokitUrlMethodsGenerator\")]
        public void MainTest(string pathToApiUrls, string outputPath)
        {
            const string idParameterName = "repositoryId";
            var syntaxTree = ExtensionMethods.GetSyntaxTree(pathToApiUrls);

            var root = (CompilationUnitSyntax)syntaxTree.GetRoot();

            var methodInfos = typeof(ApiUrls).GetMethods(BindingFlags.Public | BindingFlags.Static);
            methodInfos = methodInfos.Where(info => info.GetParameters().Length >= 2).ToArray();
            methodInfos = methodInfos.Where(info => info.GetParamName(0) == "owner" && info.GetParamName(1) == "name").ToArray();
            Console.WriteLine(methodInfos.Length);

            var builder = new StringBuilder();
            for (var index = 0; index < methodInfos.Length; index++)
            {
                var info = methodInfos[index];

                try
                {
                    var methodDeclarationSyntax = ExtensionMethods.GetMethodDeclarationSyntax(syntaxTree, info.Name, info.GetParameters().Length);
                    var formattableString = $"public static Uri {info.Name}(int {idParameterName}, ";

                    foreach (var parameter in methodDeclarationSyntax.ParameterList.Parameters)
                    {
                        var type = parameter.Type.ToString();
                        var name = parameter.Identifier.ToString();
                        if (name != "owner" && name != "name")
                            formattableString += $"{type} {name}, ";
                    }

                    formattableString = formattableString.TrimEnd(',', ' ') + ")";
                    builder.AppendLine(formattableString);
                }
                catch (DocsByReflectionException e)
                {
                    Console.WriteLine(info.Name);
                    Console.WriteLine(e);

                    throw;
                }
            }
        }
    }
}
