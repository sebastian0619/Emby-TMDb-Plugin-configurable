using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;

namespace MovieDb;

public class MovieDbProvider : MovieDbProviderBase, IRemoteMetadataProvider<Movie, MovieInfo>, IMetadataProvider<Movie>, IMetadataProvider, IRemoteMetadataProvider, IRemoteSearchProvider<MovieInfo>, IRemoteSearchProvider, IHasOrder, IHasMetadataFeatures
{
	internal class TmdbTitle
	{
		public string iso_3166_1 { get; set; }

		public string title { get; set; }
	}

	internal class TmdbAltTitleResults
	{
		public int id { get; set; }

		public List<TmdbTitle> titles { get; set; }
	}

	public class BelongsToCollection
	{
		public int id { get; set; }

		public string name { get; set; }

		public string poster_path { get; set; }

		public string backdrop_path { get; set; }
	}

	public class ProductionCompany
	{
		public int id { get; set; }

		public string logo_path { get; set; }

		public string name { get; set; }

		public string origin_country { get; set; }
	}

	public class ProductionCountry
	{
		public string iso_3166_1 { get; set; }

		public string name { get; set; }
	}

	public class Casts
	{
		public List<TmdbCast> cast { get; set; }

		public List<TmdbCrew> crew { get; set; }
	}

	public class Country
	{
		public string certification { get; set; }

		public string iso_3166_1 { get; set; }

		public bool primary { get; set; }

		public DateTimeOffset release_date { get; set; }

		public string GetRating()
		{
			return GetRating(certification, iso_3166_1);
		}

		public static string GetRating(string rating, string iso_3166_1)
		{
			if (string.IsNullOrEmpty(rating))
			{
				return null;
			}
			if (string.Equals(iso_3166_1, "us", StringComparison.OrdinalIgnoreCase))
			{
				return rating;
			}
			if (string.Equals(iso_3166_1, "de", StringComparison.OrdinalIgnoreCase))
			{
				iso_3166_1 = "FSK";
			}
			return iso_3166_1 + "-" + rating;
		}
	}

	public class Releases
	{
		public List<Country> countries { get; set; }
	}

	public class Youtube
	{
		public string name { get; set; }

		public string size { get; set; }

		public string source { get; set; }

		public string type { get; set; }
	}

	public class Trailers
	{
		public List<object> quicktime { get; set; }

		public List<Youtube> youtube { get; set; }
	}

	internal class CompleteMovieData
	{
		public bool adult { get; set; }

		public string backdrop_path { get; set; }

		public BelongsToCollection belongs_to_collection { get; set; }

		public int budget { get; set; }

		public List<TmdbGenre> genres { get; set; }

		public string homepage { get; set; }

		public int id { get; set; }

		public string imdb_id { get; set; }

		public string original_language { get; set; }

		public string original_title { get; set; }

		public string overview { get; set; }

		public double popularity { get; set; }

		public string poster_path { get; set; }

		public List<ProductionCompany> production_companies { get; set; }

		public List<ProductionCountry> production_countries { get; set; }

		public string release_date { get; set; }

		public int revenue { get; set; }

		public int runtime { get; set; }

		public List<TmdbLanguage> spoken_languages { get; set; }

		public string status { get; set; }

		public string tagline { get; set; }

		public string title { get; set; }

		public bool video { get; set; }

		public double vote_average { get; set; }

		public int vote_count { get; set; }

		public Casts casts { get; set; }

		public Releases releases { get; set; }

		public TmdbImages images { get; set; }

		public TmdbKeywords keywords { get; set; }

		public Trailers trailers { get; set; }

		public string name { get; set; }

		public string original_name { get; set; }

		public string GetOriginalTitle()
		{
			return original_name ?? original_title;
		}

		public string GetTitle()
		{
			return name ?? title ?? GetOriginalTitle();
		}
	}

	// 定义 API 路径模板
	private const string MovieInfoPath = "3/movie/{0}";
	private const string AppendToResponse = "alternative_titles,reviews,casts,releases,images,keywords,trailers";

	internal static MovieDbProvider Current { get; private set; }

	public MetadataFeatures[] Features => (MetadataFeatures[])(object)new MetadataFeatures[2]
	{
		(MetadataFeatures)2,
		(MetadataFeatures)1
	};

	public int Order => 1;

	public MovieDbProvider(IJsonSerializer jsonSerializer, IHttpClient httpClient, IFileSystem fileSystem, IServerConfigurationManager configurationManager, ILogManager logManager, ILocalizationManager localization, IServerApplicationHost appHost, ILibraryManager libraryManager)
		: base(httpClient, configurationManager, jsonSerializer, fileSystem, localization, logManager, appHost, libraryManager)
	{
		Current = this;
	}

	public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(ItemLookupInfo searchInfo, CancellationToken cancellationToken)
	{
		// 获取支持的语言列表
		var languages = await GetTmdbLanguages(cancellationToken)
			.ConfigureAwait(false);
		
		// 获取元数据语言
		string[] metadataLanguages = GetMovieDbMetadataLanguages(searchInfo, languages);

		// 根据类型使用不同的搜索实现
		var movieDbSearch = new MovieDbSearch(Logger, JsonSerializer, LibraryManager);
		
		if (searchInfo is SeriesInfo seriesInfo)
		{
			return await movieDbSearch.GetSearchResults(
				seriesInfo,
				metadataLanguages,
				cancellationToken).ConfigureAwait(false);
		}
		else if (searchInfo is MovieInfo movieInfo)
		{
			return await movieDbSearch.GetSearchResults(
				movieInfo,
				metadataLanguages,
				cancellationToken).ConfigureAwait(false);
		}
		
		// 不支持的类型返回空结果
		return new List<RemoteSearchResult>();
	}

