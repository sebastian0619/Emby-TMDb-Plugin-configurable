using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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

public class MovieDbSeasonProvider : MovieDbProviderBase, IRemoteMetadataProviderWithOptions<Season, SeasonInfo>, IRemoteMetadataProvider<Season, SeasonInfo>, IMetadataProvider<Season>, IMetadataProvider, IRemoteMetadataProvider, IRemoteSearchProvider<SeasonInfo>, IRemoteSearchProvider, IHasMetadataFeatures
{
	public class Episode
	{
		public string air_date { get; set; }

		public int episode_number { get; set; }

		public int id { get; set; }

		public string name { get; set; }

		public string overview { get; set; }

		public string still_path { get; set; }

		public double vote_average { get; set; }

		public int vote_count { get; set; }
	}

	public class Videos
	{
		public List<object> results { get; set; }
	}

	public class SeasonRootObject
	{
		public DateTimeOffset air_date { get; set; }

		public List<Episode> episodes { get; set; }

		public string name { get; set; }

		public string overview { get; set; }

		public int id { get; set; }

		public string poster_path { get; set; }

		public int season_number { get; set; }

		public TmdbCredits credits { get; set; }

		public TmdbImages images { get; set; }

		public TmdbExternalIds external_ids { get; set; }

		public Videos videos { get; set; }
	}

	private const string TvSeasonPath = "3/tv/{0}/season/{1}";
	private const string AppendToResponse = "images,keywords,external_ids,credits,videos";

	public MetadataFeatures[] Features => (MetadataFeatures[])(object)new MetadataFeatures[1] { (MetadataFeatures)2 };

	public MovieDbSeasonProvider(IJsonSerializer jsonSerializer, IHttpClient httpClient, IFileSystem fileSystem, IServerConfigurationManager configurationManager, ILogManager logManager, ILocalizationManager localization, IServerApplicationHost appHost, ILibraryManager libraryManager)
		: base(httpClient, configurationManager, jsonSerializer, fileSystem, localization, logManager, appHost, libraryManager)
	{
	}

