using System.Collections.Generic;

namespace MovieDb;

public class TmdbCredits
{
	public List<TmdbCast> cast { get; set; }

	public List<TmdbCrew> crew { get; set; }

	public List<MovieDbProviderBase.GuestStar> guest_stars { get; set; }
}
