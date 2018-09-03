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
        private int _bonus;
        private object _lock = new object();

        public EPBonusEventService()
        {
            _bonus = 0;
            _duration = TimeSpan.MaxValue;
            _elapsed = TimeSpan.Zero;
            _eventStarted = false;
            _endingEvent = false;
        }

        public int GetBonus()
        {
            lock (_lock)
            {
                return _bonus;
            }
        }

        public void SetEvent(int bonus, TimeSpan duration)
        {
            lock (_lock)
            {
                _bonus = bonus;
                _elapsed = TimeSpan.Zero;
                _duration = duration;
            }

            _endingEvent = false;
            _eventStarted = true;
        }

        private void EndEvent()
        {
            _eventStarted = false;
            _endingEvent = false;

            lock (_lock)
            {
                _bonus = 0;
                _elapsed = TimeSpan.Zero;
                _duration = TimeSpan.MaxValue;
            }
        }

        public override void Update(TimeSpan time)
        {
            if (!_eventStarted)
                return;

            if (_endingEvent)
                return;

            lock (_lock)
            {
                _elapsed += time;
                if (_elapsed < _duration)
                    return;
            }

            _endingEvent = true;
            Task.Run(() => EndEvent());
        }
    }
}
