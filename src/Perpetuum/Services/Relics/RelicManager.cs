using Perpetuum.Zones;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Perpetuum.Services.RiftSystem;
using Perpetuum.Services.Looting;
using System.Drawing;
using Perpetuum.ExportedTypes;
using System.Threading;
using Perpetuum.Threading;
using Perpetuum.Log;
using Perpetuum.Players;
using Perpetuum.Data;
using Perpetuum.Zones.Beams;

namespace Perpetuum.Services.Relics
{
    public class RelicManager
    {
        //Constants
        private const double ACTIVATION_RANGE = 3; //30m
        private const double RESPAWN_PROXIMITY = 10.0 * ACTIVATION_RANGE;
        private readonly TimeSpan RESPAWN_RANDOM_WINDOW = TimeSpan.FromHours(1);
        private readonly TimeSpan THREAD_TIMEOUT = TimeSpan.FromSeconds(4);

        private IZone _zone;
        private RiftSpawnPositionFinder _spawnPosFinder;
        private ReaderWriterLockSlim _lock;
        private Random _random;

        private int _max_relics = 0;
        private IEnumerable<RelicSpawnInfo> _spawnInfos;

        private readonly TimeSpan _respawnRate = TimeSpan.FromHours(3);
        private readonly TimeSpan _relicRefreshRate = TimeSpan.FromSeconds(19.95);

        //Cache of Relics
        private List<Relic> _relicsOnZone = new List<Relic>();

        //DB-accessing objects
        private RelicZoneConfigRepository relicZoneConfigRepository;
        private RelicSpawnInfoRepository relicSpawnInfoRepository;
        private RelicLootGenerator relicLootGenerator;
        private RelicRepository relicRepository;

        //Timers for update
        private TimeSpan _refreshElapsed;
        private TimeSpan _respawnElapsed;
        private TimeSpan _respawnRandomized;

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
            // init repositories and extract data
            relicZoneConfigRepository = new RelicZoneConfigRepository(zone);
            relicSpawnInfoRepository = new RelicSpawnInfoRepository(zone);
            relicRepository = new RelicRepository(zone);
            relicLootGenerator = new RelicLootGenerator(relicRepository);

            //Get Zone Relic-Configuration data
            var config = relicZoneConfigRepository.GetZoneConfig();
            _max_relics = config.GetMax();
            _respawnRate = config.GetTimeSpan();
            _respawnRandomized = RollNextSpawnTime();

            _spawnInfos = relicSpawnInfoRepository.GetAll();
        }

        private TimeSpan RollNextSpawnTime()
        {
            var randomFactor = _random.NextDouble() - 0.5;
            var minutesToAdd = RESPAWN_RANDOM_WINDOW.TotalMinutes * randomFactor;

            return _respawnRate.Add(TimeSpan.FromSeconds(minutesToAdd));
        }

        public void Start()
        {
            //Inject max relics on first start
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
            //TODO cleanup if using DB to cache relics
        }

        public bool ForceSpawnRelicAt(int x, int y)
        {
            bool success = false;
            try
            {
                var info = GetNextRelicType();
                if (info == null)
                {
                    return false;
                }
                Position position = new Position(x, y);
                Relic relic = new Relic(0, info, _zone, _zone.GetPosition(position));
                using (_lock.Write(THREAD_TIMEOUT))
                {
                    _relicsOnZone.Add(relic);
                    success = true;
                }
            }
            catch (Exception e)
            {
                Logger.Warning("Failed to spawn Relic by ForceSpawnRelicAt()");
            }
            return success;
        }

        //Relics are just beam locations until a player comes within range of it, then they are awarded the Loot and EP for finding the Relic
        public void CheckNearbyRelics(Player player)
        {
            //Optimization - Readonly-lock for relic in range - bail if nothing found
            Relic relicInRange = null;
            using (_lock.Read(THREAD_TIMEOUT))
            {
                foreach (Relic r in _relicsOnZone)
                {
                    if (r.GetPosition().TotalDistance3D(player.CurrentPosition) < ACTIVATION_RANGE && r.IsAlive())
                    {
                        relicInRange = r;
                        break;
                    }
                }
            }
            if (relicInRange == null)
            {
                return;
            }

            //Compute things before getting lock
            //Compute EP
            var ep = relicInRange.GetRelicInfo().GetEP();
            if (_zone.Configuration.Type == ZoneType.Pvp) ep *= 2;
            if (_zone.Configuration.Type == ZoneType.Training) ep = 0;

            //Compute loots
            var relicLoots = relicLootGenerator.GenerateLoot(relicInRange);
            if (relicLoots == null)
                return;

            using (_lock.Write(THREAD_TIMEOUT))
            {
                //Set flag on relic for removal
                relicInRange.SetAlive(false);

                //Fork task to make the lootcan and log the ep
                Task.Run(() =>
                {
                    using (var scope = Db.CreateTransaction())
                    {
                        LootContainer.Create().SetOwner(player).SetEnterBeamType(BeamType.loot_bolt).AddLoot(relicLoots.LootItems).BuildAndAddToZone(_zone, relicLoots.Position);
                        if (ep > 0) player.Character.AddExtensionPointsBoostAndLog(EpForActivityType.Artifact, ep);
                        scope.Complete();
                    }
                });

                //Remove all relics with flag set
                _relicsOnZone.RemoveAll(r => !r.IsAlive());
            }
        }


