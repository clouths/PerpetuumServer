using System.Collections.Generic;
using System.Transactions;
using Perpetuum.Data;
using Perpetuum.EntityFramework;
using Perpetuum.Host.Requests;
using Perpetuum.Items;
using Perpetuum.Robots;

namespace Perpetuum.RequestHandlers
{
    public class PaintUse : IRequestHandler
    {
        private readonly IEntityServices _entityServices;

        public PaintUse(IEntityServices entityServices)
        {
            _entityServices = entityServices;
        }

        public void HandleRequest(IRequest request)
        {
            using (var scope = Db.CreateTransaction())
            {
                var paintEid = request.Data.GetOrDefault<long>(k.eid);
                var character = request.Session.Character;


                var robot = character.GetActiveRobot();
                var container = robot.GetContainer();
                container.ThrowIfNull(ErrorCodes.RobotMustbeSingleAndNonRepacked);

                var paintItem = (Paint)container.GetItemOrThrow(paintEid, true).Unstack(1);
                paintItem.Activate(container, character);
                _entityServices.Repository.Delete(paintItem);
                container.Save();

                var result = new Dictionary<string, object> { { k.robot, robot.ToDictionary() } };
                Transaction.Current.OnCommited(() => Message.Builder.FromRequest(request).WithData(result).Send());

                scope.Complete();
            }
        }
    }
}