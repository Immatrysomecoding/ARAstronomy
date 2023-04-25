using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.State
{
    public class PreLevelState : IStateLevel
    {
        public PreLevelState(Level context)
        {
            SetContext(context);
        }

        public override void handleTime(float deltaTime)
        {
            throw new NotImplementedException();
        }
    }
}
