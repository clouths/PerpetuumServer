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
using Perpetuum.Zones.Beams;
using Perpetuum.Units;

namespace Perpetuum.Services.Relics
{
    public class RelicManager
    {
        private static readonly int MAX_RELICS = 20;
        private int _relicCount = 0;
        private IZone _zone;
        private RiftSpawnPositionFinder _spawnPosFinder;
        private ILootGenerator _lootGenerator;
        private readonly List<LootContainer> _spawnedCans = new List<LootContainer>();
        private readonly TimeSpan _relicLifeSpan = TimeSpan.FromMinutes(3);
        private readonly TimeSpan _relicRefreshRate = TimeSpan.FromSeconds(10);


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
            using (var scope = Db.CreateTransaction())
            {
                Point pt = _spawnPosFinder.FindSpawnPosition();
                Position position = pt.ToPosition();
                LootContainer container = LootContainer.Create().AddLoot(_lootGenerator).Build(_zone, position);
                Logger.Info("Relic created at: " + _zone.Configuration.Name + " " + pt.ToString());

                Transaction.Current.OnCommited(() =>
                {
                    var beamBuilder = Beam.NewBuilder().WithType(BeamType.artifact_found).WithSourcePosition(container.CurrentPosition.AddToZ(10))
                        .WithTarget(container)
                        .WithState(BeamState.Hit)
                        .WithDuration(_relicRefreshRate);

                    container.AddToZone(_zone, position, ZoneEnterType.Default, beamBuilder);
                    container.ResetDespawnTimeAndStrategy(_relicLifeSpan, DespawnRelicByEffect);
                    container.SubscribeObserver(this);
                    _spawnedCans.Add(container);
                    Interlocked.Increment(ref _relicCount);
                });
                scope.Complete();
            }
        }



        private void CheckRelics()
        {
            var count = 0;
            foreach (LootContainer cont in _spawnedCans)
            {
                var unit = _zone.GetUnit(cont.Eid);
                if (unit is LootContainer)
                {
                    var can = unit as LootContainer;
                    RefreshBeam(can);
                    count++;
                }
            }
            Interlocked.Exchange(ref _relicCount, count);
        }


        private void RefreshBeam(LootContainer can)
        {
            var beamBuilder = Beam.NewBuilder().WithType(BeamType.artifact_found)
                .WithTarget(can)
                .WithState(BeamState.Hit)
                .WithDuration(_relicRefreshRate);
            _zone.CreateBeam(beamBuilder);
        }


        private TimeSpan _elapsed;

        public void Update(TimeSpan time)
        {
            _elapsed += time;
            if (_elapsed < _relicRefreshRate)
                return;
            _elapsed = TimeSpan.Zero;

            while (_relicCount < MAX_RELICS && !(_zone is StrongHoldZone))
            {
                SpawnRelic();
            }


            CheckRelics();

        }
        //TODO implement onshutdown cleanup procedure to remove all current relics!

        public void Stop()
        {
            while (_spawnedCans.Count > 0)
            {
                _spawnedCans.Last().RemoveFromZone();
            }
        }


        public void DespawnRelic(LootContainer can)
        {
            Logger.Info("Relic removed at: " + _zone.Configuration.Name + " " + can.CurrentPosition.ToString());
            _spawnedCans.Remove(can);
            Interlocked.Decrement(ref _relicCount);
        }

        private void DespawnRelicByEffect(Unit unit)
        {
            if (unit is LootContainer)
            {
                var can = unit as LootContainer;
                Logger.Info("Removing Relic at: " + _zone.Configuration.Name + " " + can.CurrentPosition.ToString());
                can.RemoveFromZone();
            }
        }
    }
}
