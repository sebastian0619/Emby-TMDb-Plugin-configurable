using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Configuration;
using MediaBrowser.Model.Providers;

namespace MovieDb;

public class MovieDbMusicVideoProvider : IRemoteMetadataProvider<MusicVideo, MusicVideoInfo>, IMetadataProvider<MusicVideo>, IMetadataProvider, IRemoteMetadataProvider, IRemoteSearchProvider<MusicVideoInfo>, IRemoteSearchProvider, IHasMetadataFeatures
{
	public string Name => MovieDbProvider.Current.Name;

	public MetadataFeatures[] Features => (MetadataFeatures[])(object)new MetadataFeatures[2]
	{
		(MetadataFeatures)2,
		(MetadataFeatures)1
	};

	public Task<MetadataResult<MusicVideo>> GetMetadata(MusicVideoInfo info, CancellationToken cancellationToken)
	{
		return MovieDbProvider.Current.GetItemMetadata<MusicVideo>((ItemLookupInfo)(object)info, cancellationToken);
	}

	public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MusicVideoInfo searchInfo, CancellationToken cancellationToken)
	{
		return MovieDbProvider.Current.GetMovieSearchResults((ItemLookupInfo)(object)searchInfo, cancellationToken);
	}

	public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
	{
		throw new NotImplementedException();
	}
}
