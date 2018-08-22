using Perpetuum.Accounting;
using Perpetuum.Threading.Process;
using System;
using System.Threading.Tasks;

namespace Perpetuum.Services.EventServices
{
    public class EPBonusEventService : Process
    {
        private IAccountManager _accountManager;
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
            _duration = duration;
            _elapsed = TimeSpan.Zero;
            _accountManager.SetEPBonusBoost(bonus);
            _eventStarted = true;
        }

        private void DoClearBonus()
        {
            _accountManager.SetEPBonusBoost(0);
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
            Task.Run(() => DoClearBonus()).ContinueWith(t =>
            {
                _eventStarted = false;
                _endingEvent = false;
                _elapsed = TimeSpan.Zero;
            });
        }
    }
}
