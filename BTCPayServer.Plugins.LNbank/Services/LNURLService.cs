using System;
using System.Net.Http;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.LNbank.Exceptions;
using LNURL;
using MimeKit;
using NBitcoin;

namespace BTCPayServer.Plugins.LNbank.Services;

public class LNURLService
{
    // see LightningClientFactoryService
    private const string HttpHandlerOnionNamedClient = "lightning.onion";
    private const string HttpHandlerClearnetNamedClient = "lightning.clearnet";
    
    private readonly IHttpClientFactory _httpClientFactory;

    public LNURLService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<BOLT11PaymentRequest> GetBolt11(string destination, Network network)
    {
        BOLT11PaymentRequest bolt11;
        try
        {
            string lnurlTag = null;
            var lnurl = IsLightningAddress(destination)
                ? LNURL.LNURL.ExtractUriFromInternetIdentifier(destination)
                : LNURL.LNURL.Parse(destination, out lnurlTag);

            if (lnurlTag is null)
            {
                var httpClient = CreateClient(lnurl);
                var info = (LNURLPayRequest)await LNURL.LNURL.FetchInformation(lnurl, httpClient);
                lnurlTag = info.Tag;
            }

            if (lnurlTag.Equals("payRequest", StringComparison.InvariantCultureIgnoreCase))
            {
                var httpClient = CreateClient(lnurl);
                var payRequest = (LNURLPayRequest)await LNURL.LNURL.FetchInformation(lnurl, lnurlTag, httpClient);
                var amount = payRequest.MinSendable;
                var payResponse = await payRequest.SendRequest(amount, network, httpClient);
                
                return payResponse.GetPaymentRequest(network);
            }
        }
        catch (FormatException)
        {
        }
        catch
        {
            throw new ResolveLNURLException(destination, "The LNURL / Lightning Address provided was not online.");
        }

        if (!BOLT11PaymentRequest.TryParse(destination, out bolt11, network))
            throw new ResolveLNURLException(destination, "A valid BOLT11 invoice or LNURL Pay or Lightning address was not provided.");
        
        return bolt11;
    }

    private static bool IsLightningAddress(string email)
    {
        if (string.IsNullOrEmpty(email)) return false;

        var options = ParserOptions.Default.Clone();
        options.AllowAddressesWithoutDomain = false;
        return MailboxAddress.TryParse(options, email, out var mailboxAddress) && mailboxAddress is not null;
    }

    private HttpClient CreateClient(Uri uri)
    {
        return _httpClientFactory.CreateClient(uri.IsOnion()
            ? HttpHandlerOnionNamedClient
            : HttpHandlerClearnetNamedClient);
    }
}
