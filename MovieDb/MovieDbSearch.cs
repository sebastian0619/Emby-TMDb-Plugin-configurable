using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;

namespace MovieDb;

public class MovieDbSearch
{
	public class TmdbMovieSearchResult
	{
		public bool adult { get; set; }

		public string backdrop_path { get; set; }

		public int id { get; set; }

		public string release_date { get; set; }

		public string poster_path { get; set; }

		public double popularity { get; set; }

		public string title { get; set; }

		public double vote_average { get; set; }

		public string name { get; set; }

		public int vote_count { get; set; }

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

	public class TmdbMovieSearchResults
	{
		public int page { get; set; }

		public List<TmdbMovieSearchResult> results { get; set; }

		public int total_pages { get; set; }

		public int total_results { get; set; }
	}

	public class TvResult
	{
		public string backdrop_path { get; set; }

		public string first_air_date { get; set; }

		public int id { get; set; }

		public string poster_path { get; set; }

		public double popularity { get; set; }

		public string name { get; set; }

		public string title { get; set; }

		public double vote_average { get; set; }

		public int vote_count { get; set; }

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

	public class TmdbTvSearchResults
	{
		public int page { get; set; }

		public List<TvResult> results { get; set; }

		public int total_pages { get; set; }

		public int total_results { get; set; }
	}

	public class ExternalIdLookupResult
	{
		public List<TmdbMovieSearchResult> movie_results { get; set; }

		public List<object> person_results { get; set; }

		public List<TvResult> tv_results { get; set; }
	}

	private readonly ILogger _logger;

	private readonly IJsonSerializer _json;

	private readonly ILibraryManager _libraryManager;

	public MovieDbSearch(ILogger logger, IJsonSerializer json, ILibraryManager libraryManager)
	{
		_logger = logger;
		_json = json;
		_libraryManager = libraryManager;
	}

	public async Task<List<RemoteSearchResult>> GetSearchResults(
		SeriesInfo idInfo, 
		string[] tmdbLanguages, 
		CancellationToken cancellationToken)
	{
		var name = idInfo.Name;
		var year = idInfo.Year;
		
		if (string.IsNullOrEmpty(name))
		{
			return new List<RemoteSearchResult>();
		}

		var config = Plugin.Instance.Configuration;
		string imageUrl = config.GetImageUrl("original");

		return await GetSearchResultsTv(
			name,
			year,
			tmdbLanguages?.FirstOrDefault(),
			idInfo.EnableAdultMetadata,
			imageUrl,
			cancellationToken)
			.ConfigureAwait(false);
	}

	public Task<List<RemoteSearchResult>> GetMovieSearchResults(ItemLookupInfo idInfo, string[] tmdbLanguages, TmdbSettingsResult tmdbSettings, CancellationToken cancellationToken)
	{
		return GetSearchResults(idInfo, tmdbLanguages, "movie", tmdbSettings, cancellationToken);
	}

	public Task<List<RemoteSearchResult>> GetCollectionSearchResults(ItemLookupInfo idInfo, string[] tmdbLanguages, TmdbSettingsResult tmdbSettings, CancellationToken cancellationToken)
	{
		return GetSearchResults(idInfo, tmdbLanguages, "collection", tmdbSettings, cancellationToken);
	}

