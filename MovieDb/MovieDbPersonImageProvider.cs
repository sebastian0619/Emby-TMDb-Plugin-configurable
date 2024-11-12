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
		IDirectoryService directoryService = options.DirectoryService;
		string providerId = ProviderIdsExtensions.GetProviderId((IHasProviderIds)(Person)item, (MetadataProviders)3);
		
		if (!string.IsNullOrEmpty(providerId))
		{
			var images = (await MovieDbPersonProvider.Current.EnsurePersonInfo(providerId, null, directoryService, cancellationToken)
				.ConfigureAwait(continueOnCapturedContext: false))?.images ?? new MovieDbPersonProvider.Images();
			
			var config = Plugin.Instance.Configuration;
			string imageUrl = config.GetImageUrl("original");
			
			return GetImages(images, imageUrl);
		}
		
		return new List<RemoteImageInfo>();
	}

	public Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, LibraryOptions libraryOptions, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}

	private IEnumerable<RemoteImageInfo> GetImages(MovieDbPersonProvider.Images images, string baseImageUrl)
	{
		List<RemoteImageInfo> list = new List<RemoteImageInfo>();
		
		if (images.profiles != null)
		{
			list.AddRange(images.profiles.Select(i => new RemoteImageInfo
			{
				Url = baseImageUrl + i.file_path,
				ThumbnailUrl = Plugin.Instance.Configuration.GetImageUrl("w500") + i.file_path,
				CommunityRating = i.vote_average,
				VoteCount = i.vote_count,
				Width = i.width,
				Height = i.height,
				ProviderName = Name,
				Type = ImageType.Primary,
				RatingType = RatingType.Score
			}));
		}
		
		return list;
	}
}