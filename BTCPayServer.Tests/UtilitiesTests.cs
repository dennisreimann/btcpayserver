using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Auth.AccessControlPolicy;
using Amazon.Runtime.Internal;
using BTCPayServer.Client;
using BTCPayServer.Client.Models;
using BTCPayServer.Controllers;
using ExchangeSharp;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.FileSystemGlobbing;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using Xunit;
using Xunit.Abstractions;
using static System.Net.Mime.MediaTypeNames;

namespace BTCPayServer.Tests
{
    /// <summary>
    /// This class hold easy to run utilities for dev time
    /// </summary>
    public class UtilitiesTests : UnitTestBase
    {
        public ITestOutputHelper Logs { get; }

        public UtilitiesTests(ITestOutputHelper logs) : base(logs)
        {
            Logs = logs;
        }
        internal static string GetSecuritySchemeDescription()
        {
            var description =
                "BTCPay Server supports authenticating and authorizing users through an API Key that is generated by them. Send the API Key as a header value to Authorization with the format: `token {token}`. For a smoother experience, you can generate a url that redirects users to an API key creation screen.\n\n The following permissions are available to the context of the user creating the API Key:\n\n#OTHERPERMISSIONS#\n\nThe following permissions are available if the user is an administrator:\n\n#SERVERPERMISSIONS#\n\nThe following permissions applies to all stores of the user, you can limit to a specific store with the following format: `btcpay.store.cancreateinvoice:6HSHAEU4iYWtjxtyRs9KyPjM9GAQp8kw2T9VWbGG1FnZ`:\n\n#STOREPERMISSIONS#\n\nNote that API Keys only limits permission of a user and can never expand it. If an API Key has the permission `btcpay.server.canmodifyserversettings` but that the user account creating this API Key is not administrator, the API Key will not be able to modify the server settings.\nSome permissions may include other permissions, see [this operation](#operation/permissionsMetadata).\n";

            var storePolicies =
                UIManageController.AddApiKeyViewModel.PermissionValueItem.PermissionDescriptions.Where(pair =>
                    Policies.IsStorePolicy(pair.Key) && !pair.Key.EndsWith(":", StringComparison.InvariantCulture));
            var serverPolicies =
                UIManageController.AddApiKeyViewModel.PermissionValueItem.PermissionDescriptions.Where(pair =>
                    Policies.IsServerPolicy(pair.Key));
            var otherPolicies =
                UIManageController.AddApiKeyViewModel.PermissionValueItem.PermissionDescriptions.Where(pair =>
                    !Policies.IsStorePolicy(pair.Key) && !Policies.IsServerPolicy(pair.Key));

            description = description.Replace("#OTHERPERMISSIONS#",
                    string.Join("\n", otherPolicies.Select(pair => $"* `{pair.Key}`: {pair.Value.Title}")))
                .Replace("#SERVERPERMISSIONS#",
                    string.Join("\n", serverPolicies.Select(pair => $"* `{pair.Key}`: {pair.Value.Title}")))
                .Replace("#STOREPERMISSIONS#",
                    string.Join("\n", storePolicies.Select(pair => $"* `{pair.Key}`: {pair.Value.Title}")));
            return description;
        }

        //        /// <summary>
        //        /// This will take the translations from v1 or v2
        //        /// and upload them to transifex if not found
        //        /// </summary>
        //        [FactWithSecret("TransifexAPIToken")]
        //        [Trait("Utilities", "Utilities")]
        //#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        //        public async Task UpdateTransifex()
        //        {
        //            // DO NOT RUN IT, THIS WILL ERASE THE CURRENT TRANSIFEX TRANSLATIONS

        //            var client = GetTransifexClient();
        //            var translations = JsonTranslation.GetTranslations(TranslationFolder.CheckoutV2);
        //            var enTranslations = translations["en"];
        //            translations.Remove("en");

        //            foreach (var t in translations)
        //            {
        //                foreach (var w in t.Value.Words.ToArray())
        //                {
        //                    if (t.Value.Words[w.Key] == null)
        //                        t.Value.Words[w.Key] = enTranslations.Words[w.Key];
        //                }
        //                t.Value.Words.Remove("code");
        //                t.Value.Words.Remove("NOTICE_WARN");
        //            }
        //            await client.UpdateTranslations(translations);
        //        }

