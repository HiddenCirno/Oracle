using Newtonsoft.Json;
using SPT.Common.Http;
using System.Collections.Generic;

namespace Oracle.Tools
{
    /// <summary>
    /// 手册工具, 包含路由请求, 类型接口和通讯结果定义
    /// </summary>
    public class HandbookClass
    {
        /// <summary>
        /// 通过原版路由请求手册
        /// </summary>
        /// <param name="request">请求内容, 无实际影响, 这里使用了原版路由, 所以它永远不会被使用</param>
        /// <returns></returns>
        public static string GetHandbookData(string request)
        {
            return RequestHandler.PostJson("/client/handbook/templates", JsonConvert.SerializeObject(new GetHandbookRequest(request)));
        }
        //以下为类型接口和通讯结果定义
        public class HandbookResponse
        {
            [JsonProperty("err")]
            public int Err { get; set; }

            [JsonProperty("errmsg")]
            public string ErrMsg { get; set; }

            [JsonProperty("data")]
            public HandbookData Data { get; set; }
        }

        public class HandbookData
        {
            [JsonProperty("Categories")]
            public List<HandbookCategory> Categories { get; set; }

            [JsonProperty("Items")]
            public List<HandbookItem> Items { get; set; }
        }

        public class HandbookCategory
        {
            [JsonProperty("Id")]
            public string Id { get; set; }

            [JsonProperty("ParentId")]
            public string ParentId { get; set; }

            [JsonProperty("Icon")]
            public string Icon { get; set; }

            [JsonProperty("Color")]
            public string Color { get; set; }

            [JsonProperty("Order")]
            public string Order { get; set; }
        }

        public class HandbookItem
        {
            [JsonProperty("Id")]
            public string Id { get; set; }

            [JsonProperty("ParentId")]
            public string ParentId { get; set; }

            [JsonProperty("Price")]
            public int Price { get; set; }
        }
        public class GetHandbookRequest
        {
            public GetHandbookRequest(string request)
            {
                this.request = request;
            }
            public string request;
        }
    }
}

