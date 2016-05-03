using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using DocsByReflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using NUnit.Framework;
using Octokit;

namespace OctokitUrlMethodsGenerator
{
    public static class XmlElementExt
    {
        public static string GetValue(this XmlElement element, string path)
        {
            var xmlElement = element[path];
            return xmlElement != null ? xmlElement.InnerText.Trim() : string.Empty;
        }
    }

    [TestFixture]
    public class Test
    {
        private static string GetParamName(MethodInfo method, int index)
        {
            var retVal = string.Empty;

            if (method != null && method.GetParameters().Length > index)
                retVal = method.GetParameters()[index].Name;

            return retVal;
        }

        [TestCase(@"C:\Users\efremov_aa\Source\Repos\OctokitUrlMethodsGenerator\octokit\Octokit\Helpers\ApiUrls.cs", @"C:\Users\efremov_aa\Source\Repos\OctokitUrlMethodsGenerator\OctokitUrlMethodsGenerator\")]
        public void MainTest(string pathToApiUrls, string outputPath)
        {
            const string idParameterName = "repositoryId";
            var syntaxTree = GetST(pathToApiUrls);
            var methodInfos = typeof(ApiUrls).GetMethods(BindingFlags.Public | BindingFlags.Static);
            methodInfos = methodInfos.Where(info => info.GetParameters().Length >= 2).ToArray();
            methodInfos = methodInfos.Where(info => GetParamName(info, 0) == "owner" && GetParamName(info, 1) == "name").ToArray();
            Console.WriteLine(methodInfos.Length);

            methodInfos = PrintOut(methodInfos);

            var builder = new StringBuilder();
            for (var index = 0; index < methodInfos.Length; index++)
            {
                var info = methodInfos[index];
                Console.WriteLine(index + 1);

                try
                {
                    var element = DocsService.GetXmlFromMember(info);

                    builder.AppendLine("/// <summary>");
                    var summary = PrepareSummary(element.GetValue("summary"));
                    
                    builder.AppendLine("/// " + summary);
                    builder.AppendLine("/// </summary>");

                    builder.AppendLine($@"/// <param name=""{idParameterName}"">The ID of the repository</param>");

                    foreach (var parameterInfo in info.GetParameters())
                    {
                        var xmlFromParameter = DocsService.GetXmlFromParameter(parameterInfo);
                        var paramTemplate = @"/// <param name=""{0}"">{1}</param>";
                        try
                        {
                            if (xmlFromParameter.Attributes["name"].Value != "owner" && xmlFromParameter.Attributes["name"].Value != "name")
                                builder.AppendLine(string.Format(paramTemplate, xmlFromParameter.Attributes["name"].Value, xmlFromParameter.InnerText));
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(info.Name);
                            Console.WriteLine(e);
                            throw;
                        }
                    }
                    try
                    {
                        var format = PrepareReturns(element, summary);
                        builder.AppendLine(format);
                    }
                    catch (Exception)
                    {
                        Console.WriteLine(info.Name);
                        throw;
                    }

                    var methodDeclarationSyntax = GetMethodDeclarationSyntax(syntaxTree, info.Name, info.GetParameters().Length);
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

                    var methodBody = FormatMethodBody(methodDeclarationSyntax.Body.ToString());
                    methodBody = methodBody.Replace("repos/{0}/{1}", "repositories/{0}");
                    methodBody = methodBody.Replace(".FormatUri(owner, name", $".FormatUri({idParameterName}");

                    // corner cases
                    if (formattableString.Contains($"public static Uri Blob(int {idParameterName})"))
                    {
                        methodBody = methodBody.Replace(@"return Blob(owner, name, """");", $@"return Blob({idParameterName}, """");");
                    }
                    else if (formattableString.Contains($"public static Uri Blob(int {idParameterName}, string reference)"))
                    {
                        methodBody = methodBody.Replace(@"blob += ""/{2}"";", @"blob += ""/{1}"";");
                    }
                    else if (formattableString.Contains($"public static Uri NetworkEvents(int {idParameterName})"))
                    {
                        methodBody = methodBody.Replace($@"return ""networks/{0}/{1}/events"".FormatUri({idParameterName});", $@"return ""repositories/{0}/events"".FormatUri({idParameterName});");
                    }
                    else if (formattableString.Contains($"public static Uri Starred(int {idParameterName})"))
                    {
                        methodBody = methodBody.Replace($@"return ""user/starred/{0}/{1}"".FormatUri({idParameterName});", $@"return ""user/starred/repositories/{0}/"".FormatUri({idParameterName});");
                    }
                    else if (formattableString.Contains($"public static Uri RepoCompare(int {idParameterName}, string @base, string head)"))
                    {
                        methodBody = methodBody.Replace(@"Ensure.ArgumentNotNullOrEmptyString(owner, ""owner"");", string.Empty);
                        methodBody = methodBody.Replace(@"Ensure.ArgumentNotNullOrEmptyString(name, ""name"");", string.Empty);
                    }

                    // decrease string format args by one
                    methodBody = methodBody.Replace("{2}", "{1}");
                    methodBody = methodBody.Replace("{3}", "{2}");
                    methodBody = methodBody.Replace("{4}", "{3}");
                    methodBody = methodBody.Replace("{5}", "{4}");
                    methodBody = methodBody.Replace("{6}", "{5}");
                    methodBody = methodBody.Replace("{7}", "{6}");

                    builder.AppendLine(methodBody);
                }
                catch (DocsByReflectionException e)
                {
                    Console.WriteLine(info.Name);
                    Console.WriteLine(e);

                    throw;
                }
            }
            var combine = Path.Combine(outputPath, "new_methods.txt");
            File.WriteAllText(combine, builder.ToString());
        }

        private static string FirstCharToUpper(string input)
        {
            if (string.IsNullOrEmpty(input))
                throw new ArgumentException("ARGH!");
            return input.First().ToString().ToUpper() + input.Substring(1);
        }

        private static string PrepareReturns(XmlNode element, string summary)
        {
            var trim = element["returns"].InnerText.Trim();
            trim = trim.Replace("The  ", @"The <see cref=""Uri""/> ");
            if (trim == "The")
            {
                trim = trim.Replace("The", @"The <see cref=""Uri""/> that ");
            }
            if (string.IsNullOrWhiteSpace(trim))
            {
                var summaryPrapared = summary.Replace("Returns ", "");
                summaryPrapared = FirstCharToUpper(summaryPrapared);
                trim = summaryPrapared;
            }
            var format = string.Format("/// <returns>{0}</returns>", trim);
            return format;
        }

        private string PrepareSummary(string getValue)
        {
            var lines = getValue.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            var r = string.Empty;
            foreach (var line in lines)
            {
                r += line.Trim() + " ";
            }
            r = r.Replace("Returns the ", @"Returns the <see cref=""Uri""/>");
            r = r.Replace("Creates the relative  ", @"Returns the <see cref=""Uri""/> ");
            r = r.Replace("returns the  for branches", @"Returns the <see cref=""Uri""/> that returns all of the branches for the specified repository.");
            r = r.Replace("returns the  for teams use for update or deleting a team", @"Returns the <see cref=""Uri""/> that returns all of the collaborators for the specified repository.");
            r = r.Trim();

            return r;
        }

        private string FormatMethodBody(string methodBody)
        {
            var lines = methodBody.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var r = string.Empty;

            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index].Trim();
                if (index != 0 && index != lines.Length - 1)
                {
                    r += new string(' ', 4);
                }
                r += line + Environment.NewLine;
            }
            return r;
        }

        private static MethodInfo[] PrintOut(MethodInfo[] methodInfos)
        {
            methodInfos = methodInfos.OrderBy(info => info.Name).ToArray();
            for (var index = 0; index < methodInfos.Length; index++)
            {
                var methodInfo = methodInfos[index];
                var name = methodInfo.Name;
                Console.WriteLine($"{index + 1}) {name}");
            }
            return methodInfos;
        }

        private static SyntaxTree GetST(string filename)
        {
            using (var stream = File.OpenRead(filename))
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(SourceText.From(stream), path: filename);
                return syntaxTree;
            }
        }

        private static MethodDeclarationSyntax GetMethodDeclarationSyntax(SyntaxTree syntaxTree, string methodName, int paramNumber)
        {
            var members = syntaxTree.GetRoot().DescendantNodes().OfType<MemberDeclarationSyntax>();
            foreach (var member in members)
            {
                var method = member as MethodDeclarationSyntax;
                if (method != null && method.Identifier.ToString() == methodName &&
                    method.ParameterList.Parameters.Count == paramNumber)
                {
                    return method;
                }
            }
            return null;
        }
    }
}