        //#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously

        ///// <summary>
        ///// This utility will copy translations made on checkout v1 to checkout v2
        ///// </summary>
        //[Fact]
        //[Trait("Utilities", "Utilities")]
        //public void SetTranslationV1ToV2()
        //{
        //    var mappings = new Dictionary<string, string>();
        //    foreach (var kv in JsonTranslation.GetTranslations(TranslationFolder.CheckoutV1))
        //    {
        //        var v1File = kv.Value;
        //        var v2File = JsonTranslation.GetTranslation(TranslationFolder.CheckoutV2, v1File.Lang);
        //        if (mappings.Count == 0)
        //        {
        //            foreach (var prop1 in v1File.Words)
        //                foreach (var prop2 in v2File.Words)
        //                {
        //                    if (Normalize(prop1.Key) == Normalize(prop2.Key))
        //                        mappings.Add(prop1.Key, prop2.Key);
        //                }
        //            mappings.Add("Copied", "copy_confirm");
        //            mappings.Add("ConversionTab_BodyDesc", "conversion_body");
        //            mappings.Add("Return to StoreName", "return_to_store");
        //        }
        //        foreach (var m in mappings)
        //        {
        //            var orig = v1File.Words[m.Key];
        //            v2File.Words[m.Value] = orig;
        //        }
        //        v2File.Words["currentLanguage"] = v1File.Words["currentLanguage"];
        //        v2File.Save();
        //    }
        //}

        //private string Normalize(string name)
        //{
        //    return name.Replace("_", "").ToLowerInvariant();
        //}

        /// <summary>
        /// This utility will use selenium to pilot your browser to
        /// automatically translate a language.
        /// 
        /// Step 1: Close all Chrome instances
        /// Step2: Edit "v1" variable if want to translate checkout v1 or v2
        ///   - Windows: "chrome.exe --remote-debugging-port=9222 https://chat.openai.com/"
        ///   - Linux: "google-chrome --remote-debugging-port=9222 https://chat.openai.com/"
        /// Step 3: Run this.
        /// </summary>
        /// <returns></returns>
        [Trait("Utilities", "Utilities")]
        [FactWithSecret("TransifexAPIToken")]
        public async Task AutoTranslateChatGPT()
        {
            var file = TranslationFolder.CheckoutV2;

            using var driver = new ChromeDriver(new ChromeOptions()
            {
                DebuggerAddress = "127.0.0.1:9222"
            });

            var englishTranslations = JsonTranslation.GetTranslation(file, "en");

            TransifexClient client = GetTransifexClient();
            var langs = await client.GetLangs(englishTranslations.TransifexProject, englishTranslations.TransifexResource);
            foreach (var lang in langs)
            {
                if (lang == "en")
                    continue;
                var jsonLangCode = GetLangCodeTransifexToJson(lang);
                var v1LangFile = JsonTranslation.GetTranslation(TranslationFolder.CheckoutV1, jsonLangCode);

                if (!v1LangFile.Exists())
                    continue;
                var languageCurrent = v1LangFile.Words["currentLanguage"];
                if (v1LangFile.ShouldSkip())
                {
                    Logs.WriteLine("Skipped " + jsonLangCode);
                    continue;
                }

                var langFile = JsonTranslation.GetTranslation(file, jsonLangCode);
                bool askedPrompt = false;
                foreach (var translation in langFile.Words)
                {
                    if (translation.Key == "NOTICE_WARN" ||
                        translation.Key == "currentLanguage" ||
                        translation.Key == "code")
                        continue;

                    var english = englishTranslations.Words[translation.Key];
                    if (translation.Value != null)
                        continue; // Already translated

                    //TODO: A better way to avoid rate limits is to use this format:
                    //I am translating a checkout crypto payment page, and I want you to translate it from English (en-US) to French (fr-FR).
                    //##
                    //English: This invoice will expire in
                    //French:
                    //##
                    //English: Scan the QR code, or tap to copy the address.
                    //French:
                    //##
                    //English: Your payment has been received and is now processing.
                    //French:

                    if (!askedPrompt)
                    {
                        driver.FindElements(By.XPath("//button[contains(@class,'text-token-text-primary')]")).Where(e => e.Displayed).First().Click();
                        Thread.Sleep(200);
                        var input = driver.FindElement(By.XPath("//textarea[@data-id]"));
                        input.SendKeys($"I am translating a checkout crypto payment page, and I want you to translate it from English (en-US) to {languageCurrent} ({jsonLangCode}).");
                        input.SendKeys(Keys.LeftShift + Keys.Enter);
                        input.SendKeys("Reply only with the translation of the sentences I will give you and nothing more, and do not translate what is inside `{{` and `}}`." + Keys.Enter);
                        WaitCanWritePrompt(driver);
                        askedPrompt = true;
                    }
                    english = english.Replace('\n', ' ');

                    driver.FindElement(By.XPath("//textarea[@data-id]")).SendKeys(english + Keys.Enter);
                    WaitCanWritePrompt(driver);
                    string result = GetLastResponse(driver);
                    langFile.Words[translation.Key] = result;
                }
                langFile.Save();
            }
        }

