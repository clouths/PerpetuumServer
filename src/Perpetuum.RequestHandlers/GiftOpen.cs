using System.Collections.Generic;
using System.Transactions;
using Perpetuum.Data;
using Perpetuum.EntityFramework;
using Perpetuum.Host.Requests;
using Perpetuum.Items;

namespace Perpetuum.RequestHandlers
{
    public class GiftOpen : IRequestHandler
    {
        private readonly IEntityServices _entityServices;

        public GiftOpen(IEntityServices entityServices)
        {
            _entityServices = entityServices;
        }

        public void HandleRequest(IRequest request)
        {
            var itemEid = request.Data.GetOrDefault<long>(k.eid);
            var item = this._entityServices.Repository.Load(itemEid);
            if (item is Gift)
            {
                this.handleGift(request, itemEid);
            }
            else if (item is Paint) //TODO this is here until we can build a good category flag? ??How are requests routed?
            {
                this.handlePaint(request, itemEid);
            }

        }


        private void handleGift(IRequest request, long giftEid)
        {
            using (var scope = Db.CreateTransaction())
            {
                var character = request.Session.Character;

                character.IsDocked.ThrowIfFalse(ErrorCodes.CharacterHasToBeDocked);

                var publicContainer = character.GetPublicContainerWithItems();
                var giftItem = (Gift)publicContainer.GetItemOrThrow(giftEid, true).Unstack(1);
                var randomItem = giftItem.Open(publicContainer, character);
                _entityServices.Repository.Delete(giftItem);
                publicContainer.Save();

                var gifts = new Dictionary<int, int> { { randomItem.Definition, randomItem.Quantity } }.ToDictionary("g", g =>
                {
                    var oneEntry = new Dictionary<string, object>
                    {
                        {k.definition, g.Key},
                        {k.quantity, g.Value}
                    };
                    return oneEntry;
                });

                var result = new Dictionary<string, object>
                {
                    {k.container, publicContainer.ToDictionary()},
                    {"gift",gifts},
                };

                Transaction.Current.OnCommited(() => Message.Builder.FromRequest(request).WithData(result).Send());

                scope.Complete();
            }
        }

        private void handlePaint(IRequest request, long paintEid)
        {
            using (var scope = Db.CreateTransaction())
            {
                var character = request.Session.Character;

                var robot = character.GetActiveRobot();
                var container = robot.GetContainer();
                container.ThrowIfNull(ErrorCodes.RobotMustbeSingleAndNonRepacked);

                var paintItem = (Paint)container.GetItemOrThrow(paintEid, true).Unstack(1);
                paintItem.Activate(container, character);
                _entityServices.Repository.Delete(paintItem);
                container.Save();

                var result = new Dictionary<string, object> { { k.robot, robot.ToDictionary() }, { k.container, container.ToDictionary() } };
                Transaction.Current.OnCommited(() => Message.Builder.FromRequest(request).WithData(result).Send());

                scope.Complete();
            }
        }
    }
}