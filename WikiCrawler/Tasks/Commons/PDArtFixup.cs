using MediaWiki;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Tasks
{
	public class PdArtFixup : ReplaceInCategory
	{
		public static BaseReplacement CreateReplacement()
		{
			return new CompoundReplacementTask(new ImplicitCreatorsReplacement("PdArtReplacement"), new LocalizeDateReplacement(), new PdArtReplacement(), new FixInformationTemplates());
		}

		public PdArtFixup()
			: base(CreateReplacement())
		{

		}

		public override string GetCategory()
		{
			//return "Category:PD-Art (PD-old default)";
			return "Category:PD-Art (PD-old-70)";
		}

		public override IEnumerable<Article> GetPagesToAffectUncached(string startSortkey)
		{
			if (PdArtReplacement.SkipCached)
			{
				return base.GetPagesToAffectUncached(startSortkey)
					.Where(article => !File.Exists(PdArtReplacement.GetCachePath(PageTitle.Parse(article.title))));
			}
			else
			{
				return base.GetPagesToAffectUncached(startSortkey);
			}
		}
	}
}
