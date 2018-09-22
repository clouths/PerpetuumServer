using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Perpetuum.Services.Looting
{
    public class RelicContainer : LootContainer
    {
        public new static readonly TimeSpan DespawnTime = TimeSpan.FromMinutes(2);

        public RelicContainer(ILootItemRepository lootItemRepository) : base(lootItemRepository)
        {
        }
    }
}
