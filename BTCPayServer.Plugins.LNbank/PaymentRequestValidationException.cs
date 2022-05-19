using System;

namespace BTCPayServer.Plugins.LNbank;

public class PaymentRequestValidationException : Exception
{
    public PaymentRequestValidationException(string message): base(message)
    {
    }
}
