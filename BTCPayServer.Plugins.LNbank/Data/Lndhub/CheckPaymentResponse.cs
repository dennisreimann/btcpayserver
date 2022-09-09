using Newtonsoft.Json;

namespace BTCPayServer.Plugins.LNbank.Data.Lndhub;

public class CheckPaymentResponse
{
    [JsonProperty(PropertyName = "paid")]
    public bool Paid { get; set; }
}
