using Perpetuum.Accounting;
using Perpetuum.Threading.Process;
using Perpetuum.Timers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Perpetuum.Services.EventServices
{
    public class EPBonusEventService : Process
    {
        private IAccountManager _accountManager;
        private TimeTracker _timer;

        public EPBonusEventService(IAccountManager accountManager)
        {
            _accountManager = accountManager;
        }

        public void SetEvent(int bonus, TimeSpan duration)
        {
            _accountManager.SetServerEPBonusEvent(bonus);
            _timer = new TimeTracker(duration);
        }

        public override void Update(TimeSpan time)
        {
            _timer.Update(time);
            if (_timer.Expired)
            {
                _accountManager.SetServerEPBonusEvent(0);
            }
        }
    }
}