	public Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
	{
		return this.GetItemMetadata<Movie>((ItemLookupInfo)(object)info, cancellationToken);
	}

	public Task<MetadataResult<T>> GetItemMetadata<T>(ItemLookupInfo id, CancellationToken cancellationToken) where T : BaseItem, new()
	{
		return new GenericMovieDbInfo<T>(JsonSerializer, HttpClient, FileSystem, ConfigurationManager, LogManager, Localization, AppHost, LibraryManager).GetMetadata(id, cancellationToken);
	}

	internal static string GetMovieDataPath(IApplicationPaths appPaths, string tmdbId)
	{
		return Path.Combine(GetMoviesDataPath(appPaths), tmdbId);
	}

	internal static string GetMoviesDataPath(IApplicationPaths appPaths)
	{
		return Path.Combine(appPaths.CachePath, "tmdb-movies2");
	}

	private async Task<CompleteMovieData> DownloadMovieInfo(string id, string preferredMetadataLanguage, string dataFilePath, CancellationToken cancellationToken)
	{
		CompleteMovieData completeMovieData = await FetchMainResult(id, isTmdbId: true, preferredMetadataLanguage, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (completeMovieData == null)
		{
			return null;
		}
		FileSystem.CreateDirectory(FileSystem.GetDirectoryName(dataFilePath));
		JsonSerializer.SerializeToFile((object)completeMovieData, dataFilePath);
		return completeMovieData;
	}

	internal async Task<CompleteMovieData> EnsureMovieInfo(string tmdbId, string language, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(tmdbId))
		{
			throw new ArgumentNullException(nameof(tmdbId));
		}

		string dataFilePath = GetDataFilePath(tmdbId, language);
		FileSystemMetadata fileSystemInfo = FileSystem.GetFileSystemInfo(dataFilePath);

		if (fileSystemInfo.Exists && DateTimeOffset.UtcNow - FileSystem.GetLastWriteTimeUtc(fileSystemInfo) <= CacheTime)
		{
			return await JsonSerializer.DeserializeFromFileAsync<CompleteMovieData>(fileSystemInfo.FullName);
		}

		return await DownloadMovieInfo(tmdbId, language, dataFilePath, cancellationToken);
	}

	internal string GetDataFilePath(string tmdbId, string preferredLanguage)
	{
		if (string.IsNullOrEmpty(tmdbId))
		{
			throw new ArgumentNullException("tmdbId");
		}
		string text = "all";
		if (!string.IsNullOrEmpty(preferredLanguage))
		{
			text = text + "-" + preferredLanguage;
		}
		text += ".json";
		return Path.Combine(GetMovieDataPath((IApplicationPaths)(object)ConfigurationManager.ApplicationPaths, tmdbId), text);
	}

	internal async Task<CompleteMovieData> FetchMainResult(string id, bool isTmdbId, string language, CancellationToken cancellationToken)
	{
		var config = GetConfiguration();
		string path = string.Format(MovieInfoPath, id);
		string url = GetApiUrl(path) + $"&append_to_response={AppendToResponse}";

		if (!string.IsNullOrEmpty(language))
		{
			url += $"&language={language}";
		}
		url = AddImageLanguageParam(url, language);

		cancellationToken.ThrowIfCancellationRequested();

		CacheMode cacheMode = isTmdbId ? CacheMode.None : CacheMode.Unconditional;
		TimeSpan cacheTime = CacheTime;

		try
		{
			var response = await GetMovieDbResponse(new HttpRequestOptions
			{
				Url = url,
				CancellationToken = cancellationToken,
				AcceptHeader = AcceptHeader,
				CacheMode = cacheMode,
				CacheLength = cacheTime
			}).ConfigureAwait(false);

			try
			{
				using (Stream json = response.Content)
				{
					return await JsonSerializer.DeserializeFromStreamAsync<CompleteMovieData>(json)
						.ConfigureAwait(false);
				}
			}
			finally
			{
				((IDisposable)response)?.Dispose();
			}
		}
		catch (HttpException ex)
		{
			if (ex.StatusCode.HasValue && ex.StatusCode.Value == HttpStatusCode.NotFound)
			{
				return null;
			}
			throw;
		}
	}

	// 添加 new 关键字来显式隐藏基类的 GetApiUrl 方法
	private new string GetApiUrl(string path)
	{
		var config = GetConfiguration();
		var baseUrl = config.TmdbApiBaseUrl.TrimEnd('/');
		return $"{baseUrl}/{path}?api_key={config.ApiKey}";
	}

	// 修改 GetMovieDbMetadataLanguages 方法使其更通用
	protected string[] GetMovieDbMetadataLanguages(ItemLookupInfo info, List<string> languages)
	{
		if (languages == null || languages.Count == 0)
		{
			return new string[] { "en" };  // 默认使用英语
		}

		var configLanguages = ConfigurationManager.Configuration.PreferredMetadataLanguage;
		
		// 如果配置的语言在支持的语言列表中，优先使用它
		if (!string.IsNullOrEmpty(configLanguages) && 
			languages.Contains(configLanguages, StringComparer.OrdinalIgnoreCase))
		{
			return new string[] { configLanguages };
		}

		// 否则使用第一个支持的语言
		return new string[] { languages[0] };
	}
}
