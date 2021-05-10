using System;
using System.Text.Json.Serialization;
using VTuberNotifier.Watcher.Store;

namespace VTuberNotifier.Watcher.Event
{
    public abstract class NewProductEvent<T> : EventBase<T> where T : ProductBase
    {
        public NewProductEvent(string evt_name, T value) : base(evt_name, value) { }
        protected private NewProductEvent(string evt_name, T value, DateTime dt)
            : base(evt_name, value, dt) { }

        [JsonIgnore]
        public override string FormatContent
            => "新しい商品ページが公開されました\n{Title}\n{Date}\n{URL}\n参加ライバー : {Livers: / }\n{ItemsNP:\\n}";
    }
    public abstract class StartSellEvent<T> : EventBase<T> where T : ProductBase
    {
        public StartSellEvent(string evt_name, T value) : base(evt_name, value) { }
        protected private StartSellEvent(string evt_name, T value, DateTime dt)
            : base(evt_name, value, dt) { }

        [JsonIgnore]
        public override string FormatContent
            => "商品が販売開始されました\n{Title}\n{Date}\n{URL}\n参加ライバー : {Livers: / }\n{ItemsNP:\\n}";
    }

    [JsonConverter(typeof(BoothNewProductEventConverter))]
    public class BoothNewProductEvent : NewProductEvent<BoothProduct>
    {
        public BoothNewProductEvent(BoothProduct value) : base(nameof(BoothNewProductEvent), value) { }
        protected private BoothNewProductEvent(BoothProduct value, DateTime dt)
             : base(nameof(BoothNewProductEvent), value, dt) { }

        public class BoothNewProductEventConverter : EventConverter
        {
            private protected override EventBase<BoothProduct> ResultEvent(BoothProduct value, DateTime dt)
                => new BoothNewProductEvent(value, dt);
        }
    }
    [JsonConverter(typeof(BoothStartSellEventConverter))]
    public class BoothStartSellEvent : StartSellEvent<BoothProduct>
    {
        public BoothStartSellEvent(BoothProduct value) : base(nameof(BoothStartSellEvent), value) { }
        protected private BoothStartSellEvent(BoothProduct value, DateTime dt)
             : base(nameof(BoothStartSellEvent), value, dt) { }

        public class BoothStartSellEventConverter : EventConverter
        {
            private protected override EventBase<BoothProduct> ResultEvent(BoothProduct value, DateTime dt)
                => new BoothStartSellEvent(value, dt);
        }
    }

    [JsonConverter(typeof(NijisanjiNewProductEventConverter))]
    public class NijisanjiNewProductEvent : NewProductEvent<NijisanjiProduct>
    {
        public NijisanjiNewProductEvent(NijisanjiProduct value) : base(nameof(NijisanjiNewProductEvent), value) { }
        protected private NijisanjiNewProductEvent(NijisanjiProduct value, DateTime dt)
             : base(nameof(NijisanjiNewProductEvent), value, dt) { }

        public class NijisanjiNewProductEventConverter : EventConverter
        {
            private protected override EventBase<NijisanjiProduct> ResultEvent(NijisanjiProduct value, DateTime dt)
                => new NijisanjiNewProductEvent(value, dt);
        }
    }
    [JsonConverter(typeof(NijisanjiStartSellEventConverter))]
    public class NijisanjiStartSellEvent : StartSellEvent<NijisanjiProduct>
    {
        public NijisanjiStartSellEvent(NijisanjiProduct value) : base(nameof(NijisanjiStartSellEvent), value) { }
        protected private NijisanjiStartSellEvent(NijisanjiProduct value, DateTime dt)
             : base(nameof(NijisanjiStartSellEvent), value, dt) { }

        public class NijisanjiStartSellEventConverter : EventConverter
        {
            private protected override EventBase<NijisanjiProduct> ResultEvent(NijisanjiProduct value, DateTime dt)
                => new NijisanjiStartSellEvent(value, dt);
        }
    }

    [JsonConverter(typeof(DotliveNewProductEventConverter))]
    public class DotliveNewProductEvent : NewProductEvent<DotliveProduct>
    {
        public DotliveNewProductEvent(DotliveProduct value) : base(nameof(DotliveNewProductEvent), value) { }
        protected private DotliveNewProductEvent(DotliveProduct value, DateTime dt)
             : base(nameof(DotliveNewProductEvent), value, dt) { }

        public class DotliveNewProductEventConverter : EventConverter
        {
            private protected override EventBase<DotliveProduct> ResultEvent(DotliveProduct value, DateTime dt)
                => new DotliveNewProductEvent(value, dt);
        }
    }
    [JsonConverter(typeof(DotliveStartSellEventConverter))]
    public class DotliveStartSellEvent : StartSellEvent<DotliveProduct>
    {
        public DotliveStartSellEvent(DotliveProduct value) : base(nameof(DotliveStartSellEvent), value) { }
        protected private DotliveStartSellEvent(DotliveProduct value, DateTime dt)
             : base(nameof(DotliveStartSellEvent), value, dt) { }

        public class DotliveStartSellEventConverter : EventConverter
        {
            private protected override EventBase<DotliveProduct> ResultEvent(DotliveProduct value, DateTime dt)
                => new DotliveStartSellEvent(value, dt);
        }
    }
}