        private static string GetLastResponse(ChromeDriver driver)
        {
            var elements = driver.FindElements(By.XPath("//div[contains(@class,'markdown') and contains(@class,'prose')]//p"));
            var result = elements.LastOrDefault()?.Text;
            return result;
        }

        private static TransifexClient GetTransifexClient()
        {
            return new TransifexClient(FactWithSecretAttribute.GetFromSecrets("TransifexAPIToken"));
        }

        private void WaitCanWritePrompt(IWebDriver driver)
        {
            bool stopGenerating = false;
retry:
            Thread.Sleep(200);
            try
            {
                var el = driver.FindElement(By.XPath("//button[contains(@aria-label, 'Stop generating')]"));
                if (!el.Enabled)
                    goto retry;
                stopGenerating = true;
                goto retry;
            }
            catch
            {
                if (!stopGenerating)
                    goto retry;
            }
            try
            {
                var el = driver.FindElement(By.XPath("//button[contains(@data-testid, 'send-button')]"));
                if (!el.Displayed)
                    goto retry;
            }
            catch
            {
                goto retry;
            }
            Thread.Sleep(200);
        }

        class TranslatedKeyNodeWalker : IntermediateNodeWalker
        {
            private List<string> _defaultTranslatedKeys;
            private string _txt;

            public TranslatedKeyNodeWalker(List<string> defaultTranslatedKeys)
            {
                _defaultTranslatedKeys = defaultTranslatedKeys;
            }

            public TranslatedKeyNodeWalker(List<string> defaultTranslatedKeys, string txt) : this(defaultTranslatedKeys)
            {
                _txt = txt;
            }

            public override void VisitTagHelper(TagHelperIntermediateNode node)
            {
                if (node.TagName == "input")
                {
                    foreach (var tagHelper in node.TagHelpers)
                    {
                        if (tagHelper.Name.EndsWith("TranslateTagHelper"))
                        {
                            var inner = ToString(node);
                            if (inner.Contains("type=\"submit\""))
                            {
                                var m = Regex.Match(inner, "value=\"(.*?)\"");
                                if (m.Success)
                                {
                                    _defaultTranslatedKeys.Add(m.Groups[1].Value);
                                }
                            }
                        }
                    }
                    return;
                }
                foreach (var tagHelper in node.TagHelpers)
                {
                    if (tagHelper.Name.EndsWith("TranslateTagHelper"))
                    {
                        var htmlContent = node.FindDescendantNodes<HtmlContentIntermediateNode>().FirstOrDefault();
                        if (htmlContent is not null)
                        {
                            var inner = ToString(htmlContent);
                            _defaultTranslatedKeys.Add(inner);
                        }
                    }
                }
                base.VisitTagHelper(node);
            }

            private string ToString(IntermediateNode node)
            {
                return _txt.Substring(node.Source.Value.AbsoluteIndex, node.Source.Value.Length);
            }
        }