	private async Task<List<RemoteSearchResult>> GetSearchResults(ItemLookupInfo idInfo, string[] tmdbLanguages, string searchType, TmdbSettingsResult tmdbSettings, CancellationToken cancellationToken)
	{
		string name = idInfo.Name;
		if (string.IsNullOrEmpty(name))
		{
			return new List<RemoteSearchResult>();
		}
		_logger.Info("MovieDbProvider: Finding id for item: " + name, Array.Empty<object>());
		foreach (string tmdbLanguage in tmdbLanguages)
		{
			List<RemoteSearchResult> list = await GetSearchResults(idInfo, searchType, tmdbLanguage, tmdbSettings, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (list.Count > 0)
			{
				return list;
			}
		}
		return new List<RemoteSearchResult>();
	}

	private async Task<List<RemoteSearchResult>> GetSearchResults(ItemLookupInfo idInfo, string searchType, string tmdbLanguage, TmdbSettingsResult tmdbSettings, CancellationToken cancellationToken)
	{
		string name = idInfo.Name;
		int? year = idInfo.Year;
		if (string.IsNullOrEmpty(name))
		{
			return new List<RemoteSearchResult>();
		}
		string tmdbImageUrl = tmdbSettings.images.GetImageUrl("original");
		List<RemoteSearchResult> list = await GetSearchResults(name, searchType, year, tmdbLanguage, idInfo.EnableAdultMetadata, tmdbImageUrl, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (list.Count == 0)
		{
			string b = name;
			if (name.EndsWith(",the", StringComparison.OrdinalIgnoreCase))
			{
				name = name.Substring(0, name.Length - 4);
			}
			else if (name.EndsWith(", the", StringComparison.OrdinalIgnoreCase))
			{
				name = name.Substring(0, name.Length - 5);
			}
			name = name.Replace(',', ' ');
			name = name.Replace('.', ' ');
			name = name.Replace('_', ' ');
			name = name.Replace('-', ' ');
			name = name.Replace('!', ' ');
			name = name.Replace('<', ' ');
			name = name.Replace('﹤', ' ');
			name = name.Replace('>', ' ');
			name = name.Replace('﹥', ' ');
			name = name.Replace(':', ' ');
			name = name.Replace('\ua789', ' ');
			name = name.Replace('"', ' ');
			name = name.Replace('“', ' ');
			name = name.Replace('/', ' ');
			name = name.Replace('⁄', ' ');
			name = name.Replace('|', ' ');
			name = name.Replace('⼁', ' ');
			name = name.Replace('?', ' ');
			name = name.Replace('？', ' ');
			name = name.Replace('*', ' ');
			name = name.Replace('﹡', ' ');
			name = name.Replace('\\', ' ');
			name = name.Replace('∖', ' ');
			name = name.Replace("'", string.Empty);
			int num = name.IndexOfAny(new char[2] { '(', '[' });
			if (num != -1)
			{
				if (num > 0)
				{
					name = name.Substring(0, num);
				}
				else
				{
					name = name.Replace('[', ' ');
					name = name.Replace(']', ' ');
					num = name.IndexOf('(');
					if (num != -1 && num > 0)
					{
						name = name.Substring(0, num);
					}
				}
			}
			name = name.Trim();
			if (!string.Equals(name, b, StringComparison.OrdinalIgnoreCase))
			{
				list = await GetSearchResults(name, searchType, year, tmdbLanguage, idInfo.EnableAdultMetadata, tmdbImageUrl, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		return list;
	}

	private Task<List<RemoteSearchResult>> GetSearchResults(string name, string type, int? year, string tmdbLanguage, bool includeAdult, string baseImageUrl, CancellationToken cancellationToken)
	{
		if (type == "tv")
		{
			return GetSearchResultsTv(name, year, tmdbLanguage, includeAdult, baseImageUrl, cancellationToken);
		}
		return GetSearchResultsGeneric(name, type, year, tmdbLanguage, includeAdult, baseImageUrl, cancellationToken);
	}

	public async Task<RemoteSearchResult> FindMovieByExternalId(string id, string externalSource, string providerIdKey, CancellationToken cancellationToken)
	{
		var config = Plugin.Instance!.Configuration;
		string url = $"{config.TmdbApiBaseUrl.TrimEnd('/')}/3/find/{id}?api_key={config.ApiKey}&external_source={externalSource}";
		
		HttpResponseInfo response = await MovieDbProvider.Current.GetMovieDbResponse(new HttpRequestOptions
		{
			Url = url,
			CancellationToken = cancellationToken,
			AcceptHeader = MovieDbProviderBase.AcceptHeader
		}).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			using Stream json = response.Content;
			ExternalIdLookupResult externalIdLookupResult = await _json.DeserializeFromStreamAsync<ExternalIdLookupResult>(json).ConfigureAwait(continueOnCapturedContext: false);
			if (externalIdLookupResult != null && externalIdLookupResult.movie_results != null)
			{
				TmdbMovieSearchResult tv = externalIdLookupResult.movie_results.FirstOrDefault();
				if (tv != null)
				{
					string imageUrl = config.GetImageUrl("original");
					return ParseMovieSearchResult(tv, imageUrl);
				}
			}
		}
		finally
		{
			((IDisposable)response)?.Dispose();
		}
		return null;
	}

	private async Task<List<RemoteSearchResult>> GetSearchResultsGeneric(string name, string type, int? year, string tmdbLanguage, bool includeAdult, string baseImageUrl, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(name))
		{
			throw new ArgumentException("name");
		}

		var config = Plugin.Instance!.Configuration;
		string text = $"{config.TmdbApiBaseUrl.TrimEnd('/')}/3/search/{type}?api_key={config.ApiKey}&query={WebUtility.UrlEncode(name)}&language={tmdbLanguage}";
		if (includeAdult)
		{
			text += "&include_adult=true";
		}
		bool enableOneYearTolerance = false;
		if (!enableOneYearTolerance && year.HasValue)
		{
			text = text + "&year=" + year.Value.ToString(CultureInfo.InvariantCulture);
		}
		HttpResponseInfo response = await MovieDbProvider.Current.GetMovieDbResponse(new HttpRequestOptions
		{
			Url = text,
			CancellationToken = cancellationToken,
			AcceptHeader = MovieDbProviderBase.AcceptHeader
		}).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			using Stream json = response.Content;
			return (from i in (await _json.DeserializeFromStreamAsync<TmdbMovieSearchResults>(json).ConfigureAwait(continueOnCapturedContext: false)).results ?? new List<TmdbMovieSearchResult>()
				select ParseMovieSearchResult(i, baseImageUrl) into i
				where !year.HasValue || !i.ProductionYear.HasValue || !enableOneYearTolerance || Math.Abs(year.Value - i.ProductionYear.Value) <= 1
				select i).ToList();
		}
		finally
		{
			((IDisposable)response)?.Dispose();
		}
	}

	private RemoteSearchResult ParseMovieSearchResult(TmdbMovieSearchResult i, string baseImageUrl)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Expected O, but got Unknown
		RemoteSearchResult val = new RemoteSearchResult
		{
			SearchProviderName = MovieDbProvider.Current.Name,
			Name = i.GetTitle(),
			ImageUrl = (string.IsNullOrWhiteSpace(i.poster_path) ? null : (baseImageUrl + i.poster_path))
		};
		if (!string.IsNullOrEmpty(i.release_date) && DateTimeOffset.TryParseExact(i.release_date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
		{
			val.PremiereDate = result.ToUniversalTime();
			val.ProductionYear = val.PremiereDate.Value.Year;
		}
		ProviderIdsExtensions.SetProviderId((IHasProviderIds)(object)val, (MetadataProviders)3, i.id.ToString(CultureInfo.InvariantCulture));
		return val;
	}

	private async Task<List<RemoteSearchResult>> GetSearchResultsTv(string name, int? year, string language, bool includeAdult, string baseImageUrl, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(name))
		{
			throw new ArgumentException("name");
		}

		var config = Plugin.Instance!.Configuration;
		string text = $"{config.TmdbApiBaseUrl.TrimEnd('/')}/3/search/tv?api_key={config.ApiKey}&query={WebUtility.UrlEncode(name)}&language={language}";
		if (year.HasValue)
		{
			text = text + "&first_air_date_year=" + year.Value.ToString(CultureInfo.InvariantCulture);
		}
		if (includeAdult)
		{
			text += "&include_adult=true";
		}
		HttpResponseInfo response = await MovieDbProvider.Current.GetMovieDbResponse(new HttpRequestOptions
		{
			Url = text,
			CancellationToken = cancellationToken,
			AcceptHeader = MovieDbProviderBase.AcceptHeader
		}).ConfigureAwait(continueOnCapturedContext: false);
		try
		{
			using Stream json = response.Content;
			return ((await _json.DeserializeFromStreamAsync<TmdbTvSearchResults>(json).ConfigureAwait(continueOnCapturedContext: false)).results ?? new List<TvResult>()).Select((TvResult i) => ToRemoteSearchResult(i, baseImageUrl)).ToList();
		}
		finally
		{
			((IDisposable)response)?.Dispose();
		}
	}

	public static RemoteSearchResult ToRemoteSearchResult(TvResult i, string baseImageUrl)
	{

		RemoteSearchResult val = new RemoteSearchResult
		{
			SearchProviderName = MovieDbProvider.Current.Name,
			Name = i.GetTitle(),
			ImageUrl = (string.IsNullOrWhiteSpace(i.poster_path) ? null : (baseImageUrl + i.poster_path))
		};
		if (!string.IsNullOrEmpty(i.first_air_date) && DateTimeOffset.TryParseExact(i.first_air_date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
		{
			val.PremiereDate = result.ToUniversalTime();
			val.ProductionYear = val.PremiereDate.Value.Year;
		}
		ProviderIdsExtensions.SetProviderId((IHasProviderIds)(object)val, (MetadataProviders)3, i.id.ToString(CultureInfo.InvariantCulture));
		return val;
	}
}
