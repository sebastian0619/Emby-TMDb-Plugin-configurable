using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace MovieDb;

public class MovieDbMovieExternalId : IExternalId
{
	public const string BaseMovieDbUrl = "https://tmdbhome.kingscross.online:8333/";

	public string Name => Plugin.StaticName;

	public string Key => MetadataProviders.Tmdb.ToString();

	public string UrlFormatString => "https://tmdbhome.kingscross.online:8333/movie/{0}";

	public bool Supports(IHasProviderIds item)
	{
		LiveTvProgram val = (LiveTvProgram)((item is LiveTvProgram) ? item : null);
		if (val != null && val.IsMovie)
		{
			return true;
		}
		if (!(item is Movie) && !(item is MusicVideo))
		{
			return item is Trailer;
		}
		return true;
	}
}
