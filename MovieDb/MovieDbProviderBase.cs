using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Serialization;

namespace MovieDb;

public abstract class MovieDbProviderBase
{
	public class GuestStar
	{
		public int id { get; set; }

		public string name { get; set; }

		public string credit_id { get; set; }

		public string character { get; set; }

		public int order { get; set; }

		public string profile_path { get; set; }
	}

	public class RootObject
	{
		public DateTimeOffset air_date { get; set; }

		public int episode_number { get; set; }

		public string name { get; set; }

		public string overview { get; set; }

		public int id { get; set; }

		public object production_code { get; set; }

		public int season_number { get; set; }

		public string still_path { get; set; }

		public double vote_average { get; set; }

		public int vote_count { get; set; }

		public TmdbImages images { get; set; }

		public TmdbExternalIds external_ids { get; set; }

		public TmdbCredits credits { get; set; }

		public TmdbVideos videos { get; set; }
	}

	public const string BaseMovieDbUrl = "https://tmdb.kingscross.online:8333/";

	internal static string AcceptHeader = "application/json,image/*";

	private const string TmdbConfigPath = "3/configuration";

	private const string TmdbLanguagesPath = "3/configuration/primary_translations";

	private const string EpisodeUrlPattern = "3/tv/{0}/season/{1}/episode/{2}";

	protected readonly IHttpClient HttpClient;

	protected readonly IServerConfigurationManager ConfigurationManager;

	protected readonly IJsonSerializer JsonSerializer;

	protected readonly IFileSystem FileSystem;

	protected readonly ILocalizationManager Localization;

	protected readonly ILogger Logger;

	protected readonly ILogManager LogManager;

	protected readonly IServerApplicationHost AppHost;

	protected readonly ILibraryManager LibraryManager;

	public static TimeSpan CacheTime = TimeSpan.FromHours(6.0);

	private static TmdbSettingsResult _tmdbSettings;

	private static string[] _tmdbLanguages;

	private static long _lastRequestTicks;

	private static int requestIntervalMs = 100;

	private static readonly object _settingsLock = new object();

	private readonly ILogger _logger;

	public string Name => ProviderName;

	public static string ProviderName => "TheMovieDb";

	protected string GetApiUrl(string path)
	{
		try
		{
			PluginOptions config = GetConfiguration();
			string baseUrl = config.TmdbApiBaseUrl?.TrimEnd(new char[1] { '/' }) ?? "https://tmdb.kingscross.online:8333";
			string apiKey = config.ApiKey;
			if (string.IsNullOrEmpty(apiKey))
			{
				Logger.Error("TMDB API key is not configured");
				throw new InvalidOperationException("TMDB API key is not configured");
			}
			return baseUrl + "/" + path + "?api_key=" + apiKey;
		}
		catch (Exception ex)
		{
			Logger.Error("Error generating API URL: {0}", ex);
			throw;
		}
	}

	protected string GetImageUrl(string imagePath)
	{
		try
		{
			if (string.IsNullOrEmpty(imagePath))
			{
				return null;
			}
			PluginOptions config = GetConfiguration();
			string baseUrl = config.TmdbImageBaseUrl?.TrimEnd(new char[1] { '/' }) ?? "https://image.tmdb.org/t/p/";
			return baseUrl + "/" + imagePath.TrimStart(new char[1] { '/' });
		}
		catch (Exception ex)
		{
			Logger.Error("Error generating image URL: {0}", ex);
			throw;
		}
	}

