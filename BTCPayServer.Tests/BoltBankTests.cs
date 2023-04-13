using System;
using System.Threading.Tasks;
using BTCPayServer.Abstractions.Models;
using BTCPayServer.Lightning;
using BTCPayServer.Plugins.BoltBank.Data.Models;
using NBitcoin;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using Xunit;
using Xunit.Abstractions;

namespace BTCPayServer.Tests;

[Trait("Selenium", "Selenium")]
[Collection(nameof(NonParallelizableCollectionDefinition))]
public class BoltBankTests : UnitTestBase
{
    private const int TestTimeout = TestUtils.TestTimeout;

    public BoltBankTests(ITestOutputHelper helper) : base(helper)
    {
    }

    [Theory(Timeout = TestTimeout)]
    [Trait("Lightning", "Lightning")]
    [InlineData(LightningConnectionType.CLightning)]
    [InlineData(LightningConnectionType.LndREST)]
    public async Task CanUseBoltBank(string nodeType)
    {
        using var s = CreateSeleniumTester();
            
        s.Server.ActivateLightning(nodeType);
        await s.StartAsync();
        await s.Server.EnsureChannelsSetup();
        s.RegisterNewUser(true);
            
        // Setup store LN node with BoltBank
        s.CreateNewStore();
        s.Driver.FindElement(By.Id("StoreNav-LightningBTC")).Click();
        s.Driver.FindElement(By.CssSelector("label[for=\"LightningNodeType-BoltBank\"]")).Click();
        s.Driver.WaitForElement(By.Id("BoltBank-CreateWallet"));
        Assert.Equal("", s.Driver.FindElement(By.Id("BoltBankWallet")).GetAttribute("value"));
            
        // Create new wallet, which is pre-selected afterwards
        s.Driver.FindElement(By.Id("BoltBank-CreateWallet")).Click();
        var walletName = "Wallet" + RandomUtils.GetUInt64();
        s.Driver.FindElement(By.Id("Wallet_Name")).SendKeys(walletName);
        s.Driver.FindElement(By.Id("BoltBank-Create")).Click();
        s.Driver.WaitForElement(By.Id("BoltBankWallet"));
        var walletSelect = new SelectElement(s.Driver.FindElement(By.Id("BoltBankWallet")));
        Assert.Equal(walletName, walletSelect.SelectedOption.Text);
            
        // Finish and validate setup
        s.Driver.FindElement(By.Id("save")).Click();
        Assert.Contains("BoltBank", s.Driver.FindElement(By.Id("CustomNodeInfo")).Text);
            
        // BoltBank wallets
        s.Driver.FindElement(By.Id("Nav-BoltBank")).Click();
        Assert.Contains(walletName, s.Driver.FindElement(By.Id("BoltBank-Wallets")).Text);
        Assert.Single(s.Driver.FindElements(By.CssSelector("#BoltBank-Wallets a")));
        s.Driver.FindElement(By.CssSelector("#BoltBank-Wallets a")).Click();
            
        // Wallet
        Assert.Contains("0 sats", s.Driver.FindElement(By.Id("BoltBank-WalletBalance")).Text);
        Assert.Contains("There are no transactions yet.", s.Driver.FindElement(By.Id("BoltBank-WalletTransactions")).Text);
        s.Driver.FindElement(By.Id("BoltBank-WalletSettings")).Click();
        Assert.Contains(walletName, s.Driver.FindElement(By.Id("BoltBank-WalletName")).Text);
            
        // Receive
        var description = "First invoice";
        s.Driver.FindElement(By.Id("BoltBank-WalletReceive")).Click();
        s.Driver.FindElement(By.Id("Description")).SendKeys(description);
        s.Driver.FindElement(By.Id("Amount")).Clear();
        s.Driver.FindElement(By.Id("Amount")).SendKeys("21");
        s.Driver.SetCheckbox(By.Id("AttachDescription"), true);
        s.Driver.FindElement(By.Id("BoltBank-CreateInvoice")).Click();
            
        // Details
        Assert.Contains(description, s.Driver.FindElement(By.Id("BoltBank-TransactionTitle")).Text);
        Assert.Contains("21 sats unpaid", s.Driver.FindElement(By.Id("BoltBank-TransactionAmount")).Text);
        var bolt11 = s.Driver.FindElement(By.Id("BoltBank-Transaction_PaymentRequest")).GetAttribute("value");
        var shareUrl = s.Driver.FindElement(By.Id("BoltBank-Transaction_ShareUrl")).GetAttribute("value");
        Assert.StartsWith("ln", bolt11);
            
        // List
        s.Driver.FindElement(By.Id("BoltBank-WalletOverview")).Click();
        var listUrl = s.Driver.Url;
        Assert.Single(s.Driver.FindElements(By.CssSelector("#BoltBank-WalletTransactions tr")));
        Assert.Contains("21 sats", s.Driver.FindElement(By.CssSelector("#BoltBank-WalletTransactions tr .transaction-amount")).Text);
        Assert.Contains(description, s.Driver.FindElement(By.CssSelector("#BoltBank-WalletTransactions tr .transaction-description")).Text);
        Assert.Contains("unpaid", s.Driver.FindElement(By.CssSelector("#BoltBank-WalletTransactions tr .transaction-status")).Text);
        Assert.Contains("0 sats", s.Driver.FindElement(By.Id("BoltBank-WalletBalance")).Text);
            
        // Share
        s.GoToUrl(shareUrl);
        Assert.Contains(description, s.Driver.FindElement(By.Id("BoltBank-TransactionTitle")).Text);
        Assert.Contains("21 sats unpaid", s.Driver.FindElement(By.Id("BoltBank-TransactionAmount")).Text);
            
        // Pay invoice
        var resp = await s.Server.CustomerLightningD.Pay(bolt11);
        Assert.Equal(PayResult.Ok, resp.Result);
        TestUtils.Eventually(() =>
        {
            s.Driver.Navigate().Refresh();
            Assert.Contains("21 sats settled", s.Driver.FindElement(By.Id("BoltBank-TransactionSettled")).Text);
        });
            
        // List
        s.GoToUrl(listUrl);
        Assert.Single(s.Driver.FindElements(By.CssSelector("#BoltBank-WalletTransactions tr")));
        Assert.Contains("21 sats", s.Driver.FindElement(By.CssSelector("#BoltBank-WalletTransactions tr .transaction-settled")).Text);
        Assert.Contains("21 sats", s.Driver.FindElement(By.Id("BoltBank-WalletBalance")).Text);
            
        // Send
        var memo = "Donation";
        var amount = LightMoney.Satoshis(5);
        var invoice = await s.Server.CustomerLightningD.CreateInvoice(amount, memo, TimeSpan.FromHours(1));
            
        s.Driver.FindElement(By.Id("BoltBank-WalletSend")).Click();
        s.Driver.FindElement(By.Id("Destination")).SendKeys(invoice.BOLT11);
        s.Driver.FindElement(By.Id("BoltBank-Decode")).Click();
            
        // Confirm
        Assert.Contains(memo, s.Driver.FindElement(By.Id("Description")).GetAttribute("value"));
        Assert.Contains("5 sats", s.Driver.FindElement(By.Id("BoltBank-Amount")).Text);
        s.Driver.FindElement(By.Id("Description")).Clear();
        s.Driver.FindElement(By.Id("Description")).SendKeys("For Uncle Jim");
        s.Driver.FindElement(By.Id("BoltBank-Send")).Click();
        Assert.Contains("Payment successfully sent and settled.", s.FindAlertMessage().Text);

        // List
        s.Driver.FindElement(By.Id("BoltBank-WalletOverview")).Click();
        var amountEl = s.Driver.FindElement(By.CssSelector("#BoltBank-WalletTransactions tr .transaction-amount"));
        var settledEl = s.Driver.FindElement(By.CssSelector("#BoltBank-WalletTransactions tr .transaction-settled"));
        var amountMoney = LightMoney.MilliSatoshis(long.Parse(amountEl.GetAttribute("data-amount")));
        var amountSettledMoney = LightMoney.MilliSatoshis(long.Parse(settledEl.GetAttribute("data-amount-settled")));
        var feeMoney = LightMoney.MilliSatoshis(long.Parse(settledEl.GetAttribute("data-transaction-fee")));
        var amountSettled = (amountMoney + feeMoney) * -1;
        var balance = LightMoney.Satoshis(21) + amountSettled;
        Assert.Equal(2, s.Driver.FindElements(By.CssSelector("#BoltBank-WalletTransactions tr")).Count);
        Assert.Equal(amount, amountMoney);
        Assert.Equal(amountSettled, amountSettledMoney);
        Assert.Contains("For Uncle Jim", s.Driver.FindElement(By.CssSelector("#BoltBank-WalletTransactions tr .transaction-description")).Text);
        Assert.Contains($"{amount.ToUnit(LightMoneyUnit.Satoshi)} sats", amountEl.Text);
        Assert.Contains($"{amountSettled.ToUnit(LightMoneyUnit.Satoshi)} sats", settledEl.Text);
        Assert.Contains($"{balance.ToUnit(LightMoneyUnit.Satoshi)} sats", s.Driver.FindElement(By.Id("BoltBank-WalletBalance")).Text);
    }
        
