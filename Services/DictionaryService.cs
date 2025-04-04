using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using ChangeProperties.Models;
using Newtonsoft.Json;

namespace ChangeProperties.Services
{
    public class DictionaryService
    {
        public static UserDictionaries getDictionariesList(string url, string token, string id)
        {
            List<string> list = new List<string>();
            UserDictionaries userDictionaries= new UserDictionaries();

            try
            {
                string data = "{\"Token\":\"" + token + "\",\"ContextUserID\": " + id + " , \"Model\":{}\r\n}";
                userDictionaries = JsonConvert.DeserializeObject<UserDictionaries>(new WebClient
                {
                    Encoding = Encoding.UTF8,
                    Headers =
                    {
                        [HttpRequestHeader.ContentType] = "application/json",
                        [HttpRequestHeader.Accept] = "application/json"
                    }
                }.UploadString(url + "/json/reply/DB_SlownikiUzytkownika", "POST", data));
            }
            catch (Exception)
            {

            }
            return userDictionaries;
        }

        public static Dictionary<string, Dictionary<string, string>> GetDictionariesByName(List<string> names, UserDictionaries userDictionaries)
        {
            var result = new Dictionary<string, Dictionary<string, string>>();

            foreach (var name in names)
            {
                result[name] = new Dictionary<string, string>();
            }

            try
            {
                foreach (var userDictionary in userDictionaries.Wynik)
                {
                    if (result.ContainsKey(userDictionary.Nazwa))
                    {
                        foreach (var item in userDictionary.Elementy)
                        {
                            if (!result[userDictionary.Nazwa].ContainsKey(item.Tekst) && userDictionary.Nazwa == "Kolor malowania RAL")
                            {
                                result[userDictionary.Nazwa].Add(item.wartosc, item.Tekst);
                            }
                            else if(!result[userDictionary.Nazwa].ContainsKey(item.Tekst))
                            {
                                result[userDictionary.Nazwa].Add(item.Tekst, item.ElementID.ToString());
                                
                            }
                        }
                    }
                }

                return result;
            }
            catch (Exception)
            {
                return result;
            }
        }
    }
}
