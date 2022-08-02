using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.JsonConverters;
using NBitcoin;
using NBitcoin.JsonConverters;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.LNbank.Data.Lndhub;

public class CreateInvoiceRequest
{
    // Amount in satoshis
    [JsonProperty("amt")]
    [JsonConverter(typeof(LndHubLightMoneyJsonConverter))]
    public LightMoney Amount { get; set; }

    [JsonProperty("memo")]
    public string Memo { get; set; }

    [JsonProperty("description_hash")]
    [JsonConverter(typeof(UInt256JsonConverter))]
    public uint256 DescriptionHash { get; set; }
}
