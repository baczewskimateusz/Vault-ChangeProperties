using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Autodesk.Connectivity.WebServices;
using Autodesk.DataManagement.Client.Framework.Vault.Currency.Connections;
using ChangeProperties.Models;
using ChangeProperties.Services;

namespace ChangeProperties
{
    public partial class MainWindow : Window
    {
        private MainViewModel viewModel;
        private Connection _vaultConn;
        private Dictionary<object, object> _oldValues = new Dictionary<object, object>();

        private string vendoUrl { get; set; }
        private string vendoToken { get; set; }
        private string vendoUserID { get; set; }
        private string obrobkaCieplna { get; set; }
        private string przygotowaniePow { get; set; }
        private string obrobkaPow { get; set; }
        private Dictionary<string, string> kolorWartosci { get;set; }
        private string malarnia { get; set; }
        private string kolor { get; set; }
        private bool _columnsVisible = true;

        Dictionary<string, List<string>> listBoxColumn;


        private static readonly Regex _propNameRegex = new Regex(@"[/\[\]]|kg/m", RegexOptions.Compiled);

        public MainWindow(List<AssemblyFile> assemblyFiles, List<PropDef> propDefs, Connection vaultConn)
        {
            InitializeComponent();

            _vaultConn = vaultConn;
            
            var iptIcon = (BitmapImage)Resources["IptIcon"];
            var iamIcon = (BitmapImage)Resources["IamIcon"];

            GetSettings();
            SetListBoxValues();


            CreateColumn(propDefs);
            SetupBlockedColumnVisibility();

 
            viewModel = new MainViewModel(assemblyFiles, propDefs, vaultConn, iptIcon, iamIcon, kolorWartosci);
            DataContext = viewModel;
           
        }

        private void SetupBlockedColumnVisibility()
        {
            foreach (var column in GetReadOnlyColumns())
            {
                var checkBox = new CheckBox
                {
                    Content = column.Header,
                    IsChecked = true,
                    Margin = new Thickness(5, 0, 5, 0),
                    Tag = column,
                };

                checkBox.Checked += (s, e) => ToggleColumnVisibility(column, true);
                checkBox.Unchecked += (s, e) => ToggleColumnVisibility(column, false);

            }
        }

        private void ToggleColumnsButton_Click(object sender, RoutedEventArgs e)
        {
            _columnsVisible = !_columnsVisible;
            UpdateToggleButtonText();
            ToggleAllColumnsVisibility(_columnsVisible);
        }

        private IEnumerable<DataGridColumn> GetReadOnlyColumns()
        {
            var readOnlyColumns = DynamicDataGrid.Columns.OfType<DataGridColumn>()
                                   .Skip(2)
                                   .Where(c => c.IsReadOnly && c.Header?.ToString() != "Numer")
                                   .ToList();

            return readOnlyColumns;
        }

