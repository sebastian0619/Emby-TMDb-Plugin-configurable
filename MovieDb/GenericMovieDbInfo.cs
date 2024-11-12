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
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;

namespace MovieDb;

public class GenericMovieDbInfo<T> : MovieDbProviderBase where T : BaseItem, new()
{
	public GenericMovieDbInfo(IJsonSerializer jsonSerializer, IHttpClient httpClient, IFileSystem fileSystem, IServerConfigurationManager configurationManager, ILogManager logManager, ILocalizationManager localization, IServerApplicationHost appHost, ILibraryManager libraryManager)
		: base(httpClient, configurationManager, jsonSerializer, fileSystem, localization, logManager, appHost, libraryManager)
	{
	}

	public async Task<MetadataResult<T>> GetMetadata(ItemLookupInfo searchInfo, CancellationToken cancellationToken)
	{
		string tmdbId = ProviderIdsExtensions.GetProviderId((IHasProviderIds)(object)searchInfo, (MetadataProviders)3);
		string imdbId = ProviderIdsExtensions.GetProviderId((IHasProviderIds)(object)searchInfo, (MetadataProviders)2);
		string[] metadataLanguages = GetMovieDbMetadataLanguages(searchInfo, await GetTmdbLanguages(cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
		TmdbSettingsResult tmdbSettings = await GetTmdbSettings(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (string.IsNullOrEmpty(tmdbId) && string.IsNullOrEmpty(imdbId))
		{
			RemoteSearchResult val = (await new MovieDbSearch(Logger, JsonSerializer, LibraryManager).GetMovieSearchResults(searchInfo, metadataLanguages, tmdbSettings, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).FirstOrDefault();
			if (val != null)
			{
				tmdbId = ProviderIdsExtensions.GetProviderId((IHasProviderIds)(object)val, (MetadataProviders)3);
			}
		}
		MetadataResult<T> result = new MetadataResult<T>();
		if (!string.IsNullOrEmpty(tmdbId) || !string.IsNullOrEmpty(imdbId))
		{
			cancellationToken.ThrowIfCancellationRequested();
			bool isFirstLanguage = true;
			string[] array = metadataLanguages;
			foreach (string language in array)
			{
				MovieDbProvider.CompleteMovieData completeMovieData = await FetchMovieData(tmdbId, imdbId, language, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (completeMovieData != null)
				{
					((BaseMetadataResult)result).HasMetadata = true;
					if (result.Item == null)
					{
						result.Item = new T();
					}
					ProcessMainInfo(result, tmdbSettings, searchInfo.MetadataCountryCode, completeMovieData, isFirstLanguage);
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

	private bool IsComplete(T item)
	{
		if (string.IsNullOrEmpty(((BaseItem)item).Name))
		{
			return false;
		}
		if (string.IsNullOrEmpty(((BaseItem)item).Overview))
		{
			return false;
		}
		if (!(item is Trailer) && ((BaseItem)item).RemoteTrailers.Length == 0)
		{
			return false;
		}
		return true;
	}

	private async Task<MovieDbProvider.CompleteMovieData> FetchMovieData(string tmdbId, string imdbId, string language, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(tmdbId))
		{
			MovieDbProvider.CompleteMovieData completeMovieData = await MovieDbProvider.Current.FetchMainResult(imdbId, isTmdbId: false, language, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (completeMovieData != null)
			{
				tmdbId = completeMovieData.id.ToString(CultureInfo.InvariantCulture);
				string dataFilePath = MovieDbProvider.Current.GetDataFilePath(tmdbId, language);
				FileSystem.CreateDirectory(FileSystem.GetDirectoryName(dataFilePath));
				JsonSerializer.SerializeToFile((object)completeMovieData, dataFilePath);
			}
		}
		if (!string.IsNullOrWhiteSpace(tmdbId))
		{
			return await MovieDbProvider.Current.EnsureMovieInfo(tmdbId, language, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		return null;
	}

	private void ProcessMainInfo(MetadataResult<T> resultItem, TmdbSettingsResult settings, string preferredCountryCode, MovieDbProvider.CompleteMovieData movieData, bool isFirstLanguage)
	{
		//IL_022e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0233: Unknown result type (might be due to invalid IL or missing references)
		//IL_0247: Expected O, but got Unknown
		//IL_05d3: Unknown result type (might be due to invalid IL or missing references)
		//IL_04f0: Unknown result type (might be due to invalid IL or missing references)
		//IL_04f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_0507: Unknown result type (might be due to invalid IL or missing references)
		//IL_0514: Unknown result type (might be due to invalid IL or missing references)
		//IL_051d: Expected O, but got Unknown
		//IL_05eb: Unknown result type (might be due to invalid IL or missing references)
		//IL_05f7: Unknown result type (might be due to invalid IL or missing references)
		//IL_05f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0614: Unknown result type (might be due to invalid IL or missing references)
		//IL_060e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0610: Unknown result type (might be due to invalid IL or missing references)
		//IL_061d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0622: Unknown result type (might be due to invalid IL or missing references)
		//IL_0634: Unknown result type (might be due to invalid IL or missing references)
		//IL_0641: Unknown result type (might be due to invalid IL or missing references)
		//IL_0642: Unknown result type (might be due to invalid IL or missing references)
		//IL_064b: Expected O, but got Unknown
		T item = resultItem.Item;
		if (string.IsNullOrEmpty(((BaseItem)item).Name))
		{
			((BaseItem)item).Name = movieData.GetTitle();
		}
		if (string.IsNullOrEmpty(((BaseItem)item).OriginalTitle))
		{
			((BaseItem)item).OriginalTitle = movieData.GetOriginalTitle();
		}
		if (string.IsNullOrEmpty(((BaseItem)item).Overview))
		{
			((BaseItem)item).Overview = (string.IsNullOrEmpty(movieData.overview) ? null : WebUtility.HtmlDecode(movieData.overview));
			((BaseItem)item).Overview = ((((BaseItem)item).Overview != null) ? ((BaseItem)item).Overview.Replace("\n\n", "\n") : null);
		}
		if (string.IsNullOrEmpty(((BaseItem)item).Tagline) && !string.IsNullOrEmpty(movieData.tagline))
		{
			((BaseItem)item).Tagline = movieData.tagline;
		}
		if (((BaseItem)item).RemoteTrailers.Length == 0 && movieData.trailers != null && movieData.trailers.youtube != null)
		{
			((BaseItem)item).RemoteTrailers = (from i in movieData.trailers.youtube
				where string.Equals(i.type, "trailer", StringComparison.OrdinalIgnoreCase)
				select $"https://www.youtube.com/watch?v={i.source}").ToArray();
		}
		if (!isFirstLanguage)
		{
			return;
		}
		if (movieData.production_countries != null)
		{
			((BaseItem)item).ProductionLocations = movieData.production_countries.Select((MovieDbProvider.ProductionCountry i) => i.name).ToArray();
		}
		ProviderIdsExtensions.SetProviderId((IHasProviderIds)(object)item, (MetadataProviders)3, movieData.id.ToString(CultureInfo.InvariantCulture));
		ProviderIdsExtensions.SetProviderId((IHasProviderIds)(object)item, (MetadataProviders)2, movieData.imdb_id);
		if (movieData.belongs_to_collection != null && !string.IsNullOrEmpty(movieData.belongs_to_collection.name) && movieData.belongs_to_collection.id > 0)
		{
			LinkedItemInfo val = new LinkedItemInfo
			{
				Name = movieData.belongs_to_collection.name
			};
			ProviderIdsExtensions.SetProviderId((IHasProviderIds)(object)val, (MetadataProviders)3, movieData.belongs_to_collection.id.ToString(CultureInfo.InvariantCulture));
			((BaseItem)item).AddCollection(val);
		}
		if (float.TryParse(movieData.vote_average.ToString(CultureInfo.InvariantCulture), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var result))
		{
			((BaseItem)item).CommunityRating = result;
		}
		if (movieData.releases != null && movieData.releases.countries != null)
		{
			List<MovieDbProvider.Country> source = movieData.releases.countries.Where((MovieDbProvider.Country i) => !string.IsNullOrWhiteSpace(i.certification)).ToList();
			MovieDbProvider.Country country = source.FirstOrDefault((MovieDbProvider.Country c) => string.Equals(c.iso_3166_1, preferredCountryCode, StringComparison.OrdinalIgnoreCase));
			MovieDbProvider.Country country2 = source.FirstOrDefault((MovieDbProvider.Country c) => string.Equals(c.iso_3166_1, "US", StringComparison.OrdinalIgnoreCase));
			if (country != null)
			{
				((BaseItem)item).OfficialRating = country.GetRating();
			}
			else if (country2 != null)
			{
				((BaseItem)item).OfficialRating = country2.GetRating();
			}
		}
		if (!string.IsNullOrEmpty(movieData.release_date) && DateTimeOffset.TryParse(movieData.release_date, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result2))
		{
			((BaseItem)item).PremiereDate = result2.ToUniversalTime();
			((BaseItem)item).ProductionYear = ((BaseItem)item).PremiereDate.Value.Year;
		}
		if (movieData.production_companies != null)
		{
			((BaseItem)item).SetStudios(movieData.production_companies.Select((MovieDbProvider.ProductionCompany c) => c.name));
		}
		foreach (string item2 in (movieData.genres ?? new List<TmdbGenre>()).Select((TmdbGenre g) => g.name))
		{
			((BaseItem)item).AddGenre(item2);
		}
		((BaseMetadataResult)resultItem).ResetPeople();
		string imageUrl = settings.images.GetImageUrl("original");
		if (movieData.casts != null && movieData.casts.cast != null)
		{
			foreach (TmdbCast item3 in movieData.casts.cast.OrderBy((TmdbCast a) => a.order))
			{
				PersonInfo val2 = new PersonInfo
				{
					Name = item3.name.Trim(),
					Role = item3.character,
					Type = (PersonType)0
				};
				if (!string.IsNullOrWhiteSpace(item3.profile_path))
				{
					val2.ImageUrl = imageUrl + item3.profile_path;
				}
				if (item3.id > 0)
				{
					ProviderIdsExtensions.SetProviderId((IHasProviderIds)(object)val2, (MetadataProviders)3, item3.id.ToString(CultureInfo.InvariantCulture));
				}
				((BaseMetadataResult)resultItem).AddPerson(val2);
			}
		}
		if (movieData.casts == null || movieData.casts.crew == null)
		{
			return;
		}
		PersonType[] source2 = (PersonType[])(object)new PersonType[1] { (PersonType)1 };
		foreach (TmdbCrew item4 in movieData.casts.crew)
		{
			PersonType val3 = (PersonType)7;
			string department = item4.department;
			if (string.Equals(department, "writing", StringComparison.OrdinalIgnoreCase))
			{
				val3 = (PersonType)2;
			}
			if (Enum.TryParse<PersonType>(department, ignoreCase: true, out PersonType result3))
			{
				val3 = result3;
			}
			else if (Enum.TryParse<PersonType>(item4.job, ignoreCase: true, out result3))
			{
				val3 = result3;
			}
			if (source2.Contains(val3))
			{
				PersonInfo val4 = new PersonInfo
				{
					Name = item4.name.Trim(),
					Role = item4.job,
					Type = val3
				};
				if (!string.IsNullOrWhiteSpace(item4.profile_path))
				{
					val4.ImageUrl = imageUrl + item4.profile_path;
				}
				if (item4.id > 0)
				{
					ProviderIdsExtensions.SetProviderId((IHasProviderIds)(object)val4, (MetadataProviders)3, item4.id.ToString(CultureInfo.InvariantCulture));
				}
				((BaseMetadataResult)resultItem).AddPerson(val4);
			}
		}
	}
}
