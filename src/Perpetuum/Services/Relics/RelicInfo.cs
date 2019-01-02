using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Perpetuum.Services.Relics
{
    public class RelicInfo
    {
        private int _id;
        private string _name;
        private int _goalrange;
        private int? _npcpresence;
        private Position _staticRelicPosistion;
        public bool HasStaticPosistion = false;

        public RelicInfo(int id, string name, int range, int? npcpresenceid)
        {
            _id = id;
            _name = name;
            _goalrange = range;
            _npcpresence = npcpresenceid;
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

        public int GetActivationRange()
        {
            return this._goalrange;
        }

        public int getID()
        {
            return this._id;
        }

    }
}
