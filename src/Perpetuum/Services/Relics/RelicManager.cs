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

        private IZone _zone;
        private RiftSpawnPositionFinder _spawnPosFinder;

        private readonly TimeSpan _respawnRate = TimeSpan.FromMinutes(1);
        private readonly TimeSpan _relicLifeSpan = TimeSpan.FromMinutes(2);
        private readonly TimeSpan _relicRefreshRate = TimeSpan.FromSeconds(19);
        private ReaderWriterLockSlim _lock;
        private readonly TimeSpan THREAD_TIMEOUT = TimeSpan.FromSeconds(4);


        private List<Relic> _relicsOnZone = new List<Relic>();

        private RelicZoneConfigRepository relicZoneConfigRepository;
        private RelicSpawnInfoRepository relicSpawnInfoRepository;
        private RelicLootGenerator relicLootGenerator;
        private RelicRepository relicRepository;

        private int _max_relics = 0;
        private IEnumerable<RelicSpawnInfo> _spawnInfos;

        private Random _random;


        public RelicManager(IZone zone)
        {
            _random = new Random();
            _lock = new ReaderWriterLockSlim();
            _zone = zone;
            _spawnPosFinder = new PveRiftSpawnPositionFinder(zone);
            if (zone.Configuration.Terraformable)
            {
                _spawnPosFinder = new PvpRiftSpawnPositionFinder(zone);
            }
            //TODO -- init repositories and extract data
            relicZoneConfigRepository = new RelicZoneConfigRepository(zone);
            relicSpawnInfoRepository = new RelicSpawnInfoRepository(zone);
            relicRepository = new RelicRepository(zone);
            relicLootGenerator = new RelicLootGenerator(relicRepository);

            var config = relicZoneConfigRepository.GetZoneConfig();
            _max_relics = config.GetMax();
            _respawnRate = config.GetTimeSpan();

            _spawnInfos = relicSpawnInfoRepository.GetAll();

            
        }

        public void Start()
        {
            //Inject all relics on first start
            using (_lock.Write(THREAD_TIMEOUT))
            {
                while (_relicsOnZone.Count < _max_relics)
                {
                    SpawnRelic();
                }
            }
        }

        public void Stop()
        {
            //TODO cleanup if using DB to cache
        }

        public bool ForceSpawnRelicAt(int x, int y)
        {
            bool success = false;
            try
            {
                var info = GetNextRelicType();
                Position position = new Position(x, y);
                Relic relic = new Relic(0, info, _zone, _zone.GetPosition(position));
                _relicsOnZone.Add(relic);
                success = true;
            }
            catch(Exception e)
            {
                Logger.Warning("Failed to spawn Relic by ForceSpawnRelicAt()");
            }
            return success;
        }

        //Relics are just beam locations until a player comes within range of it, then they are awarded the Loot and EP for finding the Relic
        public void CheckNearbyRelics(Player player)
        {
            using (_lock.Write(THREAD_TIMEOUT))
            {
                foreach (Relic r in _relicsOnZone)
                {
                    if (r.GetPosition().TotalDistance3D(player.CurrentPosition) < r.GetRelicInfo().GetActivationRange())
                    {
                        r.PopRelic();
                        var relicLoots = relicLootGenerator.GenerateLoot(r);
                        if (relicLoots == null)
                            continue;
                        Task.Run(() =>
                        {
                            using (var scope = Db.CreateTransaction())
                            {
                                LootContainer.Create().SetOwner(player).SetEnterBeamType(BeamType.loot_bolt).AddLoot(relicLoots.LootItems).BuildAndAddToZone(_zone, relicLoots.Position);
                                var ep = _zone.Configuration.IsBeta ? 10 : 5;
                                if (_zone.Configuration.Type == ZoneType.Training) ep = 0;
                                if (ep > 0) player.Character.AddExtensionPointsBoostAndLog(EpForActivityType.Artifact, ep);
                                scope.Complete();
                            }
                        });
                    }
                }
                _relicsOnZone.RemoveAll(r => r.IsFound());
            }
        }


        private Point FindRelicPosition(RelicInfo info)
        {
            if (info.HasStaticPosistion) //If the relic spawn info has a valid static position defined - use that
            {
                return info.GetPosition().ToPoint();
            }
            return _spawnPosFinder.FindSpawnPosition();
        }

        private RelicInfo GetNextRelicType()
        {
            var spawnRates = _spawnInfos;
            double sumRate = spawnRates.Sum(r => r.GetRate());
            double minRate = 0.0;
            double chance = _random.NextDouble();
            RelicInfo info = null;
            foreach (var spawnRate in spawnRates)
            {
                double rate = (double)spawnRate.GetRate() / sumRate;
                double maxRate = rate + minRate;

                if (minRate < chance && chance <= maxRate)
                {
                    info = spawnRate.GetRelicInfo();
                    break;
                }
                minRate += rate;
            }
            return info;
        }

        
        private void SpawnRelic()
        {
            using (var scope = Db.CreateTransaction())
            {
                var maxAttempts = 100;
                var attempts = 0;
                RelicInfo info = null;
                while (info == null)
                {
                    info = GetNextRelicType();
                    if (info.HasStaticPosistion)
                    {
                        Point point = info.GetPosition();
                        foreach (var r in _relicsOnZone)
                        {
                            if(r.GetRelicInfo().GetActivationRange() > point.Distance(r.GetPosition()))
                            {
                                info = null;
                                break;
                            }
                        }
                    }
                    attempts++;
                    if (attempts > maxAttempts)
                        break;
                }

                if (info == null)
                {
                    Logger.Error("Could not get RelicInfo for next Relic on Zone: " + _zone.Id);
                    return;
                }

                attempts = 0;
                var spatialConflict = true;
                Point pt = FindRelicPosition(info);
                while (spatialConflict)
                {
                    spatialConflict = false;
                    foreach (var r in _relicsOnZone)
                    {
                        if (r.GetRelicInfo().GetActivationRange() > pt.Distance(r.GetPosition()))
                        {
                            spatialConflict = true;
                            break;
                        }
                    }
                    if (spatialConflict)
                    {
                        pt = _spawnPosFinder.FindSpawnPosition();
                        attempts++;
                        if (attempts > maxAttempts)
                            break;
                    }
                }
                if (attempts > maxAttempts)
                {
                    Logger.Error("Could not get Position for next Relic on Zone: " + _zone.Id);
                    return;
                }
                Position position = pt.ToPosition();
                Relic relic = new Relic(0, info, _zone, _zone.GetPosition(position));
                _relicsOnZone.Add(relic);
            }
        }


        private void CheckRelics()
        {
            foreach(Relic r in _relicsOnZone)
            {
                RefreshBeam(r);
#if DEBUG
                Logger.Info("Relic Location: Zone " + this._zone.Id + " " + r.GetPosition() );
#endif
            }
        }


        private void RefreshBeam(Relic relic)
        {
            var p = _zone.FixZ(relic.GetPosition());
            var beamBuilder = Beam.NewBuilder().WithType(BeamType.artifact_radar).WithTargetPosition(relic.GetPosition())
                .WithState(BeamState.AlignToTerrain)
                .WithDuration(_relicRefreshRate);
            _zone.CreateBeam(beamBuilder);
             beamBuilder = Beam.NewBuilder().WithType(BeamType.blue_20sec).WithTargetPosition(relic.GetPosition())
                .WithState(BeamState.AlignToTerrain)
                .WithDuration(_relicRefreshRate);
            _zone.CreateBeam(beamBuilder);
            beamBuilder = Beam.NewBuilder().WithType(BeamType.green_20sec).WithTargetPosition(p.AddToZ(5.0))
                .WithState(BeamState.Hit)
                .WithDuration(_relicRefreshRate);
            _zone.CreateBeam(beamBuilder);
            beamBuilder = Beam.NewBuilder().WithType(BeamType.nature_effect).WithTargetPosition(relic.GetPosition())
                .WithState(BeamState.AlignToTerrain)
                .WithDuration(_relicRefreshRate);
            _zone.CreateBeam(beamBuilder);
        }


        private TimeSpan _refreshElapsed;
        private TimeSpan _respawnElapsed;
        public void Update(TimeSpan time)
        {
            using (_lock.Write(THREAD_TIMEOUT))
            {
                _refreshElapsed += time;
                if (_refreshElapsed < _relicRefreshRate)
                    return;
                _refreshElapsed = TimeSpan.Zero;
                _respawnElapsed += time;
                if (_respawnElapsed > _respawnRate)
                {
                    if (_relicsOnZone.Count < _max_relics)
                    {
                        SpawnRelic();
                    }
                    _respawnElapsed = TimeSpan.Zero;
                }
            }

            using (_lock.Read(THREAD_TIMEOUT))
            {
                CheckRelics();
            }
        }

    }
}
