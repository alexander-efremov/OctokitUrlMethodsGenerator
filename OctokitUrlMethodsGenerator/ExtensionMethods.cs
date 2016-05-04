using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace OctokitUrlMethodsGenerator
{
    public static class ExtensionMethods
    {
        public static string GetValue(this XmlElement element, string path)
        {
            var xmlElement = element[path];
            return xmlElement != null ? xmlElement.InnerText.Trim() : string.Empty;
        }

        public static string GetParamName(this MethodInfo method, int index)
        {
            var retVal = string.Empty;

            if (method != null && method.GetParameters().Length > index)
                retVal = method.GetParameters()[index].Name;

            return retVal;
        }

        public static SyntaxTree GetSyntaxTree(string filename)
        {
            using (var stream = File.OpenRead(filename))
            {
                var syntaxTree = CSharpSyntaxTree.ParseText(SourceText.From(stream), path: filename);
                return syntaxTree;
            }
        }

        public static MethodDeclarationSyntax GetMethodDeclarationSyntax(SyntaxTree syntaxTree, string methodName, int paramNumber)
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