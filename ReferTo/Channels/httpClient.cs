using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading;

namespace CxVisual.Platform.Channels
{
    public class httpClient
    {
        public httpClient()
        {
        }

        /// <summary>
        /// 发送mes信息
        /// </summary>
        /// <typeparam name="T">信息类或结构体</typeparam>
        /// <param name="url">url地址</param>
        /// <param name="info">信息实例</param>
        /// <returns></returns>
        public string SendInformation<T>(string url, T info)
        {
            var data = PostInfomation(url, info).GetAwaiter().GetResult();
            return data;
        }

        private async Task<string> PostInfomation<T>(string url, T info)
        {
            var result = RunInfomationAsync(url, info);
            await result.ConfigureAwait(false);

            return result.Result;
        }

        private async Task<string> RunInfomationAsync<T>(string url, T info)
        {
            var client = new HttpClient();
            try
            {
                client.Timeout = TimeSpan.FromSeconds(6);
                client.BaseAddress = new Uri(url);
                client.DefaultRequestHeaders.Accept.Clear();
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var res = await CreateInfomationAsync(info, client).ConfigureAwait(false);
                return res;    //返回json字符串
            }
            catch (Exception e)
            {
                return e.Message;
            }
            finally
            {
                client?.Dispose();
            }
        }

        private async Task<string> CreateInfomationAsync<T>(T request, HttpClient client)
        {
            string str_res = "";
            //MesResponseData res;  //成功收到回复的结果
            //T message;  //未收到回复的信息
            HttpResponseMessage response;
            using (var content = CreateJsonContent(request))
                response = await client.PostAsync("", content).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                //  res = await response.Content.ReadAsAsync<MesResponseData>();  //以类的格式收到回复
                str_res = await response.Content.ReadAsStringAsync();   //以string类型的格式收到回复
            }
            else
            {
                string message = await response.Content.ReadAsStringAsync();
                throw new Exception("error:" + message.ToString());
            }
            return str_res;
        }

        private static HttpContent CreateJsonContent<T>(T request)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, request);
                return new StringContent(Encoding.UTF8.GetString(stream.ToArray()), Encoding.UTF8, "application/json");
            }
        }
    }
}
