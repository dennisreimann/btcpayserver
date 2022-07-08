using System;
using System.Threading.Tasks;
using BTCPayServer.Lightning;
using NBitcoin;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Xunit;
using Xunit.Abstractions;

namespace BTCPayServer.Tests
{
    [Trait("Selenium", "Selenium")]
    [Collection(nameof(NonParallelizableCollectionDefinition))]
    public class LNbankTests : UnitTestBase
    {
        private const int TestTimeout = TestUtils.TestTimeout;

        public LNbankTests(ITestOutputHelper helper) : base(helper)
        {
        }
        
        [Fact(Timeout = TestTimeout)]
        [Trait("Lightning", "Lightning")]
        public async Task CanUseLNbank()
        {
            var implementations = new []
            {
                LightningConnectionType.CLightning,
                LightningConnectionType.LndREST
            };
            
            foreach (var nodeType in implementations)
            {
                using var s = CreateSeleniumTester();
                
                s.Server.ActivateLightning(nodeType);
                await s.StartAsync();
                s.RegisterNewUser(true);
                
                // Setup store LN node with LNbank
                s.CreateNewStore();
                s.Driver.FindElement(By.Id("StoreNav-LightningBTC")).Click();
                s.Driver.FindElement(By.CssSelector("label[for=\"LightningNodeType-LNbank\"]")).Click();
                s.Driver.WaitForElement(By.Id("LNbank-CreateWallet"));
                Assert.Equal("", s.Driver.FindElement(By.Id("LNbankWallet")).GetAttribute("value"));
                
                // Create new wallet, which is pre-selected afterwards
                s.Driver.FindElement(By.Id("LNbank-CreateWallet")).Click();
                var walletName = "Wallet" + RandomUtils.GetUInt64();
                s.Driver.FindElement(By.Id("Wallet_Name")).SendKeys(walletName);
                s.Driver.FindElement(By.Id("LNbank-Create")).Click();
                s.Driver.WaitForElement(By.Id("LNbankWallet"));
                var walletSelect = new SelectElement(s.Driver.FindElement(By.Id("LNbankWallet")));
                Assert.Equal(walletName, walletSelect.SelectedOption.Text);
                
                // Finish and validate setup
                s.Driver.FindElement(By.Id("save")).Click();
                Assert.Contains("LNbank", s.Driver.FindElement(By.Id("CustomNodeInfo")).Text);
                
                // LNbank wallets
                s.Driver.FindElement(By.Id("Nav-LNbank")).Click();
                Assert.Contains(walletName, s.Driver.FindElement(By.Id("LNbank-Wallets")).Text);
                Assert.Single(s.Driver.FindElements(By.CssSelector("#LNbank-Wallets a")));
                s.Driver.FindElement(By.CssSelector("#LNbank-Wallets a")).Click();
                
                // Wallet
                Assert.Contains("0 sats", s.Driver.FindElement(By.Id("LNbank-WalletBalance")).Text);
                Assert.Contains("There are no transactions yet.", s.Driver.FindElement(By.Id("LNbank-WalletTransactions")).Text);
                s.Driver.FindElement(By.Id("LNbank-WalletSettings")).Click();
                Assert.Contains(walletName, s.Driver.FindElement(By.Id("LNbank-WalletName")).Text);
                
                // Receive
                var description = "First invoice";
                s.Driver.FindElement(By.Id("LNbank-WalletReceive")).Click();
                s.Driver.FindElement(By.Id("Description")).SendKeys(description);
                s.Driver.FindElement(By.Id("Amount")).Clear();
                s.Driver.FindElement(By.Id("Amount")).SendKeys("21");
                s.Driver.FindElement(By.Id("LNbank-CreateInvoice")).Click();
                
                // Details
                Assert.Contains(description, s.Driver.FindElement(By.Id("LNbank-TransactionDescription")).Text);
                Assert.Contains("21 sats unpaid", s.Driver.FindElement(By.Id("LNbank-TransactionAmount")).Text);
                var bolt11 = s.Driver.FindElement(By.Id("LNbank-CopyPaymentRequest")).GetAttribute("data-clipboard");
                var shareUrl = s.Driver.FindElement(By.Id("LNbank-CopyShareUrl")).GetAttribute("data-clipboard");
                Assert.StartsWith("ln", bolt11);
                
                // List
                s.Driver.FindElement(By.Id("LNbank-WalletOverview")).Click();
                var listUrl = s.Driver.Url;
                Assert.Single(s.Driver.FindElements(By.CssSelector("#LNbank-WalletTransactions tr")));
                Assert.Contains("21 sats", s.Driver.FindElement(By.CssSelector("#LNbank-WalletTransactions tr .transaction-amount")).Text);
                Assert.Contains(description, s.Driver.FindElement(By.CssSelector("#LNbank-WalletTransactions tr .transaction-description")).Text);
                Assert.Contains("unpaid", s.Driver.FindElement(By.CssSelector("#LNbank-WalletTransactions tr .transaction-status")).Text);
                Assert.Contains("0 sats", s.Driver.FindElement(By.Id("LNbank-WalletBalance")).Text);
                
                // Share
                s.GoToUrl(shareUrl);
                Assert.Contains(description, s.Driver.FindElement(By.Id("LNbank-TransactionDescription")).Text);
                Assert.Contains("21 sats unpaid", s.Driver.FindElement(By.Id("LNbank-TransactionAmount")).Text);
                
                // Pay invoice
                var resp = await s.Server.CustomerLightningD.Pay(bolt11);
                Assert.Equal(PayResult.Ok, resp.Result);
                TestUtils.Eventually(() =>
                {
                    s.Driver.Navigate().Refresh();
                    Assert.Contains("21 sats settled", s.Driver.FindElement(By.Id("LNbank-TransactionSettled")).Text);
                });
                
                // List
                s.GoToUrl(listUrl);
                Assert.Single(s.Driver.FindElements(By.CssSelector("#LNbank-WalletTransactions tr")));
                Assert.Contains("21 sats", s.Driver.FindElement(By.CssSelector("#LNbank-WalletTransactions tr .transaction-settled")).Text);
                Assert.Contains("21 sats", s.Driver.FindElement(By.Id("LNbank-WalletBalance")).Text);
                
                // Send
                var memo = "Donation";
                var amount = LightMoney.Satoshis(5);
                var invoice = await s.Server.CustomerLightningD.CreateInvoice(amount, memo, TimeSpan.FromHours(1));
                
                s.Driver.FindElement(By.Id("LNbank-WalletSend")).Click();
                s.Driver.FindElement(By.Id("PaymentRequest")).SendKeys(invoice.BOLT11);
                s.Driver.FindElement(By.Id("LNbank-Decode")).Click();
                
                // Confirm
                Assert.Contains(memo, s.Driver.FindElement(By.Id("Description")).GetAttribute("value"));
                Assert.Contains("5 sats", s.Driver.FindElement(By.Id("LNbank-Amount")).Text);
                s.Driver.FindElement(By.Id("Description")).Clear();
                s.Driver.FindElement(By.Id("Description")).SendKeys("For Uncle Jim");
                s.Driver.FindElement(By.Id("LNbank-Send")).Click();
                Assert.Contains("Payment successfully sent and settled.", s.FindAlertMessage().Text);

                // List
                s.Driver.FindElement(By.Id("LNbank-WalletOverview")).Click();
                var amountEl = s.Driver.FindElement(By.CssSelector("#LNbank-WalletTransactions tr .transaction-amount"));
                var settledEl = s.Driver.FindElement(By.CssSelector("#LNbank-WalletTransactions tr .transaction-settled"));
                var amountMoney = LightMoney.MilliSatoshis(long.Parse(amountEl.GetAttribute("data-amount")));
                var amountSettledMoney = LightMoney.MilliSatoshis(long.Parse(settledEl.GetAttribute("data-amount-settled")));
                var feeMoney = LightMoney.MilliSatoshis(long.Parse(settledEl.GetAttribute("data-transaction-fee")));
                var amountSettled = (amountMoney + feeMoney) * -1;
                var balance = LightMoney.Satoshis(21) + amountSettled;
                Assert.Equal(2, s.Driver.FindElements(By.CssSelector("#LNbank-WalletTransactions tr")).Count);
                Assert.Equal(amount, amountMoney);
                Assert.Equal(amountSettled, amountSettledMoney);
                Assert.Contains("For Uncle Jim", s.Driver.FindElement(By.CssSelector("#LNbank-WalletTransactions tr .transaction-description")).Text);
                Assert.Contains($"{amount.ToUnit(LightMoneyUnit.Satoshi)} sats", amountEl.Text);
                Assert.Contains($"{amountSettled.ToUnit(LightMoneyUnit.Satoshi)} sats", settledEl.Text);
                Assert.Contains($"{balance.ToUnit(LightMoneyUnit.Satoshi)} sats", s.Driver.FindElement(By.Id("LNbank-WalletBalance")).Text);
            }
        }
    }
}
