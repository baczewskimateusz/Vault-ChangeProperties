﻿using Autodesk.Connectivity.WebServices;

namespace ChangeProperties
{
    public class FileProperty
    {
        public string Name { get; set; }
        public object Value{ get; set; }
        public DataType PropertyType { get; set; }
        public PropDef PropertyDef { get; set; }

        public FileProperty(FileProperty other)
        {
            Name = other.Name;
            Value = other.Value;
            PropertyType = other.PropertyType;
            PropertyDef = other.PropertyDef;
        }
        public FileProperty() { }
    }
}
