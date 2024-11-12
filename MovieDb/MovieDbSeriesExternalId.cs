using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace MovieDb;

public class MovieDbSeriesExternalId : IExternalId
{
	public string Name => Plugin.StaticName;

	public string Key => MetadataProviders.Tmdb.ToString();

	public string UrlFormatString => "https://tmdbhome.kingscross.online:8333/tv/{0}";

	public bool Supports(IHasProviderIds item)
	{
		return item is Series;
	}
}
