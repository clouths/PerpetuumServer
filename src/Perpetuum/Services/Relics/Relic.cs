﻿using Perpetuum.Data;
using Perpetuum.Services.Looting;
using Perpetuum.Zones;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Perpetuum.Services.Relics
{
    public class Relic
    {
        private int _id;
        private RelicInfo _info;
        private IZone _zone;
        private Position _position;
        private bool _found;


        public Relic(int id, RelicInfo info, IZone zone, Position position)
        {
            _id = id;
            _info = info;
            _zone = zone;
            _position = position;
            _found = false;
        }

        public RelicInfo GetRelicInfo()
        {
            return this._info;
        }

        public Position GetPosition()
        {
            return this._position;
        }

        public void PopRelic()
        {
            this._found = true;
        }

        public bool IsFound()
        {
            return _found;
        }

    }



    public class RelicRepository
    {
        private RelicReader _relicReader;
        private RelicLootReader _relicLootReader;
        private IZone _zone;

        public RelicRepository(IZone zone)
        {
            _relicReader = new RelicReader(zone);
            _relicLootReader = new RelicLootReader();
            _zone = zone;
        }

        public IEnumerable<Relic> GetAll()
        {
            return _relicReader.GetAll();
        }

        public IEnumerable<Relic> GetAllOfType(RelicInfo info)
        {
            return _relicReader.GetAllWithInfo(info);
        }

        public IEnumerable<IRelicLoot> GetRelicLoots(RelicInfo info)
        {
            return _relicLootReader.GetRelicLoots(info);
        }

    }


    public class RelicLootReader
    {

        protected IRelicLoot CreateRelicLootFromRecord(IDataRecord record)
        {
            return new RelicLoot(record);
        }

        public IEnumerable<IRelicLoot> GetRelicLoots(RelicInfo info)
        {
            var loots = Db.Query().CommandText("SELECT definition,minquantity,maxquantity,chance,relicinfoid,packed FROM relicloots WHERE relicinfoid = @relicInfoId")
                .SetParameter("@relicInfoId", info.getID())
                .Execute()
                .Select(CreateRelicLootFromRecord);

            var resultList = new List<IRelicLoot>();
            foreach (var loot in loots)
            {
                resultList.Add(loot);
            }
            return resultList;
        }

    }

    public class RelicReader
    {
        private IZone _zone;

        public RelicReader(IZone zone)
        {
            _zone = zone;
        }


        protected RelicInfo CreateRelicInfoFromRecord(IDataRecord record)
        {
            var id = record.GetValue<int>("id");
            var name = record.GetValue<string>("name");
            var goalrange = record.GetValue<int>("goalrange");
            var npcpresenceid = record.GetValue<int?>("npcpresenceid");
            var info = new RelicInfo(id, name, goalrange, npcpresenceid);

            return info;
        }

        protected Relic CreateRelicFromRecord(IDataRecord record)
        {
            var id = record.GetValue<int>("id");
            var relicinfoid = record.GetValue<int>("relicinfoid");
            var zoneid = record.GetValue<int>("zoneid");
            var x = record.GetValue<int>("x");
            var y = record.GetValue<int>("y");

            var relicinfos = Db.Query().CommandText("SELECT TOP 1 id, name, goalrange FROM relicinfos WHERE id = @relicInfoId")
                .SetParameter("@relicInfoId", relicinfoid)
                .Execute()
                .Select(CreateRelicInfoFromRecord);

            var info = relicinfos.ToList()[0];//TODO risky

            var relic = new Relic(id, info, _zone, new Position(x, y));

            return relic;
        }

        public IEnumerable<Relic> GetAllWithInfo(RelicInfo info)
        {
            var relics = Db.Query().CommandText("SELECT id, relicinfoid, zoneid x, y FROM relics WHERE zoneid = @zoneId AND relicinfoid = @relicInfoId")
                .SetParameter("@zoneId", _zone.Id)
                .SetParameter("@relicInfoId", info.getID())
                .Execute()
                .Select(CreateRelicFromRecord);

            var resultList = new List<Relic>();
            foreach (var relic in relics)
            {
                resultList.Add(relic);
            }
            return resultList;
        }

        public IEnumerable<Relic> GetAll()
        {
            var relics = Db.Query().CommandText("SELECT id, relicinfoid, zoneid x, y FROM relics WHERE zoneid = @zoneId")
                .SetParameter("@zoneId", _zone.Id)
                .Execute()
                .Select(CreateRelicFromRecord);

            var resultList = new List<Relic>();
            foreach (var relic in relics)
            {
                resultList.Add(relic);
            }
            return resultList;
        }
    }
}