	public async Task<TmdbSettingsResult> GetTmdbSettings(CancellationToken cancellationToken)
	{
		if (_tmdbSettings != null)
		{
			EnsureImageUrls(_tmdbSettings);
			return _tmdbSettings;
		}
		HttpResponseInfo response = null;
		try
		{
			response = await GetMovieDbResponse(new HttpRequestOptions
			{
				Url = GetApiUrl("3/configuration"),
				CancellationToken = cancellationToken,
				AcceptHeader = AcceptHeader
			}).ConfigureAwait(continueOnCapturedContext: false);
			using Stream json = response.Content;
			using StreamReader reader = new StreamReader(json);
			string text = await reader.ReadToEndAsync().ConfigureAwait(continueOnCapturedContext: false);
			Logger.Info("MovieDb settings: {0}", text);
			lock (_settingsLock)
			{
				_tmdbSettings = JsonSerializer.DeserializeFromString<TmdbSettingsResult>(text);
				EnsureImageUrls(_tmdbSettings);
			}
		}
		catch (Exception ex2)
		{
			Exception ex = ex2;
			Logger.Error("Error getting TMDb settings: {0}", ex);
			lock (_settingsLock)
			{
				_tmdbSettings = new TmdbSettingsResult
				{
					images = new TmdbImageSettings
					{
						secure_base_url = GetConfiguration().TmdbImageBaseUrl
					}
				};
			}
		}
		finally
		{
			if (response != null)
			{
				((IDisposable)response)?.Dispose();
			}
		}
		return _tmdbSettings;
	}

	private void EnsureImageUrls(TmdbSettingsResult settings)
	{
		if (settings?.images != null)
		{
			settings.images.secure_base_url = GetConfiguration().TmdbImageBaseUrl;
		}
	}

	public MovieDbProviderBase(IHttpClient httpClient, IServerConfigurationManager configurationManager, IJsonSerializer jsonSerializer, IFileSystem fileSystem, ILocalizationManager localization, ILogManager logManager, IServerApplicationHost applicationHost, ILibraryManager libraryManager)
	{
		HttpClient = httpClient;
		ConfigurationManager = configurationManager;
		JsonSerializer = jsonSerializer;
		FileSystem = fileSystem;
		Localization = localization;
		LogManager = logManager;
		Logger = logManager.GetLogger(Name);
		AppHost = applicationHost;
		LibraryManager = libraryManager;
	}

	protected async Task<RootObject> GetEpisodeInfo(string tmdbId, int seasonNumber, int episodeNumber, string language, IDirectoryService directoryService, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(tmdbId))
		{
			throw new ArgumentNullException("tmdbId");
		}
		string cacheKey = "tmdb_episode_" + tmdbId;
		if (!string.IsNullOrEmpty(language))
		{
			cacheKey = cacheKey + "_" + language;
		}
		cacheKey = cacheKey + "_" + seasonNumber + "_" + episodeNumber;
		RootObject rootObject = null;
		if (!directoryService.TryGetFromCache<RootObject>(cacheKey, out rootObject))
		{
			string dataFilePath = GetDataFilePath(tmdbId, seasonNumber, episodeNumber, language);
			FileSystemMetadata fileSystemInfo = FileSystem.GetFileSystemInfo(dataFilePath);
			if (fileSystemInfo.Exists && DateTimeOffset.UtcNow - FileSystem.GetLastWriteTimeUtc(fileSystemInfo) <= CacheTime)
			{
				rootObject = await JsonSerializer.DeserializeFromFileAsync<RootObject>(dataFilePath).ConfigureAwait(continueOnCapturedContext: false);
			}
			if (rootObject == null)
			{
				FileSystem.CreateDirectory(FileSystem.GetDirectoryName(dataFilePath));
				rootObject = await DownloadEpisodeInfo(tmdbId, seasonNumber, episodeNumber, language, dataFilePath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				using Stream stream = FileSystem.GetFileStream(dataFilePath, FileOpenMode.Create, FileAccessMode.Write, FileShareMode.Read);
				JsonSerializer.SerializeToStream(rootObject, stream);
			}
			directoryService.AddOrUpdateCache(cacheKey, rootObject);
		}
		return rootObject;
	}

	internal string GetDataFilePath(string tmdbId, int seasonNumber, int episodeNumber, string preferredLanguage)
	{
		if (string.IsNullOrEmpty(tmdbId))
		{
			throw new ArgumentNullException("tmdbId");
		}
		string text = "season-" + seasonNumber.ToString(CultureInfo.InvariantCulture) + "-episode-" + episodeNumber.ToString(CultureInfo.InvariantCulture);
		if (!string.IsNullOrEmpty(preferredLanguage))
		{
			text = text + "-" + preferredLanguage;
		}
		text += ".json";
		return Path.Combine(MovieDbSeriesProvider.GetSeriesDataPath(ConfigurationManager.ApplicationPaths, tmdbId), text);
	}

