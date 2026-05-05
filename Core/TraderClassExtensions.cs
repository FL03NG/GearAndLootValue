using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using SPT.Reflection.Utils;
using System.Reflection;

namespace GearAndLootValue
{
    internal static class TraderClassExtensions
    {
        internal static ISession _session;

        internal static ISession Session
        {
            get
            {
                if (_session == null)
                {
                    object app = ClientAppUtils.GetMainApp();

                    if (app == null)
                    {
                        return null;
                    }

                    _session = ClientAppUtils.GetMainApp().GetClientBackEndSession();
                }

                return _session;
            }
        }

        private static readonly FieldInfo SupplyDataField =
            typeof(TraderClass).GetField("SupplyData_0", BindingFlags.Public | BindingFlags.Instance);

        public static SupplyData TryGetSupplyData(this TraderClass trader)
        {
            if (SupplyDataField == null || trader == null)
            {
                return null;
            }

            return SupplyDataField.GetValue(trader) as SupplyData;
        }

        public static void SetSupplyDataIfPossible(this TraderClass trader, SupplyData supplyData)
        {
            if (SupplyDataField == null || trader == null)
            {
                return;
            }

            SupplyDataField.SetValue(trader, supplyData);
        }

        public static async void RefreshTraderSupplyData(this TraderClass trader)
        {
            if (trader == null)
            {
                return;
            }

            if (Session == null)
            {
                return;
            }

            Result<SupplyData> result = await Session.GetSupplyData(trader.Id);

            if (result.Succeed)
            {
                trader.SetSupplyDataIfPossible(result.Value);
            }
        }
    }
}