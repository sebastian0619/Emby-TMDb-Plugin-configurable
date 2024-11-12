using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace MovieDb;

public class MovieDbCollectionExternalId : IExternalId
{
	public string Name => Plugin.StaticName;

	public string Key
	{
		get
		{
			return MetadataProviders.Tmdb.ToString();
		}
	}

	public string UrlFormatString => $"{Plugin.Instance.Configuration.TmdbHomeUrl}/collection/{{0}}";

	public bool Supports(IHasProviderIds item)
	{
		return item is BoxSet;
	}
}
