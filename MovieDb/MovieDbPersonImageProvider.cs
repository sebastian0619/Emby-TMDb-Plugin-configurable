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

public class MovieDbPersonImageProvider : MovieDbProviderBase, IRemoteImageProviderWithOptions, IRemoteImageProvider, IImageProvider, IHasOrder
{
	public int Order => 0;

	public MovieDbPersonImageProvider(IHttpClient httpClient, IServerConfigurationManager configurationManager, IJsonSerializer jsonSerializer, IFileSystem fileSystem, ILocalizationManager localization, ILogManager logManager, IServerApplicationHost appHost, ILibraryManager libraryManager)
		: base(httpClient, configurationManager, jsonSerializer, fileSystem, localization, logManager, appHost, libraryManager)
	{
	}

	public bool Supports(BaseItem item)
	{
		return item is Person;
	}

	public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
	{
		return new List<ImageType> { (ImageType)0 };
	}

	public async Task<IEnumerable<RemoteImageInfo>> GetImages(RemoteImageFetchOptions options, CancellationToken cancellationToken)
	{
		BaseItem item = options.Item;
		_ = options.LibraryOptions;
		IDirectoryService directoryService = options.DirectoryService;
		string providerId = ProviderIdsExtensions.GetProviderId((IHasProviderIds)(Person)item, (MetadataProviders)3);
		if (!string.IsNullOrEmpty(providerId))
		{
			MovieDbPersonProvider.Images images = (await MovieDbPersonProvider.Current.EnsurePersonInfo(providerId, null, directoryService, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).images ?? new MovieDbPersonProvider.Images();
			TmdbSettingsResult tmdbSettingsResult = await GetTmdbSettings(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			string imageUrl = tmdbSettingsResult.images.GetImageUrl("original");
			return GetImages(images, tmdbSettingsResult, imageUrl);
		}
		return new List<RemoteImageInfo>();
	}

	public Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, LibraryOptions libraryOptions, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}

	private IEnumerable<RemoteImageInfo> GetImages(MovieDbPersonProvider.Images images, TmdbSettingsResult tmdbSettings, string baseImageUrl)
	{
		List<RemoteImageInfo> list = new List<RemoteImageInfo>();
		if (images.profiles != null)
		{
			list.AddRange(((IEnumerable<TmdbImage>)images.profiles).Select((Func<TmdbImage, RemoteImageInfo>)((TmdbImage i) => new RemoteImageInfo
			{
				Url = baseImageUrl + i.file_path,
				ThumbnailUrl = tmdbSettings.images.GetProfileThumbnailImageUrl(i.file_path),
				CommunityRating = i.vote_average,
				VoteCount = i.vote_count,
				Width = i.width,
				Height = i.height,
				ProviderName = base.Name,
				Type = (ImageType)0,
				RatingType = (RatingType)0
			})));
		}
		return list;
	}
}
