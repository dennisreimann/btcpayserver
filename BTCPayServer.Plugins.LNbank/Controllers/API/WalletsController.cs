using System.Threading.Tasks;
using BTCPayServer.Abstractions.Constants;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Client;
using BTCPayServer.Data;
using BTCPayServer.Plugins.LNbank.Data.API;
using BTCPayServer.Plugins.LNbank.Data.Models;
using BTCPayServer.Plugins.LNbank.Services.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using WalletData = BTCPayServer.Plugins.LNbank.Data.API.WalletData;

namespace BTCPayServer.Plugins.LNbank.Controllers.API;

[ApiController]
[Route("~/api/v1/lnbank/[controller]")]
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Greenfield, Policy = Policies.CanViewProfile)]
public class WalletsController : ControllerBase
{
    private readonly WalletService _walletService;
    private readonly UserManager<ApplicationUser> _userManager;

    public WalletsController(UserManager<ApplicationUser> userManager, WalletService walletService)
    {
        _userManager = userManager;
        _walletService = walletService;
    }

    // --- Custom methods ---

    [HttpPost("")]
    public async Task<IActionResult> CreateWallet(CreateWalletRequest request)
    {
        var validationResult = Validate(request);
        if (validationResult != null)
        {
            return validationResult;
        }
        
        var wallet = new Wallet
        {
            UserId = GetUserId(), 
            Name = request.Name
        };

        await _walletService.AddOrUpdateWallet(wallet);
        
        return Ok(FromModel(wallet));
    }

    private IActionResult Validate(WalletData request)
    {
        if (request is null)
        {
            return BadRequest();
        }

        if (string.IsNullOrEmpty(request.Name))
            ModelState.AddModelError(nameof(request.Name), "Name is missing");
        else if (request.Name.Length is < 1 or > 50)
            ModelState.AddModelError(nameof(request.Name), "Name can only be between 1 and 50 characters");

        return !ModelState.IsValid ? this.CreateValidationError(ModelState) : null;
    }

    private WalletData FromModel(Wallet model)
    {
        return new WalletData
        {
            Id = model.WalletId,
            Name = model.Name
        };
    }

    private string GetUserId() => _userManager.GetUserId(User);
}
