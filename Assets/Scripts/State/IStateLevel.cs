using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Scripts.State
{
    public abstract class IStateLevel
    {
        protected Level context;
        public void SetContext(Level context) { this.context = context; }
        abstract public void handleTime(float deltaTime);
    }
}