        private void ToggleColumnVisibility(DataGridColumn column, bool isVisible)
        {
            column.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ToggleAllColumnsVisibility(bool isVisible)
        {
            foreach (var column in GetReadOnlyColumns())
            {
                ToggleColumnVisibility(column, isVisible);
            }
        }

        private void UpdateToggleButtonText()
        {
            ToggleColumnsButton.Content = _columnsVisible ? "Ukryj" : "Pokaż";
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

            if ((oldValue == null && newValue != null && newValue != "") || (oldValue != null && !oldValue.Equals(newValue)))
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

        public void GetSettings()
        {
            string xmlSet = _vaultConn.WebServiceManager.KnowledgeVaultService.GetVaultOption("ArkanceExportJobOptions");

            Settings settings = Settings.readfromXML(xmlSet);

            vendoUrl = settings.ERPURL;
            vendoToken = settings.ERPToken;
            vendoUserID = settings.ERPUser;
            obrobkaCieplna = settings.slownikObrobkaCieplna;
            przygotowaniePow = settings.slownikPrzygotowaniePow;
            obrobkaPow = settings.slownikObrobkaPowierzchni;
            malarnia = settings.slownikMalarnia;
            kolor = settings.slowniKolor;
        }

        public void SetListBoxValues()
        {
            UserDictionaries userDictionaries = DictionaryService.getDictionariesList(vendoUrl, vendoToken, vendoUserID);

            var names = new List<string>
            {
                obrobkaCieplna,
                przygotowaniePow,
                obrobkaPow,
                malarnia,
                kolor,
                "Część Zamienna",
                "Rodzaj zmatowienia",
                "Rodzaj wykończenia"
            };

            var dictionaries = DictionaryService.GetDictionariesByName(names, userDictionaries);

            Dictionary<string, string> obrobkaCieplnaWartosci = dictionaries[obrobkaCieplna];
            Dictionary<string, string> przygotowaniePowWartosci = dictionaries[przygotowaniePow];
            Dictionary<string, string> obrobkaPowWartosci = dictionaries[obrobkaPow];
            Dictionary<string, string> malarniaWartosci = dictionaries[malarnia];
            kolorWartosci = dictionaries[kolor];
            Dictionary<string, string> czescZamiennaWartosci = dictionaries["Część Zamienna"];
            Dictionary<string, string> rodzajZmatowieniaWartosci = dictionaries["Rodzaj zmatowienia"];
            Dictionary<string, string> rodzajWykończeniaWartosc = dictionaries["Rodzaj wykończenia"];


            List<string> obrobkaCieplnaList = obrobkaCieplnaWartosci.Keys.ToList();
            List<string> przygotowaniePowList = przygotowaniePowWartosci.Keys.ToList();
            List<string> obrobkaPowList = obrobkaPowWartosci.Keys.ToList();
            List<string> malarniaList = malarniaWartosci.Keys.ToList();
            List<string> czescZamiennaList = czescZamiennaWartosci.Keys.ToList();
            List<string> rodzajZmatowieniaList = rodzajZmatowieniaWartosci.Keys.ToList();
            List<string> rodzajWykończeniaList= rodzajWykończeniaWartosc.Keys.ToList();

            List<string> kolorList = kolorWartosci
            .Select(kvp => $"{kvp.Key} | {kvp.Value}")
            .ToList();

            kolorList.Insert(0, "");
            obrobkaCieplnaList.Insert(0, "");
            obrobkaPowList.Insert(0, "");
            przygotowaniePowList.Insert(0, "");
            malarniaList.Insert(0, "");
            czescZamiennaList.Insert(0, "");
            rodzajZmatowieniaList.Insert(0, "");
            rodzajWykończeniaList.Insert(0, "");

            listBoxColumn = new Dictionary<string, List<string>>
            {
                { obrobkaCieplna.ToLower(), obrobkaCieplnaList },
                { przygotowaniePow.ToLower(), przygotowaniePowList },
                { obrobkaPow.ToLower(), obrobkaPowList },
                { malarnia.ToLower(), malarniaList },
                { "kolor", kolorList},
                { "część zamienna", czescZamiennaList },
                { "rodzaj zmatowienia", rodzajZmatowieniaList },
                { "rodzaj wykończenia", rodzajWykończeniaList}
            };
        }

        public void CreateColumn(List<PropDef> propDefs)
        {
            DynamicDataGrid.EnableRowVirtualization = false;
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
                    propName = Regex.Replace(propDef.DispName, @"[/\[\]]|kg/m", match =>
                                    match.Value == "/" ? "lub" : (match.Value == "kg/m" ? "kg na m" : " ")
                                ).Trim();
                }

                types.Add(propName, propDef.Typ);

                mappingDirection.Add(propName, IsTwoDirectionMapping(propDef));
            }

            NumerHighlightConverter numerHighlightConverter = new NumerHighlightConverter();

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
                        if (listBoxColumn.ContainsKey(name.ToLower()))
                        {

                            var comboBoxFactory1 = new FrameworkElementFactory(typeof(ComboBox));
                            comboBoxFactory1.SetValue(ComboBox.ItemsSourceProperty, listBoxColumn[name.ToLower()]);
                            if (name.ToLower().Contains("kolor"))
                            {
                                var converter = new RalColorConverter(kolorWartosci);
                                comboBoxFactory1.SetBinding(ComboBox.SelectedItemProperty, new Binding(name)
                                {
                                    Mode = BindingMode.TwoWay,
                                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
                                    Converter = converter
                                });
                            }
                            else
                            {
                                comboBoxFactory1.SetBinding(ComboBox.SelectedItemProperty, new Binding(name)
                                {
                                    Mode = BindingMode.TwoWay,
                                    UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
                                });
                            }
                            
                            comboBoxFactory1.SetValue(ComboBox.HorizontalAlignmentProperty, HorizontalAlignment.Center);
                            comboBoxFactory1.SetValue(ComboBox.VerticalAlignmentProperty, VerticalAlignment.Center);
                            comboBoxFactory1.AddHandler(ComboBox.SelectionChangedEvent, new SelectionChangedEventHandler(ComboBox_SelectionChanged));

                            var comboBoxTemplate1 = new DataTemplate
                            {
                                VisualTree = comboBoxFactory1
                            };

                            dataGridColumn = new DataGridTemplateColumn
                            {
                                Header = name,
                                CellTemplate = comboBoxTemplate1,
                                CellEditingTemplate = comboBoxTemplate1,
                                IsReadOnly = readOnlyColumn
                            };
                        }
                        else
                        {
                            dataGridColumn = new DataGridTextColumn
                            {
                                Header = name,
                                Binding = new Binding(name),
                                Width = new DataGridLength(1, DataGridLengthUnitType.Auto),
                                IsReadOnly = readOnlyColumn,
                            };
                        }
                        break;
                }
                DynamicDataGrid.Columns.Add(dataGridColumn);
            }

