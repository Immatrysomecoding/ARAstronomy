using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.State
{
    public class PlayingState : IStateLevel
    {
        private float MaxTime;
        public PlayingState(Level context)
        {
            SetContext(context);
            this.MaxTime = 10;
        }
        public PlayingState(Level context, float MaxTime)
        {
            SetContext(context);
            this.MaxTime = MaxTime;
        }
        public override void handleTime(float deltaTime)
        {
            MaxTime -= deltaTime;
        }
    }
}
