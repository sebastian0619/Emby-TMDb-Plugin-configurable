using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;

namespace MovieDb;

internal class MovieDbImageProvider : MovieDbProviderBase, IRemoteImageProviderWithOptions, IRemoteImageProvider, IImageProvider, IHasOrder
{
	public int Order => 0;

	public MovieDbImageProvider(IHttpClient httpClient, IServerConfigurationManager configurationManager, IJsonSerializer jsonSerializer, IFileSystem fileSystem, ILocalizationManager localization, ILogManager logManager, IServerApplicationHost appHost, ILibraryManager libraryManager)
		: base(httpClient, configurationManager, jsonSerializer, fileSystem, localization, logManager, appHost, libraryManager)
	{
	}

	public bool Supports(BaseItem item)
	{
		if (!(item is Movie) && !(item is MusicVideo))
		{
			return item is Trailer;
		}
		return true;
	}

	public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
	{
		return new List<ImageType>
		{
			ImageType.Primary,
			ImageType.Backdrop,
			ImageType.Logo
		};
	}

	public async Task<IEnumerable<RemoteImageInfo>> GetImages(RemoteImageFetchOptions options, CancellationToken cancellationToken)
	{
		BaseItem item = options.Item;
		List<RemoteImageInfo> list = new List<RemoteImageInfo>();
		MovieDbProvider.CompleteMovieData movieInfo = await GetMovieInfo(item, null, JsonSerializer, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		TmdbImages results = movieInfo?.images;
		TmdbSettingsResult tmdbSettings = await GetTmdbSettings(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		string tmdbImageUrl = tmdbSettings.images.GetImageUrl("original");
		List<ImageType> list2 = GetSupportedImages(item).ToList();
		if (results != null)
		{
			if (list2.Contains(ImageType.Primary))
			{
				list.AddRange(from i in GetPosters(results)
					select new RemoteImageInfo
					{
						Url = tmdbImageUrl + i.file_path,
						ThumbnailUrl = tmdbSettings.images.GetPosterThumbnailImageUrl(i.file_path),
						CommunityRating = i.vote_average,
						VoteCount = i.vote_count,
						Width = i.width,
						Height = i.height,
						Language = NormalizeImageLanguage(i.iso_639_1),
						ProviderName = base.Name,
						Type = ImageType.Primary,
						RatingType = RatingType.Score
					});
			}
			if (list2.Contains(ImageType.Logo))
			{
				list.AddRange(from i in GetLogos(results)
					select new RemoteImageInfo
					{
						Url = tmdbImageUrl + i.file_path,
						ThumbnailUrl = tmdbSettings.images.GetLogoThumbnailImageUrl(i.file_path),
						CommunityRating = i.vote_average,
						VoteCount = i.vote_count,
						Width = i.width,
						Height = i.height,
						Language = NormalizeImageLanguage(i.iso_639_1),
						ProviderName = base.Name,
						Type = ImageType.Logo,
						RatingType = RatingType.Score
					});
			}
			if (list2.Contains(ImageType.Backdrop))
			{
				list.AddRange(from i in GetBackdrops(results)
					where string.IsNullOrEmpty(i.iso_639_1)
					select new RemoteImageInfo
					{
						Url = tmdbImageUrl + i.file_path,
						ThumbnailUrl = tmdbSettings.images.GetBackdropThumbnailImageUrl(i.file_path),
						CommunityRating = i.vote_average,
						VoteCount = i.vote_count,
						Width = i.width,
						Height = i.height,
						ProviderName = base.Name,
						Type = ImageType.Backdrop,
						RatingType = RatingType.Score
					});
			}
		}
		if (list2.Contains(ImageType.Primary))
		{
			string text = movieInfo?.poster_path;
			if (!string.IsNullOrWhiteSpace(text))
			{
				list.Add(new RemoteImageInfo
				{
					ProviderName = base.Name,
					Type = ImageType.Primary,
					Url = tmdbImageUrl + text
				});
			}
		}
		return list;
	}

	public Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, LibraryOptions libraryOptions, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}

	public static string NormalizeImageLanguage(string lang)
	{
		if (string.Equals(lang, "xx", StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		return lang;
	}

	private async Task<MovieDbProvider.CompleteMovieData> GetMovieInfo(BaseItem item, string language, IJsonSerializer jsonSerializer, CancellationToken cancellationToken)
	{
		string providerId = item.GetProviderId(MetadataProviders.Tmdb);
		MovieDbProvider.CompleteMovieData completeMovieData;
		if (string.IsNullOrWhiteSpace(providerId))
		{
			string providerId2 = item.GetProviderId(MetadataProviders.Imdb);
			if (!string.IsNullOrWhiteSpace(providerId2))
			{
				completeMovieData = await MovieDbProvider.Current.FetchMainResult(providerId2, isTmdbId: false, language, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (completeMovieData != null)
				{
					return completeMovieData;
				}
			}
			return null;
		}
		completeMovieData = await MovieDbProvider.Current.EnsureMovieInfo(providerId, language, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (completeMovieData != null)
		{
			return completeMovieData;
		}
		return null;
	}
}
