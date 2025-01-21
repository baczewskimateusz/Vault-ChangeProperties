using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using Autodesk.Connectivity.Explorer.Extensibility;
using Autodesk.Connectivity.Extensibility.Framework;
using Autodesk.Connectivity.WebServices;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Properties;
using ACW = Autodesk.Connectivity.WebServices;

[assembly: ApiVersion("15.0")]
[assembly: ExtensionId("735b0a6d-d471-48f9-bb67-d3a49e5242a6")]

namespace ChangeProperties
{
    public class ChangePropertiesCommandExtension : IExplorerExtension
    {
       
        public IEnumerable<CommandSite> CommandSites()
        {
            CommandSite fileContextCmdSite = new CommandSite("ChangePropertiesCommand.FileContextMenu", "Zmiana w³aœciwoœci")
            {
                Location = CommandSiteLocation.FileContextMenu,
                DeployAsPulldownMenu = true
            };

            CommandItem changePropertiesCmdItemIam = new CommandItem("ChangePropertiesCommandIam", "Wszystkie pliki")
            {
                NavigationTypes = new SelectionTypeId[] { SelectionTypeId.File, SelectionTypeId.FileVersion },
                MultiSelectEnabled = false
            };

            CommandItem changePropertiesCmdItemMultiple = new CommandItem("ChangePropertiesCommandIpt", "Zaznaczone pliki")
            {
                NavigationTypes = new SelectionTypeId[] { SelectionTypeId.File, SelectionTypeId.FileVersion },
                MultiSelectEnabled = true
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

            ACW.PropDef[] propDefs = vaultConn.WebServiceManager.PropertyService.GetPropertyDefinitionsByEntityClassId("FILE");

            CommandItem commandItem = (CommandItem)sender;
            #endregion

            #region Wyszukiwanie Powi¹zanych Plików

            List<File> allFiles = new List<File>();
            if (commandItem.Label == "Wszystkie pliki")
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

                if (selectionFiles[0].Locked == false) allFiles.Add(selectionFiles[0]);

                foreach (File file in assocFilesArray)
                {
                    if (!allFiles.Contains(file))
                    {
                        allFiles.Add(file);
                    }
                }
            }
            else
            {
                allFiles = selectionFiles.ToList();
            }
            
            allFiles = allFiles.GroupBy(file => file.Id).Select(group => group.First()).ToList();
            #endregion

            #region Lista w³asciwosci dla wszytkich plików
            List<PropDef> propList = CreatePropertyList(vaultConn, propDefs, allFiles);
            #endregion

            #region tworzenie listy plikow AssemblyFile
            List<AssemblyFile> assemblyFiles = new List<AssemblyFile>();

            foreach (var file in allFiles)
            {
                assemblyFiles.Add(new AssemblyFile(file, vaultConn, propList));
            }
            #endregion

            #region Wyœwietlenie Okna WPF
            //// Tworzenie i wyœwietlanie okna
            MainWindow mainWindow = new MainWindow(assemblyFiles, propList, vaultConn);
            mainWindow.ShowDialog();
            #endregion
        }
        public void Refresh_ItemLinks(Item editItem, Connection vaultConn)
        {
            var linkTypeOptions = ItemFileLnkTypOpt.Primary
                | ItemFileLnkTypOpt.PrimarySub
                | ItemFileLnkTypOpt.Secondary
                | ItemFileLnkTypOpt.SecondarySub
                | ItemFileLnkTypOpt.StandardComponent
                | ItemFileLnkTypOpt.Tertiary;
            var assocs = vaultConn.WebServiceManager.ItemService.GetItemFileAssociationsByItemIds(
                new long[] { editItem.Id }, linkTypeOptions);
            vaultConn.WebServiceManager.ItemService.AddFilesToPromote(
                assocs.Select(x => x.CldFileId).ToArray(), ItemAssignAll.No, true);
            var promoteOrderResults = vaultConn.WebServiceManager.ItemService.GetPromoteComponentOrder(out DateTime timeStamp);
            if (promoteOrderResults.PrimaryArray != null
                && promoteOrderResults.PrimaryArray.Any())
            {
                vaultConn.WebServiceManager.ItemService.PromoteComponents(timeStamp, promoteOrderResults.PrimaryArray);
            }
            if (promoteOrderResults.NonPrimaryArray != null
                && promoteOrderResults.NonPrimaryArray.Any())
            {
                vaultConn.WebServiceManager.ItemService.PromoteComponentLinks(promoteOrderResults.NonPrimaryArray);
            }
            var promoteResult = vaultConn.WebServiceManager.ItemService.GetPromoteComponentsResults(timeStamp);
        }
        public List<PropDef> CreatePropertyList(Connection vaultConn, PropDef[] propDefs, List<File> allFiles)
        {
            bool assemblyFileProcessed = false;
            bool weldedFileProcessed = false;
            bool partFileProcessed = false;
            bool sheetmetalFileProcessed = false;

            HashSet<PropDef> fileUserProperties = new HashSet<PropDef>();

            foreach (File oFile in allFiles)
            {
                string catName = oFile.Cat.CatName;
                if (assemblyFileProcessed && weldedFileProcessed && partFileProcessed && sheetmetalFileProcessed)
                {
                    break;
                }
                if (catName == "Zespó³" && !assemblyFileProcessed)
                {
                    assemblyFileProcessed = true;
                }
                else if (catName == "Czêœæ" && !partFileProcessed)
                {
                    partFileProcessed = true;
                }
                else if (catName == "Zespó³ spawany" && !weldedFileProcessed)
                {
                    weldedFileProcessed = true;
                }
                else if (catName == "Blacha" && !sheetmetalFileProcessed)
                {
                    sheetmetalFileProcessed = true;
                }
                else
                {
                    continue;
                }

                ACW.PropInst[] fileProperties = vaultConn.WebServiceManager.PropertyService.GetPropertiesByEntityIds("FILE", new long[] { oFile.Id });

                var currentFileProperties = propDefs
                    .Where(prop => fileProperties.Any(p => p.PropDefId == prop.Id) && !prop.IsSys);

                foreach (var property in currentFileProperties)
                {
                    fileUserProperties.Add(property);
                }
            }

            return fileUserProperties.OrderBy(p => p.DispName).ToList();
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
