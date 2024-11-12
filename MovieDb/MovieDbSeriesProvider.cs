using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
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

public class MovieDbSeriesProvider : MovieDbProviderBase, IRemoteMetadataProvider<Series, SeriesInfo>, IMetadataProvider<Series>, IMetadataProvider, IRemoteMetadataProvider, IRemoteSearchProvider<SeriesInfo>, IRemoteSearchProvider, IHasOrder, IHasMetadataFeatures
{
	public class CreatedBy
	{
		public int id { get; set; }

		public string name { get; set; }

		public string profile_path { get; set; }
	}

	public class Network
	{
		public int id { get; set; }

		public string name { get; set; }
	}

	public class Season
	{
		public string air_date { get; set; }

		public int episode_count { get; set; }

		public int id { get; set; }

		public string poster_path { get; set; }

		public int season_number { get; set; }
	}

	public class ContentRating
	{
		public string iso_3166_1 { get; set; }

		public string rating { get; set; }

		public string GetRating()
		{
			return MovieDbProvider.Country.GetRating(rating, iso_3166_1);
		}
	}

	public class ContentRatings
	{
		public List<ContentRating> results { get; set; }
	}

	public class SeriesRootObject
	{
		public string backdrop_path { get; set; }

		public List<CreatedBy> created_by { get; set; }

		public List<int> episode_run_time { get; set; }

		public DateTimeOffset first_air_date { get; set; }

		public List<TmdbGenre> genres { get; set; }

		public string homepage { get; set; }

		public int id { get; set; }

		public bool in_production { get; set; }

		public List<string> languages { get; set; }

		public DateTimeOffset last_air_date { get; set; }

		public string name { get; set; }

		public string title { get; set; }

		public List<Network> networks { get; set; }

		public int number_of_episodes { get; set; }

		public int number_of_seasons { get; set; }

		public List<string> origin_country { get; set; }

		public string overview { get; set; }

		public string popularity { get; set; }

		public string poster_path { get; set; }

		public List<Season> seasons { get; set; }

		public string status { get; set; }

		public double vote_average { get; set; }

		public int vote_count { get; set; }

		public TmdbCredits credits { get; set; }

		public TmdbImages images { get; set; }

		public TmdbKeywords keywords { get; set; }

		public TmdbExternalIds external_ids { get; set; }

		public TmdbVideos videos { get; set; }

		public ContentRatings content_ratings { get; set; }

		public string ResultLanguage { get; set; }

		public TmdbAlternativeTitles alternative_titles { get; set; }

		public string original_title { get; set; }

		public string original_name { get; set; }

		public string GetTitle()
		{
			return name ?? title ?? GetOriginalTitle();
		}

		public string GetOriginalTitle()
		{
			return original_name ?? original_title;
		}
	}

	private const string TvInfoPath = "3/tv/{0}";

	private const string FindPath = "3/find/{0}";

	private const string AppendToResponse = "alternative_titles,reviews,credits,images,keywords,external_ids,videos,content_ratings,episode_groups";

	internal static MovieDbSeriesProvider Current { get; private set; }

	public MetadataFeatures[] Features => new MetadataFeatures[1] { MetadataFeatures.Adult };

	public int Order => 1;

	public MovieDbSeriesProvider(IJsonSerializer jsonSerializer, IHttpClient httpClient, IFileSystem fileSystem, IServerConfigurationManager configurationManager, ILogManager logManager, ILocalizationManager localization, IServerApplicationHost appHost, ILibraryManager libraryManager)
		: base(httpClient, configurationManager, jsonSerializer, fileSystem, localization, logManager, appHost, libraryManager)
	{
		Current = this;
	}