        /// <summary>
        /// This utilities crawl through the cs files in search for
        /// Display attributes, then update Translations.Default to list them
        /// </summary>
        [Trait("Utilities", "Utilities")]
        [Fact]
        public async Task UpdateDefaultTranslations()
        {
            var soldir = TestUtils.TryGetSolutionDirectoryInfo();
            List<string> defaultTranslatedKeys = new List<string>();

            // Go through all cs files, and find [Display] and [DisplayName] attributes
            foreach (var file in soldir.EnumerateFiles("*.cs", SearchOption.AllDirectories))
            {
                var txt = File.ReadAllText(file.FullName);
                var tree = CSharpSyntaxTree.ParseText(txt, new CSharpParseOptions(LanguageVersion.Default));
                var walker = new DisplayNameWalker();
                walker.Visit(tree.GetRoot());
                foreach (var k in walker.Keys)
                {
                    defaultTranslatedKeys.Add(k);
                }

                AddLocalizers(defaultTranslatedKeys, txt);
            }

            // Go through all cshtml file, search for text-translate or ViewLocalizer usage
            using (var tester = CreateServerTester(newDb: true))
            {
                await tester.StartAsync();
                var engine = tester.PayTester.GetService<RazorProjectEngine>();
                var files = soldir.EnumerateFiles("*.cshtml", SearchOption.AllDirectories)
                    .Union(soldir.EnumerateFiles("*.razor", SearchOption.AllDirectories));
                foreach (var file in files)
                {
                    var filePath = file.FullName;
                    var txt = File.ReadAllText(file.FullName);
                    AddLocalizers(defaultTranslatedKeys, txt);

                    filePath = filePath.Replace(Path.Combine(soldir.FullName, "BTCPayServer"), "/");
                    var item = engine.FileSystem.GetItem(filePath);

                    var node = (DocumentIntermediateNode)engine.Process(item).Items[typeof(DocumentIntermediateNode)];
                    var w = new TranslatedKeyNodeWalker(defaultTranslatedKeys, txt);
                    w.Visit(node);
                }

            }
            defaultTranslatedKeys = defaultTranslatedKeys.Select(d => d.Trim().Replace("\r\n", "\n")).Distinct().OrderBy(o => o).ToList();
            JObject obj = new JObject();
            foreach (var v in defaultTranslatedKeys)
            {
                obj.Add(v, "");
            }

            var path = Path.Combine(soldir.FullName, "BTCPayServer/Services/Translations.Default.cs");
            var defaultTranslation = File.ReadAllText(path);
            var startIdx = defaultTranslation.IndexOf("\"\"\"");
            var endIdx = defaultTranslation.LastIndexOf("\"\"\"");
            var content = defaultTranslation.Substring(0, startIdx + 3);
            content += "\n" + obj.ToString(Formatting.Indented) + "\n";
            content += defaultTranslation.Substring(endIdx);
            File.WriteAllText(path, content);
        }

        private static void AddLocalizers(List<string> defaultTranslatedKeys, string txt)
        {
            foreach (string localizer in new[] { "ViewLocalizer", "StringLocalizer" })
            {
                if (txt.Contains(localizer))
                {
                    var matches = Regex.Matches(txt, localizer + "\\[\"(.*?)\"[\\],]");
                    foreach (Match match in matches)
                    {
                        var k = match.Groups[1].Value;
                        k = k.Replace("\\", "");
                        defaultTranslatedKeys.Add(k);
                    }
                }
            }
        }

        class DisplayNameWalker : CSharpSyntaxWalker
        {
            public List<string> Keys = new List<string>();
            public bool InAttribute = false;
            public override void VisitAttribute(AttributeSyntax node)
            {
                InAttribute = true;
                base.VisitAttribute(node);
                InAttribute = false;
            }
            public override void VisitIdentifierName(IdentifierNameSyntax node)
            {
                if (InAttribute)
                {
                    InAttribute = node.Identifier.Text switch
                    {
                        "Display" => true,
                        "DisplayAttribute" => true,
                        "DisplayName" => true,
                        "DisplayNameAttribute" => true,
                        _ => false
                    };
                }
            }
            public override void VisitAttributeArgument(AttributeArgumentSyntax node)
            {
                if (InAttribute)
                {
                    var name = node.Expression switch
                    {
                        LiteralExpressionSyntax les => les.Token.ValueText,
                        IdentifierNameSyntax ins => ins.Identifier.Text,
                        _ => throw new InvalidOperationException("Unknown node")
                    };
                    Keys.Add(name);
                    InAttribute = false;
                }
            }
        }

