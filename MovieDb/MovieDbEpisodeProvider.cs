using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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

public class MovieDbEpisodeProvider : MovieDbProviderBase, IRemoteMetadataProviderWithOptions<Episode, EpisodeInfo>, IRemoteMetadataProvider<Episode, EpisodeInfo>, IMetadataProvider<Episode>, IMetadataProvider, IRemoteMetadataProvider, IRemoteSearchProvider<EpisodeInfo>, IRemoteSearchProvider, IHasOrder, IHasMetadataFeatures
{
	public MetadataFeatures[] Features => new MetadataFeatures[1] { MetadataFeatures.Adult };

	public int Order => 1;

	public MovieDbEpisodeProvider(IHttpClient httpClient, IServerConfigurationManager configurationManager, IJsonSerializer jsonSerializer, IFileSystem fileSystem, ILocalizationManager localization, ILogManager logManager, IServerApplicationHost appHost, ILibraryManager libraryManager)
		: base(httpClient, configurationManager, jsonSerializer, fileSystem, localization, logManager, appHost, libraryManager)
	{
	}

	public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
	{
		List<RemoteSearchResult> list = new List<RemoteSearchResult>();
		DirectoryService directoryService = new DirectoryService(FileSystem);
		MetadataResult<Episode> val = await GetMetadata(new RemoteMetadataFetchOptions<EpisodeInfo>
		{
			SearchInfo = searchInfo,
			DirectoryService = directoryService
		}, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (val.HasMetadata)
		{
			list.Add(val.ToRemoteSearchResult(base.Name));
		}
		return list;
	}

	public async Task<MetadataResult<Episode>> GetMetadata(RemoteMetadataFetchOptions<EpisodeInfo> options, CancellationToken cancellationToken)
	{
		EpisodeInfo info = options.SearchInfo;
		MetadataResult<Episode> result = new MetadataResult<Episode>();
		if (info.IsMissingEpisode)
		{
			return result;
		}
		Dictionary<string, string> seriesProviderIds = info.SeriesProviderIds;
		seriesProviderIds.TryGetValue(MetadataProviders.Tmdb.ToString(), out var seriesTmdbId);
		if (string.IsNullOrEmpty(seriesTmdbId))
		{
			return result;
		}
		int? seasonNumber = info.ParentIndexNumber;
		int? episodeNumber = info.IndexNumber;
		if (!seasonNumber.HasValue || !episodeNumber.HasValue)
		{
			return result;
		}
		TmdbSettingsResult tmdbSettings = await GetTmdbSettings(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		ItemLookupInfo searchInfo = info;
		string[] movieDbMetadataLanguages = GetMovieDbMetadataLanguages(searchInfo, await GetTmdbLanguages(cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
		bool isFirstLanguage = true;
		string[] array = movieDbMetadataLanguages;
		string[] array2 = array;
		foreach (string language in array2)
		{
			try
			{
				RootObject rootObject = await GetEpisodeInfo(seriesTmdbId, seasonNumber.Value, episodeNumber.Value, language, options.DirectoryService, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (rootObject != null)
				{
					result.HasMetadata = true;
					result.QueriedById = true;
					if (result.Item == null)
					{
						result.Item = new Episode();
					}
					ImportData(result, info, rootObject, tmdbSettings, isFirstLanguage);
					isFirstLanguage = false;
					if (IsComplete(result.Item))
					{
						return result;
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

	private bool IsComplete(Episode item)
	{
		if (string.IsNullOrEmpty(item.Name))
		{
			return false;
		}
		if (string.IsNullOrEmpty(item.Overview))
		{
			return false;
		}
		return true;
	}

	private void ImportData(MetadataResult<Episode> result, EpisodeInfo info, RootObject response, TmdbSettingsResult settings, bool isFirstLanguage)
	{
		Episode item = result.Item;
		if (string.IsNullOrEmpty(item.Name))
		{
			item.Name = response.name;
		}
		if (string.IsNullOrEmpty(item.Overview))
		{
			item.Overview = response.overview;
		}
		if (!isFirstLanguage)
		{
			return;
		}
		if (item.RemoteTrailers.Length == 0 && response.videos != null && response.videos.results != null)
		{
			foreach (TmdbVideo result3 in response.videos.results)
			{
				if (string.Equals(result3.type, "trailer", StringComparison.OrdinalIgnoreCase) && string.Equals(result3.site, "youtube", StringComparison.OrdinalIgnoreCase))
				{
					string text = "http://www.youtube.com/watch?v=" + result3.key;
					item.AddTrailerUrl(text);
				}
			}
		}
		item.IndexNumber = info.IndexNumber;
		item.ParentIndexNumber = info.ParentIndexNumber;
		item.IndexNumberEnd = info.IndexNumberEnd;
		if (response.external_ids != null)
		{
			if (response.external_ids.tvdb_id > 0)
			{
				item.SetProviderId(MetadataProviders.Tvdb, response.external_ids.tvdb_id.Value.ToString(CultureInfo.InvariantCulture));
			}
			if (response.external_ids.tvrage_id > 0)
			{
				item.SetProviderId(MetadataProviders.TvRage, response.external_ids.tvrage_id.Value.ToString(CultureInfo.InvariantCulture));
			}
			if (!string.IsNullOrEmpty(response.external_ids.imdb_id) && !string.Equals(response.external_ids.imdb_id, "0", StringComparison.OrdinalIgnoreCase))
			{
				item.SetProviderId(MetadataProviders.Imdb, response.external_ids.imdb_id);
			}
		}
		item.PremiereDate = response.air_date;
		item.ProductionYear = item.PremiereDate.Value.Year;
		item.CommunityRating = (float)response.vote_average;
		string imageUrl = settings.images.GetImageUrl("original");
		result.ResetPeople();
		TmdbCredits credits = response.credits;
		if (credits == null)
		{
			return;
		}
		if (credits.cast != null)
		{
			foreach (TmdbCast item2 in credits.cast.OrderBy((TmdbCast a) => a.order))
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
				result.AddPerson(val);
			}
		}
		if (credits.guest_stars != null)
		{
			foreach (GuestStar item3 in credits.guest_stars.OrderBy((GuestStar a) => a.order))
			{
				PersonInfo val2 = new PersonInfo
				{
					Name = item3.name.Trim(),
					Role = item3.character,
					Type = PersonType.GuestStar
				};
				if (!string.IsNullOrWhiteSpace(item3.profile_path))
				{
					val2.ImageUrl = imageUrl + item3.profile_path;
				}
				if (item3.id > 0)
				{
					val2.SetProviderId(MetadataProviders.Tmdb, item3.id.ToString(CultureInfo.InvariantCulture));
				}
				result.AddPerson(val2);
			}
		}
		if (credits.crew == null)
		{
			return;
		}
		PersonType[] source = new PersonType[1] { PersonType.Director };
		foreach (TmdbCrew item4 in credits.crew)
		{
			PersonType val3 = PersonType.Lyricist;
			string department = item4.department;
			if (string.Equals(department, "writing", StringComparison.OrdinalIgnoreCase))
			{
				val3 = PersonType.Writer;
			}
			if (Enum.TryParse<PersonType>(department, ignoreCase: true, out var result2))
			{
				val3 = result2;
			}
			else if (Enum.TryParse<PersonType>(item4.job, ignoreCase: true, out result2))
			{
				val3 = result2;
			}
			if (source.Contains(val3))
			{
				result.AddPerson(new PersonInfo
				{
					Name = item4.name.Trim(),
					Role = item4.job,
					Type = val3
				});
			}
		}
	}

	public Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}
}
