namespace GearAndLootValue
{
    internal static class TarkovMoney
    {
        internal const string RoubleTpl = "5449016a4bdc2d6f028b456f";
        internal const string DollarTpl = "5696686a4bdc2da3298b456a";
        internal const string EuroTpl = "569668774bdc2da2298b4568";

        // Rough fallback rates if trader data has not loaded yet.
        internal const int DefaultDollarRate = 130;
        internal const int DefaultEuroRate = 145;
        internal const int MinUsefulRigPrice = 5000;
    }
}