//using System.Collections.Generic;
//using System.ComponentModel;

//public class DynamicFileRow : INotifyPropertyChanged
//{
//    private Dictionary<string, object> _propertyValues = new Dictionary<string, object>();

//    public event PropertyChangedEventHandler PropertyChanged;

//    public object this[string columnName]
//    {
//        get
//        {
//            _propertyValues.TryGetValue(columnName, out var value);
//            return value;
//        }
//        set
//        {
//            _propertyValues[columnName] = value;
//            OnPropertyChanged(columnName);
//        }
//    }

//    public IEnumerable<string> GetColumns() => _propertyValues.Keys;

//    public void SetProperty(string columnName, object value)
//    {
//        _propertyValues[columnName] = value;
//    }

//    protected void OnPropertyChanged(string propertyName)
//    {
//        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
//    }
//}