        /// <summary>
        /// This utility will make sure that permission documentation is properly written in swagger.template.json
        /// </summary>
        [Trait("Utilities", "Utilities")]
        [Fact]
        public void UpdateSwagger()
        {
            var filePath = Path.Combine(TestUtils.TryGetSolutionDirectoryInfo().FullName, "BTCPayServer", "wwwroot", "swagger", "v1", "swagger.template.json");
            var o = JObject.Parse(File.ReadAllText(filePath));
            o["components"]["securitySchemes"]["API_Key"]["description"] = GetSecuritySchemeDescription();
            File.WriteAllText(filePath, o.ToString(Newtonsoft.Json.Formatting.Indented));
        }

        /// <summary>
        /// Download transifex transactions and put them in BTCPayServer\wwwroot\locales and BTCPayServer\wwwroot\locales\checkout
        /// </summary>
        [FactWithSecret("TransifexAPIToken")]
        [Trait("Utilities", "Utilities")]
        public async Task PullTransifexTranslations()
        {
            // 1. Generate an API Token on https://www.transifex.com/user/settings/api/
            // 2. Run "dotnet user-secrets set TransifexAPIToken <youapitoken>"
            await PullTransifexTranslationsCore(TranslationFolder.CheckoutV1);
            await PullTransifexTranslationsCore(TranslationFolder.CheckoutV2);

        }

        private async Task PullTransifexTranslationsCore(TranslationFolder folder)
        {
            var enTranslation = JsonTranslation.GetTranslation(folder, "en");
            var client = GetTransifexClient();
            var langs = await client.GetLangs(enTranslation.TransifexProject, enTranslation.TransifexResource);
            var resourceStrings = await client.GetResourceStrings(enTranslation.TransifexResource);

            enTranslation.Words.Clear();
            enTranslation.Translate(resourceStrings.SourceTranslations);
            enTranslation.Save();

            Task.WaitAll(langs.Select(async l =>
            {
                if (l == "en")
                    return;
retry:
                try
                {
                    var langCode = GetLangCodeTransifexToJson(l);
                    var langTranslations = await client.GetTranslations(resourceStrings, l);
                    var translation = JsonTranslation.GetTranslation(folder, langCode);
                    if (translation.ShouldSkip())
                    {
                        Logs.WriteLine("Skipping " + langCode);
                        return;
                    }

                    if (translation.Words.ContainsKey("InvoiceExpired_Body_3") && translation.Words["InvoiceExpired_Body_3"] == enTranslation.Words["InvoiceExpired_Body_3"])
                    {
                        translation.Words["InvoiceExpired_Body_3"] = string.Empty;
                    }
                    translation.Translate(langTranslations);
                    translation.Save();
                }
                catch
                {
                    await Task.Delay(1000);
                    goto retry;
                }
            }).ToArray());
        }

        internal static string GetLangCodeTransifexToJson(string l)
        {
            if (l == "ne_NP")
                l = "np-NP";
            if (l == "zh_CN")
                l = "zh-SP";
            if (l == "kk")
                l = "kk-KZ";

            return l.Replace("_", "-");
        }
        internal static string GetLangCodeJsonToTransifex(string l)
        {
            if (l == "np-NP")
                l = "ne_NP";
            if (l == "zh-SP")
                l = "zh_CN";
            if (l == "kk-KZ")
                l = "kk";

            return l.Replace("-", "_");
        }
    }

    public class TransifexClient
    {
        public TransifexClient(string apiToken)
        {
            Client = new HttpClient();
            APIToken = apiToken;
        }

        public HttpClient Client { get; }
        public string APIToken { get; }

        public async Task<JObject> GetTransifexAsync(string uri)
        {
            var message = new HttpRequestMessage(HttpMethod.Get, uri);
            message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", APIToken);
            message.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
            using var response = await Client.SendAsync(message);
            var str = await response.Content.ReadAsStringAsync();
            return JObject.Parse(str);
        }

