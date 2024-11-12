using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Extensions;
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
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;

namespace MovieDb;

public class MovieDbPersonProvider : MovieDbProviderBase, IRemoteMetadataProviderWithOptions<Person, PersonLookupInfo>, IRemoteMetadataProvider<Person, PersonLookupInfo>, IMetadataProvider<Person>, IMetadataProvider, IRemoteMetadataProvider, IRemoteSearchProvider<PersonLookupInfo>, IRemoteSearchProvider
{
	public class PersonSearchResult
	{
		public bool Adult { get; set; }

		public int Id { get; set; }

		public string Name { get; set; }

		public string Profile_Path { get; set; }
	}

	public class PersonSearchResults
	{
		public int Page { get; set; }

		public List<PersonSearchResult> Results { get; set; }

		public int Total_Pages { get; set; }

		public int Total_Results { get; set; }

		public PersonSearchResults()
		{
			Results = new List<PersonSearchResult>();
		}
	}

	public class GeneralSearchResults
	{
		public List<PersonSearchResult> person_results { get; set; }

		public GeneralSearchResults()
		{
			person_results = new List<PersonSearchResult>();
		}
	}

	public class Images
	{
		public List<TmdbImage> profiles { get; set; }
	}

	public class PersonResult
	{
		public bool adult { get; set; }

		public List<object> also_known_as { get; set; }

		public string biography { get; set; }

		public string birthday { get; set; }

		public string deathday { get; set; }

		public string homepage { get; set; }

		public int id { get; set; }

		public string imdb_id { get; set; }

		public string name { get; set; }

		public string place_of_birth { get; set; }

		public double popularity { get; set; }

		public string profile_path { get; set; }

		public TmdbCredits credits { get; set; }

		public Images images { get; set; }

		public TmdbExternalIds external_ids { get; set; }
	}

	internal static MovieDbPersonProvider Current { get; private set; }

	public MovieDbPersonProvider(IJsonSerializer jsonSerializer, IHttpClient httpClient, IFileSystem fileSystem, IServerConfigurationManager configurationManager, ILogManager logManager, ILocalizationManager localization, IServerApplicationHost appHost, ILibraryManager libraryManager)
		: base(httpClient, configurationManager, jsonSerializer, fileSystem, localization, logManager, appHost, libraryManager)
	{
		Current = this;
	}

	// 定义 API 路径模板
	private const string PersonSearchPath = "3/search/person";
	private const string PersonFindPath = "3/find/{0}";
	private const string PersonMetadataPath = "3/person/{0}";
	private const string AppendToResponse = "credits,images,external_ids";