            foreach (DataGridColumn column in DynamicDataGrid.Columns)
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
                            new Setter(TextBlock.PaddingProperty,
                                column.Header?.ToString() == "Nazwa Części"
                                    ? new Thickness(5)
                                    : new Thickness(3))
                        }
                    };

                    if (column.Header?.ToString() == "Nazwa Części")
                    {
                        textColumn.ElementStyle.Setters.Add(
                            new Setter(Control.FontWeightProperty, FontWeights.Bold));
                    }
                }


                if (column.Header?.ToString() == "Numer")
                {
                    var highlightStyle = new Style(typeof(DataGridCell));


                    highlightStyle.Setters.Add(new Setter(Control.BackgroundProperty,
                        new Binding(".")
                        {
                            Converter = new NumerHighlightConverter(),
                            Mode = BindingMode.OneWay
                        }));

                    highlightStyle.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.Black));
                    highlightStyle.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
                    highlightStyle.Setters.Add(new Setter(DataGridCell.IsHitTestVisibleProperty, false));

                    column.CellStyle = highlightStyle;

                }

                else if (column.IsReadOnly)
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
            e.Handled = !Regex.IsMatch(e.Text, @"^[0-9]*(?:[.,][0-9]*)?$");
        }

        private void DynamicDataGridPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject depObj)
            {
                DataGridCell cell = FindParent<DataGridCell>(depObj);
                if (cell != null && !cell.IsEditing)
                {
                    DataGrid dataGrid = sender as DataGrid;
                    if (dataGrid != null && !cell.IsReadOnly)
                    {
                        if (!cell.IsFocused)
                        {
                            cell.Focus();
                        }
                        dataGrid.BeginEdit();
                        e.Handled = true;
                    }
                }
            }
        }

        private bool IsTwoDirectionMapping(PropDef propDef)
        {

            PropDefInfo propDefInfo = _vaultConn.WebServiceManager.PropertyService.GetPropertyDefinitionInfosByEntityClassId("ITEM", new long[] { propDef.Id }).First();
            MappingDirection mappingDirection;
            if (propDefInfo.EntClassCtntSrcPropCfgArray != null)
            {
                EntClassCtntSrcPropCfg ent = propDefInfo.EntClassCtntSrcPropCfgArray.Where(n => n.EntClassId == "ITEM").FirstOrDefault();
                
                if(ent != null)
                {
                    mappingDirection = ent.MapDirectionArray.First();
                    if (mappingDirection == MappingDirection.Write) return true;
                } 
                return false;
            }
            return true;
        }
    }
}