using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace MovieDb;

public class MovieDbPersonExternalId : IExternalId
{
	public string Name => Plugin.StaticName;

	public string Key => MetadataProviders.Tmdb.ToString();

	public string UrlFormatString
	{
		get
		{
			var config = Plugin.Instance?.Configuration;
			if (config != null)
			{
				return $"{config.TmdbHomeUrl}/person/{{0}}";
			}
			return "https://www.themoviedb.org/person/{0}";
		}
	}

	public bool Supports(IHasProviderIds item)
	{
		return item is Person;
	}
}
