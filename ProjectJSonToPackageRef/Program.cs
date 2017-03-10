using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace ProjectJSonToPackageRef
{
    class Program
    {
        private const string MsBuildNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";

        static void Main(string[] args)
        {
            foreach(var projectJSON in Directory.EnumerateFiles(args[0], "project.json", SearchOption.AllDirectories))
            {
                var o = JObject.Parse(File.ReadAllText(projectJSON));

                var q = from dependencyNode in o.SelectTokens($"..dependencies")
                        from node in dependencyNode.Children().OfType<JProperty>()
                        select node;

                var csprojFiles = Directory.GetFiles(Path.GetDirectoryName(projectJSON), "*.csproj", SearchOption.TopDirectoryOnly);

                if (csprojFiles.Length > 1)
                {
                    Console.WriteLine($"Too many csproj files {projectJSON}");
                }
                else if (csprojFiles.Length == 0)
                {
                    Console.WriteLine($"Unable to find csproj file {projectJSON}");
                }
                else
                {
                    Console.WriteLine($"Processing {projectJSON}");
                    var d = new XmlDocument();
                    d.Load(csprojFiles.First());
                    var nsMgr = new XmlNamespaceManager(d.NameTable);
                    nsMgr.AddNamespace("x", MsBuildNamespace);

                    var refsNode = d.SelectSingleNode("//x:Reference", nsMgr);

                    var propertyGroupRoot = refsNode?.ParentNode ?? d.DocumentElement.FirstChild;

                    var itemGroup = d.CreateElement("", "ItemGroup", MsBuildNamespace);

                    foreach (var reference in q)
                    {
                        var packageRef = d.CreateElement("", "PackageReference", MsBuildNamespace);
                        packageRef.SetAttribute("Include", reference.Name);
                        var versionNode = d.CreateElement("", "Version", MsBuildNamespace);
                        versionNode.InnerText = reference.Value.Value<string>();
                        packageRef.AppendChild(versionNode);

                        itemGroup.AppendChild(packageRef);
                    }

                    propertyGroupRoot.ParentNode.InsertAfter(itemGroup, propertyGroupRoot);

                    var node = d.SelectSingleNode("//*[@Include='project.json']");

                    if(node != null)
                    {
                        node.ParentNode.RemoveChild(node);
                    }

                    d.Save(csprojFiles.First());

                    File.Delete(projectJSON);
                    File.Delete(Path.Combine(Path.GetDirectoryName(projectJSON), "project.lock.json"));
                }
            }
        }
    }
}
