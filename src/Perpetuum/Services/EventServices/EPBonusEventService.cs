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
        private TimerAction _timer;

        public EPBonusEventService(IAccountManager accountManager)
        {
            _accountManager = accountManager;
        }

        public void SetEvent(int bonus, TimeSpan duration)
        {
            _accountManager.SetEPBonusBoost(bonus);
            _timer = new TimerAction(DoClearBonus, duration, true);
        }

        private void DoClearBonus()
        {
            _accountManager.SetEPBonusBoost(0);
            _timer = null;
        }

        public override void Update(TimeSpan time)
        {
            if (_timer != null)
            {
                _timer.Update(time);
            }
        }
    }
}
