using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Muxarr.Core.Api;
using Muxarr.Core.Config;
using Muxarr.Data;
using Muxarr.Data.Entities;
using Muxarr.Data.Extensions;
using Muxarr.Web.Services;

namespace Muxarr.Tests.Integration;

/// <summary>
/// Integration-test harness for the conversion pipeline.
/// Builds a minimal DI container backed by a per-test SQLite file, applies
/// migrations, and exposes helpers to seed profiles, media files and
/// conversions without reaching into the Web project's hosting setup.
///
/// Does NOT register notification providers, the scheduled-service manager,
/// webhook services or anything else that would reach out to the network.
/// </summary>
public sealed class ConverterIntegrationFixture : IDisposable
{
    private readonly ServiceProvider _root;
    private readonly string _dbPath;

    public IServiceProvider Services => _root;
    public IServiceScopeFactory ScopeFactory { get; }
    public MediaConverterService Converter { get; }
    public MediaScannerService Scanner { get; }
    public string TempDir { get; }

    private ConverterIntegrationFixture(ServiceProvider root, string tempDir, string dbPath)
    {
        _root = root;
        _dbPath = dbPath;
        TempDir = tempDir;
        ScopeFactory = root.GetRequiredService<IServiceScopeFactory>();
        Converter = root.GetRequiredService<MediaConverterService>();
        Scanner = root.GetRequiredService<MediaScannerService>();
    }

    public static async Task<ConverterIntegrationFixture> CreateAsync(string tempDir)
    {
        var dbPath = Path.Combine(tempDir, "muxarr-it.db");
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={dbPath}"
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(b => b.ClearProviders().SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);

        services.AddHttpClient();
        services.AddHttpClient(ArrApiClient.HttpClientName,
            c => { c.Timeout = TimeSpan.FromSeconds(10); });

        services.AddDbContext<AppDbContext>();

        services.AddSingleton<ArrApiClient>();
        services.AddSingleton<ArrSyncService>();
        services.AddSingleton<MediaScannerService>();
        services.AddSingleton<MediaConverterService>();
        services.AddScoped<LibraryStatsService>();

        // Apply migrations + seed defaults BEFORE resolving services whose
        // constructors call ReloadConfig (ConfigurableServiceBase does this
        // eagerly) and would otherwise hit an unmigrated DB.
        var bootstrap = services.BuildServiceProvider();
        using (var scope = bootstrap.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await ctx.Database.MigrateAsync();
            ctx.Configs.Set(new ProcessingConfig
            {
                ScanIntervalMinutes = 0,
                ConversionTimeoutMinutes = 5,
                PostProcessingEnabled = false,
                PostProcessingCommand = string.Empty
            });
            await ctx.SaveChangesAsync();
        }
        await bootstrap.DisposeAsync();

        var root = services.BuildServiceProvider();
        return new ConverterIntegrationFixture(root, tempDir, dbPath);
    }

    // --- DB access helpers ---

    public async Task<T> WithDbContext<T>(Func<AppDbContext, Task<T>> fn)
    {
        using var scope = ScopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await fn(ctx);
    }

    public async Task WithDbContext(Func<AppDbContext, Task> fn)
    {
        using var scope = ScopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await fn(ctx);
    }

    // --- Seeding helpers ---

    /// <summary>
    /// Materializes a committed or derived fixture into the per-test temp dir
    /// and returns its new path. Tests mutate this copy freely.
    /// </summary>
    public string MaterializeFixture(string name, string? newName = null)
    {
        var source = FixtureFactory.Resolve(name);
        var dest = Path.Combine(TempDir, newName ?? name);
        File.Copy(source, dest, overwrite: true);
        return dest;
    }

    public async Task<Profile> SeedProfile(string name = "test-profile",
        bool clearVideoTrackNames = false, bool skipHardlinkedFiles = false)
    {
        return await WithDbContext(async ctx =>
        {
            var profile = new Profile
            {
                Name = name,
                Directories = new List<string> { TempDir },
                ClearVideoTrackNames = clearVideoTrackNames,
                SkipHardlinkedFiles = skipHardlinkedFiles,
                AudioSettings = new TrackSettings(),
                SubtitleSettings = new TrackSettings()
            };
            ctx.Profiles.Add(profile);
            await ctx.SaveChangesAsync();
            return profile;
        });
    }

    /// <summary>
    /// Scans a file with the real ffprobe-backed scanner and persists it.
    /// Returns a detached copy with tracks eagerly loaded.
    /// </summary>
    public async Task<MediaFile> ScanAndPersist(string filePath, Profile profile)
    {
        await Scanner.ScanFile(filePath, forceRescan: true, profile);

        return await WithDbContext(async ctx =>
        {
            var file = await ctx.MediaFiles.WithTracks()
                .FirstOrDefaultAsync(x => x.Path == filePath);
            Assert.IsNotNull(file, $"Scan did not persist {filePath}");
            return file;
        });
    }

    public async Task<MediaConversion> SeedConversion(MediaFile file, MediaSnapshot target,
        bool custom = false)
    {
        return await WithDbContext(async ctx =>
        {
            var conversion = new MediaConversion
            {
                MediaFileId = file.Id,
                SizeBefore = file.Size,
                SnapshotBefore = file.ToMediaSnapshot(),
                TargetSnapshot = target,
                State = ConversionState.New,
                Name = file.GetName(),
                IsCustomConversion = custom
            };
            ctx.MediaConversions.Add(conversion);
            await ctx.SaveChangesAsync();
            return conversion;
        });
    }

    public async Task<MediaConversion> ReloadConversion(int id)
    {
        return await WithDbContext(async ctx =>
        {
            var conv = await ctx.MediaConversions.FirstOrDefaultAsync(c => c.Id == id);
            Assert.IsNotNull(conv, $"Conversion #{id} not found");
            return conv;
        });
    }

    public async Task<MediaFile> ReloadFile(int id)
    {
        return await WithDbContext(async ctx =>
        {
            var file = await ctx.MediaFiles.WithTracks().FirstOrDefaultAsync(f => f.Id == id);
            Assert.IsNotNull(file, $"MediaFile #{id} not found");
            return file;
        });
    }

    public void Dispose()
    {
        try
        {
            _root.Dispose();
        }
        catch
        {
            // best effort
        }

        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
            // best effort - SQLite may still hold the file on some platforms
        }
    }
}
