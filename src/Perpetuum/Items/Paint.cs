using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Perpetuum.Accounting.Characters;
using Perpetuum.Common.Loggers.Transaction;
using Perpetuum.Containers;
using Perpetuum.Data;
using Perpetuum.EntityFramework;
using Perpetuum.Robots;

namespace Perpetuum.Items
{
    public class Paint : Item
    {

        static Paint()
        {

        }

        public void Activate(RobotInventory targetContainer, Character character)
        {
            targetContainer.CheckParentRobotAndThrowIfFailed(character.Eid);
            var robot = targetContainer.GetOrLoadParentEntity() as Robot;
            robot.Tint = this.ED.Config.Tint;
            robot.Save();
        }

    }
}