using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
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

internal class MovieDbBoxSetImageProvider : MovieDbProviderBase, IRemoteImageProviderWithOptions, IRemoteImageProvider, IImageProvider, IHasOrder
{
	public int Order => 0;

	public MovieDbBoxSetImageProvider(IHttpClient httpClient, IServerConfigurationManager configurationManager, IJsonSerializer jsonSerializer, IFileSystem fileSystem, ILocalizationManager localization, ILogManager logManager, IServerApplicationHost appHost, ILibraryManager libraryManager)
		: base(httpClient, configurationManager, jsonSerializer, fileSystem, localization, logManager, appHost, libraryManager)
	{
	}

	public bool Supports(BaseItem item)
	{
		return item is BoxSet;
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
		string providerId = options.Item.GetProviderId(MetadataProviders.Tmdb);
		if (!string.IsNullOrEmpty(providerId))
		{
			MovieDbBoxSetProvider.BoxSetRootObject mainResult = await MovieDbBoxSetProvider.Current.GetMovieDbResult(providerId, null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (mainResult != null)
			{
				TmdbSettingsResult tmdbSettingsResult = await GetTmdbSettings(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				string imageUrl = tmdbSettingsResult.images.GetImageUrl("original");
				return GetImages(mainResult, tmdbSettingsResult, imageUrl);
			}
		}
		return new List<RemoteImageInfo>();
	}

	public Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, LibraryOptions libraryOptions, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}

	private IEnumerable<RemoteImageInfo> GetImages(MovieDbBoxSetProvider.BoxSetRootObject obj, TmdbSettingsResult tmdbSettings, string baseUrl)
	{
		List<RemoteImageInfo> list = new List<RemoteImageInfo>();
		TmdbImages images = obj.images ?? new TmdbImages();
		list.AddRange(from i in GetPosters(images)
			select new RemoteImageInfo
			{
				Url = baseUrl + i.file_path,
				ThumbnailUrl = tmdbSettings.images.GetPosterThumbnailImageUrl(i.file_path),
				CommunityRating = i.vote_average,
				VoteCount = i.vote_count,
				Width = i.width,
				Height = i.height,
				Language = MovieDbImageProvider.NormalizeImageLanguage(i.iso_639_1),
				ProviderName = base.Name,
				Type = ImageType.Primary,
				RatingType = RatingType.Score
			});
		list.AddRange(from i in GetLogos(images)
			select new RemoteImageInfo
			{
				Url = baseUrl + i.file_path,
				ThumbnailUrl = tmdbSettings.images.GetLogoThumbnailImageUrl(i.file_path),
				CommunityRating = i.vote_average,
				VoteCount = i.vote_count,
				Width = i.width,
				Height = i.height,
				Language = MovieDbImageProvider.NormalizeImageLanguage(i.iso_639_1),
				ProviderName = base.Name,
				Type = ImageType.Logo,
				RatingType = RatingType.Score
			});
		list.AddRange(from i in GetBackdrops(images)
			where string.IsNullOrEmpty(i.iso_639_1)
			select new RemoteImageInfo
			{
				Url = baseUrl + i.file_path,
				ThumbnailUrl = tmdbSettings.images.GetBackdropThumbnailImageUrl(i.file_path),
				CommunityRating = i.vote_average,
				VoteCount = i.vote_count,
				Width = i.width,
				Height = i.height,
				ProviderName = base.Name,
				Type = ImageType.Backdrop,
				RatingType = RatingType.Score
			});
		return list;
	}
}
