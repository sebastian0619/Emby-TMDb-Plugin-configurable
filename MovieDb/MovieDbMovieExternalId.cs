using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.LiveTv;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace MovieDb;

public class MovieDbMovieExternalId : IExternalId
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
				return $"{config.TmdbHomeUrl}/movie/{{0}}";
			}
			return "https://www.themoviedb.org/movie/{0}";
		}
	}

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
