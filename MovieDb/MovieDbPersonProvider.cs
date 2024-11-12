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

	private const string PersonSearchPath = "3/search/person";

	private const string PersonFindPath = "3/find/{0}";

	private const string PersonMetadataPath = "3/person/{0}";

	private const string AppendToResponse = "credits,images,external_ids";

	internal static MovieDbPersonProvider Current { get; private set; }

	public MovieDbPersonProvider(IJsonSerializer jsonSerializer, IHttpClient httpClient, IFileSystem fileSystem, IServerConfigurationManager configurationManager, ILogManager logManager, ILocalizationManager localization, IServerApplicationHost appHost, ILibraryManager libraryManager)
		: base(httpClient, configurationManager, jsonSerializer, fileSystem, localization, logManager, appHost, libraryManager)
	{
		Current = this;
	}

	public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(PersonLookupInfo searchInfo, CancellationToken cancellationToken)
	{
		string tmdbId = searchInfo.GetProviderId(MetadataProviders.Tmdb);
		string tmdbImageUrl = (await GetTmdbSettings(cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).images.GetImageUrl("original");
		if (!string.IsNullOrEmpty(tmdbId))
		{
			DirectoryService directoryService = new DirectoryService(FileSystem);
			MetadataResult<Person> val = await GetMetadata(new RemoteMetadataFetchOptions<PersonLookupInfo>
			{
				SearchInfo = searchInfo,
				DirectoryService = directoryService
			}, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			if (!val.HasMetadata)
			{
				return new List<RemoteSearchResult>();
			}
			RemoteSearchResult result = val.ToRemoteSearchResult(base.Name);
			List<TmdbImage> images = ((await EnsurePersonInfo(tmdbId, null, directoryService, cancellationToken).ConfigureAwait(continueOnCapturedContext: false))?.images ?? new Images()).profiles ?? new List<TmdbImage>();
			await GetTmdbSettings(cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			result.ImageUrl = ((images.Count == 0) ? null : (tmdbImageUrl + images[0].file_path));
			return new RemoteSearchResult[1] { result };
		}
		string providerId = searchInfo.GetProviderId(MetadataProviders.Imdb);
		if (!string.IsNullOrEmpty(providerId))
		{
			string path = $"3/find/{providerId}";
			string apiUrl = GetApiUrl(path) + "&external_source=imdb_id";
			using HttpResponseInfo httpResponseInfo = await GetMovieDbResponse(new HttpRequestOptions
			{
				Url = apiUrl,
				CancellationToken = cancellationToken,
				AcceptHeader = MovieDbProviderBase.AcceptHeader
			}).ConfigureAwait(continueOnCapturedContext: false);
			using Stream stream2 = httpResponseInfo.Content;
			return ((await JsonSerializer.DeserializeFromStreamAsync<GeneralSearchResults>(stream2).ConfigureAwait(continueOnCapturedContext: false)) ?? new GeneralSearchResults()).person_results.Select((PersonSearchResult i) => GetSearchResult(i, tmdbImageUrl));
		}
		if (searchInfo.IsAutomated)
		{
			return new List<RemoteSearchResult>();
		}
		string url = GetApiUrl("3/search/person") + "&query=" + WebUtility.UrlEncode(searchInfo.Name);
		using HttpResponseInfo httpResponseInfo = await GetMovieDbResponse(new HttpRequestOptions
		{
			Url = url,
			CancellationToken = cancellationToken,
			AcceptHeader = MovieDbProviderBase.AcceptHeader
		}).ConfigureAwait(continueOnCapturedContext: false);
		using Stream stream = httpResponseInfo.Content;
		return ((await JsonSerializer.DeserializeFromStreamAsync<PersonSearchResults>(stream).ConfigureAwait(continueOnCapturedContext: false)) ?? new PersonSearchResults()).Results.Select((PersonSearchResult i) => GetSearchResult(i, tmdbImageUrl));
	}

	private RemoteSearchResult GetSearchResult(PersonSearchResult i, string baseImageUrl)
	{
		RemoteSearchResult val = new RemoteSearchResult
		{
			SearchProviderName = base.Name,
			Name = i.Name,
			ImageUrl = (string.IsNullOrEmpty(i.Profile_Path) ? null : (baseImageUrl + i.Profile_Path))
		};
		val.SetProviderId(MetadataProviders.Tmdb, i.Id.ToString(CultureInfo.InvariantCulture));
		return val;
	}

	public async Task<MetadataResult<Person>> GetMetadata(RemoteMetadataFetchOptions<PersonLookupInfo> options, CancellationToken cancellationToken)
	{
		PersonLookupInfo id = options.SearchInfo;
		IDirectoryService directoryService = options.DirectoryService;
		string tmdbId = id.GetProviderId(MetadataProviders.Tmdb);
		if (string.IsNullOrEmpty(tmdbId))
		{
			tmdbId = await GetTmdbId(id, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
		}
		MetadataResult<Person> result = new MetadataResult<Person>();
		ItemLookupInfo searchInfo = id;
		string[] movieDbMetadataLanguages = GetMovieDbMetadataLanguages(searchInfo, await GetTmdbLanguages(cancellationToken).ConfigureAwait(continueOnCapturedContext: false));
		if (!string.IsNullOrEmpty(tmdbId))
		{
			bool isFirstLanguage = true;
			string[] array = movieDbMetadataLanguages;
			string[] array2 = array;
			foreach (string language in array2)
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
					result.HasMetadata = true;
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
		if (string.IsNullOrEmpty(item.Name))
		{
			item.Name = info.name;
		}
		if (string.IsNullOrEmpty(item.Overview))
		{
			item.Overview = info.biography;
		}
		if (isFirstLanguage)
		{
			if (!string.IsNullOrWhiteSpace(info.place_of_birth))
			{
				item.ProductionLocations = new string[1] { info.place_of_birth };
			}
			if (DateTimeOffset.TryParseExact(info.birthday, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
			{
				item.PremiereDate = result.ToUniversalTime();
			}
			if (DateTimeOffset.TryParseExact(info.deathday, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var result2))
			{
				item.EndDate = result2.ToUniversalTime();
			}
			item.SetProviderId(MetadataProviders.Tmdb, info.id.ToString(CultureInfo.InvariantCulture));
			if (!string.IsNullOrEmpty(info.imdb_id))
			{
				item.SetProviderId(MetadataProviders.Imdb, info.imdb_id);
			}
		}
	}

	public Task<MetadataResult<Person>> GetMetadata(PersonLookupInfo id, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}

	private async Task<string> GetTmdbId(PersonLookupInfo info, CancellationToken cancellationToken)
	{
		return (await GetSearchResults(info, cancellationToken).ConfigureAwait(continueOnCapturedContext: false)).Select((RemoteSearchResult i) => i.GetProviderId(MetadataProviders.Tmdb)).FirstOrDefault();
	}

	internal async Task<PersonResult> EnsurePersonInfo(string id, string language, IDirectoryService directoryService, CancellationToken cancellationToken)
	{
		string cacheKey = "tmdb_person_" + id + "_" + language;
		PersonResult personResult = null;
		if (!directoryService.TryGetFromCache<PersonResult>(cacheKey, out personResult))
		{
			string dataFilePath = GetPersonDataFilePath(ConfigurationManager.ApplicationPaths, id, language);
			FileSystemMetadata fileSystemInfo = FileSystem.GetFileSystemInfo(dataFilePath);
			if (fileSystemInfo.Exists && DateTimeOffset.UtcNow - FileSystem.GetLastWriteTimeUtc(fileSystemInfo) <= MovieDbProviderBase.CacheTime)
			{
				personResult = await JsonSerializer.DeserializeFromFileAsync<PersonResult>(dataFilePath).ConfigureAwait(continueOnCapturedContext: false);
			}
			if (personResult == null)
			{
				FileSystem.CreateDirectory(FileSystem.GetDirectoryName(dataFilePath));
				personResult = await FetchPersonResult(id, language, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
				using Stream stream = FileSystem.GetFileStream(dataFilePath, FileOpenMode.Create, FileAccessMode.Write, FileShareMode.Read);
				JsonSerializer.SerializeToStream(personResult, stream);
			}
			directoryService.AddOrUpdateCache(cacheKey, personResult);
		}
		return personResult;
	}

	private string GetPersonMetadataUrl(string id)
	{
		string path = $"3/person/{id}";
		return GetApiUrl(path) + "&append_to_response=credits,images,external_ids";
	}

	private async Task<PersonResult> FetchPersonResult(string id, string language, CancellationToken cancellationToken)
	{
		string url = GetPersonMetadataUrl(id);
		if (!string.IsNullOrEmpty(language))
		{
			url = url + "&language=" + language;
		}
		using HttpResponseInfo response = await GetMovieDbResponse(new HttpRequestOptions
		{
			Url = url,
			CancellationToken = cancellationToken,
			AcceptHeader = MovieDbProviderBase.AcceptHeader
		}).ConfigureAwait(continueOnCapturedContext: false);
		using Stream json = response.Content;
		return await JsonSerializer.DeserializeFromStreamAsync<PersonResult>(json).ConfigureAwait(continueOnCapturedContext: false);
	}

	private static string GetPersonDataPath(IApplicationPaths appPaths, string tmdbId)
	{
		string path = tmdbId.GetMD5().ToString().Substring(0, 1);
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

	private new string GetApiUrl(string path)
	{
		PluginOptions config = GetConfiguration();
		string baseUrl = config.TmdbApiBaseUrl.TrimEnd(new char[1] { '/' });
		return baseUrl + "/" + path + "?api_key=" + config.ApiKey;
	}
}
