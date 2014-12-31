using Newtonsoft.Json;

namespace OwinDuplex.Messages
{
    internal class Hello
    {
        [JsonProperty("from")]
        public string From;
    }
}