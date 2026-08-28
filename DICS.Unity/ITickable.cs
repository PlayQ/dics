
namespace DICS.Unity
{
    /**
     * Gets called every Update call of the MonoModule, that the ITickable instance was created in.
     * Important, objects created through factories do NOT get this called, unless the prefab itself
     * derives from a PrefabModule.
     */
    public interface ITickable
    {
        public void Tick();
    }
}