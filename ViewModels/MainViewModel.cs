using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;
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
        private Dictionary<string, string> _kolorWartosci { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private readonly BitmapImage _iptIcon;
        private readonly BitmapImage _iamIcon;

        public MainViewModel(List<AssemblyFile> assemblyFiles, List<PropDef> propDefs, Connection vaultConn, BitmapImage iptIcon, BitmapImage iamIcon, Dictionary<string, string> kolorWartosci)
        {

            _iptIcon = iptIcon;
            _iamIcon = iamIcon;
            _assemblyFiles = assemblyFiles;
            _vaultConn = vaultConn;
            _kolorWartosci = kolorWartosci;
            PropertyDefinitions = propDefs;
            ConvertToDynamicRows();

            SaveCommand = new RelayCommand(OnSave);

        }

        private void OnSave()
        {
            bool flag1=false;
            foreach (AssemblyFile assemblyFile in _assemblyFiles)
            {
                
                JobParam[] jobParams = new JobParam[assemblyFile.ChangedProperties.Count + 1];

                jobParams[0] = new JobParam()
                {
                    Name = "FileId",
                    Val = assemblyFile.File.Id.ToString()
                };

                int index = 1;
                foreach (var property in assemblyFile.ChangedProperties)
                {
                    jobParams[index] = new JobParam()
                    {
                        Name = property.Key,
                        Val = property.Value.ToString()
                    };
                    index++;
                }
                if (assemblyFile.ChangedProperties.Count > 0)
                {
                    _vaultConn.WebServiceManager.JobService.AddJob("KRATKI.UpdateProperties", $"KRATKI.UpdateProperties: {assemblyFile.File.Name}", jobParams, 1);
                    flag1 = true;
                }
                assemblyFile.ChangedProperties.Clear();
            }
            if (flag1)
            {
                System.Windows.MessageBox.Show("Zmiana właściwości została wysłana do kolejki zadań.", "Wysłanie zadań", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show("Żadne właściwość nie została zmieniona.", "Uwaga!", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }

        }

        private void UpdateFileProperties(ACW.File file, Dictionary<ACW.PropDef, object> mPropDictonary)
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

                string fileExtension = Path.GetExtension(file.File.Name).ToLower();
                BitmapImage icon = null;

                if (fileExtension == ".ipt")
                {
                    icon = _iptIcon;
                }
                else if (fileExtension == ".iam")
                {
                    icon = _iamIcon;
                }

                row["FileIcon"] = icon;

                foreach (var propDef in PropertyDefinitions)
                {
                    var displayName = Regex.Replace(propDef.DispName, @"[/\[\]]|kg/m", match =>
                                            match.Value == "/" ? "lub" : (match.Value == "kg/m" ? "kg na m" : " ")
                                        ).Trim();
                    var property = file.Properties.FirstOrDefault(p => p.Name == displayName);

                    if (displayName.ToLower().Contains("kolor") && property != null)
                    {
                        var propertyValue = property.Value?.ToString();

                        if (propertyValue != null && _kolorWartosci.TryGetValue(propertyValue, out var colorName))
                        {
                            row[displayName] = $"{propertyValue} | {colorName}";
                        }
                        else
                        {
                            row[displayName] = property.Value; 
                        }
                    }
                    else
                    {
                        row[displayName] = property != null ? property.Value : "";
                    }
                }
                
                Rows.Add(row);
                file.ChangedProperties.Clear();
            }
            
        }

        public void UpdateProperty(dynamic row, string propertyName, object newValue)
        {
            var modifiedPropertyName = Regex.Replace(propertyName, @"[/\[\]]|kg/m", match =>
                                                match.Value == "/" ? "lub" : (match.Value == "kg/m" ? "kg na m" : " ")
                                            ).Trim();

            var assemblyFile = row.AssemblyFile as AssemblyFile;

            if (assemblyFile != null)
            {
                var fileProperty = assemblyFile.Properties.FirstOrDefault(p => p.Name == modifiedPropertyName);

                if (fileProperty != null)
                {
                    string propertySysName = fileProperty.PropertyDef.SysName.ToString();

                    if (newValue is string combinedValue)
                    {
                        newValue = combinedValue.Split('|')[0].Trim();
                    }

                    object primaryValue = assemblyFile.PrimaryProperties[fileProperty.Name];
                    if (!newValue.Equals(primaryValue))
                    {
                        if (assemblyFile.ChangedProperties.ContainsKey(propertySysName))
                        {
                            assemblyFile.ChangedProperties[propertySysName] = newValue.ToString();
                        }
                        else
                        {
                            assemblyFile.ChangedProperties.Add(propertySysName, newValue.ToString());
                        }
                    }
                    else
                    {
                        if (assemblyFile.ChangedProperties.ContainsKey(propertySysName))
                        {
                            assemblyFile.ChangedProperties.Remove(propertySysName);
                        }
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