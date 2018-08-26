using Perpetuum.Accounting;
using Perpetuum.Threading.Process;
using System;
using System.Threading.Tasks;

namespace Perpetuum.Services.EventServices
{
    public class EPBonusEventService : Process
    {
        private readonly IAccountManager _accountManager;
        private TimeSpan _duration; 
        private TimeSpan _elapsed; 
        private bool _eventStarted; 
        private bool _endingEvent; 

        public EPBonusEventService(IAccountManager accountManager)
        {
            _accountManager = accountManager;
            _eventStarted = false;
            _endingEvent = false;
            _duration = TimeSpan.MaxValue;
            _elapsed = TimeSpan.Zero;
        }

        public void SetEvent(int bonus, TimeSpan duration)
        {
            _accountManager.SetEPBonusBoost(bonus);
            _elapsed = TimeSpan.Zero;
            _duration = duration;
            _endingEvent = false;
            _eventStarted = true;
        }

        private void EndEvent()
        {
            _accountManager.SetEPBonusBoost(0);
            _elapsed = TimeSpan.Zero;
            _duration = TimeSpan.MaxValue;
            _eventStarted = false;
            _endingEvent = false;
        }

        public override void Update(TimeSpan time)
        {
            if (!_eventStarted)
                return;

            if (_endingEvent)
                return;

            _elapsed += time;

            if (_elapsed < _duration)
                return;

            _endingEvent = true;
            Task.Run(() =>
            {
                EndEvent();
            });
        }
    }
}
