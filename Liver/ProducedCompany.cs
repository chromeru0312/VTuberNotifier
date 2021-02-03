using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace VTuberNotifier.Liver
{
    public static class ProducedCompany
    {
        public static CompanyDetail Ichikara { get; }
            = new(30865, "いちから株式会社", "https://www.ichikara.co.jp/", "Ichikara_Inc");
        public static CompanyDetail Cover { get; }
            = new(30268, "カバー株式会社", "https://cover-corp.com/", "cover_corp");
        public static CompanyDetail AppLand { get; }
            = new(13275, "株式会社アップランド", "https://www.appland.co.jp/");
        public static CompanyDetail BitStar { get; }
            = new(15232, "株式会社BitStar", "https://bitstar.tokyo/", "bitstar_tokyo", "UCtb5Mtc8-bKH9mcnCD7wMkA");
    }

    public class CompanyDetail : Address
    {
        public string HomePage { get; }

        public CompanyDetail(int id, string name, string hp, string twitter = null, string youtube = null)
            : base(id, name, youtube, twitter)
        {
            HomePage = hp;
        }
    }
}
