using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Xml;
using Autodesk.Connectivity.Explorer.Extensibility;
using Autodesk.Connectivity.Extensibility.Framework;
using Autodesk.Connectivity.WebServices;
using Autodesk.DataManagement.Client.Framework.Currency;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections;
using ChangeProperties.Models;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;
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
            //Stopwatch stopwatchAll = Stopwatch.StartNew();
            Connection vaultConn = e.Context.Application.Connection;
            long[] selectionId = e.Context.CurrentSelectionSet.Select(n => n.Id).ToArray();

            File[] selectionFiles = vaultConn.WebServiceManager.DocumentService.GetLatestFilesByMasterIds(selectionId);

            LoadingWindowHelper loadingWindowHelper = new LoadingWindowHelper();
            loadingWindowHelper.ShowProgressWindow();

            #region Lista Wszystkich W³aœciwoœci Wraz z Nazwami
            ACW.PropDef[] propDefsItem = vaultConn.WebServiceManager.PropertyService.GetPropertyDefinitionsByEntityClassId("ITEM").Where(n => n.IsAct == true && !n.IsSys).ToArray();

            HashSet<long> propDefsItemIds = new HashSet<long>(propDefsItem.Select(p => p.Id));
            #endregion

            List<File> allFiles = GetAllAssociatedFiles(sender, vaultConn, selectionFiles);

            Dictionary<File, Item> filesItems = GetItems(allFiles, vaultConn);
            List<Item> items = filesItems.Values.ToList();
            List<File> files = filesItems.Keys.ToList();


            #region Lista w³asciwosci dla wszytkich plików
            List<PropDef> propList = CreatePropertyList(vaultConn, propDefsItem, items);
            #endregion

            #region tworzenie s³owników w³aœciwoœci (id/nazwa) (id/property)
            Dictionary<long, string> propDefsDictionary;
            Dictionary<long, PropDef> propDefsIdDict;

            CreatePropertiesDictionary(propDefsItem, out propDefsDictionary, out propDefsIdDict);
            #endregion


            #region tworzenie listy plikow AssemblyFile
            var filesArray = filesItems.ToArray();
            var tempArray = new AssemblyFile[filesArray.Length];


            Parallel.For(0, filesArray.Length, i =>
            {
                var kvp = filesArray[i];
                tempArray[i] = new AssemblyFile(kvp.Key, kvp.Value, vaultConn, propList, propDefsDictionary, propDefsIdDict);
            });

            List<AssemblyFile> assemblyFiles = new List<AssemblyFile>();
            assemblyFiles.AddRange(tempArray);


            //stopwatch.Stop();
            //MessageBox.Show($"Allfiles : {stopwatch.ElapsedMilliseconds} ms");
            #endregion

            #region Wyœwietlenie MainWindow oraz rejestracja Eventu do zamkniêcia LoadingWindow
            MainWindow mainWindow = new MainWindow(assemblyFiles, propList, vaultConn);

            mainWindow.ContentRendered += (s, args) =>
            {
                loadingWindowHelper.CloseProgressWindow();
            };
            mainWindow.ShowDialog();
            #endregion

            //stopwatchAll.Stop();
            //MessageBox.Show($"stopwatchAll : {stopwatchAll.ElapsedMilliseconds} ms");

        }

        private static void CreatePropertiesDictionary(PropDef[] propDefsItem, out Dictionary<long, string> propDefsDictionary, out Dictionary<long, PropDef> propDefsIdDict)
        {
            var regex = new Regex(@"[/\[\]]|kg/m", RegexOptions.Compiled);
            var concurrentDict = new ConcurrentDictionary<long, string>();

            Parallel.ForEach(propDefsItem, p =>
            {
                string processed = regex.Replace(p.DispName, match =>
                    match.Value switch
                    {
                        "/" => "lub",
                        "kg/m" => "kg na m",
                        _ => " "
                    }).Trim();

                concurrentDict.TryAdd(p.Id, processed);
            });

            propDefsDictionary = new Dictionary<long, string>(concurrentDict);
            propDefsIdDict = propDefsItem.ToDictionary(p => p.Id, p => p);
        }

        private static List<File> GetAllAssociatedFiles(object sender, Connection vaultConn, File[] selectionFiles)
        {
            CommandItem commandItem = (CommandItem)sender;
            List<File> allFiles = new List<File>();
            if (commandItem.Label == "Itemy wszystkich plików")
            {
                FilePathArray associationFiles = vaultConn.WebServiceManager.DocumentService.GetLatestAssociatedFilePathsByMasterIds(
                    new long[] { selectionFiles[0].MasterId },
                    FileAssociationTypeEnum.None,
                    false,
                    FileAssociationTypeEnum.Dependency,
                    true,
                    false,
                    false,
                    false
                ).First();

                var selectedFile = selectionFiles[0].Name;

                allFiles = associationFiles.FilePaths
                    .Select((file, index) => new { File = file, Index = index })
                    .Where(item => !item.File.Path.ToString().Contains("Biblioteka") && !item.File.Path.ToString().Contains("Content Center"))
                    .OrderBy(item => item.File.File.Name == selectedFile ? 0 : 1)
                    .ThenBy(item => item.File.File.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(item => item.File.File)
                    .ToList();
            }
            else
            {
                allFiles = selectionFiles.ToList();
            }

            return allFiles;
        }

        public Dictionary<File, Item> GetItems(List<File> files, Connection vaultConnection)
        {
            var itemsBag = new ConcurrentBag<KeyValuePair<File, Item>>();

            Parallel.ForEach(files, file =>
            {
                try
                {
                    var item = vaultConnection.WebServiceManager.ItemService.GetItemsByFileId(file.Id).FirstOrDefault();
                    if (item != null)
                    {
                        itemsBag.Add(new KeyValuePair<File, Item>(file, item));
                    }
                }
                catch
                {
                }
            });

            return files
                .Where(file => itemsBag.Any(kvp => kvp.Key == file))
                .ToDictionary(file => file, file => itemsBag.First(kvp => kvp.Key == file).Value);
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
                    .Where(prop => itemProperties.Any(p => p.PropDefId == prop.Id))
                    .ToList(); 

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