        public async Task UpdateTranslations(Dictionary<string, JsonTranslation> translations)
        {
            var resourceStrings = await GetResourceStrings(translations.First().Value.TransifexResource);
            List<JObject> patches = new List<JObject>();
            List<JObject[]> batches = new List<JObject[]>();
            foreach (var translation in translations.Values)
            {
                foreach (var word in translation.Words)
                {
                    if (word.Key == "NOTICE_WARN")
                        continue;
                    patches.Add(new JObject()
                    {
                        ["id"] = $"{translation.TransifexResource}:s:{resourceStrings.KeyToHashMapping[word.Key]}:l:{UtilitiesTests.GetLangCodeJsonToTransifex(translation.Lang)}",
                        ["type"] = "resource_translations",
                        ["attributes"] = new JObject()
                        {
                            ["strings"] = word.Value is null ? null : new JObject()
                            {
                                ["other"] = word.Value
                            }
                        }
                    });
                    if (patches.Count >= 150)
                    {
                        batches.Add(patches.ToArray());
                        patches = new List<JObject>();
                    }
                }
                if (patches.Count > 0)
                {
                    batches.Add(patches.ToArray());
                    patches = new List<JObject>();
                }
            }

            if (patches.Count > 0)
            {
                batches.Add(patches.ToArray());
                patches = new List<JObject>();
            }
            await Task.WhenAll(batches.Select(async batch =>
            {
                var message = new HttpRequestMessage(HttpMethod.Get, "https://rest.api.transifex.com/resource_translations");
                message.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", APIToken);
                message.Method = HttpMethod.Patch;
                var content = new StringContent(new JObject()
                {
                    ["data"] = new JArray(batch.OfType<object>().ToArray())
                }.ToString(), Encoding.UTF8);
                content.Headers.Remove("Content-Type");
                content.Headers.TryAddWithoutValidation("Content-Type", "application/vnd.api+json;profile=\"bulk\"");
                message.Content = content;
                using var response = await Client.SendAsync(message);
                await response.Content.ReadAsStringAsync();
            }).ToArray());
        }

        public async Task<Dictionary<string, string>> GetTranslations(ResourceStrings resourceStrings, string lang)
        {
            var j = await GetTransifexAsync($"https://rest.api.transifex.com/resource_translations?filter[resource]={resourceStrings.ResourceId}&filter[language]=l:{lang}");
            if (j["data"] is null)
            {
                return resourceStrings.SourceTranslations.ToDictionary(kv => kv.Key, kv => null as string);
            }
            return
                    j["data"].Select(o => (Key: resourceStrings.GetKey(o["id"].Value<string>()), Strings: o["attributes"]["strings"]))
                    .ToDictionary(
                        o => o.Key,
                        o => o.Strings.Type == JTokenType.Null ? null : o.Strings["other"].Value<string>());
        }

        public async Task<string[]> GetLangs(string projectId, string resourceId)
        {
            var json = await GetTransifexAsync($"https://rest.api.transifex.com/resource_language_stats?filter[project]={projectId}&filter[resource]={resourceId}");
            return json["data"].Select(o => o["id"].Value<string>().Split(':').Last()).ToArray();
        }


        public async Task<ResourceStrings> GetResourceStrings(string resourceId)
        {
            var res = new ResourceStrings();
            res.ResourceId = resourceId;
            var json = await GetTransifexAsync($"https://rest.api.transifex.com/resource_strings?filter[resource]={resourceId}");
            res.HashToKeyMapping =
                json["data"]
                .ToDictionary(
                o => o["id"].Value<string>().Split(':').Last(),
                o => o["attributes"]["key"].Value<string>().Replace("\\.", "."));
            res.KeyToHashMapping = res.HashToKeyMapping.ToDictionary(o => o.Value, o => o.Key);
            res.SourceTranslations =
                json["data"]
                .ToDictionary(
                    o => o["attributes"]["key"].Value<string>().Replace("\\.", "."),
                    o => o["attributes"]["strings"]["other"].Value<string>());
            return res;
        }
    }

    public class ResourceStrings
    {
        public string ResourceId { get; set; }
        public Dictionary<string, string> HashToKeyMapping { get; set; }
        public Dictionary<string, string> SourceTranslations { get; set; }
        public Dictionary<string, string> KeyToHashMapping { get; internal set; }

