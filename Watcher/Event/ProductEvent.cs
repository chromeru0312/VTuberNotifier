using System;
using System.Text.Json.Serialization;
using VTuberNotifier.Watcher.Store;

namespace VTuberNotifier.Watcher.Event
{
    public abstract class NewProductEvent<T> : EventBase<T> where T : ProductBase
    {
        public NewProductEvent(T value) : this(value, DateTime.Now) { }
        protected private NewProductEvent(T value, DateTime dt) : base(value, null, dt) { }

        [JsonIgnore]
        public override string FormatContent
            => "新しい商品ページが公開されました\n{Title}\n{Date}\n{URL}\n参加ライバー : {Livers: / }\n{ItemsNP:\\n}";
    }
    public abstract class StartSellEvent<T> : EventBase<T> where T : ProductBase
    {
        public StartSellEvent(T value) : this(value, DateTime.Now) { }
        protected private StartSellEvent(T value, DateTime dt) : base(value, null, dt) { }

        [JsonIgnore]
        public override string FormatContent
            => "商品が販売開始されました\n{Title}\n{Date}\n{URL}\n参加ライバー : {Livers: / }\n{ItemsNP:\\n}";
    }

    public class BoothNewProductEvent : NewProductEvent<BoothProduct>
    {
        public override string EventTypeName => "BoothNewProduct";
        public BoothNewProductEvent(BoothProduct value) : base(value) { }
        protected private BoothNewProductEvent(BoothProduct value, DateTime dt) : base(value, dt) { }
    }
    public class BoothStartSellEvent : StartSellEvent<BoothProduct>
    {
        public override string EventTypeName => "BoothStartSell";
        public BoothStartSellEvent(BoothProduct value) : base(value) { }
        protected private BoothStartSellEvent(BoothProduct value, DateTime dt) : base(value, dt) { }
    }

    public class NijisanjiNewProductEvent : NewProductEvent<NijisanjiProduct>
    {
        public override string EventTypeName => "NijisanjiNewProduct";
        public NijisanjiNewProductEvent(NijisanjiProduct value) : base(value) { }
        protected private NijisanjiNewProductEvent(NijisanjiProduct value, DateTime dt) : base(value, dt) { }
    }
    public class NijisanjiStartSellEvent : StartSellEvent<NijisanjiProduct>
    {
        public override string EventTypeName => "NijisanjiStartSell";
        public NijisanjiStartSellEvent(NijisanjiProduct value) : base(value) { }
        protected private NijisanjiStartSellEvent(NijisanjiProduct value, DateTime dt) : base(value, dt) { }
    }
    public class DotliveNewProductEvent : NewProductEvent<DotliveProduct>
    {
        public override string EventTypeName => "DotliveNewProduct";
        public DotliveNewProductEvent(DotliveProduct value) : base(value) { }
        protected private DotliveNewProductEvent(DotliveProduct value, DateTime dt) : base(value, dt) { }
    }
    public class DotliveStartSellEvent : StartSellEvent<DotliveProduct>
    {
        public override string EventTypeName => "DotliveStartSell";
        public DotliveStartSellEvent(DotliveProduct value) : base(value) { }
        protected private DotliveStartSellEvent(DotliveProduct value, DateTime dt) : base(value, dt) { }
    }
}