using Newtonsoft.Json;

namespace BTCPayServer.Plugins.LNbank.Data.Lndhub;

public class AuthRequest
{
    // wallet id
    [JsonProperty(PropertyName = "login")]
    public string Login { get; set; }
    
    // wallet access key
    [JsonProperty(PropertyName = "password")]
    public string Password { get; set; }
    
    // wallet access key
    [JsonProperty(PropertyName = "refresh_token")]
    public string RefreshToken { get; set; }
}
