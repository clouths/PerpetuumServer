using Perpetuum;
using Perpetuum.Builders;
using Perpetuum.Data;
using Perpetuum.Services.Looting;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Perpetuum.Services.Relics
{

    public class RelicLootGenerator
    {
        private readonly RelicRepository _relicRepository;
        private Random _random;

        public RelicLootGenerator(RelicRepository relicRepository)
        {
            _relicRepository = relicRepository;
            _random = new Random();
        }

        //Guard loot-loop from empty or low probability loot tables (would indicate bad/missing Relicloot entries)
        private bool HasValidLoots(IRelicLoot[] loots)
        {
            return loots.Length > 0 && (0.1 < loots.Sum(loot => loot.Chance)); 
        }

        public RelicLootItems GenerateLoot(Relic relic)
        {
            var relicInfo = relic.GetRelicInfo();

            var result = new List<LootItem>();

            var loots = _relicRepository.GetRelicLoots(relicInfo).ToArray();

            if (!HasValidLoots(loots))
                return null;

            do
            {
                foreach (var loot in loots)
                {
                    var chance = _random.NextDouble();
                    if (chance > loot.Chance)
                        continue;

                    var builder = loot.GetLootItemBuilder();
                    var lootItem = builder.Build();
                    result.Add(lootItem);
                }

            } while (result.Count < 1);

            return new RelicLootItems(relic.GetPosition(), result);
        }
    }
}

public class RelicLootItems
{
    public Position Position { get; private set; }
    public IEnumerable<LootItem> LootItems { get; private set; }

    public RelicLootItems(Position position, IEnumerable<LootItem> lootItems)
    {
        Position = position;
        LootItems = lootItems;
    }
}


public interface IRelicLoot
{
    double Chance { get; }
    IBuilder<LootItem> GetLootItemBuilder();
}

/// <summary>
/// Describes one loot item can be found in a discovered relic
/// </summary>
public class RelicLoot : IRelicLoot
{
    private int Definition { get; set; }
    private IntRange Quantity { get; set; }
    public double Chance { get; private set; }

    public IBuilder<LootItem> GetLootItemBuilder()
    {
        return LootItemBuilder.Create(Definition)
            .SetQuantity(FastRandom.NextInt(Quantity))
            .SetRepackaged(Packed);
    }

    private bool Packed { get; set; }
    private int RelicInfoId { get; set; }

    public RelicLoot(IDataRecord record)
    {
        Definition = record.GetValue<int>("definition");
        Quantity = new IntRange(record.GetValue<int>("minquantity"), record.GetValue<int>("maxquantity"));
        Chance = record.GetValue<double>("chance");
        Packed = record.GetValue<bool>("packed");
        RelicInfoId = record.GetValue<int>("relicinfoid");

    }

}
