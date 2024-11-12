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

	public static MovieDbSeriesProvider Current { get; private set; }

	public MetadataFeatures[] Features => (MetadataFeatures[])(object)new MetadataFeatures[1] { (MetadataFeatures)2 };

	public int Order => 1;

	public MovieDbSeriesProvider(IJsonSerializer jsonSerializer, IHttpClient httpClient, IFileSystem fileSystem, IServerConfigurationManager configurationManager, ILogManager logManager, ILocalizationManager localization, IServerApplicationHost appHost, ILibraryManager libraryManager)
		: base(httpClient, configurationManager, jsonSerializer, fileSystem, localization, logManager, appHost, libraryManager)
	{
		Current = this;
	}

	public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
	{
		string tmdbId = ProviderIdsExtensions.GetProviderId((IHasProviderIds)(object)searchInfo, (MetadataProviders)3);
		
		if (!string.IsNullOrEmpty(tmdbId))
		{
			MetadataResult<Series> val = await GetMetadata(searchInfo, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!((BaseMetadataResult)val).HasMetadata)
			{
				return new List<RemoteSearchResult>();
			}
			RemoteSearchResult result = ((BaseMetadataResult)val).ToRemoteSearchResult(base.Name);
			List<TmdbImage> list = ((await EnsureSeriesInfo(tmdbId, null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false))?.images ?? new TmdbImages()).posters ?? new List<TmdbImage>();
			var config = Plugin.Instance.Configuration;
			string imageUrl = config.GetImageUrl("original");
			result.ImageUrl = list.Count == 0 ? null : imageUrl + list[0].file_path;
			return new RemoteSearchResult[] { result };
		}

		string providerId = ProviderIdsExtensions.GetProviderId((IHasProviderIds)(object)searchInfo, (MetadataProviders)2);
		if (!string.IsNullOrEmpty(providerId))
		{
			RemoteSearchResult val3 = await FindByExternalId(providerId, "imdb_id", MetadataProviders.Imdb.ToString(), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (val3 != null)
			{
				return new RemoteSearchResult[] { val3 };
			}
		}

		string providerId2 = ProviderIdsExtensions.GetProviderId((IHasProviderIds)(object)searchInfo, (MetadataProviders)4);
		if (!string.IsNullOrEmpty(providerId2))
		{
			RemoteSearchResult val4 = await FindByExternalId(providerId2, "tvdb_id", MetadataProviders.Tvdb.ToString(), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (val4 != null)
			{
				return new RemoteSearchResult[] { val4 };
			}
		}

		string[] movieDbMetadataLanguages = GetMovieDbMetadataLanguages((ItemLookupInfo)(object)searchInfo, await GetTmdbLanguages(cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
		return await FilterSearchResults(
			await new MovieDbSearch(Logger, JsonSerializer, LibraryManager)
				.GetSearchResults(searchInfo, movieDbMetadataLanguages, cancellationToken)
				.ConfigureAwait(continueOnCapturedContext: false),
			searchInfo,
			foundByName: true,
			cancellationToken
		).ConfigureAwait(continueOnCapturedContext: false);
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
		Logger.Info("Checking AiredWithin for {0}. episodeAirDate: {1}", new object[2]
		{
			remoteSearchResult.Name,
			episodeAirDate.UtcDateTime.ToShortDateString()
		});
		if (remoteSearchResult.PremiereDate.HasValue)
		{
			if (episodeAirDate.Year < remoteSearchResult.PremiereDate.Value.Year)
			{
				return false;
			}
			SeriesInfo seriesInfo = new SeriesInfo
			{
				ProviderIds = remoteSearchResult.ProviderIds,
				MetadataLanguage = ((ItemLookupInfo)searchInfo).MetadataLanguage,
				MetadataCountryCode = ((ItemLookupInfo)searchInfo).MetadataCountryCode,
				Name = remoteSearchResult.Name,
				Year = remoteSearchResult.ProductionYear,
				PremiereDate = remoteSearchResult.PremiereDate,
				DisplayOrder = searchInfo.DisplayOrder,
				EnableAdultMetadata = ((ItemLookupInfo)searchInfo).EnableAdultMetadata
			};
			MetadataResult<Series> val = await GetMetadata(seriesInfo, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (((BaseMetadataResult)val).HasMetadata)
			{
				Logger.Info("AiredWithin for {0} Item.PremiereDate: {1}, Item.EndDate: {2}", new object[3]
				{
					((ItemLookupInfo)seriesInfo).Name,
					((BaseItem)val.Item).PremiereDate?.UtcDateTime.ToShortDateString(),
					((BaseItem)val.Item).EndDate?.UtcDateTime.ToShortDateString()
				});
				if (((BaseItem)val.Item).PremiereDate.HasValue)
				{
					if (episodeAirDate.Year < ((BaseItem)val.Item).PremiereDate.Value.Year)
					{
						return false;
					}
					if (((BaseItem)val.Item).EndDate.HasValue && episodeAirDate.Year > ((BaseItem)val.Item).EndDate.Value.Year)
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
		MetadataResult<Series> result = new MetadataResult<Series>();
		((BaseMetadataResult)result).QueriedById = true;
		string tmdbId = ProviderIdsExtensions.GetProviderId((IHasProviderIds)(object)info, (MetadataProviders)3);
		if (string.IsNullOrEmpty(tmdbId))
		{
			string providerId = ProviderIdsExtensions.GetProviderId((IHasProviderIds)(object)info, (MetadataProviders)2);
			if (!string.IsNullOrEmpty(providerId))
			{
				MovieDbSeriesProvider movieDbSeriesProvider = this;
				RemoteSearchResult val2 = await movieDbSeriesProvider.FindByExternalId(providerId, "imdb_id", MetadataProviders.Imdb.ToString(), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (val2 != null)
				{
					tmdbId = ProviderIdsExtensions.GetProviderId((IHasProviderIds)(object)val2, (MetadataProviders)3);
				}
			}
		}
		if (string.IsNullOrEmpty(tmdbId))
		{
			string providerId2 = ProviderIdsExtensions.GetProviderId((IHasProviderIds)(object)info, (MetadataProviders)4);
			if (!string.IsNullOrEmpty(providerId2))
			{
				MovieDbSeriesProvider movieDbSeriesProvider2 = this;
				RemoteSearchResult val3 = await movieDbSeriesProvider2.FindByExternalId(providerId2, "tvdb_id", MetadataProviders.Tvdb.ToString(), cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (val3 != null)
				{
					tmdbId = ProviderIdsExtensions.GetProviderId((IHasProviderIds)(object)val3, (MetadataProviders)3);
				}
			}
		}
		string[] metadataLanguages = GetMovieDbMetadataLanguages((ItemLookupInfo)(object)info, await GetTmdbLanguages(cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
		if (string.IsNullOrEmpty(tmdbId))
		{
			((BaseMetadataResult)result).QueriedById = false;
			RemoteSearchResult val4 = (await new MovieDbSearch(Logger, JsonSerializer, LibraryManager).GetSearchResults(info, metadataLanguages, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).FirstOrDefault();
			if (val4 != null)
			{
				tmdbId = ProviderIdsExtensions.GetProviderId((IHasProviderIds)(object)val4, (MetadataProviders)3);
			}
		}
		if (!string.IsNullOrEmpty(tmdbId))
		{
			cancellationToken.ThrowIfCancellationRequested();
			bool isFirstLanguage = true;
			string[] array = metadataLanguages;
			foreach (string language in array)
			{
				SeriesRootObject seriesRootObject = await EnsureSeriesInfo(tmdbId, language, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (seriesRootObject != null)
				{
					((BaseMetadataResult)result).HasMetadata = true;
					if (result.Item == null)
					{
						result.Item = new Series();
					}
					ImportData(result, seriesRootObject, ((ItemLookupInfo)info).MetadataCountryCode, cancellationToken, isFirstLanguage);
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
		if (string.IsNullOrEmpty(((BaseItem)item).Name))
		{
			return false;
		}
		if (string.IsNullOrEmpty(((BaseItem)item).Overview))
		{
			return false;
		}
		if (((BaseItem)item).RemoteTrailers.Length == 0)
		{
			return false;
		}
		return true;
	}

	private void ImportData(MetadataResult<Series> seriesResult, SeriesRootObject seriesInfo, string preferredCountryCode, CancellationToken cancellationToken, bool isFirstLanguage)
	{
		//IL_0437: Unknown result type (might be due to invalid IL or missing references)
		//IL_043c: Unknown result type (might be due to invalid IL or missing references)
		//IL_044e: Unknown result type (might be due to invalid IL or missing references)
		//IL_045b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0464: Expected O, but got Unknown
		Series item = seriesResult.Item;
		if (string.IsNullOrEmpty(((BaseItem)item).Name))
		{
			((BaseItem)item).Name = seriesInfo.GetTitle();
		}
		if (string.IsNullOrEmpty(((BaseItem)item).OriginalTitle))
		{
			((BaseItem)item).OriginalTitle = seriesInfo.GetOriginalTitle();
		}
		if (string.IsNullOrEmpty(((BaseItem)item).Overview))
		{
			((BaseItem)item).Overview = (string.IsNullOrEmpty(seriesInfo.overview) ? null : WebUtility.HtmlDecode(seriesInfo.overview));
			((BaseItem)item).Overview = ((((BaseItem)item).Overview != null) ? ((BaseItem)item).Overview.Replace("\n\n", "\n") : null);
		}
		if (((BaseItem)item).RemoteTrailers.Length == 0)
		{
			foreach (TmdbVideo trailer in GetTrailers(seriesInfo))
			{
				string text = $"http://www.youtube.com/watch?v={trailer.key}";
				Extensions.AddTrailerUrl((BaseItem)(object)item, text);
			}
		}
		if (!isFirstLanguage)
		{
			return;
		}
		ProviderIdsExtensions.SetProviderId((IHasProviderIds)(object)item, (MetadataProviders)3, seriesInfo.id.ToString(CultureInfo.InvariantCulture));
		if (float.TryParse(seriesInfo.vote_average.ToString(CultureInfo.InvariantCulture), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var result))
		{
			((BaseItem)item).CommunityRating = result;
		}
		if (seriesInfo.networks != null)
		{
			((BaseItem)item).SetStudios(seriesInfo.networks.Select((Network i) => i.name));
		}
		if (seriesInfo.genres != null)
		{
			((BaseItem)item).SetGenres(seriesInfo.genres.Select((TmdbGenre i) => i.name));
		}
		((BaseItem)item).RunTimeTicks = seriesInfo.episode_run_time.Select((int i) => TimeSpan.FromMinutes(i).Ticks).FirstOrDefault();
		if (string.Equals(seriesInfo.status, "Ended", StringComparison.OrdinalIgnoreCase) || string.Equals(seriesInfo.status, "Cancelled", StringComparison.OrdinalIgnoreCase) || string.Equals(seriesInfo.status, "Canceled", StringComparison.OrdinalIgnoreCase))
		{
			item.Status = (SeriesStatus)2;
			((BaseItem)item).EndDate = seriesInfo.last_air_date;
		}
		else
		{
			item.Status = (SeriesStatus)1;
		}
		((BaseItem)item).PremiereDate = seriesInfo.first_air_date;
		((BaseItem)item).ProductionYear = seriesInfo.first_air_date.Year;
		TmdbExternalIds external_ids = seriesInfo.external_ids;
		if (external_ids != null)
		{
			if (!string.IsNullOrWhiteSpace(external_ids.imdb_id))
			{
				ProviderIdsExtensions.SetProviderId((IHasProviderIds)(object)item, (MetadataProviders)2, external_ids.imdb_id);
			}
			if (external_ids.tvrage_id > 0)
			{
				ProviderIdsExtensions.SetProviderId((IHasProviderIds)(object)item, (MetadataProviders)15, external_ids.tvrage_id.Value.ToString(CultureInfo.InvariantCulture));
			}
			if (external_ids.tvdb_id > 0)
			{
				ProviderIdsExtensions.SetProviderId((IHasProviderIds)(object)item, (MetadataProviders)4, external_ids.tvdb_id.Value.ToString(CultureInfo.InvariantCulture));
			}
		}
		List<ContentRating> source = (seriesInfo.content_ratings ?? new ContentRatings()).results ?? new List<ContentRating>();
		ContentRating contentRating = source.FirstOrDefault((ContentRating c) => string.Equals(c.iso_3166_1, preferredCountryCode, StringComparison.OrdinalIgnoreCase));
		ContentRating contentRating2 = source.FirstOrDefault((ContentRating c) => string.Equals(c.iso_3166_1, "US", StringComparison.OrdinalIgnoreCase));
		ContentRating contentRating3 = source.FirstOrDefault();
		if (contentRating != null)
		{
			((BaseItem)item).OfficialRating = contentRating.GetRating();
		}
		else if (contentRating2 != null)
		{
			((BaseItem)item).OfficialRating = contentRating2.GetRating();
		}
		else if (contentRating3 != null)
		{
			((BaseItem)item).OfficialRating = contentRating3.GetRating();
		}
		((BaseMetadataResult)seriesResult).ResetPeople();
		var config = Plugin.Instance.Configuration;
		string imageUrl = config.GetImageUrl("original");
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
				Type = (PersonType)0
			};
			if (!string.IsNullOrWhiteSpace(item2.profile_path))
			{
				val.ImageUrl = imageUrl + item2.profile_path;
			}
			if (item2.id > 0)
			{
				ProviderIdsExtensions.SetProviderId((IHasProviderIds)(object)val, (MetadataProviders)3, item2.id.ToString(CultureInfo.InvariantCulture));
			}
			((BaseMetadataResult)seriesResult).AddPerson(val);
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
		JsonSerializer.SerializeToFile((object)seriesRootObject, dataFilePath);
		return seriesRootObject;
	}

	internal async Task<SeriesRootObject> FetchMainResult(string id, string language, CancellationToken cancellationToken)
	{
		var config = GetConfiguration();
		string url = GetApiUrl(string.Format(TvInfoPath, id)) + $"&append_to_response={AppendToResponse}";
		
		if (!string.IsNullOrEmpty(language))
		{
			url += $"&language={language}";
		}
		url = AddImageLanguageParam(url, language);

		cancellationToken.ThrowIfCancellationRequested();
		try
		{
			var response = await GetMovieDbResponse(new HttpRequestOptions
			{
				Url = url,
				CancellationToken = cancellationToken,
				AcceptHeader = AcceptHeader
			}).ConfigureAwait(false);

			try
			{
				using (Stream json = response.Content)
				{
					return await JsonSerializer.DeserializeFromStreamAsync<SeriesRootObject>(json).ConfigureAwait(false);
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
		return Path.Combine(GetSeriesDataPath((IApplicationPaths)(object)ConfigurationManager.ApplicationPaths, tmdbId), text);
	}

	private async Task<RemoteSearchResult> FindByExternalId(string id, string externalSource, string providerIdKey, CancellationToken cancellationToken)
	{
		var config = GetConfiguration();
		string url = GetApiUrl(string.Format(FindPath, id)) + $"&external_source={externalSource}";

		var response = await GetMovieDbResponse(new HttpRequestOptions
		{
			Url = url,
			CancellationToken = cancellationToken,
			AcceptHeader = AcceptHeader
		}).ConfigureAwait(false);

		try
		{
			using (Stream json = response.Content)
			{
				var externalIdLookupResult = await JsonSerializer.DeserializeFromStreamAsync<MovieDbSearch.ExternalIdLookupResult>(json).ConfigureAwait(false);
				if (externalIdLookupResult?.tv_results != null)
				{
					var tvResult = externalIdLookupResult.tv_results.FirstOrDefault();
					if (tvResult != null)
					{
						string imageUrl = config.GetImageUrl("original");
						var obj = MovieDbSearch.ToRemoteSearchResult(tvResult, imageUrl);
						ProviderIdsExtensions.SetProviderId((IHasProviderIds)obj, providerIdKey, id);
						return obj;
					}
				}
			}
		}
		finally
		{
			((IDisposable)response)?.Dispose();
		}
		return null;
	}

	// 使用 new 关键字显式隐藏基类方法
	private new string GetApiUrl(string path)
	{
		var config = GetConfiguration();
		var baseUrl = config.TmdbApiBaseUrl?.TrimEnd('/');
		return baseUrl != null ? $"{baseUrl}/{path}?api_key={config.ApiKey}" : null;
	}
}
