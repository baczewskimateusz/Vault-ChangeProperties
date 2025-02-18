using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml.Linq;
using Autodesk.Connectivity.Explorer.Extensibility;
using Autodesk.Connectivity.Extensibility.Framework;
using Autodesk.Connectivity.WebServices;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections;
using ACW = Autodesk.Connectivity.WebServices;

[assembly: ApiVersion("15.0")]
[assembly: ExtensionId("735b0a6d-d471-48f9-bb67-d3a49e5242a6")]

namespace ChangeProperties
{
   
    public class ChangePropertiesCommandExtension : IExplorerExtension
    {
       
        public IEnumerable<CommandSite> CommandSites()
        {
            CommandSite fileContextCmdSite = new CommandSite("ChangePropertiesCommand.FileContextMenu", "Zmiana w³aœciwoœci itemu")
            {
                Location = CommandSiteLocation.FileContextMenu,
                DeployAsPulldownMenu = true
            };

            CommandItem changePropertiesCmdItemIam = new CommandItem("ChangePropertiesCommandIam", "Itemy wszystkich plików")
            {
                NavigationTypes = new SelectionTypeId[] { SelectionTypeId.File, SelectionTypeId.FileVersion },
                MultiSelectEnabled = false,
            };

            CommandItem changePropertiesCmdItemMultiple = new CommandItem("ChangePropertiesCommandIpt", "Itemy zaznaczonych plików")
            {
                NavigationTypes = new SelectionTypeId[] { SelectionTypeId.File, SelectionTypeId.FileVersion },
                MultiSelectEnabled = true,
            };

            fileContextCmdSite.AddCommand(changePropertiesCmdItemIam);
            fileContextCmdSite.AddCommand(changePropertiesCmdItemMultiple);
            changePropertiesCmdItemIam.Execute += ChangePropertiesCommandHandler;
            changePropertiesCmdItemMultiple.Execute += ChangePropertiesCommandHandler;
            List<CommandSite> sites = new List<CommandSite>();
            sites.Add(fileContextCmdSite);
            return sites;
        }

        void ChangePropertiesCommandHandler(object sender, CommandItemEventArgs e)
        {
            Connection vaultConn = e.Context.Application.Connection;
            long[] selectionId = e.Context.CurrentSelectionSet.Select(n => n.Id).ToArray();

            File[] selectionFiles = vaultConn.WebServiceManager.DocumentService.GetLatestFilesByMasterIds(selectionId);

            #region Lista Wszystkich W³aœciwoœci Wraz z Nazwami

            ACW.PropDef[] propDefsItem = vaultConn.WebServiceManager.PropertyService.GetPropertyDefinitionsByEntityClassId("ITEM").Where(n => n.IsAct == true && !n.IsSys).ToArray();

            
            #endregion

            List<string> propDefName = new List<string>();
            var propDefsItemIds = new HashSet<long>(propDefsItem.Select(p => p.Id));


            foreach (PropDef propDef in propDefsItem)
            {
                if (!propDefsItemIds.Contains(propDef.Id))
                {
                    propDefName.Add(propDef.DispName);
                }
            }

            #region Wyszukiwanie Powi¹zanych Plików
            CommandItem commandItem = (CommandItem)sender;
            List<File> allFiles = new List<File>();
            if (commandItem.Label == "Itemy wszystkich plików")
            {
                FileAssocLite[] associationFiles = vaultConn.WebServiceManager.DocumentService.GetFileAssociationLitesByIds(
                new long[] { selectionFiles[0].Id },
                FileAssocAlg.LatestTip,
                FileAssociationTypeEnum.Dependency,
                false,
                FileAssociationTypeEnum.Dependency,
                true,
                false,
                false,
                false
                );

                long[] assocDirectFiles = associationFiles
                    .Where(file => !file.ExpectedVaultPath.Contains("Biblioteka") & !file.ExpectedVaultPath.Contains("Content Center"))
                    .Select(n => n.CldFileId).ToArray();

                File[] assocFilesArray = vaultConn.WebServiceManager.DocumentService.GetFilesByIds(assocDirectFiles);

                allFiles.Add(selectionFiles[0]);

                foreach (File file in assocFilesArray)
                {
                    if (!allFiles.Any(f => f.Name == file.Name))
                    {
                        allFiles.Add(file);
                    }
                }
            }
            else

            {
                allFiles = selectionFiles.ToList();
            }


            Dictionary<File, Item> filesItems = GetItems(allFiles, vaultConn);

            List<Item> items = filesItems.Values.ToList();
            List<File> files = filesItems.Keys.ToList();

                        
            #endregion

            #region Lista w³asciwosci dla wszytkich plików
            List <PropDef> propList = CreatePropertyList(vaultConn, propDefsItem, items);
            #endregion
      
            #region tworzenie listy plikow AssemblyFile
            List<AssemblyFile> assemblyFiles = new List<AssemblyFile>();

            Dictionary<long, string> propDefsDictionary = propDefsItem
               .ToDictionary(
                   p => p.Id,
                   p => Regex.Replace(p.DispName, @"[/\[\]]|kg/m", match =>
                                match.Value == "/" ? "lub" : (match.Value == "kg/m" ? "kg na m" : " ")
                            ).Trim()
               );
            Dictionary<long, PropDef> propDefsIdDict = propDefsItem.ToDictionary(p => p.Id, p => p);

            //Stopwatch stopwatch = Stopwatch.StartNew();
            Parallel.ForEach(files, file =>
            {
                var assemblyFile = new AssemblyFile(file, vaultConn, propList, propDefsDictionary, propDefsIdDict);
                lock (assemblyFiles) { assemblyFiles.Add(assemblyFile); }
            });
            //stopwatch.Stop();
            //MessageBox.Show($"Allfiles : {stopwatch.ElapsedMilliseconds} ms");
            #endregion

            #region Wyœwietlenie Okna WPF

            MainWindow mainWindow = new MainWindow(assemblyFiles, propList, vaultConn);
            mainWindow.ShowDialog();

            #endregion
        }