	public async Task<MetadataResult<Season>> GetMetadata(RemoteMetadataFetchOptions<SeasonInfo> options, CancellationToken cancellationToken)
	{
		SeasonInfo info = options.SearchInfo;
		MetadataResult<Season> result = new MetadataResult<Season>();
		ProviderIdDictionary seriesProviderIds = info.SeriesProviderIds;
		seriesProviderIds.TryGetValue(MetadataProviders.Tmdb.ToString(), out string seriesTmdbId);
		int? seasonNumber = ((ItemLookupInfo)info).IndexNumber;
		if (!string.IsNullOrWhiteSpace(seriesTmdbId) && seasonNumber.HasValue)
		{
			string[] movieDbMetadataLanguages = GetMovieDbMetadataLanguages((ItemLookupInfo)(object)info, await GetTmdbLanguages(cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
			try
			{
				bool isFirstLanguage = true;
				string[] array = movieDbMetadataLanguages;
				foreach (string language in array)
				{
					SeasonRootObject seasonRootObject = await EnsureSeasonInfo(seriesTmdbId, seasonNumber.Value, language, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
					if (seasonRootObject != null)
					{
						((BaseMetadataResult)result).HasMetadata = true;
						if (result.Item == null)
						{
							result.Item = new Season();
						}
						ImportData(result.Item, seasonRootObject, ((ItemLookupInfo)info).Name, seasonNumber.Value, isFirstLanguage);
						isFirstLanguage = false;
						if (IsComplete(result.Item))
						{
							return result;
						}
					}
				}
			}
			catch (HttpException val2)
			{
				HttpException val3 = val2;
				if (val3.StatusCode.HasValue && val3.StatusCode.Value == HttpStatusCode.NotFound)
				{
					return result;
				}
				throw;
			}
		}
		return result;
	}

	private bool IsComplete(Season item)
	{
		if (string.IsNullOrEmpty(((BaseItem)item).Name))
		{
			return false;
		}
		if (string.IsNullOrEmpty(((BaseItem)item).Overview))
		{
			return false;
		}
		return true;
	}

	private void ImportData(Season item, SeasonRootObject seasonInfo, string name, int seasonNumber, bool isFirstLanguage)
	{
		if (string.IsNullOrEmpty(((BaseItem)item).Name))
		{
			((BaseItem)item).Name = seasonInfo.name;
		}
		if (string.IsNullOrEmpty(((BaseItem)item).Overview))
		{
			((BaseItem)item).Overview = seasonInfo.overview;
		}
		if (isFirstLanguage)
		{
			((BaseItem)item).IndexNumber = seasonNumber;
			if (seasonInfo.external_ids.tvdb_id > 0)
			{
				ProviderIdsExtensions.SetProviderId((IHasProviderIds)(object)item, (MetadataProviders)4, seasonInfo.external_ids.tvdb_id.Value.ToString(CultureInfo.InvariantCulture));
			}
			TmdbCredits credits = seasonInfo.credits;
			if (credits != null)
			{
				_ = credits.cast;
				_ = credits.crew;
			}
			((BaseItem)item).PremiereDate = seasonInfo.air_date;
			((BaseItem)item).ProductionYear = ((BaseItem)item).PremiereDate.Value.Year;
		}
	}

	public Task<MetadataResult<Season>> GetMetadata(SeasonInfo info, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}

	public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeasonInfo searchInfo, CancellationToken cancellationToken)
	{
		return Task.FromResult((IEnumerable<RemoteSearchResult>)new List<RemoteSearchResult>());
	}

	internal async Task<SeasonRootObject> EnsureSeasonInfo(string tmdbId, int seasonNumber, string language, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(tmdbId))
		{
			throw new ArgumentNullException("tmdbId");
		}
		string path = GetDataFilePath(tmdbId, seasonNumber, language);
		FileSystemMetadata fileSystemInfo = FileSystem.GetFileSystemInfo(path);
		SeasonRootObject seasonRootObject = null;
		if (fileSystemInfo.Exists && DateTimeOffset.UtcNow - FileSystem.GetLastWriteTimeUtc(fileSystemInfo) <= MovieDbProviderBase.CacheTime)
		{
			seasonRootObject = await JsonSerializer.DeserializeFromFileAsync<SeasonRootObject>(fileSystemInfo.FullName).ConfigureAwait(continueOnCapturedContext: false);
		}
		if (seasonRootObject == null)
		{
			seasonRootObject = await FetchMainResult(tmdbId, seasonNumber, language, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			FileSystem.CreateDirectory(FileSystem.GetDirectoryName(path));
			JsonSerializer.SerializeToFile((object)seasonRootObject, path);
		}
		return seasonRootObject;
	}

	internal string GetDataFilePath(string tmdbId, int seasonNumber, string preferredLanguage)
	{
		if (string.IsNullOrEmpty(tmdbId))
		{
			throw new ArgumentNullException("tmdbId");
		}
		string text = $"season-{seasonNumber.ToString(CultureInfo.InvariantCulture)}";
		if (!string.IsNullOrEmpty(preferredLanguage))
		{
			text = text + "-" + preferredLanguage;
		}
		text += ".json";
		return Path.Combine(MovieDbSeriesProvider.GetSeriesDataPath((IApplicationPaths)(object)ConfigurationManager.ApplicationPaths, tmdbId), text);
	}

	private async Task<SeasonRootObject> FetchMainResult(string id, int seasonNumber, string language, CancellationToken cancellationToken)
	{
		var config = GetConfiguration();
		string path = string.Format(TvSeasonPath, 
			id, 
			seasonNumber.ToString(CultureInfo.InvariantCulture));

		string url = GetApiUrl(path) + $"&append_to_response={AppendToResponse}";

		if (!string.IsNullOrEmpty(language))
		{
			url += $"&language={language}";
		}
		url = AddImageLanguageParam(url, language);

		cancellationToken.ThrowIfCancellationRequested();

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
				return await JsonSerializer.DeserializeFromStreamAsync<SeasonRootObject>(json)
					.ConfigureAwait(false);
			}
		}
		finally
		{
			((IDisposable)response)?.Dispose();
		}
	}

	private new string GetApiUrl(string path)
	{
		var config = GetConfiguration();
		var baseUrl = config.TmdbApiBaseUrl.TrimEnd('/');
		return $"{baseUrl}/{path}?api_key={config.ApiKey}";
	}
}
