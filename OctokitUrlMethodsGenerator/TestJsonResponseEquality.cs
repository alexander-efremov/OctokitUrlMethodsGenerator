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
        private readonly OctokitHttpClient _httpClient = new OctokitHttpClient();

        private async Task TestEqualityInternal(Uri firstUrl, Uri secondUrl)
        {
            var firstRespond = await _httpClient.GetAsync(firstUrl);
            var secondRespond = await _httpClient.GetAsync(secondUrl);
            Assert.AreEqual(firstRespond, secondRespond,
                $"Firs url: {firstUrl + Environment.NewLine}Second url {secondUrl}");
        }

        private object[] GetLastParameters(string methodName)
        {
            switch (methodName)
            {
                case "IssueLock":
                    return new object[] { 1285 };
                case "Issue":
                    return new object[] { 1 };
            }
            return new object[0];
        }

        [TestCase("Issue")]
        [TestCase("IssueLock")]
        public async Task Test(string methodName)
        {
            const int repositoryId = 7528679;
            const string owner = "octokit";
            const string name = "octokit.net";

            var methodInfo = typeof(ApiUrls).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(m => m.Name == methodName && m.GetParameters().Length >= 2)
                .OrderByDescending(m => m.GetParameters().Length);
            var firstMethod = methodInfo.First(info => info.GetParameters().Length >= 2 && info.GetParameters()[0].ParameterType
                                                       == typeof(string) && info.GetParameters()[1].ParameterType == typeof(string));

            var secondName = methodInfo.First(info =>
            {
                var length = firstMethod.GetParameters().Length;
                Console.WriteLine(length);
                var parameterInfos = info.GetParameters();
                return parameterInfos.Length == length - 1
                       && parameterInfos[0].ParameterType == typeof(int) && parameterInfos[1].ParameterType != typeof(string);
            });

            var firstUrl = (Uri)firstMethod.Invoke(null, new object[] { owner, name }.Union(GetLastParameters(methodName)).ToArray());
            var secondUrl = (Uri)secondName.Invoke(null, new object[] { repositoryId }.Union(GetLastParameters(methodName)).ToArray());
            await TestEqualityInternal(firstUrl, secondUrl);
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
