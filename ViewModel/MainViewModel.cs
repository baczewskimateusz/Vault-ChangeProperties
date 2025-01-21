using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Dynamic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Connectivity.Explorer.ExtensibilityTools;
using Autodesk.Connectivity.WebServices;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections;
using GalaSoft.MvvmLight.Command;
using ACET = Autodesk.Connectivity.Explorer.ExtensibilityTools;
using ACW = Autodesk.Connectivity.WebServices;

namespace ChangeProperties
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private List<AssemblyFile> _assemblyFiles;
        private List<AssemblyFile> _currentAssemblyFiles = new List<AssemblyFile> { };
        private Connection _vaultConn;
        public ObservableCollection<dynamic> Rows { get; set; } = new ObservableCollection<dynamic>();
        public List<PropDef> PropertyDefinitions { get; set; } = new List<PropDef>();
        public ICommand SaveCommand { get; set; }
        public ICommand ReloadCommand { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        public MainViewModel(List<AssemblyFile> assemblyFiles, List<PropDef> propDefs, Connection vaultConn)
        {
            CloneListOfFiles(assemblyFiles);
            _assemblyFiles = assemblyFiles;
            _vaultConn = vaultConn;
            PropertyDefinitions = propDefs;
            ConvertToDynamicRows();

            SaveCommand = new RelayCommand(OnSave);
            ReloadCommand = new RelayCommand(OnRefresh);
        }

        public void CloneListOfFiles(List<AssemblyFile> assemblyFiles)
        {
            _currentAssemblyFiles = assemblyFiles.Select(file => new AssemblyFile(file.File, file.VaultConn, file.PropDefs.Select(p => p).ToList())
            { }).ToList();
        }

        private void OnSave()
        {
            foreach(AssemblyFile assemblyFile in _assemblyFiles)
            {
                if(assemblyFile.ChangedProperties.Count > 0) 
                {
                    UpdateFileProperties(assemblyFile.File, assemblyFile.ChangedProperties);
                }
                assemblyFile.ChangedProperties.Clear();
            }
            System.Windows.MessageBox.Show("Zapisano");
            
            CloneListOfFiles(_assemblyFiles);

        }
        private void OnRefresh()
        {
            _assemblyFiles = _currentAssemblyFiles;
            ConvertToDynamicRows();
            CloneListOfFiles(_assemblyFiles);
            
        }

        private void UpdateFileProperties(File file, Dictionary<ACW.PropDef, object> mPropDictonary)
        {
            ACET.IExplorerUtil mExplUtil = ExplorerLoader.LoadExplorerUtil(
            _vaultConn.Server, _vaultConn.Vault, _vaultConn.UserID, _vaultConn.Ticket);
   
            mExplUtil.UpdateFileProperties(file, mPropDictonary);
            
        }
        private void ConvertToDynamicRows()
        {
            Rows.Clear();
            foreach (var file in _assemblyFiles)
            {
                var row = new System.Dynamic.ExpandoObject() as IDictionary<string, object>;

                row["AssemblyFile"] = file;
                row["Nazwa Części"] = file.File.Name;

                foreach (var propDef in PropertyDefinitions)
                {
                    //var displayName = propDef.DispName.Contains("/") ? propDef.DispName.Replace("/", "lub") : propDef.DispName;
                    var displayName = Regex.Replace(propDef.DispName, @"[/\[\]]", match => match.Value == "/" ? "lub" : " ").TrimEnd();

                    var property = file.Properties.FirstOrDefault(p => p.Name == displayName);
                    row[displayName] = property != null ? property.Value : "";
                }
                Rows.Add(row);
                file.ChangedProperties.Clear();
            }
        }

        public void UpdateProperty(dynamic row, string propertyName, object newValue)
        {
            
            //var modifiedPropertyName = propertyName.Contains("/") ? propertyName.Replace("/", "lub") : propertyName;
            var modifiedPropertyName = Regex.Replace(propertyName, @"[/\[\]]", match => match.Value == "/" ? "lub" : " ").TrimEnd();
            var assemblyFile = row.AssemblyFile as AssemblyFile;

            if (assemblyFile != null)
            {
                var fileProperty = assemblyFile.Properties.FirstOrDefault(p => p.Name == modifiedPropertyName);

                if (fileProperty != null)
                {
                    fileProperty.Value = newValue;
                    if (assemblyFile.ChangedProperties.ContainsKey(fileProperty.PropertyDef))
                    {
                        assemblyFile.ChangedProperties[fileProperty.PropertyDef] = newValue;
                    }
                    else
                    {
                        assemblyFile.ChangedProperties.Add(fileProperty.PropertyDef, newValue);
                    }
                }
            }
        }
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}