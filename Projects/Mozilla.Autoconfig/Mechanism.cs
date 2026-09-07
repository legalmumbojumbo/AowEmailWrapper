using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace Mozilla.Autoconfig
{
    internal class Mechanism
    {
        private const int FetchTimeoutSeconds = 15;
        private static readonly HttpClient Http = new HttpClient() { Timeout = TimeSpan.FromSeconds(FetchTimeoutSeconds) };

        private string _format;
        private MechanismOriginType _type;

        public Mechanism(string format, MechanismOriginType type)
        {
            _format = format;
            _type = type;
        }

        public MechanismResponse Attempt(string domain)
        {
            MechanismResponse returnVal = new MechanismResponse();
            returnVal.Origin = _type;

            string xmlPath = string.Format(_format, domain);
            string xml = IspDbHandler.IsTrustedSource(xmlPath) ? GetXml(xmlPath) : null;

            if (!string.IsNullOrEmpty(xml))
            {
                try
                {
                    returnVal.ClientConfig = Deserialize<ClientConfig>(xml);
                    returnVal.ResponseType = MechanismResponseType.Success;
                }
                catch (Exception ex)
                {
                    returnVal.Exception = ex;
                    returnVal.ResponseType = MechanismResponseType.Exception;
                }
            }
            else
            {
                returnVal.ResponseType = MechanismResponseType.NotFound;
            }

            return returnVal;
        }

        private static string GetXml(string path)
        {
            try
            {
                Uri uri = new Uri(path);
                if (uri.Scheme == Uri.UriSchemeFile)
                {
                    return File.Exists(uri.LocalPath) ? File.ReadAllText(uri.LocalPath) : null;
                }

                //Certificate validation is left on: a bad certificate means this source is skipped
                return Http.GetStringAsync(uri).GetAwaiter().GetResult();
            }
            catch
            {
                return null;
            }
        }

        private static T Deserialize<T>(string theXml)
        {
            T value = default(T);

            if (!string.IsNullOrEmpty(theXml))
            {
                XmlSerializer serial = new XmlSerializer(typeof(T));
                {
                    using (StringReader reader = new StringReader(theXml))
                    {
                        try
                        {
                            return (T)Convert.ChangeType(serial.Deserialize(reader), typeof(T));
                        }
                        finally
                        {
                            serial = null;
                        }
                    }
                }
            }

            return value;
        }
    }
}
