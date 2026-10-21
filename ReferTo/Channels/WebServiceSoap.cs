using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace CxVisual.Platform.Channels
{
    public class WebServiceSoap
    {
        private static HttpWebRequest request = null;

        private static string _url = string.Empty;
        public static string InitRequest(string url)
        {
            try
            {
                _url = url;
                request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST";
                request.ContentType = "text/xml; charset=utf-8";
                request.Timeout = 3000;
                return "";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        public static string Request(string account, string pswd,string machineSn, string localIP, string param, string serviceName)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(_url);
                request.Method = "POST";
                request.ContentType = "text/xml; charset=utf-8";
                request.Timeout = 3000;
               

                string msg = $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\r\n<soap:Envelope xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" " +
                    $"xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:soap=\"http://schemas.xmlsoap.org/soap/envelope/\">\r\n  " +
                    $"<soap:Body>\r\n    " +
                    $"<UniRequest xmlns=\"http://tempuri.org/\">\r\n      " +
                    $"<rb>\r\n        " +
                    $"<account>{account}</account>\r\n        " +
                    $"<company>{machineSn}</company>\r\n        " +
                    $"<ip>{localIP}</ip>\r\n        " +
                    $"<optype>0</optype>\r\n        " +
                    $"<param>{param}</param>\r\n        " +
                    $"<paramEncoding>utf-8</paramEncoding>\r\n        " +
                    $"<password>{pswd}</password>\r\n        " +
                    $"<sericeName>{serviceName}</sericeName>\r\n        " +
                    $"<sysType>0</sysType>\r\n      " +
                    $"</rb>\r\n    " +
                    $"</UniRequest>\r\n  " +
                    $"</soap:Body>\r\n</soap:Envelope>";

                string result = "";

                Encoding encoding = Encoding.UTF8;
                byte[] buffer = encoding.GetBytes(msg.ToString());
                request.ContentLength = buffer.Length;
                using (Stream reqStream = request.GetRequestStream())
                {
                    reqStream.Write(buffer, 0, buffer.Length);
                    reqStream.Flush();
                }
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                using (StreamReader reader = new StreamReader(response.GetResponseStream(), encoding))
                {
                    result = reader.ReadToEnd();
                }
                return result;
            }
            catch(Exception ex)
            {
                return "error:" + ex.Message;
            }
        }

    }
}
