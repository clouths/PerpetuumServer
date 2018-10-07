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
using Perpetuum.Threading;
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
        private static readonly int MAX_RELICS = 50;
        private IZone _zone;
        private RiftSpawnPositionFinder _spawnPosFinder;
        private ILootGenerator _lootGenerator;
        private readonly List<LootContainer> _spawnedCans = new List<LootContainer>();
        private readonly TimeSpan _relicLifeSpan = TimeSpan.FromMinutes(2);
        private readonly TimeSpan _relicRefreshRate = TimeSpan.FromSeconds(10);
        private ReaderWriterLockSlim _lock;
        private readonly TimeSpan THREAD_TIMEOUT = TimeSpan.FromSeconds(4);


        public RelicManager(IZone zone)
        {
            _lock = new ReaderWriterLockSlim();
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
                    var beamBuilder = Beam.NewBuilder().WithType(BeamType.artifact_found).WithSourcePosition(container.CurrentPosition)
                        .WithTarget(container)
                        .WithState(BeamState.Hit)
                        .WithDuration(_relicRefreshRate);

                    container.ResetDespawnTime(_relicLifeSpan);
                    container.AddToZone(_zone, position, ZoneEnterType.Default, beamBuilder);
                    container.SubscribeObserver(this);
                    _spawnedCans.Add(container);
                });
                scope.Complete();
            }
        }



        private void CheckRelics()
        {
            foreach (LootContainer cont in _spawnedCans)
            {
                var unit = _zone.GetUnit(cont.Eid);
                if (unit == null)
                    continue;
                RefreshBeam(unit);
            }
        }


        private void RefreshBeam(Unit can)
        {
            var beamBuilder = Beam.NewBuilder().WithType(BeamType.green_10sec).WithTargetPosition(can.PositionWithHeight.AddToZ(0.2))
                .WithState(BeamState.Hit)
                .WithDuration(_relicRefreshRate);
            _zone.CreateBeam(beamBuilder);
        }


        private TimeSpan _elapsed;

        public void Update(TimeSpan time)
        {
            using (_lock.Write(THREAD_TIMEOUT))
            {
                _elapsed += time;
                if (_elapsed < _relicRefreshRate)
                    return;
                _elapsed = TimeSpan.Zero;
                while (_spawnedCans.Count < MAX_RELICS && !(_zone is StrongHoldZone))
                {
                    SpawnRelic();
                }
            }

            using (_lock.Read(THREAD_TIMEOUT))
            {
                CheckRelics();
            }
        }


        public void Stop()
        {
            using (_lock.Write(THREAD_TIMEOUT))
            {
                for (int i = _spawnedCans.Count - 1; i >= 0; i--)
                {
                    _spawnedCans[i].RemoveFromZone(); //TODO still have leftover cans after server shutdown!
                }
            }
        }


        public void DespawnRelic(LootContainer can)
        {
            Logger.Info("Relic removed at: " + _zone.Configuration.Name + " " + can.CurrentPosition.ToString());
            _spawnedCans.Remove(can);
        }
    }
}
