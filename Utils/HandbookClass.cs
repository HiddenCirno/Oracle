using Newtonsoft.Json;
using SPT.Common.Http;
using System.Collections.Generic;

namespace Oracle.Utils
{
    public class HandbookClass
    {
        public static string GetHandbookData(string request)
        {
            return RequestHandler.PostJson("/client/handbook/templates", JsonConvert.SerializeObject(new GetHandbookRequest(request)));
        }
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

            // ParentId 有时为 null，在 C# 中 string 本身可以接受 null
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

