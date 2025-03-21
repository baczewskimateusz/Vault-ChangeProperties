using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Autodesk.Connectivity.WebServices;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections;

namespace ChangeProperties
{
    public class AssemblyFile
    {
        public File File { get; set; }
        public Item Item { get; set; }
        public Connection VaultConn { get; set; }
        public List<FileProperty> Properties { get; set; } = new List<FileProperty> { };

        public Dictionary<string, string> ChangedProperties { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, object> PrimaryProperties { get; set; } = new Dictionary<string, object>();
        public List<PropDef> PropDefs { get; set; }

        public AssemblyFile(File file, Item item, Connection vaultConn, List<PropDef> propDefs, Dictionary<long, string> propDefsDictionary, Dictionary<long, PropDef> propDefsIdDict)
        {
            File = file;
            Item = item;
            VaultConn = vaultConn;
            PropDefs = propDefs;
            CreatePropertiesList(propDefsDictionary, propDefsIdDict);
        }
        public void CreatePropertiesList(Dictionary<long, string> propDefsDictionary, Dictionary<long, PropDef> propDefsIdDict)
        {
            PropInst[] itemProperties = VaultConn.WebServiceManager.PropertyService.GetPropertiesByEntityIds("ITEM", new long[] { Item.Id });

            itemProperties = itemProperties
                .Where(prop => propDefsDictionary.ContainsKey(prop.PropDefId))
                .ToArray();

            foreach (PropInst propInst in itemProperties)
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
