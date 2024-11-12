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

	public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
	{
		return GetMovieSearchResults((ItemLookupInfo)(object)searchInfo, cancellationToken);
	}

	public async Task<IEnumerable<RemoteSearchResult>> GetMovieSearchResults(ItemLookupInfo searchInfo, CancellationToken cancellationToken)
	{
		string tmdbId = ProviderIdsExtensions.GetProviderId((IHasProviderIds)(object)searchInfo, (MetadataProviders)3);
		TmdbSettingsResult tmdbSettings = null;
		if (!string.IsNullOrEmpty(tmdbId))
		{
			MetadataResult<Movie> val = await this.GetItemMetadata<Movie>(searchInfo, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!((BaseMetadataResult)val).HasMetadata)
			{
				return new List<RemoteSearchResult>();
			}
			RemoteSearchResult result = ((BaseMetadataResult)val).ToRemoteSearchResult(base.Name);
			List<TmdbImage> images = ((await EnsureMovieInfo(tmdbId, null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false))?.images ?? new TmdbImages()).posters ?? new List<TmdbImage>();
			string imageUrl = (await GetTmdbSettings(cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).images.GetImageUrl("original");
			result.ImageUrl = ((images.Count == 0) ? null : (imageUrl + images[0].file_path));
			return (IEnumerable<RemoteSearchResult>)(object)new RemoteSearchResult[1] { result };
		}
		string providerId = ProviderIdsExtensions.GetProviderId((IHasProviderIds)(object)searchInfo, (MetadataProviders)2);
		if (!string.IsNullOrEmpty(providerId))
		{
			MovieDbSearch movieDbSearch = new MovieDbSearch(Logger, JsonSerializer, LibraryManager);
			RemoteSearchResult val3 = await movieDbSearch.FindMovieByExternalId(providerId, "imdb_id", MetadataProviders.Imdb.ToString(), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (val3 != null)
			{
				return (IEnumerable<RemoteSearchResult>)(object)new RemoteSearchResult[1] { val3 };
			}
		}
		if (tmdbSettings == null)
		{
			tmdbSettings = await GetTmdbSettings(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		string[] movieDbMetadataLanguages = GetMovieDbMetadataLanguages(searchInfo, await GetTmdbLanguages(cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
		return await new MovieDbSearch(Logger, JsonSerializer, LibraryManager).GetMovieSearchResults(searchInfo, movieDbMetadataLanguages, tmdbSettings, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
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
}
