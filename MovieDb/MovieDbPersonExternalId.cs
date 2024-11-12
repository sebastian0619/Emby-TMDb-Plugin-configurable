using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace MovieDb;

public class MovieDbPersonExternalId : IExternalId
{
	public string Name => Plugin.StaticName;

	public string Key => MetadataProviders.Tmdb.ToString();

	public string UrlFormatString => "https://tmdbhome.kingscross.online:8333/person/{0}";

	public bool Supports(IHasProviderIds item)
	{
		return item is Person;
	}
}
