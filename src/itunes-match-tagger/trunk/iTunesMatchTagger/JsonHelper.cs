using System;
using System.Text;
using System.IO;
using System.Runtime.Serialization.Json;

namespace iTunesMatchTagger
{
    class JsonHelper
    {
        public static T Deserialise<T>(string json)
        {
            T obj = Activator.CreateInstance<T>();
            using (MemoryStream ms = new MemoryStream(Encoding.Unicode.GetBytes(json)))
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(obj.GetType());
                obj = (T)serializer.ReadObject(ms);
                return obj;
            }
        }
    }
}