	public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(PersonLookupInfo searchInfo, CancellationToken cancellationToken)
	{
		string tmdbId = ProviderIdsExtensions.GetProviderId((IHasProviderIds)searchInfo, MetadataProviders.Tmdb);
		string tmdbImageUrl = (await GetTmdbSettings(cancellationToken).ConfigureAwait(false)).images.GetImageUrl("original");

		if (!string.IsNullOrEmpty(tmdbId))
		{
			DirectoryService directoryService = new DirectoryService(FileSystem);
			MetadataResult<Person> val = await GetMetadata(new RemoteMetadataFetchOptions<PersonLookupInfo>
			{
				SearchInfo = searchInfo,
				DirectoryService = (IDirectoryService)(object)directoryService
			}, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!((BaseMetadataResult)val).HasMetadata)
			{
				return new List<RemoteSearchResult>();
			}
			RemoteSearchResult result = ((BaseMetadataResult)val).ToRemoteSearchResult(base.Name);
			List<TmdbImage> images = ((await EnsurePersonInfo(tmdbId, null, (IDirectoryService)(object)directoryService, cancellationToken).ConfigureAwait(continueOnCapturedContext: false))?.images ?? new Images()).profiles ?? new List<TmdbImage>();
			await GetTmdbSettings(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			result.ImageUrl = ((images.Count == 0) ? null : (tmdbImageUrl + images[0].file_path));
			return (IEnumerable<RemoteSearchResult>)(object)new RemoteSearchResult[1] { result };
		}

		string providerId = ProviderIdsExtensions.GetProviderId((IHasProviderIds)searchInfo, MetadataProviders.Imdb);
		HttpResponseInfo response;

		if (!string.IsNullOrEmpty(providerId))
		{
			string path = string.Format(PersonFindPath, providerId);
			string apiUrl = GetApiUrl(path) + "&external_source=imdb_id";
			
			response = await GetMovieDbResponse(new HttpRequestOptions
			{
				Url = apiUrl,
				CancellationToken = cancellationToken,
				AcceptHeader = AcceptHeader
			}).ConfigureAwait(false);

			try
			{
				using (Stream stream = response.Content)
				{
					return (await JsonSerializer.DeserializeFromStreamAsync<GeneralSearchResults>(stream).ConfigureAwait(false) ?? new GeneralSearchResults())
						.person_results.Select(i => GetSearchResult(i, tmdbImageUrl));
				}
			}
			finally
			{
				((IDisposable)response)?.Dispose();
			}
		}

		if (searchInfo.IsAutomated)
		{
			return new List<RemoteSearchResult>();
		}

		string url = GetApiUrl(PersonSearchPath) + $"&query={WebUtility.UrlEncode(searchInfo.Name)}";
		
		response = await GetMovieDbResponse(new HttpRequestOptions
		{
			Url = url,
			CancellationToken = cancellationToken,
			AcceptHeader = AcceptHeader
		}).ConfigureAwait(false);

		try
		{
			using (Stream stream = response.Content)
			{
				return (await JsonSerializer.DeserializeFromStreamAsync<PersonSearchResults>(stream).ConfigureAwait(false) ?? new PersonSearchResults())
					.Results.Select(i => GetSearchResult(i, tmdbImageUrl));
			}
		}
		finally
		{
			((IDisposable)response)?.Dispose();
		}
	}

	private RemoteSearchResult GetSearchResult(PersonSearchResult i, string baseImageUrl)
	{

		RemoteSearchResult val = new RemoteSearchResult
		{
			SearchProviderName = base.Name,
			Name = i.Name,
			ImageUrl = string.IsNullOrEmpty(i.Profile_Path) ? null : (baseImageUrl + i.Profile_Path)
		};
		ProviderIdsExtensions.SetProviderId(val, (MetadataProviders)3, i.Id.ToString(CultureInfo.InvariantCulture));
		return val;
	}

	public async Task<MetadataResult<Person>> GetMetadata(RemoteMetadataFetchOptions<PersonLookupInfo> options, CancellationToken cancellationToken)
	{
		PersonLookupInfo id = options.SearchInfo;
		IDirectoryService directoryService = options.DirectoryService;
		string tmdbId = ProviderIdsExtensions.GetProviderId((IHasProviderIds)(object)id, (MetadataProviders)3);
		if (string.IsNullOrEmpty(tmdbId))
		{
			tmdbId = await GetTmdbId(id, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		MetadataResult<Person> result = new MetadataResult<Person>();
		string[] movieDbMetadataLanguages = GetMovieDbMetadataLanguages((ItemLookupInfo)(object)id, await GetTmdbLanguages(cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
		if (!string.IsNullOrEmpty(tmdbId))
		{
			bool isFirstLanguage = true;
			string[] array = movieDbMetadataLanguages;
			foreach (string language in array)
			{
				PersonResult personResult;
				try
				{
					personResult = await EnsurePersonInfo(tmdbId, language, directoryService, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				}
				catch (HttpException val)
				{
					HttpException val2 = val;
					if (val2.StatusCode.HasValue && val2.StatusCode.Value == HttpStatusCode.NotFound)
					{
						return result;
					}
					throw;
				}
				if (personResult != null)
				{
					((BaseMetadataResult)result).HasMetadata = true;
					if (result.Item == null)
					{
						result.Item = new Person();
					}
					ImportData(result.Item, personResult, isFirstLanguage);
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

	private bool IsComplete(Person item)
	{
		if (string.IsNullOrEmpty(item.Overview))
		{
			return false;
		}
		return true;
	}

	private void ImportData(Person item, PersonResult info, bool isFirstLanguage)
	{
		if (string.IsNullOrEmpty(((BaseItem)item).Name))
		{
			((BaseItem)item).Name = info.name;
		}
		if (string.IsNullOrEmpty(((BaseItem)item).Overview))
		{
			((BaseItem)item).Overview = info.biography;
		}
		if (isFirstLanguage)
		{
			if (!string.IsNullOrWhiteSpace(info.place_of_birth))
			{
				((BaseItem)item).ProductionLocations = new string[1] { info.place_of_birth };
			}
			if (DateTimeOffset.TryParseExact(info.birthday, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
			{
				((BaseItem)item).PremiereDate = result.ToUniversalTime();
			}
			if (DateTimeOffset.TryParseExact(info.deathday, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result2))
			{
				((BaseItem)item).EndDate = result2.ToUniversalTime();
			}
			ProviderIdsExtensions.SetProviderId((IHasProviderIds)(object)item, (MetadataProviders)3, info.id.ToString(CultureInfo.InvariantCulture));
			if (!string.IsNullOrEmpty(info.imdb_id))
			{
				ProviderIdsExtensions.SetProviderId((IHasProviderIds)(object)item, (MetadataProviders)2, info.imdb_id);
			}
		}
	}

	public Task<MetadataResult<Person>> GetMetadata(PersonLookupInfo id, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}

	private async Task<string> GetTmdbId(PersonLookupInfo info, CancellationToken cancellationToken)
	{
		return (await GetSearchResults(info, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).Select((RemoteSearchResult i) => ProviderIdsExtensions.GetProviderId((IHasProviderIds)(object)i, (MetadataProviders)3)).FirstOrDefault();
	}

	internal async Task<PersonResult> EnsurePersonInfo(string id, string language, IDirectoryService directoryService, CancellationToken cancellationToken)
	{
		string cacheKey = $"tmdb_person_{id}_{language}";
        PersonResult personResult;
        if (!directoryService.TryGetFromCache<PersonResult>(cacheKey, out personResult))
		{
			string dataFilePath = GetPersonDataFilePath((IApplicationPaths)(object)ConfigurationManager.ApplicationPaths, id, language);
			FileSystemMetadata fileSystemInfo = FileSystem.GetFileSystemInfo(dataFilePath);
			if (fileSystemInfo.Exists && DateTimeOffset.UtcNow - FileSystem.GetLastWriteTimeUtc(fileSystemInfo) <= MovieDbProviderBase.CacheTime)
			{
				personResult = await JsonSerializer.DeserializeFromFileAsync<PersonResult>(dataFilePath).ConfigureAwait(continueOnCapturedContext: false);
			}
			if (personResult == null)
			{
				FileSystem.CreateDirectory(FileSystem.GetDirectoryName(dataFilePath));
				personResult = await FetchPersonResult(id, language, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				using Stream stream = FileSystem.GetFileStream(dataFilePath, (FileOpenMode)2, (FileAccessMode)2, (FileShareMode)1, false);
				JsonSerializer.SerializeToStream((object)personResult, stream);
			}
			directoryService.AddOrUpdateCache(cacheKey, (object)personResult);
		}
		return personResult;
	}

	private string GetPersonMetadataUrl(string id)
	{
		string path = string.Format(PersonMetadataPath, id);
		return GetApiUrl(path) + $"&append_to_response={AppendToResponse}";
	}

	private async Task<PersonResult> FetchPersonResult(string id, string language, CancellationToken cancellationToken)
	{
		string url = GetPersonMetadataUrl(id);
		
		if (!string.IsNullOrEmpty(language))
		{
			url += $"&language={language}";
		}

		var response = await GetMovieDbResponse(new HttpRequestOptions
		{
			Url = url,
			CancellationToken = cancellationToken,
			AcceptHeader = AcceptHeader
		}).ConfigureAwait(false);

		try
		{
			using (Stream json = response.Content)
			{
				return await JsonSerializer.DeserializeFromStreamAsync<PersonResult>(json).ConfigureAwait(false);
			}
		}
		finally
		{
			((IDisposable)response)?.Dispose();
		}
	}

	private static string GetPersonDataPath(IApplicationPaths appPaths, string tmdbId)
	{
		string path = BaseExtensions.GetMD5(tmdbId).ToString().Substring(0, 1);
		return Path.Combine(GetPersonsDataPath(appPaths), path, tmdbId);
	}

	internal static string GetPersonDataFilePath(IApplicationPaths appPaths, string tmdbId, string language)
	{
		string text = "info";
		if (!string.IsNullOrEmpty(language))
		{
			text = text + "-" + language;
		}
		text += ".json";
		return Path.Combine(GetPersonDataPath(appPaths, tmdbId), text);
	}

	private static string GetPersonsDataPath(IApplicationPaths appPaths)
	{
		return Path.Combine(appPaths.CachePath, "tmdb-people");
	}

	// 添加 new 关键字来显式隐藏基类的 GetApiUrl 方法
	private new string GetApiUrl(string path)
	{
		var config = GetConfiguration();
		var baseUrl = config.TmdbApiBaseUrl.TrimEnd('/');
		return $"{baseUrl}/{path}?api_key={config.ApiKey}";
	}
}
