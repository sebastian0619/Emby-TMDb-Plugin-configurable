using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;

namespace MovieDb;

public class MovieDbBoxSetProvider : MovieDbProviderBase, IRemoteMetadataProvider<BoxSet, ItemLookupInfo>, IMetadataProvider<BoxSet>, IMetadataProvider, IRemoteMetadataProvider, IRemoteSearchProvider<ItemLookupInfo>, IRemoteSearchProvider
{
	internal class Part
	{
		public string title { get; set; }

		public int id { get; set; }

		public string release_date { get; set; }

		public string poster_path { get; set; }

		public string backdrop_path { get; set; }
	}

	internal class BoxSetRootObject
	{
		public int id { get; set; }

		public string name { get; set; }

		public string overview { get; set; }

		public string poster_path { get; set; }

		public string backdrop_path { get; set; }

		public List<Part> parts { get; set; }

		public TmdbImages images { get; set; }
	}

	internal static MovieDbBoxSetProvider Current;

	public MovieDbBoxSetProvider(IHttpClient httpClient, IServerConfigurationManager configurationManager, IJsonSerializer jsonSerializer, IFileSystem fileSystem, ILocalizationManager localization, ILogManager logManager, IServerApplicationHost appHost, ILibraryManager libraryManager)
		: base(httpClient, configurationManager, jsonSerializer, fileSystem, localization, logManager, appHost, libraryManager)
	{
		Current = this;
	}

