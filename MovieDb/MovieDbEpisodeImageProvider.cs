using System;
using System.Collections.Generic;
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
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;

namespace MovieDb;

public class MovieDbEpisodeImageProvider : MovieDbProviderBase, IRemoteImageProviderWithOptions, IRemoteImageProvider, IImageProvider, IHasOrder
{
	public int Order => 1;

	public MovieDbEpisodeImageProvider(IHttpClient httpClient, IServerConfigurationManager configurationManager, IJsonSerializer jsonSerializer, IFileSystem fileSystem, ILocalizationManager localization, ILogManager logManager, IServerApplicationHost appHost, ILibraryManager libraryManager)
		: base(httpClient, configurationManager, jsonSerializer, fileSystem, localization, logManager, appHost, libraryManager)
	{
	}

	public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
	{
		return new List<ImageType> { ImageType.Primary };
	}

	public async Task<IEnumerable<RemoteImageInfo>> GetImages(RemoteImageFetchOptions options, CancellationToken cancellationToken)
	{
		BaseItem item = options.Item;
		_ = options.LibraryOptions;
		Episode val = (Episode)item;
		Series series = val.Series;
		string text = ((series != null) ? series.GetProviderId(MetadataProviders.Tmdb) : null);
		List<RemoteImageInfo> list = new List<RemoteImageInfo>();
		if (string.IsNullOrEmpty(text))
		{
			return list;
		}
		int? parentIndexNumber = val.ParentIndexNumber;
		int? indexNumber = val.IndexNumber;
		if (!parentIndexNumber.HasValue || !indexNumber.HasValue)
		{
			return list;
		}
		try
		{
			RootObject response = await GetEpisodeInfo(text, parentIndexNumber.Value, indexNumber.Value, null, options.DirectoryService, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			TmdbSettingsResult tmdbSettings;
			try
			{
				tmdbSettings = await GetTmdbSettings(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (tmdbSettings?.images == null)
				{
					Logger.Error("TMDB settings or images configuration is null");
					return list;
				}
			}
			catch (Exception ex2)
			{
				Exception ex = ex2;
				Logger.Error("Error getting TMDB settings: {0}", ex);
				return list;
			}
			string tmdbImageUrl = tmdbSettings.images.GetImageUrl("original");
			if (string.IsNullOrEmpty(tmdbImageUrl))
			{
				Logger.Error("TMDB image URL is empty");
				return list;
			}
			list.AddRange(from i in GetPosters(response.images)
				select new RemoteImageInfo
				{
					Url = tmdbImageUrl + i.file_path,
					ThumbnailUrl = tmdbSettings.images.GetBackdropThumbnailImageUrl(i.file_path),
					CommunityRating = i.vote_average,
					VoteCount = i.vote_count,
					Width = i.width,
					Height = i.height,
					Language = MovieDbImageProvider.NormalizeImageLanguage(i.iso_639_1),
					ProviderName = base.Name,
					Type = ImageType.Primary,
					RatingType = RatingType.Score
				});
			return list;
		}
		catch (HttpException val3)
		{
			if (val3.StatusCode.HasValue && val3.StatusCode.Value == HttpStatusCode.NotFound)
			{
				return list;
			}
			throw;
		}
		catch (Exception ex)
		{
			Logger.Error("Unexpected error in GetImages: {0}", ex);
			throw;
		}
	}

	public Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, LibraryOptions libraryOptions, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}

	protected override List<TmdbImage> GetPosters(TmdbImages images)
	{
		return images?.stills ?? new List<TmdbImage>();
	}

	public bool Supports(BaseItem item)
	{
		return item is Episode;
	}
}
