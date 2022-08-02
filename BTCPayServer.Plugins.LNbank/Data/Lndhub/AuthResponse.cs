using Newtonsoft.Json;

namespace BTCPayServer.Plugins.LNbank.Data.Lndhub;

public class AuthResponse
{
    [JsonProperty(PropertyName = "access_token")]
    public string AccessToken { get; init; }
    
    [JsonProperty(PropertyName = "refresh_token")]
    public string RefreshToken { get; init; }

    public AuthResponse(string accessKey)
    {
        AccessToken = accessKey;
        RefreshToken = accessKey;
    }
}