        public string GetKey(string hash)
        {
            if (HashToKeyMapping.TryGetValue(hash, out var v))
                return v;
            hash = hash.Split(':')[^3];
            if (HashToKeyMapping.TryGetValue(hash, out v))
                return v;
            throw new InvalidOperationException();
        }
    }

    public enum TranslationFolder
    {
        CheckoutV1,
        CheckoutV2
    }
    public class JsonTranslation
    {

        public static Dictionary<string, JsonTranslation> GetTranslations(TranslationFolder folder)
        {
            var res = new Dictionary<string, JsonTranslation>();
            var source = GetTranslation(null, folder, "en");
            foreach (var f in Directory.GetFiles(GetFolder(folder)))
            {
                var lang = Path.GetFileNameWithoutExtension(f);
                res.Add(lang, GetTranslation(source, folder, lang));
            }
            return res;
        }
        public static JsonTranslation GetTranslation(TranslationFolder folder, string lang)
        {
            var source = GetTranslation(null, folder, "en");
            return GetTranslation(source, folder, lang);
        }
        private static JsonTranslation GetTranslation(JsonTranslation sourceTranslation, TranslationFolder folder, string lang)
        {
            var fullPath = Path.Combine(GetFolder(folder), $"{lang}.json");
            var proj = "o:btcpayserver:p:btcpayserver";
            var resource = $"{proj}:r:checkout-v2";
            var words = new Dictionary<string, string>();
            if (File.Exists(fullPath))
            {
                var obj = JObject.Parse(File.ReadAllText(fullPath));
                foreach (var prop in obj.Properties())
                    words.Add(prop.Name, prop.Value.Value<string>());
            }
            if (sourceTranslation != null)
            {
                foreach (var w in sourceTranslation.Words)
                {
                    if (!words.ContainsKey(w.Key))
                        words.Add(w.Key, null);
                }
            }
            return new JsonTranslation()
            {
                FullPath = fullPath,
                Lang = lang,
                Words = words,
                TransifexProject = proj,
                TransifexResource = resource
            };
        }

        private static string GetFolder(TranslationFolder file)
        {
            if (file == TranslationFolder.CheckoutV1)
                return Path.Combine(TestUtils.TryGetSolutionDirectoryInfo().FullName, "BTCPayServer", "wwwroot", "locales");
            else
                return Path.Combine(TestUtils.TryGetSolutionDirectoryInfo().FullName, "BTCPayServer", "wwwroot", "locales", "checkout");
        }

        public string Lang { get; set; }
        public Dictionary<string, string> Words { get; set; }


        public string FullPath { get; set; }
        public string TransifexProject { get; set; }
        public string TransifexResource { get; private set; }

        public void Save()
        {
            JObject obj = new JObject
            {
                { "NOTICE_WARN", "THIS CODE HAS BEEN AUTOMATICALLY GENERATED FROM TRANSIFEX, IF YOU WISH TO HELP TRANSLATION COME ON THE SLACK https://chat.btcpayserver.org/ TO REQUEST PERMISSION TO https://www.transifex.com/btcpayserver/btcpayserver/" },
                { "code", Lang },
                { "currentLanguage", Words["currentLanguage"] }
            };
            foreach (var kv in Words)
            {
                if (obj[kv.Key] is not null)
                    continue;
                if (kv.Value is null)
                    continue;
                obj.Add(kv.Key, kv.Value);
            }
            try
            {
                File.WriteAllText(FullPath, obj.ToString(Newtonsoft.Json.Formatting.Indented));
            }
            catch (FileNotFoundException)
            {
                File.Create(FullPath).Close();
                File.WriteAllText(FullPath, obj.ToString(Newtonsoft.Json.Formatting.Indented));
            }
        }

        public void Translate(Dictionary<string, string> sourceTranslations)
        {
            foreach (var o in sourceTranslations)
                if (o.Value != null)
                    Words.AddOrReplace(o.Key, o.Value);
        }

        public bool ShouldSkip()
        {
            if (!Words.ContainsKey("currentLanguage"))
                return true;
            if (Words["currentLanguage"] == "English")
                return true;
            if (Words["currentLanguage"] == "disable")
                return true;
            return false;
        }

        public bool Exists()
        {
            return File.Exists(FullPath);
        }
    }
}
