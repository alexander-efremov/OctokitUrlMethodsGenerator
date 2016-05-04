using System;
using System.Linq;
using System.Reflection;
using System.Text;
using DocsByReflection;
using NUnit.Framework;
using Octokit;

namespace OctokitUrlMethodsGenerator
{
    [TestFixture]
    public class TestJsonResponseEquality
    {
        [TestCase(@"C:\Users\efremov_aa\Source\Repos\OctokitUrlMethodsGenerator\octokit\Octokit\Helpers\ApiUrls.cs", @"C:\Users\efremov_aa\Source\Repos\OctokitUrlMethodsGenerator\OctokitUrlMethodsGenerator\")]
        public void MainTest(string pathToApiUrls, string outputPath)
        {
            const string idParameterName = "repositoryId";
            var syntaxTree = ExtensionMethods.GetSyntaxTree(pathToApiUrls);
            var methodInfos = typeof(ApiUrls).GetMethods(BindingFlags.Public | BindingFlags.Static);
            methodInfos = methodInfos.Where(info => info.GetParameters().Length >= 2).ToArray();
            methodInfos = methodInfos.Where(info => info.GetParamName(0) == "owner" && info.GetParamName(1) == "name").ToArray();
            Console.WriteLine(methodInfos.Length);

            var builder = new StringBuilder();
            for (var index = 0; index < methodInfos.Length; index++)
            {
                var info = methodInfos[index];
                Console.WriteLine(index + 1);

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
