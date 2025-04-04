/*=====================================================================
  
  This file is part of the Autodesk Vault API Code Samples.

  Copyright (C) Autodesk Inc.  All rights reserved.

THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY
KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
PARTICULAR PURPOSE.
=====================================================================*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.Xml;

namespace ChangeProperties
{
    [XmlRoot("settings")]
    public class Settings
    {
        [XmlElement("SettingName")]
        public string mSettingName;

        [XmlElement("ERPURL")]
        public string ERPURL;

        [XmlElement("ERPToken")]
        public string ERPToken;

        [XmlElement("ERPUser")]
        public string ERPUser;

        [XmlElement("slownikObrobkaPowierzchni")]
        public string slownikObrobkaPowierzchni;

        [XmlElement("slownikObrobkaCieplna")]
        public string slownikObrobkaCieplna;

        [XmlElement("slownikPrzygotowaniePow")]
        public string slownikPrzygotowaniePow;

        [XmlElement("slownikMalarnia")]
        public string slownikMalarnia;

        [XmlElement("slowniKolor")]
        public string slowniKolor;

        public static Settings readfromXML(string xml)
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(Settings));
            XmlTextReader xmlReader = new XmlTextReader(new StringReader(xml));
            return (Settings)xmlSerializer.Deserialize(xmlReader);
        }

        public Settings()
        {

        
        }

        #region for future use
        //[XmlElement("OutputPath")]
        //public string mOutPutPath;
        #endregion for future use


        public void Save()
        {
            try
            {
                string codeFolder = Util.GetAssemblyPath();
                string xmlPath = Path.Combine(codeFolder, "Settings.xml");

                using (System.IO.StreamWriter writer = new System.IO.StreamWriter(xmlPath))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(Settings));
                    serializer.Serialize(writer, this);
                }
            }
            catch
            { }
        }

        public static Settings Load()
        {
            Settings retVal = new Settings();


            string codeFolder = Util.GetAssemblyPath();
            string xmlPath = Path.Combine(codeFolder, "Settings.xml");

            using (System.IO.StreamReader reader = new System.IO.StreamReader(xmlPath))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(Settings));
                retVal = (Settings)serializer.Deserialize(reader);
            }
            return retVal;
        }
    }

}
