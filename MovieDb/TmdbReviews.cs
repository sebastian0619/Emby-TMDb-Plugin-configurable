using System.Collections.Generic;

namespace MovieDb;

public class TmdbReviews
{
	public int page { get; set; }

	public List<TmdbReview> results { get; set; }

	public int total_pages { get; set; }

	public int total_results { get; set; }
}
