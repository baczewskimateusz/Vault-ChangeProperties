using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Connectivity.WebServices;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Properties;

namespace ChangeProperties
{
    public partial class MainWindow : Window
    {
        private MainViewModel viewModel;
        private Connection _vaultConn;
        public MainWindow(List<AssemblyFile> assemblyFiles, List<PropDef> propDefs, Connection vaultConn)
        {
            _vaultConn = vaultConn;
            InitializeComponent();

            CreateColumn(propDefs);
            viewModel = new MainViewModel(assemblyFiles, propDefs, vaultConn);
            this.DataContext = viewModel;
        }
        //private void OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        //{
        //    var row = e.Row.Item;
        //    var columnName = e.Column.Header.ToString();

        //    object newValue = null;

        //    if (e.EditingElement is TextBox textBox)
        //    {
        //        if (decimal.TryParse(textBox.Text, out var numericValue))
        //        {
        //            newValue = numericValue;
        //        }
        //        else
        //        {
        //            newValue = textBox.Text;
        //        }
        //    }
        //    else if (e.EditingElement is ComboBox comboBox)
        //    {
        //        newValue = comboBox.SelectedItem;
        //    }
        //    else if (e.EditingElement is CheckBox checkBox)
        //    {
        //        newValue = checkBox.IsChecked;
        //    }
        //    viewModel.UpdateProperty(row, columnName, newValue);
        //}

        private void OnCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            var row = e.Row.Item;
            if (row == null) return;

            string propertyName = null;

            if (e.Column is DataGridBoundColumn boundColumn && boundColumn.Binding is Binding binding)
            {
                propertyName = binding.Path?.Path;
            }

            if (string.IsNullOrEmpty(propertyName)) return;

            object oldValue = null;

            if (row is IDictionary<string, object> expando)
            {
                expando.TryGetValue(propertyName, out oldValue);  // Pobranie wartości
            }
            else
            {
                Console.WriteLine("Row is not an ExpandoObject!");
                return;
            }

            object newValue = null;

            if (e.EditingElement is TextBox textBox)
            {
                if (decimal.TryParse(textBox.Text, out var numericValue))
                {
                    newValue = numericValue;
                }
                else
                {
                    newValue = textBox.Text;
                }
            }
            else if (e.EditingElement is ComboBox comboBox)
            {
                newValue = comboBox.SelectedItem;
            }
            else if (e.EditingElement is CheckBox checkBox)
            {
                newValue = checkBox.IsChecked;
            }
            if ((oldValue == null && newValue != null) || (oldValue != null && !oldValue.Equals(newValue)))
            {
                expando[propertyName] = newValue;  // Aktualizacja dynamicznej właściwości

                viewModel.UpdateProperty(row, propertyName, newValue);
            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            if (comboBox == null || !comboBox.IsLoaded) 
                return;
        
            var selectedValue = comboBox.SelectedItem;

            var dataGridRow = FindParent<DataGridRow>(comboBox);
            if (dataGridRow == null)
                return;

            var rowData = dataGridRow.Item;
            var dataGridCell = FindParent<DataGridCell>(comboBox);
            var columnHeader = dataGridCell?.Column.Header.ToString();

            viewModel.UpdateProperty(rowData, columnHeader, selectedValue);
        }

        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null)
                return null;

            if (parentObject is T parent)
                return parent;