    [Fact(Timeout = TestTimeout)]
    [Trait("Lightning", "Lightning")]
    public async Task CanUseBoltBankAccessKeys()
    {
        using var s = CreateSeleniumTester();

        s.Server.ActivateLightning();
        await s.StartAsync();
        await s.Server.EnsureChannelsSetup();
        s.GoToRegister();
        var user = s.RegisterNewUser();

        s.GoToRegister();
        var admin = s.RegisterNewUser(true);

        // Skip create store
        s.Driver.FindElement(By.Id("SkipWizard")).Click();

        // Create new wallet
        s.Driver.FindElement(By.Id("Nav-BoltBank")).Click();
        var walletName = "AccessKeys" + RandomUtils.GetUInt64();
        s.Driver.FindElement(By.Id("Wallet_Name")).SendKeys(walletName);
        s.Driver.FindElement(By.Id("BoltBank-Create")).Click();
        Assert.Contains("Wallet successfully created.", s.FindAlertMessage().Text);

        s.Driver.FindElement(By.Id("BoltBank-WalletSettings")).Click();
        var walletId = s.Driver.FindElement(By.Id("BoltBank-WalletId")).Text;
        var walletNavId = $"Nav-BoltBank-Wallet-{walletId}";

        // Check if the user sees it
        s.Logout();
        s.LogIn(user);
        s.Driver.FindElement(By.Id("SkipWizard")).Click();
        s.Driver.AssertElementNotFound(By.Id(walletNavId));

        void SetAccessLevel(AccessLevel level)
        {
            s.Logout();
            s.LogIn(admin);
            s.Driver.FindElement(By.Id("SkipWizard")).Click();
            s.Driver.FindElement(By.Id(walletNavId)).Click();
            s.Driver.FindElement(By.Id("BoltBank-WalletSettings")).Click();
            s.Driver.FindElement(By.Id("SectionNav-WalletAccessKeys")).Click();
            s.Driver.FindElement(By.Id("AccessKey_Email")).SendKeys(user);
            var levelSelect = new SelectElement(s.Driver.FindElement(By.Id("AccessKey_Level")));
            levelSelect.SelectByValue(level.ToString());
            s.Driver.FindElement(By.Id("BoltBank-CreateAccessKey")).Click();
            Assert.Contains("Access key added successfully.", s.FindAlertMessage().Text);

            // Switch user
            s.Logout();
            s.LogIn(user);
            s.Driver.FindElement(By.Id(walletNavId)).Click();
        }

        // Add read-only access key for user
        SetAccessLevel(AccessLevel.ReadOnly);

        s.Driver.AssertElementNotFound(By.Id("BoltBank-WalletSend"));
        s.Driver.AssertElementNotFound(By.Id("BoltBank-WalletReceive"));
        s.Driver.AssertElementNotFound(By.Id("BoltBank-WalletSettings"));

        // Update access key for user: Invoice
        SetAccessLevel(AccessLevel.Invoice);

        s.Driver.AssertElementNotFound(By.Id("BoltBank-WalletSend"));
        s.Driver.AssertElementNotFound(By.Id("BoltBank-WalletSettings"));

        // Receive is allowed now
        var description = "My invoice";
        s.Driver.FindElement(By.Id("BoltBank-WalletReceive")).Click();
        s.Driver.FindElement(By.Id("Description")).SendKeys(description);
        s.Driver.SetCheckbox(By.Id("AttachDescription"), true);
        s.Driver.FindElement(By.Id("Amount")).Clear();
        s.Driver.FindElement(By.Id("Amount")).SendKeys("21");
        s.Driver.FindElement(By.Id("BoltBank-CreateInvoice")).Click();
        Assert.Contains(description, s.Driver.FindElement(By.Id("BoltBank-TransactionTitle")).Text);
        Assert.Contains("21 sats unpaid", s.Driver.FindElement(By.Id("BoltBank-TransactionAmount")).Text);
        var bolt11 = s.Driver.FindElement(By.Id("BoltBank-Transaction_PaymentRequest")).GetAttribute("value");
        Assert.StartsWith("ln", bolt11);

        // Update access key for user: Send
        SetAccessLevel(AccessLevel.Send);

        s.Driver.AssertElementNotFound(By.Id("BoltBank-WalletSettings"));

        // Send is allowed now
        s.Driver.FindElement(By.Id("BoltBank-WalletSend")).Click();
        s.Driver.FindElement(By.Id("Destination")).SendKeys(bolt11);
        s.Driver.FindElement(By.Id("BoltBank-Decode")).Click();
        Assert.Contains(description, s.Driver.FindElement(By.Id("Description")).GetAttribute("value"));
        Assert.Contains("21 sats", s.Driver.FindElement(By.Id("BoltBank-Amount")).Text);
        s.Driver.FindElement(By.Id("BoltBank-Send")).Click();
        Assert.Contains("Insufficient balance: 0 sats — tried to send 21 sats.", s.FindAlertMessage(StatusMessageModel.StatusSeverity.Error).Text);

        // Update access key for user: Send
        SetAccessLevel(AccessLevel.Admin);

        s.Driver.FindElement(By.Id("BoltBank-WalletSettings")).Click();
    }
}
