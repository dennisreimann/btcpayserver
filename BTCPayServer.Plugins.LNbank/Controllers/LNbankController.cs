using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Plugins.LNbank
{
    [Route("LNbank")]
    public class LNbankController : Controller
    {
        public ActionResult Index()
        {
            return View();
        }
    }
}
