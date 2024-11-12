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
		string tmdbId = searchInfo.GetProviderId(MetadataProviders.Tmdb);
		TmdbSettingsResult tmdbSettings = null;
		if (!string.IsNullOrEmpty(tmdbId))
		{
			MetadataResult<BoxSet> val = await GetMetadata(searchInfo, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!val.HasMetadata)
			{
				return new List<RemoteSearchResult>();
			}
			RemoteSearchResult result = val.ToRemoteSearchResult(base.Name);
			List<TmdbImage> images = ((await EnsureInfo(tmdbId, null, cancellationToken).ConfigureAwait(continueOnCapturedContext: false))?.images ?? new TmdbImages()).posters ?? new List<TmdbImage>();
			string imageUrl = (await GetTmdbSettings(cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).images.GetImageUrl("original");
			result.ImageUrl = (images.Count == 0) ? null : (imageUrl + images[0].file_path);
			return new RemoteSearchResult[1] { result };
		}
		if (tmdbSettings == null)
		{
			tmdbSettings = await GetTmdbSettings(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		string[] movieDbMetadataLanguages = GetMovieDbMetadataLanguages(searchInfo, await GetTmdbLanguages(cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
		MovieDbSearch movieDbSearch = new MovieDbSearch(Logger, JsonSerializer, LibraryManager);
		return await movieDbSearch.GetCollectionSearchResults(searchInfo, movieDbMetadataLanguages, tmdbSettings, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
	}

	public async Task<MetadataResult<BoxSet>> GetMetadata(ItemLookupInfo id, CancellationToken cancellationToken)
	{
		string tmdbId = id.GetProviderId(MetadataProviders.Tmdb);
		string[] metadataLanguages = GetMovieDbMetadataLanguages(id, await GetTmdbLanguages(cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
		if (string.IsNullOrEmpty(tmdbId))
		{
			TmdbSettingsResult tmdbSettings = await GetTmdbSettings(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			MovieDbSearch movieDbSearch = new MovieDbSearch(Logger, JsonSerializer, LibraryManager);
			RemoteSearchResult val = (await movieDbSearch.GetCollectionSearchResults(id, metadataLanguages, tmdbSettings, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).FirstOrDefault();
			if (val != null)
			{
				tmdbId = val.GetProviderId(MetadataProviders.Tmdb);
			}
		}
		MetadataResult<BoxSet> result = new MetadataResult<BoxSet>();
		if (!string.IsNullOrEmpty(tmdbId))
		{
			string[] array = metadataLanguages;
			string[] array2 = array;
			foreach (string preferredMetadataLanguage in array2)
			{
				BoxSetRootObject boxSetRootObject = await GetMovieDbResult(tmdbId, preferredMetadataLanguage, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				if (boxSetRootObject != null)
				{
					result.HasMetadata = true;
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
		BoxSet val = new BoxSet
		{
			Name = obj.name,
			Overview = obj.overview
		};
		val.SetProviderId(MetadataProviders.Tmdb, obj.id.ToString(CultureInfo.InvariantCulture));
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
		JsonSerializer.SerializeToFile(boxSetRootObject, dataFilePath);
		return boxSetRootObject;
	}

	private async Task<BoxSetRootObject> FetchMainResult(string id, string metadataLanguage, CancellationToken cancellationToken)
	{
		PluginOptions config = Plugin.Instance.Configuration;
		string text = config.TmdbApiBaseUrl.TrimEnd(new char[1] { '/' }) + "/3/collection/" + id + "?api_key=" + config.ApiKey + "&append_to_response=images";
		if (!string.IsNullOrEmpty(metadataLanguage))
		{
			text = text + "&language=" + metadataLanguage;
		}
		text = AddImageLanguageParam(text, metadataLanguage);
		cancellationToken.ThrowIfCancellationRequested();
		using HttpResponseInfo response = await GetMovieDbResponse(new HttpRequestOptions
		{
			Url = text,
			CancellationToken = cancellationToken,
			AcceptHeader = MovieDbProviderBase.AcceptHeader
		}).ConfigureAwait(continueOnCapturedContext: false);
		using Stream json = response.Content;
		return await JsonSerializer.DeserializeFromStreamAsync<BoxSetRootObject>(json).ConfigureAwait(continueOnCapturedContext: false);
	}

	internal Task<BoxSetRootObject> EnsureInfo(string tmdbId, string preferredMetadataLanguage, CancellationToken cancellationToken)
	{
		string dataFilePath = GetDataFilePath(ConfigurationManager.ApplicationPaths, tmdbId, preferredMetadataLanguage);
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