	public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(ItemLookupInfo searchInfo, CancellationToken cancellationToken)
	{
		string tmdbId = ProviderIdsExtensions.GetProviderId((IHasProviderIds)(object)searchInfo, (MetadataProviders)3);
		TmdbSettingsResult tmdbSettings = null;
		if (!string.IsNullOrEmpty(tmdbId))
		{
			MetadataResult<BoxSet> val = await GetMetadata(searchInfo, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!((BaseMetadataResult)val).HasMetadata)
			{
				return new List<RemoteSearchResult>();
			}
			RemoteSearchResult result = ((BaseMetadataResult)val).ToRemoteSearchResult(base.Name);
			List<TmdbImage> images = ((await EnsureInfo(tmdbId, null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false))?.images ?? new TmdbImages()).posters ?? new List<TmdbImage>();
			string imageUrl = (await GetTmdbSettings(cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).images.GetImageUrl("original");
			result.ImageUrl = ((images.Count == 0) ? null : (imageUrl + images[0].file_path));
			return (IEnumerable<RemoteSearchResult>)(object)new RemoteSearchResult[1] { result };
		}
		if (tmdbSettings == null)
		{
			tmdbSettings = await GetTmdbSettings(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		string[] movieDbMetadataLanguages = GetMovieDbMetadataLanguages(searchInfo, await GetTmdbLanguages(cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
		var movieDbSearch = new MovieDbSearch(Logger, JsonSerializer, LibraryManager);
		return await movieDbSearch.GetCollectionSearchResults(searchInfo, movieDbMetadataLanguages, tmdbSettings, cancellationToken)
			.ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task<MetadataResult<BoxSet>> GetMetadata(ItemLookupInfo id, CancellationToken cancellationToken)
	{
		string tmdbId = ProviderIdsExtensions.GetProviderId((IHasProviderIds)(object)id, (MetadataProviders)3);
		string[] metadataLanguages = GetMovieDbMetadataLanguages(id, await GetTmdbLanguages(cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
		if (string.IsNullOrEmpty(tmdbId))
		{
			TmdbSettingsResult tmdbSettings = await GetTmdbSettings(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			var movieDbSearch = new MovieDbSearch(Logger, JsonSerializer, LibraryManager);
			RemoteSearchResult val = (await movieDbSearch.GetCollectionSearchResults(id, metadataLanguages, tmdbSettings, cancellationToken)
				.ConfigureAwait(continueOnCapturedContext: false)).FirstOrDefault();
			if (val != null)
			{
				tmdbId = ProviderIdsExtensions.GetProviderId((IHasProviderIds)(object)val, (MetadataProviders)3);
			}
		}
		MetadataResult<BoxSet> result = new MetadataResult<BoxSet>();
		if (!string.IsNullOrEmpty(tmdbId))
		{
			string[] array = metadataLanguages;
			foreach (string preferredMetadataLanguage in array)
			{
				BoxSetRootObject boxSetRootObject = await GetMovieDbResult(tmdbId, preferredMetadataLanguage, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (boxSetRootObject != null)
				{
					((BaseMetadataResult)result).HasMetadata = true;
					if (result.Item == null)
					{
						result.Item = GetItem(boxSetRootObject);
					}
					return result;
				}
			}
		}
		return result;
	}

	internal Task<BoxSetRootObject> GetMovieDbResult(string tmdbId, string preferredMetadataLanguage, CancellationToken cancellationToken)
	{
		if (string.IsNullOrEmpty(tmdbId))
		{
			throw new ArgumentNullException("tmdbId");
		}
		return EnsureInfo(tmdbId, preferredMetadataLanguage, cancellationToken);
	}

	private BoxSet GetItem(BoxSetRootObject obj)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0037: Expected O, but got Unknown
		//IL_0038: Expected O, but got Unknown
		BoxSet val = new BoxSet
		{
			Name = obj.name,
			Overview = obj.overview
		};
		ProviderIdsExtensions.SetProviderId((IHasProviderIds)val, (MetadataProviders)3, obj.id.ToString(CultureInfo.InvariantCulture));
		return val;
	}

	private async Task<BoxSetRootObject> DownloadInfo(string tmdbId, string preferredMetadataLanguage, string dataFilePath, CancellationToken cancellationToken)
	{
		BoxSetRootObject boxSetRootObject = await FetchMainResult(tmdbId, preferredMetadataLanguage, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		if (boxSetRootObject == null)
		{
			return boxSetRootObject;
		}
		FileSystem.CreateDirectory(FileSystem.GetDirectoryName(dataFilePath));
		JsonSerializer.SerializeToFile((object)boxSetRootObject, dataFilePath);
		return boxSetRootObject;
	}

	private async Task<BoxSetRootObject> FetchMainResult(string id, string metadataLanguage, CancellationToken cancellationToken)
	{
		var config = Plugin.Instance!.Configuration;
		string text = $"{config.TmdbApiBaseUrl.TrimEnd('/')}/3/collection/{id}?api_key={config.ApiKey}&append_to_response=images";
		
		if (!string.IsNullOrEmpty(metadataLanguage))
		{
			text += $"&language={metadataLanguage}";
		}
		
		text = AddImageLanguageParam(text, metadataLanguage);
		
		cancellationToken.ThrowIfCancellationRequested();
		
		HttpResponseInfo response = await GetMovieDbResponse(new HttpRequestOptions
		{
			Url = text,
			CancellationToken = cancellationToken,
			AcceptHeader = AcceptHeader
		}).ConfigureAwait(continueOnCapturedContext: false);
		
		try
		{
			using Stream json = response.Content;
			return await JsonSerializer.DeserializeFromStreamAsync<BoxSetRootObject>(json)
				.ConfigureAwait(continueOnCapturedContext: false);
		}
		finally
		{
			((IDisposable)response)?.Dispose();
		}
	}

	internal Task<BoxSetRootObject> EnsureInfo(string tmdbId, string preferredMetadataLanguage, CancellationToken cancellationToken)
	{
		string dataFilePath = GetDataFilePath((IApplicationPaths)(object)ConfigurationManager.ApplicationPaths, tmdbId, preferredMetadataLanguage);
		FileSystemMetadata fileSystemInfo = FileSystem.GetFileSystemInfo(dataFilePath);
		if (fileSystemInfo.Exists && DateTimeOffset.UtcNow - FileSystem.GetLastWriteTimeUtc(fileSystemInfo) <= MovieDbProviderBase.CacheTime)
		{
			return JsonSerializer.DeserializeFromFileAsync<BoxSetRootObject>(dataFilePath);
		}
		return DownloadInfo(tmdbId, preferredMetadataLanguage, dataFilePath, cancellationToken);
	}

	private static string GetDataFilePath(IApplicationPaths appPaths, string tmdbId, string preferredLanguage)
	{
		string dataPath = GetDataPath(appPaths, tmdbId);
		string text = "all";
		if (!string.IsNullOrEmpty(preferredLanguage))
		{
			text = text + "-" + preferredLanguage;
		}
		text += ".json";
		return Path.Combine(dataPath, text);
	}

	private static string GetDataPath(IApplicationPaths appPaths, string tmdbId)
	{
		return Path.Combine(GetCollectionsDataPath(appPaths), tmdbId);
	}

	private static string GetCollectionsDataPath(IApplicationPaths appPaths)
	{
		return Path.Combine(appPaths.CachePath, "tmdb-collections");
	}
}
