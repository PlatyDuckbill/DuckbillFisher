using System.Collections.Frozen;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace DuckbillFisher.Plugin;

internal enum FishDeliveryKind { Appraiser, CustomDelivery }
internal enum FishDeliveryDataState { Unavailable, Ready }
internal sealed record FishDeliveryView(FishDeliveryKind Kind, uint TargetId, string TargetName, int Low, int Mid, int High);
internal sealed record FishDeliveryCatalog(FishDeliveryDataState State, IReadOnlyDictionary<uint, FishDeliveryView[]> ByItem)
{
    public static readonly FishDeliveryCatalog Unavailable = new(FishDeliveryDataState.Unavailable, FrozenDictionary<uint, FishDeliveryView[]>.Empty);
}

/// <summary>Client-language static delivery facts, loaded once on the catalog's Framework update.</summary>
internal static class FishDeliveryData
{
    // KR/global: RewardType 1 is the scrip appraiser; type 2 also contains unrelated tool-material exchanges.
    private const byte AppraiserRewardType = 1;

    public static FishDeliveryCatalog Load(IDataManager data, IReadOnlySet<uint> fishItems)
    {
        try
        {
            var shops = data.GetExcelSheet<CollectablesShop>() ?? throw new InvalidDataException("DeliveryShopsMissing");
            var npcs = data.GetExcelSheet<SatisfactionNpc>() ?? throw new InvalidDataException("DeliveryNpcsMissing");
            var supplySheet = data.GetSubrowExcelSheet<SatisfactionSupply>() ?? throw new InvalidDataException("DeliverySuppliesMissing");
            var supplies = supplySheet.SelectMany(group => group).Where(row => row.RowId != 0 && row.Item.RowId != 0).ToLookup(row => row.RowId);
            var entries = new List<(uint Item, FishDeliveryView Delivery)>();
            foreach (var shop in shops.Where(shop => shop.RewardType == AppraiserRewardType))
                foreach (var reference in shop.ShopItems.Where(reference => reference.RowId != 0))
                    foreach (var item in reference.Value)
                    {
                        if (item.Item.RowId == 0 || !fishItems.Contains(item.Item.RowId)) continue;
                        var cuts = item.CollectablesShopRefine.Value;
                        if (!Valid(cuts.LowCollectability, cuts.MidCollectability, cuts.HighCollectability)) continue;
                        entries.Add((item.Item.RowId, new(FishDeliveryKind.Appraiser, shop.RowId, "",
                            cuts.LowCollectability, cuts.MidCollectability, cuts.HighCollectability)));
                    }
            foreach (var npc in npcs.Where(npc => npc.RowId != 0 && npc.Npc.RowId != 0))
            {
                var name = npc.Npc.Value.Singular.ExtractText();
                if (string.IsNullOrWhiteSpace(name)) continue;
                foreach (var index in npc.SatisfactionNpcParams.Select(rank => rank.SupplyIndex).Where(index => index > 0).Distinct())
                    foreach (var item in supplies[(uint)index])
                    {
                        if (!fishItems.Contains(item.Item.RowId) || !Valid(item.CollectabilityLow, item.CollectabilityMid, item.CollectabilityHigh)) continue;
                        entries.Add((item.Item.RowId, new(FishDeliveryKind.CustomDelivery, npc.RowId, name,
                            item.CollectabilityLow, item.CollectabilityMid, item.CollectabilityHigh)));
                    }
            }
            return new(FishDeliveryDataState.Ready, entries.Distinct().GroupBy(entry => entry.Item)
                .ToFrozenDictionary(group => group.Key, group => group.Select(entry => entry.Delivery)
                    .OrderBy(entry => entry.Kind).ThenBy(entry => entry.TargetId).ThenBy(entry => entry.Low).ToArray()));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException and not StackOverflowException)
        {
            // A missing/changed optional sheet must not disable the catalog or imply there are no deliveries.
            return FishDeliveryCatalog.Unavailable;
        }
    }

    private static bool Valid(int low, int mid, int high) => low > 0 && mid >= low && high >= mid;
}
