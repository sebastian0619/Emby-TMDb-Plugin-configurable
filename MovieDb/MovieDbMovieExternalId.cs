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

	public string Key
	{
		get
		{
			//IL_0001: Unknown result type (might be due to invalid IL or missing references)
			return MetadataProviders.Tmdb.ToString();
		}
	}

	public string UrlFormatString => "https://tmdbhome.kingscross.online:8333/movie/{0}";

	public bool Supports(IHasProviderIds item)
	{
		LiveTvProgram val = (LiveTvProgram)(object)((item is LiveTvProgram) ? item : null);
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
