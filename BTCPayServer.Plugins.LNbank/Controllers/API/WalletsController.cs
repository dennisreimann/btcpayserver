using System;
using System.Linq;
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
[Authorize(AuthenticationSchemes = AuthenticationSchemes.Greenfield, Policy = Policies.CanModifyProfile)]
public class WalletsController : ControllerBase
{
    private readonly WalletService _walletService;
    private readonly UserManager<ApplicationUser> _userManager;

    public WalletsController(UserManager<ApplicationUser> userManager, WalletService walletService)
    {
        _userManager = userManager;
        _walletService = walletService;
    }
    
    [HttpGet("")]
    public async Task<IActionResult> GetWallets()
    {
        var wallets = await _walletService.GetWallets(new WalletsQuery {
            UserId = new[] { GetUserId() },
            IncludeTransactions = true,
            IncludeAccessKeys = true
        });

        return Ok(wallets.Select(FromModel));
    }

    [HttpPost("")]
    public async Task<IActionResult> CreateWallet(EditWalletRequest request)
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

        var entry = await _walletService.AddOrUpdateWallet(wallet);
        
        return Ok(FromModel(entry));
    }
    
    [HttpGet("{walletId}")]
    public async Task<IActionResult> GetWallet(string walletId)
    {
        var wallet = await _walletService.GetWallet(new WalletQuery {
            UserId = GetUserId(),
            WalletId = walletId,
            IncludeTransactions = true,
            IncludeAccessKeys = true
        });

        if (wallet == null) 
            return this.CreateAPIError(404, "wallet-not-found", "The wallet was not found");

        return Ok(FromModel(wallet));
    }
    
    [HttpPut("{walletId}")]
    public async Task<IActionResult> UpdateWallet(string walletId, EditWalletRequest request)
    {
        var validationResult = Validate(request);
        if (validationResult != null)
        {
            return validationResult;
        }

        var wallet = await _walletService.GetWallet(new WalletQuery {
            UserId = GetUserId(),
            WalletId = walletId,
            IncludeTransactions = true,
            IncludeAccessKeys = true
        });

        if (wallet == null) 
            return this.CreateAPIError(404, "wallet-not-found", "The wallet was not found");

        wallet.Name = request.Name;

        var entry = await _walletService.AddOrUpdateWallet(wallet);

        return Ok(FromModel(entry));
    }
    
    [HttpDelete("{walletId}")]
    public async Task<IActionResult> DeleteWallet(string walletId)
    {
        var wallet = await _walletService.GetWallet(new WalletQuery {
            UserId = GetUserId(),
            WalletId = walletId,
            IncludeTransactions = true,
            IncludeAccessKeys = true
        });

        if (wallet == null) 
            return this.CreateAPIError(404, "wallet-not-found", "The wallet was not found");

        try
        {
            await _walletService.RemoveWallet(wallet);

            return Ok();
        }
        catch (Exception e)
        {
            return this.CreateAPIError("wallet-not-empty", e.Message);
        }
    }

    private IActionResult Validate(EditWalletRequest request)
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

    private WalletData FromModel(Wallet model) =>
        new()
        {
            Id = model.WalletId,
            Name = model.Name,
            CreatedAt = model.CreatedAt,
            Balance = model.Balance,
            AccessKey = model.AccessKeys.First().Key
        };

    private string GetUserId() => _userManager.GetUserId(User);
}