        private Point FindRelicPosition(RelicInfo info)
        {
            if (info.HasStaticPosistion) //If the relic spawn info has a valid static position defined - use that
            {
                return info.GetPosition().ToPoint();
            }
            return _spawnPosFinder.FindSpawnPosition(); //Else use random-walkable
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
                //Get Next Relictype based on the distribution of their probabilities on this zone
                var maxAttempts = 100;
                var attempts = 0;
                RelicInfo info = null;
                while (info == null && _spawnInfos != null)
                {
                    info = GetNextRelicType();
                    if (info.HasStaticPosistion) //The selected Relic type is static!  We must check if another relic is in this location
                    {
                        Point point = info.GetPosition();
                        foreach (var r in _relicsOnZone)
                        {
                            if (RESPAWN_PROXIMITY > point.Distance(r.GetPosition()))
                            {
                                info = null; //We cannot spawn this type because another relic (possibly of the same type) is already present near this location
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
                        if (RESPAWN_PROXIMITY > pt.Distance(r.GetPosition()))
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


        private void UpdateBeams()
        {
            foreach (Relic r in _relicsOnZone)
            {
                RefreshBeam(r);
            }
        }


        private void RefreshBeam(Relic relic)
        {
            var level = relic.GetRelicInfo().GetLevel();
            var faction = relic.GetRelicInfo().GetFaction();
            var factionalBeamType = BeamType.orange_20sec;
            switch (faction)
            {
                case 0:
                    factionalBeamType = BeamType.orange_20sec;
                    break;
                case 1:
                    factionalBeamType = BeamType.green_20sec;
                    break;
                case 2:
                    factionalBeamType = BeamType.blue_20sec;
                    break;
                case 3:
                    factionalBeamType = BeamType.red_20sec;
                    break;
                default:
                    factionalBeamType = BeamType.orange_20sec;
                    break;
            }


            var p = _zone.FixZ(relic.GetPosition());
            var beamBuilder = Beam.NewBuilder().WithType(BeamType.artifact_radar).WithTargetPosition(relic.GetPosition())
                .WithState(BeamState.AlignToTerrain)
                .WithDuration(_relicRefreshRate);
            _zone.CreateBeam(beamBuilder);
            beamBuilder = Beam.NewBuilder().WithType(BeamType.nature_effect).WithTargetPosition(relic.GetPosition())
                .WithState(BeamState.AlignToTerrain)
                .WithDuration(_relicRefreshRate);
                _zone.CreateBeam(beamBuilder);
            for (var i =0; i<level; i++)
            {
                beamBuilder = Beam.NewBuilder().WithType(factionalBeamType).WithTargetPosition(p.AddToZ(3.5*i+1.0))
                    .WithState(BeamState.Hit)
                    .WithDuration(_relicRefreshRate);
                    _zone.CreateBeam(beamBuilder);
            }
        }

        private void UpdateRelics(TimeSpan elapsed)
        {
            foreach (Relic r in _relicsOnZone)
            {
                r.Update(elapsed);
            }

            //Remove all expired relics
            _relicsOnZone.RemoveAll(r => !r.IsAlive());
        }

        public void Update(TimeSpan time)
        {
            using (_lock.Write(THREAD_TIMEOUT))
            {
                //Minimum tick rate
                _refreshElapsed += time;
                if (_refreshElapsed < _relicRefreshRate)
                    return;

                //Update Relic lifespans and expire
                UpdateRelics(_refreshElapsed);

                _respawnElapsed += _refreshElapsed;
                _refreshElapsed = TimeSpan.Zero;

                //check if time to spawn a new Relic
                if (_respawnElapsed > _respawnRandomized)
                {
                    if (_relicsOnZone.Count < _max_relics)
                    {
                        SpawnRelic();
                        _respawnRandomized = RollNextSpawnTime();
                    }
                    _respawnElapsed = TimeSpan.Zero;
                }
            }

            //Update beams - readonly
            using (_lock.Read(THREAD_TIMEOUT))
            {
                UpdateBeams();
            }
        }

    }
}
