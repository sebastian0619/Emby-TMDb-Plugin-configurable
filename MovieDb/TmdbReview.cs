using System;

namespace MovieDb;

public class TmdbReview
{
	public string author { get; set; }

	public string content { get; set; }

	public DateTime created_at { get; set; }

	public string id { get; set; }

	public DateTime updated_at { get; set; }

	public string url { get; set; }
}
