using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.JsonConverters;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.LNbank.Data.Lndhub;

public class BalanceData
{
    [JsonProperty(PropertyName = "BTC")]
    public BtcBalance BTC { get; set; }
}

public class BtcBalance
{
    [JsonProperty(PropertyName = "AvailableBalance")]
    [JsonConverter(typeof(LndHubLightMoneyJsonConverter))]
    public LightMoney AvailableBalance { get; set; }
}
