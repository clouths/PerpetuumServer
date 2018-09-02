using Perpetuum.Threading.Process;
using System;
using System.Threading.Tasks;

namespace Perpetuum.Services.EventServices
{
    public class EPBonusEventService : Process
    {
        private TimeSpan _duration;
        private TimeSpan _elapsed;
        private bool _eventStarted;
        private bool _endingEvent;
        private volatile int _bonus;

        public EPBonusEventService()
        {
            _bonus = 0;
            _eventStarted = false;
            _endingEvent = false;
            _duration = TimeSpan.MaxValue;
            _elapsed = TimeSpan.Zero;
        }

        public int GetBonus()
        {
            return _bonus;
        }

        public void SetEvent(int bonus, TimeSpan duration)
        {
            _bonus = bonus;
            _elapsed = TimeSpan.Zero;
            _duration = duration;
            _endingEvent = false;
            _eventStarted = true;
        }

        private void EndEvent()
        {
            _bonus = 0;
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
            Task.Run(() => EndEvent());
        }
    }
}
