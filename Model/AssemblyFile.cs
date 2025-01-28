using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Connectivity.WebServices;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections;

namespace ChangeProperties
{
    public class AssemblyFile
    {
        public File File { get; set; }
        public Connection VaultConn { get; set; }
        public List<FileProperty> Properties { get; set; } = new List<FileProperty> { };

        public Dictionary<string, string> ChangedProperties { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, object> PrimaryProperties { get; set; } = new Dictionary<string, object>();
        public List<PropDef> PropDefs { get; set; }

        public AssemblyFile(File file, Connection vaultConn, List<PropDef> propDefs)
        {
            File = file;
            VaultConn = vaultConn;
            PropDefs = propDefs;
            CreatePropertiesList(PropDefs);
        }
        public void CreatePropertiesList(List<PropDef> propDefs)
        {
            PropInst[] fileProperties = VaultConn.WebServiceManager.PropertyService.GetPropertiesByEntityIds("FILE", new long[] { File.Id });

            Dictionary<long, string> propDefsDictionary = propDefs
                .ToDictionary(
                    p => p.Id,
                    p => Regex.Replace(p.DispName, @"[/\[\]]", match => match.Value == "/" ? "lub" : " ").TrimEnd()
                );
            Dictionary<long, PropDef> propDefsIdDict = propDefs.ToDictionary(p => p.Id, p => p);

            fileProperties = fileProperties
                .Where(prop => propDefsDictionary.ContainsKey(prop.PropDefId))
                .ToArray();

            foreach (PropInst propInst in fileProperties)
            {
                propDefsIdDict.TryGetValue(propInst.PropDefId, out PropDef propertyDef);

                if (propDefsDictionary.TryGetValue(propInst.PropDefId, out string name))
                {
                    Properties.Add(new FileProperty
                    {
                        Name = name,
                        PropertyType = propInst.ValTyp,
                        Value = propInst.Val,
                        PropertyDef = propertyDef
                    });

                    PrimaryProperties.Add(name, propInst.Val?.ToString() ?? null);
                }
            }
        }
    }
}
