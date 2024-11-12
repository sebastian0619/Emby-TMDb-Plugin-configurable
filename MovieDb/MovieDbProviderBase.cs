using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Services;
using MovieDb;

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

	private static long _lastRequestTicks;

	private static int requestIntervalMs = 100;

	public string Name => ProviderName;

	public static string ProviderName => "TheMovieDb";

	private static readonly object _settingsLock = new object();

	protected string GetImageUrl(string size, string imagePath)
	{
		if (string.IsNullOrEmpty(imagePath))
		{
			return null;
		}

		var config = GetConfiguration();
		var baseUrl = config.TmdbImageBaseUrl?.TrimEnd('/') ?? "https://image.tmdb.org/t/p";
		return $"{baseUrl}/{size}{imagePath}";
	}

	protected string GetApiUrl(string path)
	{
		var config = GetConfiguration();
		var baseUrl = config.TmdbApiBaseUrl?.TrimEnd('/') ?? "https://api.themoviedb.org";
		var apiKey = config.ApiKey;
		
		return $"{baseUrl}/{path.TrimStart('/')}?api_key={apiKey}";
	}

	internal async Task<HttpResponseInfo> GetMovieDbResponse(HttpRequestOptions options)
	{
		long num = Math.Min((requestIntervalMs * 10000 - (DateTimeOffset.UtcNow.Ticks - _lastRequestTicks)) / 10000, requestIntervalMs);
		if (num > 0)
		{
			Logger.Debug("Throttling Tmdb by {0} ms", num);
			await Task.Delay(Convert.ToInt32(num)).ConfigureAwait(false);
		}
		_lastRequestTicks = DateTimeOffset.UtcNow.Ticks;
		
		options.BufferContent = true;
		options.UserAgent = "Emby/" + AppHost.ApplicationVersion;
		options.AcceptHeader = AcceptHeader;
		
		return await HttpClient.SendAsync(options, "GET").ConfigureAwait(false);
	}

	public async Task<string[]> GetTmdbLanguages(CancellationToken cancellationToken)
	{
		var response = await GetMovieDbResponse(new HttpRequestOptions
		{
			Url = GetApiUrl(TmdbLanguagesPath),
			CancellationToken = cancellationToken,
			AcceptHeader = AcceptHeader
		}).ConfigureAwait(false);

		using (Stream json = response.Content)
		using (StreamReader reader = new StreamReader(json))
		{
			var text = await reader.ReadToEndAsync().ConfigureAwait(false);
			return JsonSerializer.DeserializeFromString<string[]>(text);
		}
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
		if (!list.Contains<string>("en", StringComparer.OrdinalIgnoreCase) && !list.Contains<string>("en-us", StringComparer.OrdinalIgnoreCase))
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
		return (images?.backdrops ?? new List<TmdbImage>()).OrderByDescending(i => i.vote_average)
			.ThenByDescending(i => i.vote_count);
	}

	protected PluginOptions GetConfiguration()
	{
		var instance = Plugin.Instance;
		if (instance?.Configuration == null)
		{
			Logger.Error("MovieDb Plugin 配置无效");
			throw new InvalidOperationException("MovieDb Plugin 配置无效");
		}

		return instance.Configuration;
	}

	protected string GetApiKey()
	{
		var config = GetConfiguration();
		return config.ApiKey;
	}

	protected MovieDbProviderBase(
		IHttpClient httpClient,
		IServerConfigurationManager configurationManager,
		IJsonSerializer jsonSerializer,
		IFileSystem fileSystem,
		ILocalizationManager localization,
		ILogManager logManager,
		IServerApplicationHost appHost,
		ILibraryManager libraryManager)
	{
		HttpClient = httpClient;
		ConfigurationManager = configurationManager;
		JsonSerializer = jsonSerializer;
		FileSystem = fileSystem;
		Localization = localization;
		Logger = logManager.GetLogger(Name);
		LogManager = logManager;
		AppHost = appHost;
		LibraryManager = libraryManager;
	}

	protected async Task<RootObject> GetEpisodeInfo(
		string seriesId,
		int seasonNumber,
		int episodeNumber,
		string language,
		IDirectoryService directoryService,
		CancellationToken cancellationToken)
	{
		var path = string.Format(EpisodeUrlPattern, seriesId, seasonNumber, episodeNumber);
		var url = GetApiUrl(path);
		
		if (!string.IsNullOrEmpty(language))
		{
			url += $"&language={language}";
		}

		url += "&append_to_response=images,external_ids,credits,videos";

		var options = new HttpRequestOptions
		{
			Url = url,
			CancellationToken = cancellationToken
		};

		using var response = await GetMovieDbResponse(options).ConfigureAwait(false);
		using var json = response.Content;
		
		return await JsonSerializer.DeserializeFromStreamAsync<RootObject>(json).ConfigureAwait(false);
	}
}
