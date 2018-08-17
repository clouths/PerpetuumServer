using System;
using Perpetuum.Accounting;
using Perpetuum.Data;
using Perpetuum.Host.Requests;
using Perpetuum.Services.EventServices;

namespace Perpetuum.RequestHandlers.Extensions
{
    public class EPBonusEvent : IRequestHandler
    {
        private readonly EPBonusEventService _eventService;
        private TimeSpan MAX_DURATION = TimeSpan.FromDays(7);
        private int MIN_BONUS = 0;
        private int MAX_BONUS = 15;

        public EPBonusEvent(EPBonusEventService eventService)
        {
            _eventService = eventService;
        }

        public void HandleRequest(IRequest request)
        {
            using (var scope = Db.CreateTransaction())
            {
                var bonusAmount = request.Data.GetOrDefault<int>(k.bonus);
                var durationHours = request.Data.GetOrDefault<int>(k.duration);

                var checkArgs = bonusAmount >= MIN_BONUS && bonusAmount <= MAX_BONUS;
                checkArgs = checkArgs && durationHours <= MAX_DURATION.Hours;
                checkArgs.ThrowIfFalse(ErrorCodes.RequiredArgumentIsNotSpecified);

                _eventService.SetEvent(bonusAmount, TimeSpan.FromHours(durationHours));

                scope.Complete();
            }
        }
    }
}