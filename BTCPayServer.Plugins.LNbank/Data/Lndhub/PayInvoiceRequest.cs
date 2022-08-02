using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.JsonConverters;
using Newtonsoft.Json;

namespace BTCPayServer.Plugins.LNbank.Data.Lndhub;

public class PayInvoiceRequest
{
    [JsonProperty("invoice")]
    public string PaymentRequest { get; set; }

    // Amount in satoshis
    [JsonProperty("amount")]
    [JsonConverter(typeof(LndHubLightMoneyJsonConverter))]
    public LightMoney Amount { get; set; }
}
