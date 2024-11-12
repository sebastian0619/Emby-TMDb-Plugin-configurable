using System;
using System.Collections.Generic;
using System.Linq;
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
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;

namespace MovieDb;

public class MovieDbSeriesImageProvider : MovieDbProviderBase, IRemoteImageProviderWithOptions, IRemoteImageProvider, IImageProvider, IHasOrder
{
	public int Order => 2;

	public MovieDbSeriesImageProvider(IHttpClient httpClient, IServerConfigurationManager configurationManager, IJsonSerializer jsonSerializer, IFileSystem fileSystem, ILocalizationManager localization, ILogManager logManager, IServerApplicationHost appHost, ILibraryManager libraryManager)
		: base(httpClient, configurationManager, jsonSerializer, fileSystem, localization, logManager, appHost, libraryManager)
	{
	}

	public bool Supports(BaseItem item)
	{
		return item is Series;
	}

	public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
	{
		return new List<ImageType>
		{
			(ImageType)0,
			(ImageType)2,
			(ImageType)4
		};
	}

	public async Task<IEnumerable<RemoteImageInfo>> GetImages(RemoteImageFetchOptions options, CancellationToken cancellationToken)
	{
		List<RemoteImageInfo> list = new List<RemoteImageInfo>();
		BaseItem item = options.Item;
		TmdbImages results = await FetchImages(item, JsonSerializer, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (results == null)
		{
			return list;
		}
		TmdbSettingsResult tmdbSettings = await GetTmdbSettings(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		string tmdbImageUrl = tmdbSettings.images.GetImageUrl("original");
		list.AddRange(((IEnumerable<TmdbImage>)GetPosters(results)).Select((Func<TmdbImage, RemoteImageInfo>)((TmdbImage i) => new RemoteImageInfo
		{
			Url = tmdbImageUrl + i.file_path,
			ThumbnailUrl = tmdbSettings.images.GetPosterThumbnailImageUrl(i.file_path),
			CommunityRating = i.vote_average,
			VoteCount = i.vote_count,
			Width = i.width,
			Height = i.height,
			Language = MovieDbImageProvider.NormalizeImageLanguage(i.iso_639_1),
			ProviderName = base.Name,
			Type = (ImageType)0,
			RatingType = (RatingType)0
		})));
		list.AddRange(((IEnumerable<TmdbImage>)GetLogos(results)).Select((Func<TmdbImage, RemoteImageInfo>)((TmdbImage i) => new RemoteImageInfo
		{
			Url = tmdbImageUrl + i.file_path,
			ThumbnailUrl = tmdbSettings.images.GetLogoThumbnailImageUrl(i.file_path),
			CommunityRating = i.vote_average,
			VoteCount = i.vote_count,
			Width = i.width,
			Height = i.height,
			Language = MovieDbImageProvider.NormalizeImageLanguage(i.iso_639_1),
			ProviderName = base.Name,
			Type = (ImageType)4,
			RatingType = (RatingType)0
		})));
		IEnumerable<RemoteImageInfo> collection = (from i in GetBackdrops(results)
			where string.IsNullOrEmpty(i.iso_639_1)
			select i).Select((Func<TmdbImage, RemoteImageInfo>)((TmdbImage i) => new RemoteImageInfo
		{
			Url = tmdbImageUrl + i.file_path,
			ThumbnailUrl = tmdbSettings.images.GetBackdropThumbnailImageUrl(i.file_path),
			CommunityRating = i.vote_average,
			VoteCount = i.vote_count,
			Width = i.width,
			Height = i.height,
			ProviderName = base.Name,
			Type = (ImageType)2,
			RatingType = (RatingType)0
		}));
		list.AddRange(collection);
		return list;
	}

	public Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, LibraryOptions libraryOptions, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}

	private async Task<TmdbImages> FetchImages(BaseItem item, IJsonSerializer jsonSerializer, CancellationToken cancellationToken)
	{
		string providerId = ProviderIdsExtensions.GetProviderId((IHasProviderIds)(object)item, (MetadataProviders)3);
		if (string.IsNullOrEmpty(providerId))
		{
			return null;
		}
		return (await MovieDbSeriesProvider.Current.EnsureSeriesInfo(providerId, null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false))?.images;
	}
}