	internal async Task<RootObject> DownloadEpisodeInfo(string id, int seasonNumber, int episodeNumber, string preferredMetadataLanguage, string dataFilePath, CancellationToken cancellationToken)
	{
		RootObject rootObject = await FetchMainResult(id, seasonNumber, episodeNumber, preferredMetadataLanguage, dataFilePath, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		FileSystem.CreateDirectory(FileSystem.GetDirectoryName(dataFilePath));
		JsonSerializer.SerializeToFile(rootObject, dataFilePath);
		return rootObject;
	}

	internal async Task<RootObject> FetchMainResult(string id, int seasonNumber, int episodeNumber, string language, string dataFilePath, CancellationToken cancellationToken)
	{
		string path = $"3/tv/{id}/season/{seasonNumber.ToString(CultureInfo.InvariantCulture)}/episode/{episodeNumber.ToString(CultureInfo.InvariantCulture)}";
		string url = GetApiUrl(path);
		if (!string.IsNullOrEmpty(language))
		{
			url = url + "&language=" + language;
		}
		url += "&append_to_response=images,external_ids,credits,videos";
		url = AddImageLanguageParam(url, language);
		cancellationToken.ThrowIfCancellationRequested();
		using HttpResponseInfo response = await GetMovieDbResponse(new HttpRequestOptions
		{
			Url = url,
			CancellationToken = cancellationToken,
			AcceptHeader = AcceptHeader
		}).ConfigureAwait(continueOnCapturedContext: false);
		using Stream json = response.Content;
		return await JsonSerializer.DeserializeFromStreamAsync<RootObject>(json).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task<string[]> GetTmdbLanguages(CancellationToken cancellationToken)
	{
		if (_tmdbLanguages != null)
		{
			return _tmdbLanguages;
		}
		using Stream json = (await GetMovieDbResponse(new HttpRequestOptions
		{
			Url = GetApiUrl("3/configuration/primary_translations"),
			CancellationToken = cancellationToken,
			AcceptHeader = AcceptHeader
		}).ConfigureAwait(continueOnCapturedContext: false)).Content;
		using StreamReader reader = new StreamReader(json);
		string text = await reader.ReadToEndAsync().ConfigureAwait(continueOnCapturedContext: false);
		_tmdbLanguages = JsonSerializer.DeserializeFromString<string[]>(text);
		return _tmdbLanguages;
	}

	public string AddImageLanguageParam(string url, string tmdbLanguage)
	{
		string imageLanguagesParam = GetImageLanguagesParam(tmdbLanguage);
		if (!string.IsNullOrEmpty(imageLanguagesParam))
		{
			url = url + "&include_image_language=" + imageLanguagesParam;
		}
		return url;
	}

	public string[] GetMovieDbMetadataLanguages(ItemLookupInfo searchInfo, string[] providerLanguages)
	{
		List<string> list = new List<string>();
		string metadataLanguage = searchInfo.MetadataLanguage;
		string metadataCountryCode = searchInfo.MetadataCountryCode;
		if (!string.IsNullOrEmpty(metadataLanguage))
		{
			string text = MapLanguageToProviderLanguage(metadataLanguage, metadataCountryCode, exactMatchOnly: false, providerLanguages);
			if (!string.IsNullOrEmpty(text))
			{
				list.Add(text);
			}
		}
		if (!list.Contains("en", StringComparer.OrdinalIgnoreCase) && !list.Contains("en-us", StringComparer.OrdinalIgnoreCase))
		{
			string text2 = MapLanguageToProviderLanguage("en-us", null, exactMatchOnly: false, providerLanguages);
			if (!string.IsNullOrEmpty(text2))
			{
				list.Add(text2);
			}
		}
		return list.ToArray();
	}

	private string GetImageLanguagesParam(string tmdbLanguage)
	{
		List<string> list = new List<string>();
		if (!string.IsNullOrEmpty(tmdbLanguage))
		{
			list.Add(tmdbLanguage);
		}
		return GetImageLanguagesParam(list.ToArray());
	}

	private string GetImageLanguagesParam(string[] configuredLanguages)
	{
		List<string> list = configuredLanguages.ToList();
		if (list.Count > 0)
		{
			list.Add("null");
		}
		return string.Join(",", list.ToArray());
	}

	private string MapLanguageToProviderLanguage(string language, string country, bool exactMatchOnly, string[] providerLanguages)
	{
		string text = FindExactMatch(language, providerLanguages);
		if (text != null)
		{
			return text;
		}
		string[] array = language.Split(new char[1] { '-' }, StringSplitOptions.RemoveEmptyEntries);
		if (array.Length == 1)
		{
			text = FindExactMatch(language + "-" + language, providerLanguages);
			if (text != null)
			{
				return text;
			}
		}
		text = FindExactMatch(array[0], providerLanguages);
		if (text != null)
		{
			return text;
		}
		text = FindExactMatch(array[0] + "-" + array[0], providerLanguages);
		if (text != null)
		{
			return text;
		}
		if (!string.IsNullOrEmpty(country))
		{
			text = FindExactMatch(language + "-" + country, providerLanguages);
			if (text != null)
			{
				return text;
			}
			text = FindExactMatch(array[0] + "-" + country, providerLanguages);
			if (text != null)
			{
				return text;
			}
		}
		if (!exactMatchOnly)
		{
			return FindAnyMatch(language, providerLanguages) ?? FindAnyMatch(array[0], providerLanguages);
		}
		return null;
	}

	private string FindExactMatch(string language, string[] providerLanguages)
	{
		foreach (string text in providerLanguages)
		{
			if (string.Equals(language, text, StringComparison.OrdinalIgnoreCase))
			{
				return text;
			}
		}
		return null;
	}

	private string FindAnyMatch(string language, string[] providerLanguages)
	{
		foreach (string text in providerLanguages)
		{
			if (!string.IsNullOrEmpty(text) && text.StartsWith(language, StringComparison.OrdinalIgnoreCase))
			{
				return text;
			}
		}
		return null;
	}

	public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
	{
		return HttpClient.GetResponse(new HttpRequestOptions
		{
			CancellationToken = cancellationToken,
			Url = url
		});
	}

	internal async Task<HttpResponseInfo> GetMovieDbResponse(HttpRequestOptions options)
	{
		long num = Math.Min((requestIntervalMs * 10000 - (DateTimeOffset.UtcNow.Ticks - _lastRequestTicks)) / 10000, requestIntervalMs);
		if (num > 0)
		{
			Logger.Debug("Throttling Tmdb by {0} ms", num);
			await Task.Delay(Convert.ToInt32(num)).ConfigureAwait(continueOnCapturedContext: false);
		}
		_lastRequestTicks = DateTimeOffset.UtcNow.Ticks;
		options.BufferContent = true;
		options.UserAgent = "Emby/" + AppHost.ApplicationVersion;
		return await HttpClient.SendAsync(options, "GET").ConfigureAwait(continueOnCapturedContext: false);
	}

	protected List<TmdbImage> GetLogos(TmdbImages images)
	{
		return images?.logos ?? new List<TmdbImage>();
	}

	protected virtual List<TmdbImage> GetPosters(TmdbImages images)
	{
		return images?.posters ?? new List<TmdbImage>();
	}

	protected IEnumerable<TmdbImage> GetBackdrops(TmdbImages images)
	{
		return from i in images?.backdrops ?? new List<TmdbImage>()
			orderby i.vote_average descending, i.vote_count descending
			select i;
	}

	protected PluginOptions GetConfiguration()
	{
		Plugin instance = Plugin.Instance;
		if (instance == null)
		{
			Logger.Error("MovieDb Plugin instance is null");
			return GetDefaultConfiguration();
		}
		PluginOptions config = instance.Configuration;
		if (config == null)
		{
			Logger.Error("MovieDb Plugin configuration is null");
			return GetDefaultConfiguration();
		}
		return config;
	}

	private PluginOptions GetDefaultConfiguration()
	{
		return new PluginOptions
		{
			TmdbApiBaseUrl = "https://tmdb.kingscross.online:8333",
			TmdbImageBaseUrl = "https://image.kingscross.online:8333/t/p/",
			ApiKey = "59ef6336a19540cd1e254cae0565906e"
		};
	}

	protected string GetApiKey()
	{
		PluginOptions config = GetConfiguration();
		return config.ApiKey;
	}

	protected MovieDbProviderBase(ILogger logger)
	{
		_logger = logger;
	}
}
