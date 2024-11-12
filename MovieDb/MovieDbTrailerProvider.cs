using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Globalization;
using MediaBrowser.Model.IO;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;

namespace MovieDb;

public class MovieDbTrailerProvider : MovieDbProviderBase, IHasOrder, IRemoteMetadataProvider<Trailer, TrailerInfo>, IMetadataProvider<Trailer>, IMetadataProvider, IRemoteMetadataProvider, IRemoteSearchProvider<TrailerInfo>, IRemoteSearchProvider, IHasMetadataFeatures
{
	public int Order => 0;

	public MetadataFeatures[] Features => (MetadataFeatures[])(object)new MetadataFeatures[2]
	{
		(MetadataFeatures)2,
		(MetadataFeatures)1
	};

	public MovieDbTrailerProvider(IHttpClient httpClient, IServerConfigurationManager configurationManager, IJsonSerializer jsonSerializer, IFileSystem fileSystem, ILocalizationManager localization, ILogManager logManager, IServerApplicationHost appHost, ILibraryManager libraryManager)
		: base(httpClient, configurationManager, jsonSerializer, fileSystem, localization, logManager, appHost, libraryManager)
	{
	}

	public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(TrailerInfo searchInfo, CancellationToken cancellationToken)
	{
		return MovieDbProvider.Current.GetMovieSearchResults((ItemLookupInfo)(object)searchInfo, cancellationToken);
	}

	public Task<MetadataResult<Trailer>> GetMetadata(TrailerInfo info, CancellationToken cancellationToken)
	{
		return MovieDbProvider.Current.GetItemMetadata<Trailer>((ItemLookupInfo)(object)info, cancellationToken);
	}
}
