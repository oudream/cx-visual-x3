using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CxVisual.Platform.Channels
{
    public class httpServer
    {
        HttpListener listener1;
        Thread listenThread1;
        /// <summary>
        /// 服务器是否启动
        /// </summary>
        public bool ListenInited = false;

        public httpServer()
        {
            ListenInited = false;
        }

        public void InitListen(string url)
        {
            if (ListenInited)
                return;
            listener1 = new HttpListener();
            listener1.Prefixes.Add(url);

            listener1.Start();

            listenThread1 = new Thread(AcceptClient1);
            listenThread1.Name = "httpserver";
            listenThread1.Start();

            ListenInited = true;
        }

        public void StopListen()
        {
            if (!ListenInited)
                return;
            listenThread1.Abort();
            listener1.Stop();
            ListenInited = false;
        }

        public void AcceptClient1()
        {
            while (listener1.IsListening)
            {
                try
                {
                    HttpListenerContext context = listener1.GetContext();
                    new Thread(HandleRequest).Start(context);
                }
                catch
                {
                }
                Thread.Sleep(100);
            }
        }

        void HandleRequest(object ctx)
        {
            HttpListenerContext context = ctx as HttpListenerContext;
            HttpListenerRequest request = context.Request;
            HttpListenerResponse response = context.Response;

            Stream body = request.InputStream;
            Encoding encoding = request.ContentEncoding;
            StreamReader reader = new StreamReader(body, encoding);
            string s = reader.ReadToEnd();
            Console.WriteLine(s);
            body.Close();
            reader.Close();

            string responseString = "";     //回复的信息

            if (request.RawUrl == "/mesTest/")
            {
                //收到的信息转换为json字符串，转换为类的实例
//                TestMesInformation data = JsonConvert.DeserializeObject<TestMesInformation>(s);
//
//                MesResponseData res = new MesResponseData()
//                {
//                    Result = true,
//                    Message = "已收到" + data.Name + "的信息",
//                };
//
//                responseString = JsonConvert.SerializeObject(res);
            }


            if (request.HttpMethod.ToUpper() == "POST")
            {
                response.ContentEncoding = request.ContentEncoding;
                response.ContentType = request.ContentType;
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                Stream output = response.OutputStream;
                output.Write(buffer, 0, buffer.Length);
                output.Close();
                return;
            }

        }

    }

    /// <summary>
    /// 测试mes使用例程，可删除
    /// </summary>
    public class TestMesInformation
    {
        public string Name { get; set; }
        public string Gender { get; set; }
        public int Age { get; set; }
    }
    /// <summary>
    /// 测试mes使用回复例程，可删除
    /// </summary>
    public class MesResponseData
    {
        public string Message { get; set; }
        public bool Result { get; set; }
    }
}
