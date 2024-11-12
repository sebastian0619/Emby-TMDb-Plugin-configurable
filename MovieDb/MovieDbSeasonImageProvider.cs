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
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;

namespace MovieDb;

public class MovieDbSeasonImageProvider : MovieDbProviderBase, IRemoteImageProviderWithOptions, IRemoteImageProvider, IImageProvider, IHasOrder
{
	private readonly MovieDbSeasonProvider _seasonProvider;

	public int Order => 2;

	public MovieDbSeasonImageProvider(IHttpClient httpClient, IServerConfigurationManager configurationManager, IJsonSerializer jsonSerializer, IFileSystem fileSystem, ILocalizationManager localization, ILogManager logManager, IServerApplicationHost appHost, ILibraryManager libraryManager)
		: base(httpClient, configurationManager, jsonSerializer, fileSystem, localization, logManager, appHost, libraryManager)
	{
		_seasonProvider = new MovieDbSeasonProvider(jsonSerializer, httpClient, fileSystem, configurationManager, logManager, localization, appHost, libraryManager);
	}

	public bool Supports(BaseItem item)
	{
		return item is Season;
	}

	public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
	{
		return new List<ImageType> { ImageType.Primary };
	}

	public async Task<IEnumerable<RemoteImageInfo>> GetImages(RemoteImageFetchOptions options, CancellationToken cancellationToken)
	{
		BaseItem item = options.Item;
		List<RemoteImageInfo> list = new List<RemoteImageInfo>();
		Season val = (Season)item;
		ProviderIdDictionary providerIds = val.Series.ProviderIds;
		providerIds.TryGetValue(MetadataProviders.Tmdb.ToString(), out var value);
		if (!string.IsNullOrWhiteSpace(value) && val.IndexNumber.HasValue)
		{
			try
			{
				MovieDbSeasonProvider.SeasonRootObject seasonInfo = await _seasonProvider.EnsureSeasonInfo(value, val.IndexNumber.Value, null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				TmdbSettingsResult tmdbSettings = await GetTmdbSettings(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				string tmdbImageUrl = tmdbSettings.images.GetImageUrl("original");
				list.AddRange(from i in GetPosters(seasonInfo?.images)
					select new RemoteImageInfo
					{
						Url = tmdbImageUrl + i.file_path,
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
			}
			catch (HttpException)
			{
			}
		}
		return list;
	}

	public Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, LibraryOptions libraryOptions, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}
}
