using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Configuration;
using BTCPayServer.Plugins.PodServer.Data.Models;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Options;

namespace BTCPayServer.Plugins.PodServer.Services.Imports;

public class ImportService
{
    private readonly IFileService _fileService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<DataDirectories> _dataDirectories;
    private readonly PodServerPluginDbContextFactory _dbContextFactory;

    public ImportService(
        IFileService fileService,
        IHttpClientFactory httpClientFactory,
        IOptions<DataDirectories> dataDirectories,
        PodServerPluginDbContextFactory dbContextFactory)
    {
        _fileService = fileService;
        _dbContextFactory = dbContextFactory;
        _httpClientFactory = httpClientFactory;
        _dataDirectories = dataDirectories;
        _dbContextFactory = dbContextFactory;
    }
    
    public async Task<IStoredFile> DownloadFile(Uri url, string userId)
    {
        return await _fileService.AddFile(url, userId);
    }
    
    public async Task<IEnumerable<Import>> GetUnfinishedImports()
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        return await dbContext.Imports
            .Where(i => i.Status != ImportStatus.Succeeded && i.Status != ImportStatus.Failed)
            .ToListAsync();
    }
    
    public async Task<Import> GetImport(string importId)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        return await dbContext.Imports
            .Where(i => i.ImportId == importId)
            .FirstOrDefaultAsync();
    }
    
    public async Task<Import> CreateImport(string rss, string podcastId, string userId)
    {
        var import = new Import { PodcastId = podcastId, UserId = userId, Raw = rss };
        return await AddOrUpdateImport(import);
    }
    
    public async Task UpdateStatus(Import import, ImportStatus status, string log = null)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        
        import.Status = status;
        if (!string.IsNullOrEmpty(log)) import.Log += log;
        
        dbContext.Imports.Update(import);
        await dbContext.SaveChangesAsync();
    }

    public async Task RemoveImport(Import import)
    {
        await using var dbContext = _dbContextFactory.CreateContext();
        dbContext.Imports.Remove(import);
        await dbContext.SaveChangesAsync();
    }

    private async Task<Import> AddOrUpdateImport(Import import)
    {
        await using var dbContext = _dbContextFactory.CreateContext();

        EntityEntry entry;
        if (string.IsNullOrEmpty(import.ImportId))
        {
            entry = await dbContext.Imports.AddAsync(import);
        }
        else
        {
            entry = dbContext.Update(import);
        }
        await dbContext.SaveChangesAsync();

        return (Import)entry.Entity;
    }

    private static string GetContentType(string filePath)
    {
        var mimeProvider = new FileExtensionContentTypeProvider();
        if (!mimeProvider.TryGetContentType(filePath, out string contentType))
        {
            contentType = "application/octet-stream";
        }

        return contentType;
    }
}
