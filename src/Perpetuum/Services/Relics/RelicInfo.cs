using Perpetuum.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Perpetuum.Services.Relics
{
    public class RelicInfo
    {
        public static RelicInfo CreateRelicInfoFromRecord(IDataRecord record)
        {
            var id = record.GetValue<int>("id");
            var name = record.GetValue<string>("name");
            var ep = record.GetValue<int>("ep");
            var info = new RelicInfo(id, name, ep);

            return info;
        }

        public static RelicInfo GetByIDFromDB(int id)
        {
            var relicinfos = Db.Query().CommandText("SELECT TOP 1 id, name, ep FROM relicinfos WHERE id = @relicInfoId")
                .SetParameter("@relicInfoId", id)
                .Execute()
                .Select(CreateRelicInfoFromRecord);

            var info = relicinfos.ToList()[0];//TODO risky?
            return info;
        }


        private int _id;
        private string _name;
        private int _ep;
        private Position _staticRelicPosistion;
        public bool HasStaticPosistion = false;

        public RelicInfo(int id, string name, int ep)
        {
            _id = id;
            _name = name;
            _ep = ep;
        }

        public void SetPosition(Position p)
        {
            HasStaticPosistion = true;
            _staticRelicPosistion = p;
        }

        public Position GetPosition()
        {
            return _staticRelicPosistion;
        }

        public int GetEP()
        {
            return this._ep;
        }

        public int GetID()
        {
            return this._id;
        }

    }
}
