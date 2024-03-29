﻿using System;
using System.Collections;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NovelAIClient.Models;

namespace NovelAIClient
{
    /// <summary>
    /// WebUI客户端
    /// </summary>
    public class CustomWebUIClient : BaseClient
    {
        private int _fnIndex;
        /// <summary>
        /// 实例化一个自定义WebUI客户端对象
        /// WebUI默认地址为：http://127.0.0.1:7860/
        /// </summary>
        /// <param name="fnIndex">请求函数索引（请在浏览器手动发起一次请求获取）</param>
        /// <param name="host">服务地址</param>
        public CustomWebUIClient(int fnIndex, string host = "http://127.0.0.1:7860/") : base(host)
        {
            _fnIndex = fnIndex;
        }

        /// <summary>
        /// 使用字符串自定义参数请求WebUI（适用于魔改版本）
        /// 可打开浏览器开发者工具，发起一次请求，在 负载/请求 中拷贝data的内容
        /// </summary>
        /// <param name="strDatas">字符串参数列表</param>
        /// <returns>图片字节</returns>
        public async Task<byte[]?> PostAsync(string strDatas)
        {
            ArrayList data = StringToArrayListData(strDatas);
            return await PostAsync(data);
        }

        /// <summary>
        /// 使用字符串自定义参数请求WebUI（适用于魔改版本）
        /// 可打开浏览器开发者工具，发起一次请求，在 负载/请求 中拷贝data的内容
        /// </summary>
        /// <param name="strDatas">字符串参数列表,此模式下会忽略前两个参数(prompt 和 negative prompt)</param>
        /// <param name="prompt">生成提示词</param>
        /// <param name="negativePrompt">屏蔽词</param>
        /// <returns>图片字节</returns>
        public async Task<byte[]?> PostAsync(string strDatas, string prompt, string negativePrompt = "")
        {
            ArrayList data = StringToArrayListData(strDatas);
            data[0] = prompt;
            data[1] = negativePrompt;
            return await PostAsync(data);
        }

        private ArrayList StringToArrayListData(string strData)
        {
            string[] strDatas = strData.Replace("\r", "").Replace("\n", "").Replace("[", "").Replace("]", "").Split(",");
            ArrayList data = new ArrayList();
            for (int i = 0; i < strDatas.Length; i++)
            {
                string onceData = strDatas[i].Trim();
                if (onceData == "null")
                    data.Add(null);
                else if (onceData.Contains('\"'))
                    data.Add(onceData.Replace("\"", ""));
                else if (long.TryParse(onceData, out long valInt64))
                    data.Add(valInt64);
                else if (float.TryParse(onceData, out float valFloat))
                    data.Add(valFloat);
                else if (bool.TryParse(onceData, out bool valBool))
                    data.Add(valBool);
                else
                    data.Add(onceData);
            }
            return data;
        }

        /// <summary>
        /// 使用自定义参数调用WebUI（适用于魔改版本）
        /// </summary>
        /// <param name="data">参数数组</param>
        /// <returns>图片字节</returns>
        public async Task<byte[]?> PostAsync(ArrayList data)
        {
            input input = new input(_fnIndex, data);
            string json = JsonConvert.SerializeObject(input);
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{_host}api/predict"))
            {
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                using (HttpClient client = new HttpClient() { Timeout = Timeout.InfiniteTimeSpan })
                {
                    var response = await client.SendAsync(request);
                    string result = await response.Content.ReadAsStringAsync();
                    JToken? jToken = JsonConvert.DeserializeObject<JToken>(result);
                    if (jToken != null)
                    {
                        if (jToken["error"] is null)
                        {
                            bool isFile = Convert.ToBoolean(jToken["data"]![0]![0]!["is_file"]);
                            if (isFile)
                            {
                                string fileName = jToken["data"]![0]![0]!["name"]!.ToString();
                                if (File.Exists(fileName))  //如果是绝对路径
                                {
                                    using (FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                    {
                                        byte[] imageBytes = new byte[fs.Length];
                                        await fs.ReadAsync(imageBytes, 0, imageBytes.Length);
                                        return imageBytes;
                                    }
                                }
                                else
                                {
                                    string imgUrl = $@"{_host}file={fileName}";
                                    var imgResp = await client.GetAsync(imgUrl);
                                    return await imgResp.Content.ReadAsByteArrayAsync();
                                }
                            }
                            else  //不知道怎么让它返回非文件, 猜测是base64, 没测试过
                            {
                                return Convert.FromBase64String(jToken["data"]![0]![0]!["data"]!.ToString());
                            }
                        }
                        else
                        {
                            throw new Exception("参数有误，请核对");
                        }
                    }
                    return null;
                }
            }
        }
    }
}