            return FindParent<T>(parentObject);
        }

        public void CreateColumn(List<PropDef> propDefs)
        {
            DynamicDataGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Nazwa Części",
                Binding = new Binding("Nazwa Części"),
                Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
                IsReadOnly = true,
                CellStyle = new Style(typeof(DataGridCell))
                {
                    Setters =
                    {
                        new Setter(Control.BackgroundProperty, Brushes.LightGray),
                        new Setter(Control.ForegroundProperty, Brushes.Black),    
                        new Setter(Control.FontWeightProperty, FontWeights.Bold),
                        new Setter(DataGridCell.IsHitTestVisibleProperty, false)
                    }
                }
            });

            Dictionary<string, DataType> types = new Dictionary<string, DataType>();
            Dictionary<string, bool> mappingDirection = new Dictionary<string, bool>();

            foreach (PropDef propDef in propDefs)
            {
                string propName = propDef.DispName;

                //if (propName.Contains("/"))
                //    propName = propName.Replace("/", "lub");

                if (Regex.IsMatch(propName, @"[/\[\]]"))
                {
                    propName = Regex.Replace(propName, @"[/\[\]]", match => match.Value == "/" ? "lub" : " ").TrimEnd();
                }

                types.Add(propName, propDef.Typ);

                mappingDirection.Add(propName, IsTwoDirectionMapping(propDef));
            } 

            foreach (var type in types)
            {
                DataGridColumn dataGridColumn;
                string name = type.Key;
                bool readOnlyColumn = false;

                if(mappingDirection[name] == false)
                {
                    readOnlyColumn = true;
                }
                
                switch (type.Value)
                {
                    case DataType.Bool:
                        var comboBoxFactory = new FrameworkElementFactory(typeof(ComboBox));

                        comboBoxFactory.SetValue(ComboBox.ItemsSourceProperty, new List<bool> { true, false });

                        comboBoxFactory.SetBinding(ComboBox.SelectedItemProperty, new Binding(name)
                        {
                            Mode = BindingMode.TwoWay,
                            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                        });

                        comboBoxFactory.SetValue(ComboBox.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                        comboBoxFactory.SetValue(ComboBox.VerticalAlignmentProperty, VerticalAlignment.Center);

                        comboBoxFactory.AddHandler(ComboBox.SelectionChangedEvent, new SelectionChangedEventHandler(ComboBox_SelectionChanged));

                        var comboBoxTemplate = new DataTemplate
                        {
                            VisualTree = comboBoxFactory
                        };

                        dataGridColumn = new DataGridTemplateColumn
                        {
                            Header = name,
                            CellTemplate = comboBoxTemplate,
                            CellEditingTemplate = comboBoxTemplate,
                            IsReadOnly = readOnlyColumn
                        };
                        break;
                    //case DataType.Numeric:
                    //    dataGridColumn = new DataGridTextColumn
                    //    {
                    //        Header = name,
                    //        Binding = new Binding(name),
                    //        Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
                    //        EditingElementStyle = new Style(typeof(TextBox))
                    //        {
                    //            Setters = { new Setter(TextBox.InputScopeProperty, new InputScope { Names = { new InputScopeName { NameValue = InputScopeNameValue.Number } } }) }
                    //        },
                    //        IsReadOnly = readOnlyColumn
                    //    };
                    //    break;
                    case DataType.Numeric:
                        var numericTextBoxStyle = new Style(typeof(TextBox));
                        numericTextBoxStyle.Setters.Add(new Setter(TextBox.InputScopeProperty, new InputScope
                        {
                            Names = { new InputScopeName { NameValue = InputScopeNameValue.Number } }
                        }));
                        numericTextBoxStyle.Setters.Add(new EventSetter(TextBox.PreviewTextInputEvent, new TextCompositionEventHandler(NumericOnly_PreviewTextInput)));

                        dataGridColumn = new DataGridTextColumn
                        {
                            Header = name,
                            Binding = new Binding(name)
                            {
                                Mode = BindingMode.TwoWay,
                                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                            },
                            Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
                            EditingElementStyle = numericTextBoxStyle,
                            IsReadOnly = readOnlyColumn
                        };
                        break;

                    case DataType.DateTime:
                        dataGridColumn = new DataGridTemplateColumn
                        {
                            Header = name,
                            CellTemplate = (DataTemplate)FindResource("DateTimeCellTemplate"),
                            CellEditingTemplate = (DataTemplate)FindResource("DateTimeEditTemplate"),
                            Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
                            IsReadOnly = readOnlyColumn
                        };
                        break;
                    case DataType.String:
                    default:
                        dataGridColumn = new DataGridTextColumn
                        {
                            Header = name,
                            Binding = new Binding(name),
                            Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
                            IsReadOnly = readOnlyColumn

                        };
                        break;
                }
                DynamicDataGrid.Columns.Add(dataGridColumn);
            }
            foreach (DataGridColumn column in DynamicDataGrid.Columns)
            {
                if (column.IsReadOnly)
                {
                    column.CellStyle = new Style(typeof(DataGridCell))
                    {
                    Setters =
                    {
                        new Setter(Control.BackgroundProperty, Brushes.LightGray),
                        new Setter(Control.ForegroundProperty, Brushes.Black),
                        new Setter(Control.FontWeightProperty, FontWeights.Bold),
                        new Setter(DataGridCell.IsHitTestVisibleProperty, false)
                    }
                    };
                }
            }
        }
        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = sender as TextBox;

            if (!IsValidNumericInput(textBox, e.Text))
            {
                e.Handled = true;
            }
        }

        private bool IsValidNumericInput(TextBox textBox, string newText)
        {
            if (newText.All(char.IsDigit) ||
                (newText == "," && !textBox.Text.Contains(",")) ||
                (newText == "." && !textBox.Text.Contains(".")))
            {
                return true;
            }
            return false;
        }

        private bool IsTwoDirectionMapping(PropDef propDef)
        {

            PropDefInfo propDefInfo = _vaultConn.WebServiceManager.PropertyService.GetPropertyDefinitionInfosByEntityClassId("FILE", new long[] { propDef.Id }).First();

            if (propDefInfo.EntClassCtntSrcPropCfgArray != null)
            {
                EntClassCtntSrcPropCfg ctntSrcPropCfg = propDefInfo.EntClassCtntSrcPropCfgArray.Where(p => p.EntClassId == "FILE").First();

                Autodesk.Connectivity.WebServices.MappingDirection[] mappingDirections = ctntSrcPropCfg.MapDirectionArray;

                foreach (Autodesk.Connectivity.WebServices.MappingDirection mappingDirection in mappingDirections)
                {
                    if (mappingDirection == Autodesk.Connectivity.WebServices.MappingDirection.Write) return true;
                }
                return false;
            }

            return true;
        }
    }
}