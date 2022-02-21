using System;
using BTCPayServer.Lightning;

namespace BTCPayServer.Plugins.LNbank.Pages;

public static class Helpers
{
    public static string Sats(LightMoney amount) => $"{Math.Round(amount.ToUnit(LightMoneyUnit.Satoshi))} sats";
    public static string Millisats(LightMoney amount) => $"{amount.ToUnit(LightMoneyUnit.MilliSatoshi)} millisats";
}
