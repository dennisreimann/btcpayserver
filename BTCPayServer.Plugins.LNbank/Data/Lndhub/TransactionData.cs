using System;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.JsonConverters;
using NBitcoin;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.LNbank.Data.Lndhub;

public class TransactionData
{
    [JsonProperty("payment_hash")]
    [JsonConverter(typeof(LndHubBufferJsonConverter))]
    public uint256 PaymentHash { get; set; }
        
    [JsonProperty("payment_preimage")]
    public string PaymentPreimage { get; set; }

    [JsonProperty("type")]
    public static string Type { get => "paid_invoice"; }
        
    [JsonProperty("memo")]
    public string Memo { get; set; }
        
    [JsonProperty("value")]
    [JsonConverter(typeof(LndHubLightMoneyJsonConverter))]
    public LightMoney Value { get; set; }

    [JsonProperty("fee")]
    [JsonConverter(typeof(LndHubLightMoneyJsonConverter))]
    public LightMoney Fee { get; set; }
    
    [JsonConverter(typeof(LndHubDateTimeOffsetConverter))]
    public DateTimeOffset? Timestamp { get; set; }
}
