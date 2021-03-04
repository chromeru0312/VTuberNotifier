using System;
using System.Text.Json.Serialization;
using VTuberNotifier.Liver;
using VTuberNotifier.Watcher.Store;

namespace VTuberNotifier.Watcher.Event
{
    [JsonConverter(typeof(BoothNewProductEventConverter))]
    public class BoothNewProductEvent : EventBase<BoothProduct>
    {
        public BoothNewProductEvent(BoothProduct value) : base(nameof(BoothNewProductEvent), value) { }
        protected private BoothNewProductEvent(BoothProduct value, DateTime dt)
             : base(nameof(BoothNewProductEvent), value, dt) { }

        public override string GetDiscordContent(LiverDetail liver)
        {
            var format = "新しい商品ページが公開されました\n{Title}\n{Date}\n{URL}\n参加ライバー:{Livers: / }\n{ItemsNP:\\n}";
            return ConvertContent(format, liver);
        }

        public class BoothNewProductEventConverter : EventConverter
        {
            private protected override EventBase<BoothProduct> ResultEvent(BoothProduct value, DateTime dt)
                => new BoothNewProductEvent(value, dt);
        }
    }
    [JsonConverter(typeof(BoothStartSellEventConverter))]
    public class BoothStartSellEvent : EventBase<BoothProduct>
    {
        public BoothStartSellEvent(BoothProduct value) : base(nameof(BoothStartSellEvent), value) { }
        protected private BoothStartSellEvent(BoothProduct value, DateTime dt)
             : base(nameof(BoothStartSellEvent), value, dt) { }

        public override string GetDiscordContent(LiverDetail liver)
        {
            var format = "商品が販売開始されました\n{Title}\n{Date}\n{URL}\n参加ライバー:{Livers: / }\n{ItemsNP:\\n}";
            return ConvertContent(format, liver);
        }

        public class BoothStartSellEventConverter : EventConverter
        {
            private protected override EventBase<BoothProduct> ResultEvent(BoothProduct value, DateTime dt)
                => new BoothStartSellEvent(value, dt);
        }
    }

    [JsonConverter(typeof(NijisanjiNewProductEventConverter))]
    public class NijisanjiNewProductEvent : EventBase<NijisanjiProduct>
    {
        public NijisanjiNewProductEvent(NijisanjiProduct value) : base(nameof(NijisanjiNewProductEvent), value) { }
        protected private NijisanjiNewProductEvent(NijisanjiProduct value, DateTime dt)
             : base(nameof(NijisanjiNewProductEvent), value, dt) { }

        public override string GetDiscordContent(LiverDetail liver)
        {
            var format = "新しい商品ページが公開されました\n{Title}\n{Date}\n{URL}\n参加ライバー:{Livers: / }\n{ItemsNP:\\n}";
            return ConvertContent(format, liver);
        }

        public class NijisanjiNewProductEventConverter : EventConverter
        {
            private protected override EventBase<NijisanjiProduct> ResultEvent(NijisanjiProduct value, DateTime dt)
                => new NijisanjiNewProductEvent(value, dt);
        }
    }
    [JsonConverter(typeof(NijisanjiStartSellEventConverter))]
    public class NijisanjiStartSellEvent : EventBase<NijisanjiProduct>
    {
        public NijisanjiStartSellEvent(NijisanjiProduct value) : base(nameof(NijisanjiStartSellEvent), value) { }
        protected private NijisanjiStartSellEvent(NijisanjiProduct value, DateTime dt)
             : base(nameof(NijisanjiStartSellEvent), value, dt) { }

        public override string GetDiscordContent(LiverDetail liver)
        {
            var format = "商品が販売開始されました\n{Title}\n{Date}\n{URL}\n参加ライバー:{Livers: / }\n{ItemsNP:\\n}";
            return ConvertContent(format, liver);
        }

        public class NijisanjiStartSellEventConverter : EventConverter
        {
            private protected override EventBase<NijisanjiProduct> ResultEvent(NijisanjiProduct value, DateTime dt)
                => new NijisanjiStartSellEvent(value, dt);
        }
    }
}