        public Dictionary<File,Item> GetItems(List<File> files, Connection vaultConnection)
        {
            Dictionary<File, Item> filesItems = new Dictionary<File, Item>();
            foreach (File file in files)
            {
                try
                {
                    Item item = vaultConnection.WebServiceManager.ItemService.GetItemsByFileId(file.Id).First();
                    filesItems.Add(file, item);
                }
                catch{}
            }

            return filesItems;   
        }
        public List<PropDef> CreatePropertyList(Connection vaultConn, PropDef[] propDefs, List<Item> allItems)
        {
            bool assemblyItemProcessed = false;
            bool weldedItemProcessed = false;
            bool partItemProcessed = false;
            bool sheetmetalItemProcessed = false;

            HashSet<PropDef> itemUserProperties = new HashSet<PropDef>();
           
            foreach (Item oItem in allItems)
            {
                string itemCatName = oItem.Cat.CatName;
                if (assemblyItemProcessed && weldedItemProcessed && partItemProcessed && sheetmetalItemProcessed)
                {
                    break;
                }
                if (itemCatName == "Zespó³" && !assemblyItemProcessed)
                {
                    assemblyItemProcessed = true;
                }
                else if (itemCatName == "Czêœæ" && !partItemProcessed)
                {
                    partItemProcessed = true;
                }
                else if (itemCatName == "Zespó³ spawany" && !weldedItemProcessed)
                {
                    weldedItemProcessed = true;
                }
                else if (itemCatName == "Blacha" && !sheetmetalItemProcessed)
                {
                    sheetmetalItemProcessed = true;
                }
                else
                {
                    continue;
                }
                ACW.PropInst[] itemProperties = vaultConn.WebServiceManager.PropertyService.GetPropertiesByEntityIds("ITEM", new long[] { oItem.Id });

                var currentItemProperties = propDefs
                    .Where(prop => itemProperties.Any(p => p.PropDefId == prop.Id));

                foreach (var property in currentItemProperties)
                {
                    itemUserProperties.Add(property);
                }
            }

            return itemUserProperties.OrderBy(p => p.DispName).ToList();

        }

        public IEnumerable<CustomEntityHandler> CustomEntityHandlers()
        {
            return null;
        }
        public IEnumerable<DetailPaneTab> DetailTabs()
        {
            return null;
        }
        public IEnumerable<string> HiddenCommands()
        {
            return null;
        }
        public void OnLogOff(IApplication application)
        {
        }
        public void OnLogOn(IApplication application)
        {

        }
        public void OnShutdown(IApplication application)
        {
        }
        public void OnStartup(IApplication application)
        {
        }
    }
}
