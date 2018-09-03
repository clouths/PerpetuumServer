using Perpetuum.Threading.Process;
using System;
using System.Threading;
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
        private ReaderWriterLockSlim _lock;

        public EPBonusEventService()
        {
            Init();
        }

        private void Init()
        {
            if (_lock == null)
                _lock = new ReaderWriterLockSlim();

            try
            {
                _lock.EnterWriteLock();
                _bonus = 0;
                _duration = TimeSpan.MaxValue;
                _elapsed = TimeSpan.Zero;
                _eventStarted = false;
                _endingEvent = false;
            }
            finally
            {
                if (_lock.IsWriteLockHeld)
                    _lock.ExitWriteLock();
            }
        }

        private void Cleanup()
        {
            if (_lock != null)
                _lock.Dispose();
        }

        public int GetBonus()
        {
            try
            {
                _lock.EnterReadLock();
                return _bonus;
            }
            finally
            {
                if (_lock.IsReadLockHeld)
                    _lock.ExitReadLock();
            }
        }

        public void SetEvent(int bonus, TimeSpan duration)
        {
            try
            {
                _lock.EnterWriteLock();
                _bonus = bonus;
                _elapsed = TimeSpan.Zero;
                _duration = duration;
                _endingEvent = false;
                _eventStarted = true;
            }
            finally
            {
                if (_lock.IsWriteLockHeld)
                    _lock.ExitWriteLock();
            }
        }

        private void EndEvent()
        {
            try
            {
                _lock.EnterWriteLock();
                _bonus = 0;
                _elapsed = TimeSpan.Zero;
                _duration = TimeSpan.MaxValue;
                _eventStarted = false;
                _endingEvent = false;
            }
            finally
            {
                if (_lock.IsWriteLockHeld)
                    _lock.ExitWriteLock();
            }
        }

        public override void Update(TimeSpan time)
        {
            try
            {
                _lock.EnterReadLock();

                if (!_eventStarted)
                    return;

                if (_endingEvent)
                    return;
            }
            finally
            {
                if (_lock.IsReadLockHeld)
                    _lock.ExitReadLock();
            }

            try
            {
                _lock.EnterWriteLock();
                _elapsed += time;
                if (_elapsed < _duration)
                    return;
                _endingEvent = true;
            }
            finally
            {
                if (_lock.IsWriteLockHeld)
                    _lock.ExitWriteLock();
            }
            Task.Run(() => EndEvent());
        }

        public override void Stop()
        {
            base.Stop();
            Cleanup();
        }

        public override void Start()
        {
            Init();
            base.Start();
        }
    }
}
