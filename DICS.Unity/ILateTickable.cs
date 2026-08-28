namespace DICS.Unity
{
    /**
     * Gets called every LateUpdate call of the MonoModule, that the ITickable instance was created in.
     * Important, objects created through factories do NOT get this called, unless the prefab itself
     * derives from a PrefabModule.
     */
    public interface ILateTickable
    {
        public void LateTick();
    }
}