using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Connectivity.WebServices;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections;

namespace ChangeProperties
{
    public partial class MainWindow : Window
    {
        private MainViewModel viewModel;
        private Connection _vaultConn;
        public MainWindow(List<AssemblyFile> assemblyFiles, List<PropDef> propDefs, Connection vaultConn)
        {
            InitializeComponent();

            _vaultConn = vaultConn;
            
            var iptIcon = (BitmapImage)Resources["IptIcon"];
            var iamIcon = (BitmapImage)Resources["IamIcon"];

            
            CreateColumn(propDefs);
            Stopwatch stopwatch = Stopwatch.StartNew();
            //stopwatch.Stop();
            //MessageBox.Show($"Create column : {stopwatch.ElapsedMilliseconds} ms");

            //stopwatch.Restart();
            viewModel = new MainViewModel(assemblyFiles, propDefs, vaultConn, iptIcon, iamIcon);
            

            DataContext = viewModel;
            stopwatch.Stop();
            MessageBox.Show($"MainViewModel : {stopwatch.ElapsedMilliseconds} ms");


        }

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
                expando.TryGetValue(propertyName, out oldValue);  
            }
            else
            {
                Console.WriteLine("Row is not an ExpandoObject!");
                return;
            }

            object newValue = null;


            if (e.EditingElement is TextBox textBox)
            {
                

                if (float.TryParse(textBox.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out float numericValue) ||
                    float.TryParse(textBox.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out numericValue))
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

            if (oldValue != null)
            {
                oldValue = oldValue.ToString();
                newValue = newValue.ToString();
            }

            if ((oldValue == null && newValue != null && newValue!="") || (oldValue != null && !oldValue.Equals(newValue)))
            {
                expando[propertyName] = newValue;  

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
            DataGridTemplateColumn iconColumn = new DataGridTemplateColumn
            {
                Header = "Ikona",
                IsReadOnly = true,
                CellTemplate = new DataTemplate()
            };

            FrameworkElementFactory imageFactory = new FrameworkElementFactory(typeof(Image));
            imageFactory.SetValue(Image.WidthProperty, 20.0);
            imageFactory.SetBinding(Image.SourceProperty, new Binding("FileIcon"));
            iconColumn.CellTemplate.VisualTree = imageFactory;
            DynamicDataGrid.Columns.Add(iconColumn);

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
                        new Setter(DataGridCell.IsHitTestVisibleProperty, false),
                        new Setter(Control.BackgroundProperty, Brushes.LightGray)
                    }
                },

                ElementStyle = new Style(typeof(TextBlock))
                {
                    Setters =
                    {
                        new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center),
                        new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center),
                        new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center),
                        new Setter(TextBlock.FontSizeProperty, 13.0),
                        new Setter(TextBlock.PaddingProperty, new Thickness(5)),
                        new Setter(Control.FontWeightProperty, FontWeights.Bold),

                    }
                }
            });

            Dictionary<string, DataType> types = new Dictionary<string, DataType>();
            Dictionary<string, bool> mappingDirection = new Dictionary<string, bool>();

            foreach (PropDef propDef in propDefs)
            {
                string propName = propDef.DispName;
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

                if (mappingDirection[name] == false)
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
                                UpdateSourceTrigger = UpdateSourceTrigger.LostFocus
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
                            IsReadOnly = readOnlyColumn,

                        };
                        break;
                }
                DynamicDataGrid.Columns.Add(dataGridColumn);
            }
            foreach (DataGridColumn column in DynamicDataGrid.Columns)
            {
                if (column.Header.ToString() != "Nazwa Części")
                {
                    if (column is DataGridTextColumn textColumn)
                    {
                        textColumn.ElementStyle = new Style(typeof(TextBlock))
                        {
                            Setters =
                            {
                            new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center),
                            new Setter(TextBlock.HorizontalAlignmentProperty, HorizontalAlignment.Center),
                            new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Center),
                            new Setter(TextBlock.PaddingProperty, new Thickness(3))
                            }
                        };
                    }
                    if (column.IsReadOnly)
                    {
                        column.CellStyle = new Style(typeof(DataGridCell))
                        {
                            Setters =
                            {
                            new Setter(Control.BackgroundProperty, Brushes.LightGray),
                            new Setter(Control.ForegroundProperty, Brushes.Black),
                            new Setter(Control.FontWeightProperty, FontWeights.Bold),
                            new Setter(DataGridCell.IsHitTestVisibleProperty, false),

                            }
                        };
                    }
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