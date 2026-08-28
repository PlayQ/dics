using System.Collections.Generic;

namespace DICS.Unity
{
    public abstract class ExternalMonoModule : BaseMonoModule, IModuleWithRoots
    {
        public Module Module {
            get
            {
                if (module == null)
                {
                    module = new InternalModule();
                    Make();
                }
                return module;
            }
        }

        public new virtual ISet<Key> RequiredRoots()
        {
            return base.RequiredRoots();
        }
    }
}