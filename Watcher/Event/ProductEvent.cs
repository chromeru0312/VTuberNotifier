using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VTuberNotifier.Discord;
using VTuberNotifier.Liver;
using VTuberNotifier.Watcher.Store;

namespace VTuberNotifier.Watcher.Event
{
    public class BoothNewProductEvent : EventBase<BoothProduct>
    {
        public BoothNewProductEvent(BoothProduct value) : base(value, new(value.Livers)) { }

        public override string GetDiscordContent(LiverDetail liver)
        {
            var format = "新しい商品ページが公開されました\n{Title}\n{Date}\n{URL}\n参加ライバー:{Livers: / }\n{ItemsNP:\\n}";
            return ConvertContent(format, liver);
        }
    }
    public class BoothStartSellEvent : EventBase<BoothProduct>
    {
        public BoothStartSellEvent(BoothProduct value) : base(value, new(value.Livers)) { }

        public override string GetDiscordContent(LiverDetail liver)
        {
            var format = "商品が販売開始されました\n{Title}\n{Date}\n{URL}\n参加ライバー:{Livers: / }\n{ItemsNP:\\n}";
            return ConvertContent(format, liver);
        }
    }

    public class NijisanjiNewProductEvent : EventBase<NijisanjiProduct>
    {
        public NijisanjiNewProductEvent(NijisanjiProduct value) : base(value, new(value.Livers)) { }

        public override string GetDiscordContent(LiverDetail liver)
        {
            var format = "新しい商品ページが公開されました\n{Title}\n{Date}\n{URL}\n参加ライバー:{Livers: / }\n{ItemsNP:\\n}";
            return ConvertContent(format, liver);
        }
    }
    public class NijisanjiStartSellEvent : EventBase<NijisanjiProduct>
    {
        public NijisanjiStartSellEvent(NijisanjiProduct value) : base(value, new(value.Livers)) { }

        public override string GetDiscordContent(LiverDetail liver)
        {
            var format = "商品が販売開始されました\n{Title}\n{Date}\n{URL}\n参加ライバー:{Livers: / }\n{ItemsNP:\\n}";
            return ConvertContent(format, liver);
        }
    }
}