	public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
	{
		string tmdbId = searchInfo.GetProviderId(MetadataProviders.Tmdb);
		TmdbSettingsResult tmdbSettings = await GetTmdbSettings(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (!string.IsNullOrEmpty(tmdbId))
		{
			MetadataResult<Series> val = await GetMetadata(searchInfo, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!val.HasMetadata)
			{
				return new List<RemoteSearchResult>();
			}
			RemoteSearchResult result = val.ToRemoteSearchResult(base.Name);
			List<TmdbImage> list = ((await EnsureSeriesInfo(tmdbId, null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false))?.images ?? new TmdbImages()).posters ?? new List<TmdbImage>();
			string imageUrl = tmdbSettings.images.GetImageUrl("original");
			result.ImageUrl = ((list.Count == 0) ? null : (imageUrl + list[0].file_path));
			return new RemoteSearchResult[1] { result };
		}
		string providerId = searchInfo.GetProviderId(MetadataProviders.Imdb);
		if (!string.IsNullOrEmpty(providerId))
		{
			MovieDbSeriesProvider movieDbSeriesProvider = this;
			RemoteSearchResult val3 = await movieDbSeriesProvider.FindByExternalId(providerId, "imdb_id", MetadataProviders.Imdb.ToString(), tmdbSettings, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (val3 != null)
			{
				return new RemoteSearchResult[1] { val3 };
			}
		}
		string providerId2 = searchInfo.GetProviderId(MetadataProviders.Tvdb);
		if (!string.IsNullOrEmpty(providerId2))
		{
			MovieDbSeriesProvider movieDbSeriesProvider2 = this;
			RemoteSearchResult val4 = await movieDbSeriesProvider2.FindByExternalId(providerId2, "tvdb_id", MetadataProviders.Tvdb.ToString(), tmdbSettings, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (val4 != null)
			{
				return new RemoteSearchResult[1] { val4 };
			}
		}
		ItemLookupInfo searchInfo2 = searchInfo;
		string[] movieDbMetadataLanguages = GetMovieDbMetadataLanguages(searchInfo2, await GetTmdbLanguages(cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
		return await FilterSearchResults(await new MovieDbSearch(Logger, JsonSerializer, LibraryManager).GetSearchResults(searchInfo, movieDbMetadataLanguages, tmdbSettings, cancellationToken).ConfigureAwait(continueOnCapturedContext: false), searchInfo, foundByName: true, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	private async Task<List<RemoteSearchResult>> FilterSearchResults(List<RemoteSearchResult> results, SeriesInfo searchInfo, bool foundByName, CancellationToken cancellationToken)
	{
		DateTimeOffset? episodeAirDate = searchInfo.EpisodeAirDate;
		if (episodeAirDate.HasValue && foundByName)
		{
			List<RemoteSearchResult> list = new List<RemoteSearchResult>();
			foreach (RemoteSearchResult item in results)
			{
				if (await AiredWithin(item, episodeAirDate.Value, searchInfo, cancellationToken).ConfigureAwait(continueOnCapturedContext: false))
				{
					list.Add(item);
				}
			}
			results = list;
		}
		return results;
	}

	private async Task<bool> AiredWithin(RemoteSearchResult remoteSearchResult, DateTimeOffset episodeAirDate, SeriesInfo searchInfo, CancellationToken cancellationToken)
	{
		Logger.Info("Checking AiredWithin for {0}. episodeAirDate: {1}", remoteSearchResult.Name, episodeAirDate.UtcDateTime.ToShortDateString());
		if (remoteSearchResult.PremiereDate.HasValue)
		{
			if (episodeAirDate.Year < remoteSearchResult.PremiereDate.Value.Year)
			{
				return false;
			}
			SeriesInfo seriesInfo = new SeriesInfo
			{
				ProviderIds = remoteSearchResult.ProviderIds,
				MetadataLanguage = searchInfo.MetadataLanguage,
				MetadataCountryCode = searchInfo.MetadataCountryCode,
				Name = remoteSearchResult.Name,
				Year = remoteSearchResult.ProductionYear,
				PremiereDate = remoteSearchResult.PremiereDate,
				DisplayOrder = searchInfo.DisplayOrder,
				EnableAdultMetadata = searchInfo.EnableAdultMetadata
			};
			MetadataResult<Series> val = await GetMetadata(seriesInfo, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (val.HasMetadata)
			{
				Logger.Info("AiredWithin for {0} Item.PremiereDate: {1}, Item.EndDate: {2}", seriesInfo.Name, val.Item.PremiereDate?.UtcDateTime.ToShortDateString(), val.Item.EndDate?.UtcDateTime.ToShortDateString());
				if (val.Item.PremiereDate.HasValue)
				{
					if (episodeAirDate.Year < val.Item.PremiereDate.Value.Year)
					{
						return false;
					}
					if (val.Item.EndDate.HasValue && episodeAirDate.Year > val.Item.EndDate.Value.Year)
					{
						return false;
					}
					return true;
				}
				return false;
			}
			return false;
		}
		return false;
	}

	public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
	{
		MetadataResult<Series> result = new MetadataResult<Series>
		{
			QueriedById = true
		};
		TmdbSettingsResult tmdbSettings = await GetTmdbSettings(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		string tmdbId = info.GetProviderId(MetadataProviders.Tmdb);
		if (string.IsNullOrEmpty(tmdbId))
		{
			string providerId = info.GetProviderId(MetadataProviders.Imdb);
			if (!string.IsNullOrEmpty(providerId))
			{
				MovieDbSeriesProvider movieDbSeriesProvider = this;
				RemoteSearchResult val2 = await movieDbSeriesProvider.FindByExternalId(providerId, "imdb_id", MetadataProviders.Imdb.ToString(), tmdbSettings, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (val2 != null)
				{
					tmdbId = val2.GetProviderId(MetadataProviders.Tmdb);
				}
			}
		}
		if (string.IsNullOrEmpty(tmdbId))
		{
			string providerId2 = info.GetProviderId(MetadataProviders.Tvdb);
			if (!string.IsNullOrEmpty(providerId2))
			{
				MovieDbSeriesProvider movieDbSeriesProvider2 = this;
				RemoteSearchResult val3 = await movieDbSeriesProvider2.FindByExternalId(providerId2, "tvdb_id", MetadataProviders.Tvdb.ToString(), tmdbSettings, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (val3 != null)
				{
					tmdbId = val3.GetProviderId(MetadataProviders.Tmdb);
				}
			}
		}
		ItemLookupInfo searchInfo = info;
		string[] metadataLanguages = GetMovieDbMetadataLanguages(searchInfo, await GetTmdbLanguages(cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
		if (string.IsNullOrEmpty(tmdbId))
		{
			result.QueriedById = false;
			RemoteSearchResult val4 = (await new MovieDbSearch(Logger, JsonSerializer, LibraryManager).GetSearchResults(info, metadataLanguages, tmdbSettings, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).FirstOrDefault();
			if (val4 != null)
			{
				tmdbId = val4.GetProviderId(MetadataProviders.Tmdb);
			}
		}
		if (!string.IsNullOrEmpty(tmdbId))
		{
			cancellationToken.ThrowIfCancellationRequested();
			bool isFirstLanguage = true;
			string[] array = metadataLanguages;
			string[] array2 = array;
			foreach (string language in array2)
			{
				SeriesRootObject seriesRootObject = await EnsureSeriesInfo(tmdbId, language, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (seriesRootObject != null)
				{
					result.HasMetadata = true;
					if (result.Item == null)
					{
						result.Item = new Series();
					}
					ImportData(result, seriesRootObject, info.MetadataCountryCode, tmdbSettings, isFirstLanguage);
					isFirstLanguage = false;
					if (IsComplete(result.Item))
					{
						return result;
					}
				}
			}
		}
		return result;
	}

	private bool IsComplete(Series item)
	{
		if (string.IsNullOrEmpty(item.Name))
		{
			return false;
		}
		if (string.IsNullOrEmpty(item.Overview))
		{
			return false;
		}
		if (item.RemoteTrailers.Length == 0)
		{
			return false;
		}
		return true;
	}

	private void ImportData(MetadataResult<Series> seriesResult, SeriesRootObject seriesInfo, string preferredCountryCode, TmdbSettingsResult settings, bool isFirstLanguage)
	{
		Series item = seriesResult.Item;
		if (string.IsNullOrEmpty(item.Name))
		{
			item.Name = seriesInfo.GetTitle();
		}
		if (string.IsNullOrEmpty(item.OriginalTitle))
		{
			item.OriginalTitle = seriesInfo.GetOriginalTitle();
		}
		if (string.IsNullOrEmpty(item.Overview))
		{
			item.Overview = string.IsNullOrEmpty(seriesInfo.overview) ? null : WebUtility.HtmlDecode(seriesInfo.overview);
			item.Overview = (item.Overview != null) ? item.Overview.Replace("\n\n", "\n") : null;
		}
		if (item.RemoteTrailers.Length == 0)
		{
			foreach (TmdbVideo trailer in GetTrailers(seriesInfo))
			{
				string text = "http://www.youtube.com/watch?v=" + trailer.key;
				item.AddTrailerUrl(text);
			}
		}
		if (!isFirstLanguage)
		{
			return;
		}
		item.SetProviderId(MetadataProviders.Tmdb, seriesInfo.id.ToString(CultureInfo.InvariantCulture));
		if (float.TryParse(seriesInfo.vote_average.ToString(CultureInfo.InvariantCulture), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var result))
		{
			item.CommunityRating = result;
		}
		if (seriesInfo.networks != null)
		{
			item.SetStudios(seriesInfo.networks.Select((Network i) => i.name));
		}
		if (seriesInfo.genres != null)
		{
			item.SetGenres(seriesInfo.genres.Select((TmdbGenre i) => i.name));
		}
		item.RunTimeTicks = seriesInfo.episode_run_time.Select((int i) => TimeSpan.FromMinutes(i).Ticks).FirstOrDefault();
		if (string.Equals(seriesInfo.status, "Ended", StringComparison.OrdinalIgnoreCase) || string.Equals(seriesInfo.status, "Cancelled", StringComparison.OrdinalIgnoreCase) || string.Equals(seriesInfo.status, "Canceled", StringComparison.OrdinalIgnoreCase))
		{
			item.Status = SeriesStatus.Ended;
			item.EndDate = seriesInfo.last_air_date;
		}
		else
		{
			item.Status = SeriesStatus.Continuing;
		}
		item.PremiereDate = seriesInfo.first_air_date;
		item.ProductionYear = seriesInfo.first_air_date.Year;
		TmdbExternalIds external_ids = seriesInfo.external_ids;
		if (external_ids != null)
		{
			if (!string.IsNullOrWhiteSpace(external_ids.imdb_id))
			{
				item.SetProviderId(MetadataProviders.Imdb, external_ids.imdb_id);
			}
			if (external_ids.tvrage_id > 0)
			{
				item.SetProviderId(MetadataProviders.TvRage, external_ids.tvrage_id.Value.ToString(CultureInfo.InvariantCulture));
			}
			if (external_ids.tvdb_id > 0)
			{
				item.SetProviderId(MetadataProviders.Tvdb, external_ids.tvdb_id.Value.ToString(CultureInfo.InvariantCulture));
			}
		}
		List<ContentRating> source = (seriesInfo.content_ratings ?? new ContentRatings()).results ?? new List<ContentRating>();
		ContentRating contentRating = source.FirstOrDefault((ContentRating c) => string.Equals(c.iso_3166_1, preferredCountryCode, StringComparison.OrdinalIgnoreCase));
		ContentRating contentRating2 = source.FirstOrDefault((ContentRating c) => string.Equals(c.iso_3166_1, "US", StringComparison.OrdinalIgnoreCase));
		ContentRating contentRating3 = source.FirstOrDefault();
		if (contentRating != null)
		{
			item.OfficialRating = contentRating.GetRating();
		}
		else if (contentRating2 != null)
		{
			item.OfficialRating = contentRating2.GetRating();
		}
		else if (contentRating3 != null)
		{
			item.OfficialRating = contentRating3.GetRating();
		}
		seriesResult.ResetPeople();
		string imageUrl = settings.images.GetImageUrl("original");
		if (seriesInfo.credits == null || seriesInfo.credits.cast == null)
		{
			return;
		}
		foreach (TmdbCast item2 in seriesInfo.credits.cast.OrderBy((TmdbCast a) => a.order))
		{
			PersonInfo val = new PersonInfo
			{
				Name = item2.name.Trim(),
				Role = item2.character,
				Type = PersonType.Actor
			};
			if (!string.IsNullOrWhiteSpace(item2.profile_path))
			{
				val.ImageUrl = imageUrl + item2.profile_path;
			}
			if (item2.id > 0)
			{
				val.SetProviderId(MetadataProviders.Tmdb, item2.id.ToString(CultureInfo.InvariantCulture));
			}
			seriesResult.AddPerson(val);
		}
	}

	private List<TmdbVideo> GetTrailers(SeriesRootObject seriesInfo)
	{
		List<TmdbVideo> list = new List<TmdbVideo>();
		if (seriesInfo.videos != null && seriesInfo.videos.results != null)
		{
			foreach (TmdbVideo result in seriesInfo.videos.results)
			{
				if (string.Equals(result.type, "trailer", StringComparison.OrdinalIgnoreCase) && string.Equals(result.site, "youtube", StringComparison.OrdinalIgnoreCase))
				{
					list.Add(result);
				}
			}
		}
		return list;
	}

	internal static string GetSeriesDataPath(IApplicationPaths appPaths, string tmdbId)
	{
		return Path.Combine(GetSeriesDataPath(appPaths), tmdbId);
	}

	internal static string GetSeriesDataPath(IApplicationPaths appPaths)
	{
		return Path.Combine(appPaths.CachePath, "tmdb-tv");
	}

	internal async Task<SeriesRootObject> DownloadSeriesInfo(string id, string preferredMetadataLanguage, CancellationToken cancellationToken)
	{
		SeriesRootObject seriesRootObject = await FetchMainResult(id, preferredMetadataLanguage, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (seriesRootObject == null)
		{
			return null;
		}
		string dataFilePath = GetDataFilePath(id, preferredMetadataLanguage);
		FileSystem.CreateDirectory(FileSystem.GetDirectoryName(dataFilePath));
		JsonSerializer.SerializeToFile(seriesRootObject, dataFilePath);
		return seriesRootObject;
	}

	internal async Task<SeriesRootObject> FetchMainResult(string id, string language, CancellationToken cancellationToken)
	{
		GetConfiguration();
		string url = GetApiUrl($"3/tv/{id}") + "&append_to_response=alternative_titles,reviews,credits,images,keywords,external_ids,videos,content_ratings,episode_groups";
		if (!string.IsNullOrEmpty(language))
		{
			url = url + "&language=" + language;
		}
		url = AddImageLanguageParam(url, language);
		cancellationToken.ThrowIfCancellationRequested();
		try
		{
			using HttpResponseInfo response = await GetMovieDbResponse(new HttpRequestOptions
			{
				Url = url,
				CancellationToken = cancellationToken,
				AcceptHeader = MovieDbProviderBase.AcceptHeader
			}).ConfigureAwait(continueOnCapturedContext: false);
			using Stream json = response.Content;
			return await JsonSerializer.DeserializeFromStreamAsync<SeriesRootObject>(json).ConfigureAwait(continueOnCapturedContext: false);
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

	internal Task<SeriesRootObject> EnsureSeriesInfo(string tmdbId, string language, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(tmdbId))
		{
			throw new ArgumentNullException("tmdbId");
		}
		string dataFilePath = GetDataFilePath(tmdbId, language);
		FileSystemMetadata fileSystemInfo = FileSystem.GetFileSystemInfo(dataFilePath);
		if (fileSystemInfo.Exists && DateTimeOffset.UtcNow - FileSystem.GetLastWriteTimeUtc(fileSystemInfo) <= MovieDbProviderBase.CacheTime)
		{
			return JsonSerializer.DeserializeFromFileAsync<SeriesRootObject>(dataFilePath);
		}
		return DownloadSeriesInfo(tmdbId, language, cancellationToken);
	}

	internal string GetDataFilePath(string tmdbId, string preferredLanguage)
	{
		if (string.IsNullOrEmpty(tmdbId))
		{
			throw new ArgumentNullException("tmdbId");
		}
		string text = "series";
		if (!string.IsNullOrEmpty(preferredLanguage))
		{
			text = text + "-" + preferredLanguage;
		}
		text += ".json";
		return Path.Combine(GetSeriesDataPath(ConfigurationManager.ApplicationPaths, tmdbId), text);
	}

	private async Task<RemoteSearchResult> FindByExternalId(string id, string externalSource, string providerIdKey, TmdbSettingsResult tmdbSettings, CancellationToken cancellationToken)
	{
		GetConfiguration();
		string url = GetApiUrl($"3/find/{id}") + "&external_source=" + externalSource;
		using (HttpResponseInfo response = await GetMovieDbResponse(new HttpRequestOptions
		{
			Url = url,
			CancellationToken = cancellationToken,
			AcceptHeader = MovieDbProviderBase.AcceptHeader
		}).ConfigureAwait(continueOnCapturedContext: false))
		{
			using Stream json = response.Content;
			MovieDbSearch.ExternalIdLookupResult externalIdLookupResult = await JsonSerializer.DeserializeFromStreamAsync<MovieDbSearch.ExternalIdLookupResult>(json).ConfigureAwait(continueOnCapturedContext: false);
			if (externalIdLookupResult?.tv_results != null)
			{
				MovieDbSearch.TvResult tvResult = externalIdLookupResult.tv_results.FirstOrDefault();
				if (tvResult != null)
				{
					string imageUrl = tmdbSettings.images.GetImageUrl("original");
					RemoteSearchResult obj = MovieDbSearch.ToRemoteSearchResult(tvResult, imageUrl);
					obj.SetProviderId(providerIdKey, id);
					return obj;
				}
			}
		}
		return null;
	}

	private new string GetApiUrl(string path)
	{
		PluginOptions config = GetConfiguration();
		string baseUrl = config.TmdbApiBaseUrl?.TrimEnd(new char[1] { '/' });
		return (baseUrl != null) ? (baseUrl + "/" + path + "?api_key=" + config.ApiKey) : null;
	}
}
