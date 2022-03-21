using System.Xml;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.Configuration;
using BTCPayServer.Plugins.PodServer.Data.Models;
using BTCPayServer.Plugins.PodServer.Services.Podcasts;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;

namespace BTCPayServer.Plugins.PodServer.Services.Feeds;

public class FeedImporter
{
    private readonly IFileService _fileService;
    private readonly ILogger<FeedImporter> _logger;
    private readonly PodcastService _podcastService;
    private readonly PodServerPluginDbContextFactory _dbContextFactory;
    private readonly IOptions<DataDirectories> _dataDirectories;
    private readonly IHttpClientFactory _httpClientFactory;

    public FeedImporter(
        IFileService fileService,
        ILogger<FeedImporter> logger,
        PodcastService podcastService,
        PodServerPluginDbContextFactory dbContextFactory,
        IHttpClientFactory httpClientFactory,
        IOptions<DataDirectories> dataDirectories)
    {
        _logger = logger;
        _fileService = fileService;
        _podcastService = podcastService;
        _dbContextFactory = dbContextFactory;
        _httpClientFactory = httpClientFactory;
        _dataDirectories = dataDirectories;
    }

    public async Task<Podcast> Import(IFormFile rssFile, string userId)
    {
        if (!rssFile.ContentType.EndsWith("xml"))
        {
            throw new Exception($"Invalid RSS file: Content type {rssFile.ContentType} does not match XML.");
        }

        using var reader = new StreamReader(rssFile.OpenReadStream());
        var rss = await reader.ReadToEndAsync();

        XmlDocument doc = new();
        doc.LoadXml(rss);

        var channel = doc.SelectSingleNode("/rss/channel");
        if (channel == null)
        {
            throw new Exception("Invalid RSS file: Channel information missing.");
        }

        var title = channel["title"]?.InnerText;
        var description = channel["description"]?.InnerText;
        var url = channel["link"]?.InnerText;
        var language = channel["language"]?.InnerText;
        var category = channel["itunes:category"]?.Attributes["text"]?.Value;
        var imageUrl = channel["image"]?["url"]?.InnerText;
        var owner = channel["itunes:owner"]?["itunes:name"]?.InnerText;
        var email = channel["itunes:owner"]?["itunes:email"]?.InnerText;
        
        // TODO:
        // - Import image from URL
        // - Value info
        IStoredFile imageFile = null;
        if (!string.IsNullOrEmpty(imageUrl))
        {
            imageFile = await DownloadFile(new Uri(imageUrl), userId);
        }
        
        var podcast = new Podcast
        {
            UserId = userId,
            Title = title,
            Description = description,
            Language = language,
            Url = url,
            Category = category,
            Email = email,
            Owner = owner,
            ImageFileId = imageFile?.Id
        };

        await _podcastService.AddOrUpdatePodcast(podcast);
        
        // Create import job
        await CreateImport(podcast, rss);

        // Episodes
        var items = channel.SelectNodes("item");

        return podcast;
    }

    private async Task CreateImport(Podcast podcast, string rss)
    {
        var import = new Import
        {
            PodcastId = podcast.PodcastId,
            Raw = rss
        };
        await using var dbContext = _dbContextFactory.CreateContext();
        await dbContext.Imports.AddAsync(import);
        await dbContext.SaveChangesAsync();
    }
    
    private async Task<IStoredFile> DownloadFile(Uri url, string userId)
    {
        var fileName = Path.GetFileName(url.AbsolutePath);
        if (!fileName.IsValidFileName())
            throw new InvalidOperationException("Invalid file name");
            
        // download
        var filePath = Path.Join(_dataDirectories.Value.TempStorageDir, fileName);
        var httClient = _httpClientFactory.CreateClient();
        using var resp = await httClient.GetAsync(url);
        await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite);
        await resp.Content.CopyToAsync(stream);
        var file = new FormFile(stream, 0, stream.Length, fileName, fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = GetContentType(filePath)
        };
        await stream.FlushAsync();

        var storedFile = await _fileService.AddFile(file, userId);
        return storedFile;
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
