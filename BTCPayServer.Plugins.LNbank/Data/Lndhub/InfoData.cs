using BTCPayServer.Lightning.LNDhub.Models;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.LNbank.Data.Lndhub;

public class InfoData : NodeInfoData
{
    [JsonProperty(PropertyName = "alias")]
    public string Alias { get; set; }
}
