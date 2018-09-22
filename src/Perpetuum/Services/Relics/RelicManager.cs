using Perpetuum.Threading.Process;
using Perpetuum.Zones;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Perpetuum.Services.RiftSystem;
using Perpetuum.Services.Looting;
using System.Drawing;
using Perpetuum.Items;
using Perpetuum.EntityFramework;
using Perpetuum.ExportedTypes;
using System.Threading;
using Perpetuum.Log;
using Perpetuum.Timers;
using Perpetuum.Players;
using Perpetuum.Data;
using System.Transactions;

namespace Perpetuum.Services.Relics
{
    public class RelicManager
    {
        private static readonly int MAX_RELICS = 20;
        private int _relicCount = 0;
        private IZone _zone;
        private RiftSpawnPositionFinder _spawnPosFinder;
        private ILootGenerator _lootGenerator;
        private readonly LinkedList<TimeTracker> _nextRelicSpawns = new LinkedList<TimeTracker>();
        private readonly TimeSpan _relicLifeSpan = TimeSpan.FromMinutes(2);


        public RelicManager(IZone zone)
        {
            _zone = zone;
            _spawnPosFinder = new PveRiftSpawnPositionFinder(zone);
            if (zone.Configuration.Terraformable)
            {
                _spawnPosFinder = new PvpRiftSpawnPositionFinder(zone);
            }
            //TODO arbitrary item generated for testing -- TODO make table of loots, need repository object, query random in spawnRelic method!
            ItemInfo itemInfo = new ItemInfo(EntityDefault.GetByName(DefinitionNames.COMMON_REACTOR_PLASMA).Definition, 200, 500);
            LootGeneratorItemInfo info = new LootGeneratorItemInfo(itemInfo, false, 1.0);
            _lootGenerator = new LootGenerator(new List<LootGeneratorItemInfo>() { info });
        }

        //TODO creates can, injects into zone, we lose handle on can so we will not know if looted, expired etc...
        //To discuss: should we respawn cans at some fixed interval? -- regardless if looted
        //Should we respawn cans after one is removed/looted? -- need to attach event RemovedFromZone
        private void SpawnRelic()
        {
            Point pt = _spawnPosFinder.FindSpawnPosition();
            using (var scope = Db.CreateTransaction())
            {
                LootContainer.Create().AddLoot(_lootGenerator).SetEnterBeamType(BeamType.artifact_found).SetType(LootContainerType.Relic).BuildAndAddToZone(_zone, pt.ToPosition());

                Logger.Info("Relic created at: " + _zone.Configuration.Name + " " + pt.ToString());


                Transaction.Current.OnCommited(() =>
                {
                    _nextRelicSpawns.AddLast(new TimeTracker(_relicLifeSpan));
                    Interlocked.Increment(ref _relicCount);
                });
                scope.Complete();
            }
        }


        public void Update(TimeSpan time)
        {
            while (_relicCount < MAX_RELICS && !(_zone is StrongHoldZone))
            {
                SpawnRelic();
            }

            _nextRelicSpawns.RemoveAll(t =>
            {
                t.Update(time);
                if (!t.Expired)
                    return false;

                SpawnRelic();
                return true;
            });
        }
    }
}
