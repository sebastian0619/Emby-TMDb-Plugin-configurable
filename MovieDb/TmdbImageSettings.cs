using System.Collections.Generic;
using System.Linq;

namespace MovieDb;

public class TmdbImageSettings
{
	public string base_url { get; set; }

	public List<string> backdrop_sizes { get; set; }

	public string secure_base_url { get; set; }

	public List<string> poster_sizes { get; set; }

	public List<string> profile_sizes { get; set; }

	public List<string> logo_sizes { get; set; }

	public List<string> still_sizes { get; set; }

	public string GetImageUrl(string image)
	{
		return secure_base_url + image;
	}

	public string GetOriginalImageUrl(string image)
	{
		return GetImageUrl("original") + image;
	}

	public string GetPosterThumbnailImageUrl(string image)
	{
		if (poster_sizes != null)
		{
			string text = poster_sizes.ElementAtOrDefault(1) ?? poster_sizes.FirstOrDefault();
			if (!string.IsNullOrEmpty(text))
			{
				return GetImageUrl(text) + image;
			}
		}
		return GetOriginalImageUrl(image);
	}

	public string GetStillThumbnailImageUrl(string image)
	{
		return GetOriginalImageUrl(image);
	}

	public string GetProfileThumbnailImageUrl(string image)
	{
		if (profile_sizes != null)
		{
			string text = profile_sizes.ElementAtOrDefault(1) ?? profile_sizes.FirstOrDefault();
			if (!string.IsNullOrEmpty(text))
			{
				return GetImageUrl(text) + image;
			}
		}
		return GetOriginalImageUrl(image);
	}

	public string GetLogoThumbnailImageUrl(string image)
	{
		if (logo_sizes != null)
		{
			string text = logo_sizes.ElementAtOrDefault(3) ?? logo_sizes.ElementAtOrDefault(2) ?? logo_sizes.ElementAtOrDefault(1) ?? logo_sizes.LastOrDefault();
			if (!string.IsNullOrEmpty(text))
			{
				return GetImageUrl(text) + image;
			}
		}
		return GetOriginalImageUrl(image);
	}

	public string GetBackdropThumbnailImageUrl(string image)
	{
		if (backdrop_sizes != null)
		{
			string text = backdrop_sizes.FirstOrDefault();
			if (!string.IsNullOrEmpty(text))
			{
				return GetImageUrl(text) + image;
			}
		}
		return GetOriginalImageUrl(image);
	}
}
