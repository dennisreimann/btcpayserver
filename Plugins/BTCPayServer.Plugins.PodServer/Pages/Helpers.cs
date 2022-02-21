using BTCPayServer.Lightning;

namespace BTCPayServer.Plugins.PodServer.Pages;

public static class Helpers
{
    public static string Sats(LightMoney amount) => $"{Math.Round(amount.ToUnit(LightMoneyUnit.Satoshi))} sats";
}
